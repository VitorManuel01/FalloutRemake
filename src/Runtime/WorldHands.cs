using System;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using FalloutRemake.Import;
using FalloutRemake.Runtime.Combat;

namespace FalloutRemake.Runtime;

/// <summary>FO2's two item slots: CE critterGetItem1 (left hand) and critterGetItem2 (right hand).</summary>
public enum Hand { Left, Right }
public enum HandStatus { Ready, NoItem, NotWeapon, Unsupported, InCombat, Busy }
/// <summary>A carried item held in a hand, by its area and source item key.</summary>
public sealed record HeldItem([property: JsonRequired] int Map, [property: JsonRequired] string ItemKey);

/// <summary>
/// The player's hands. Each holds a carried weapon or nothing; an empty left hand punches and an empty right hand kicks
/// (CE interface.cc), each with the best blow the sheet allows. The active hand is the one that strikes; a new game starts
/// on the left (gInterfaceCurrentHand). An item that leaves the inventory (returned, consumed, kept in the challenge chest)
/// leaves the hand at once. Melee weapons only until ammunition is tracked; wielding happens outside fights here (CE charges
/// the inventory's 4 AP in one).
/// </summary>
public sealed partial class WorldSession
{
    private HeldItem? _leftHand, _rightHand;
    public Hand ActiveHand { get; private set; }
    public void SetActiveHand(Hand hand) => ActiveHand = hand is Hand.Left or Hand.Right ? hand : throw new ArgumentOutOfRangeException(nameof(hand));
    public HeldItem? Held(Hand hand) => hand == Hand.Left ? _leftHand : _rightHand;
    public static Hand Other(Hand hand) => hand == Hand.Left ? Hand.Right : Hand.Left;

    private CarriedItem? CarriedHeld(HeldItem held) => Carried.FirstOrDefault(c => c.Map == held.Map && c.Source.Key == held.ItemKey);
    private int HandsHolding(HeldItem held) => (_leftHand == held ? 1 : 0) + (_rightHand == held ? 1 : 0);

    private Prototype? PrototypeOf(CarriedItem item) =>
        _areas.TryGetValue(item.Map, out WorldArea? area) ? area.Map.Prototypes.FirstOrDefault(p => p.Pid == item.Source.Header.Pid) : null;

    public HandStatus CanWield(CarriedItem item, Hand hand, bool moving)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (InCombat && _activeFight?.InventoryOpen != true) return HandStatus.InCombat;
        if (moving) return HandStatus.Busy;
        var held = new HeldItem(item.Map, item.Source.Key);
        if (CarriedHeld(held) is not { } carried) return HandStatus.NoItem;
        if (PrototypeOf(carried) is not { Type: 0, Subtype: WeaponPrototypeData.Subtype } prototype) return HandStatus.NotWeapon;
        CombatWeapon weapon = CombatWeapon.Primary("", WeaponPrototypeData.Read(prototype));
        if (weapon.IsRanged) return HandStatus.Unsupported;
        // One unit per hand: a single item moves from the other hand, a stack of two can fill both.
        return HandStatus.Ready;
    }

    public HandStatus Wield(CarriedItem item, Hand hand, bool moving)
    {
        HandStatus status = CanWield(item, hand, moving);
        if (status != HandStatus.Ready) return status;
        var held = new HeldItem(item.Map, item.Source.Key);
        Hand other = Other(hand);
        if (Held(other) == held && CarriedHeld(held)!.Quantity < 2) SetHeld(other, null);
        SetHeld(hand, held);
        RearmFight();
        return HandStatus.Ready;
    }

    public HandStatus Unwield(Hand hand, bool moving)
    {
        if (InCombat && _activeFight?.InventoryOpen != true) return HandStatus.InCombat;
        if (moving) return HandStatus.Busy;
        if (Held(hand) == null) return HandStatus.NoItem;
        SetHeld(hand, null);
        RearmFight();
        return HandStatus.Ready;
    }

    // Inside a fight's open inventory the player's blows follow the hands at once; the fight's own active hand decides.
    private void RearmFight()
    {
        if (_activeFight is not { InventoryOpen: true } fight || fight.Combatants.FirstOrDefault(c => c.IsPlayer) is not { } player) return;
        Hand active = player.RightHandActive ? Hand.Right : Hand.Left;
        fight.ChangeWeapons(HandWeapon(active), HandWeapon(Other(active)));
    }

    private void SetHeld(Hand hand, HeldItem? held) { if (hand == Hand.Left) _leftHand = held; else _rightHand = held; }

    /// <summary>The blow of a hand: its weapon's primary attack, or the hand's best unarmed blow.</summary>
    public CombatWeapon HandWeapon(Hand hand)
    {
        var stats = DerivedCombatStats.ForPlayer(Sheet, Level);
        if (Held(hand) is { } held && CarriedHeld(held) is { } item && PrototypeOf(item) is { } prototype)
            return CombatWeapon.Primary($"PID {prototype.Pid}", WeaponPrototypeData.Read(prototype));
        return CombatWeapon.UnarmedFor(stats, Level, kick: hand == Hand.Right);
    }

    /// <summary>The weapon the player fights with: the active hand's.</summary>
    public CombatWeapon PlayerWeapon() => HandWeapon(ActiveHand);

    // An item no longer carried (or carried in fewer units than hands hold it) leaves the hands.
    private void ReleaseHands()
    {
        foreach (Hand hand in new[] { Hand.Right, Hand.Left })
            if (Held(hand) is { } held && (CarriedHeld(held)?.Quantity ?? 0) < HandsHolding(held)) SetHeld(hand, null);
    }

    private void ResetHands() { _leftHand = null; _rightHand = null; ActiveHand = Hand.Left; }

    // The hand active when a fight ends stays active (the player may have swapped in it).
    private void KeepHand(CombatantState player) => ActiveHand = player.RightHandActive ? Hand.Right : Hand.Left;

    private SavedHands? CaptureHands() => _leftHand == null && _rightHand == null && ActiveHand == Hand.Left
        ? null : new SavedHands(_leftHand, _rightHand, ActiveHand == Hand.Right ? true : null);

    // After items, body loot and the challenge's custody: each held item must be a carried melee weapon in enough units.
    private void RestoreHands(SavedHands? saved)
    {
        ResetHands();
        if (saved == null) return;
        if (saved.RightActive == false || saved.Left == null && saved.Right == null && saved.RightActive == null)
            throw new InvalidDataException("Checkpoint hands store their defaults explicitly.");
        foreach (var (hand, held) in new[] { (Hand.Left, saved.Left), (Hand.Right, saved.Right) })
        {
            if (held == null) continue;
            if (held.ItemKey == null || CarriedHeld(held) is not { } item || CanWield(item, hand, false) != HandStatus.Ready)
                throw new InvalidDataException("Checkpoint hand holds an item that is not a carried melee weapon.");
            SetHeld(hand, held);
        }
        if (saved.Left != null && saved.Left == saved.Right && CarriedHeld(saved.Left)!.Quantity < 2)
            throw new InvalidDataException("Checkpoint hands hold one unit twice.");
        ActiveHand = saved.RightActive == true ? Hand.Right : Hand.Left;
    }
}

/// <summary>The player's hands (optional, inside v13): the held items and true when the right hand strikes; absent at the defaults.</summary>
public sealed record SavedHands(HeldItem? Left, HeldItem? Right, bool? RightActive);
