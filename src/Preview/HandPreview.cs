using System.Linq;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    // FO2's item slots in the preview: the bag puts carried weapons into a hand, X picks the hand that strikes.
    private Hand? HeldIn(CarriedItem item) => _campaign == null ? null
        : _campaign.Held(Hand.Left) is { } left && left.Map == item.Map && left.ItemKey == item.Source.Key ? Hand.Left
        : _campaign.Held(Hand.Right) is { } right && right.Map == item.Map && right.ItemKey == item.Source.Key ? Hand.Right : null;

    private string HandBlowName(Hand hand)
    {
        if (_campaign!.Held(hand) is { } held && _campaign.Carried.FirstOrDefault(c => c.Map == held.Map && c.Source.Key == held.ItemKey) is { } item)
            return CarriedText(item)?.Name is { } name ? ShortName(name) : $"PID {item.Source.Header.Pid}";
        return _campaign.HandWeapon(hand).Name;
    }

    private string HandsLine() => _campaign == null ? ""
        : $"Mãos: esquerda {HandBlowName(Hand.Left)}{(_campaign.ActiveHand == Hand.Left ? " (ativa)" : "")} · direita {HandBlowName(Hand.Right)}{(_campaign.ActiveHand == Hand.Right ? " (ativa)" : "")}";

    // The status panel's second line: what each hand holds and which one strikes.
    private string HandsSuffix() => _campaign == null ? "" : "\n" + HandsLine();

    private void WieldFromBag(CarriedItem item, Hand hand)
    {
        if (_campaign == null) return;
        HandStatus status = _campaign.Wield(item, hand, _walker?.Moving == true);
        NavigationStatus(status == HandStatus.Ready ? $"{HandBlowName(hand)} na mão {(hand == Hand.Left ? "esquerda" : "direita")} · {HandsLine()}" : PlayerBagWindow.HandHint(status));
        RefreshBag(); RefreshDoorAction();
    }

    private void PutAwayFromBag(Hand hand)
    {
        if (_campaign == null) return;
        HandStatus status = _campaign.Unwield(hand, _walker?.Moving == true);
        NavigationStatus(status == HandStatus.Ready ? $"Item guardado na bolsa · {HandsLine()}" : PlayerBagWindow.HandHint(status));
        RefreshBag(); RefreshDoorAction();
    }

    // Out of a fight: X makes the other hand the active one (FO2's swap; B already opens the bag here).
    private void SwitchUnarmedHand()
    {
        if (_campaign == null || InCombat) return;
        _campaign.SetActiveHand(WorldSession.Other(_campaign.ActiveHand));
        CombatWeapon weapon = _campaign.PlayerWeapon();
        int meleeDamage = weapon.AddsMeleeDamage ? DerivedCombatStats.ForPlayer(_campaign.Sheet, _campaign.Level).MeleeDamage : 0;
        NavigationStatus($"Mão {(_campaign.ActiveHand == Hand.Left ? "esquerda" : "direita")} ativa · {HandBlowName(_campaign.ActiveHand)} ({weapon.ActionPoints} AP, dano "
            + $"{weapon.BonusDamage + weapon.MinDamage}–{weapon.BonusDamage + weapon.MaxDamage + meleeDamage}) · {HandsLine()}");
        RefreshDoorAction();
    }
}
