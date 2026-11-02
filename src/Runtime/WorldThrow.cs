using System;
using System.Collections.Generic;
using System.Linq;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime.Combat;

namespace FalloutRemake.Runtime;

/// <summary>
/// FO2's attack modes and thrown weapons. A held weapon with a second attack (the spear's throw) switches the hand between
/// its modes, as the item button does; a throw moves the unit out of the hand to the landing hex (CE _action_ranged), where
/// it lies until the player picks it up beside it (CE actionPickUp, which costs only the walk). The mode is not saved:
/// a loaded game starts every hand on its first mode.
/// </summary>
public sealed partial class WorldSession
{
    private readonly HashSet<Hand> _secondMode = new();

    /// <summary>The weapon held in the hand has a second attack the hand can switch to.</summary>
    public bool HasSecondMode(Hand hand) => HeldWeaponData(hand) is { } weapon && CombatWeapon.Secondary("", weapon) != null;
    /// <summary>The hand strikes with its weapon's second attack.</summary>
    public bool UsesSecondMode(Hand hand) => _secondMode.Contains(hand) && HasSecondMode(hand);

    private WeaponPrototypeData? HeldWeaponData(Hand hand) =>
        Held(hand) is { } held && CarriedHeld(held) is { } item && PrototypeOf(item) is { Type: 0, Subtype: WeaponPrototypeData.Subtype } prototype
            ? WeaponPrototypeData.Read(prototype) : null;

    /// <summary>
    /// Switches the striking hand between its weapon's attacks. Out of a fight anytime but walking; in a fight on the player's
    /// own turn, for nothing (the fight's active hand decides).
    /// </summary>
    public HandStatus ToggleAttackMode(bool moving)
    {
        if (moving) return HandStatus.Busy;
        CombatantState? player = _activeFight?.Combatants.FirstOrDefault(c => c.IsPlayer);
        Hand hand = player == null ? ActiveHand : player.RightHandActive ? Hand.Right : Hand.Left;
        if (Held(hand) == null) return HandStatus.NoItem;
        if (!HasSecondMode(hand)) return HandStatus.Unsupported;
        if (player != null && _activeFight!.Active != player) return HandStatus.InCombat;
        if (!_secondMode.Remove(hand)) _secondMode.Add(hand);
        if (player != null) _activeFight!.ChangeMode(HandWeapon(hand));
        return HandStatus.Ready;
    }

    // A hand that takes another item starts on its first attack.
    private void ResetModeIfChanged(Hand hand, HeldItem? held) { if (Held(hand) != held) _secondMode.Remove(hand); }

    // The fight's player threw the weapon of its active hand: one unit lies on the landing hex, the hands strike with what is left.
    private (CombatWeapon Active, CombatWeapon Other)? OnThrown(CombatantState player, int landing)
    {
        Hand hand = player.RightHandActive ? Hand.Right : Hand.Left;
        if (Held(hand) is not { } held || CarriedHeld(held) is not { } item || !_areas.TryGetValue(item.Map, out WorldArea? area)) return null;
        if (area.Items.Drop(item.OwnerKey, held.ItemKey, new ActorLocation(CurrentMap, Location.Elevation, landing, 0)) != ItemUseStatus.Ready) return null;
        return (HandWeapon(hand), HandWeapon(Other(hand)));
    }

    /// <summary>Thrown units lying anywhere, by the area that keeps their source item.</summary>
    public (int SourceMap, GroundItem Item)[] GroundItems =>
        Areas.SelectMany(a => a.Items.CaptureGround().Select(g => (a.Map.Prefix.Header.MapIndex, g))).ToArray();

    /// <summary>The source object of a ground row (to name and draw it).</summary>
    public MapObject? GroundSource(int sourceMap, GroundItem item) =>
        _areas.TryGetValue(sourceMap, out WorldArea? area) ? area.Map.ObjectLayers.SelectMany(l => l.Objects).SelectMany(o => o.Inventory)
            .Select(e => e.Object).FirstOrDefault(o => o.Key == item.ItemKey) : null;

    public ItemTakeStatus CanPickUp(int sourceMap, GroundItem item, bool moving)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!_areas.TryGetValue(sourceMap, out WorldArea? area) || !area.Items.CaptureGround().Contains(item)) return ItemTakeStatus.NoItem;
        if (item.Place.Map != CurrentMap || item.Place.Elevation != Location.Elevation) return ItemTakeStatus.Unavailable;
        if (moving || _activeFight is { } fight && fight.Active is not { IsPlayer: true }) return ItemTakeStatus.Busy;
        if (Location.Hex != item.Place.Hex && FalloutGrid.Distance(Location.Hex, item.Place.Hex) != 1) return ItemTakeStatus.TooFar;
        // CE itemAdd refuses what the carry weight cannot take.
        long? unit = GroundSource(sourceMap, item) is { } source ? UnitWeight(sourceMap, source) : null, carried = CarriedWeight;
        if (!unit.HasValue || !carried.HasValue) return ItemTakeStatus.WeightUnavailable;
        return carried.Value > CarryCapacity || unit.Value * item.Quantity > CarryCapacity - carried.Value ? ItemTakeStatus.OverCapacity : ItemTakeStatus.Ready;
    }

    public ItemTakeStatus PickUp(int sourceMap, GroundItem item, bool moving)
    {
        ItemTakeStatus status = CanPickUp(sourceMap, item, moving);
        return status == ItemTakeStatus.Ready ? _areas[sourceMap].Items.PickUp(item) : status;
    }
}
