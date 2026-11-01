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

    private bool IsWorn(CarriedItem item) => _campaign?.Worn is { } worn && worn.Map == item.Map && worn.ItemKey == item.Source.Key;

    private string CarriedName(HeldItem held, string fallback) =>
        _campaign!.Carried.FirstOrDefault(c => c.Map == held.Map && c.Source.Key == held.ItemKey) is { } item
            ? CarriedText(item)?.Name is { } name ? ShortName(name) : $"PID {item.Source.Header.Pid}" : fallback;

    private string HandBlowName(Hand hand) => _campaign!.Held(hand) is { } held ? CarriedName(held, _campaign.HandWeapon(hand).Name) : _campaign.HandWeapon(hand).Name;

    private string HandsLine() => _campaign == null ? ""
        : $"Mãos: esquerda {HandBlowName(Hand.Left)}{(_campaign.ActiveHand == Hand.Left ? " (ativa)" : "")} · direita {HandBlowName(Hand.Right)}{(_campaign.ActiveHand == Hand.Right ? " (ativa)" : "")}"
            + (_campaign.Worn is { } worn ? $" · Armadura: {CarriedName(worn, "?")} (CA {_campaign.PlayerStats().ArmorClass}{PerkText(_campaign.WornPerk())})" : "");

    // The worn armor's perk (CE perkAddEffect): bonus Strength and radiation resistance.
    private static string PerkText(ArmorPerk perk) => (perk.Strength > 0 ? $", Força +{perk.Strength}" : "")
        + (perk.RadiationResistance > 0 ? $", radiação +{perk.RadiationResistance}%" : "");

    // The status panel's second line: what each hand holds and which one strikes.
    private string HandsSuffix() => _campaign == null ? "" : "\n" + HandsLine();

    private void WieldFromBag(CarriedItem item, Hand hand)
    {
        if (_campaign == null) return;
        int logged = _combat?.Log.Count ?? 0;
        HandStatus status = _campaign.Wield(item, hand, _walker?.Moving == true);
        // In a fight the change shows in the combat log (WeaponsChanged) and the HUD names the new blow.
        if (status == HandStatus.Ready && _combat != null) { _combatWeapon = NamedWeapon(_combat.Get("player").Weapon); ApplyCombatEvents(_combat.Log.Skip(logged).ToArray()); }
        NavigationStatus(status == HandStatus.Ready ? $"{HandBlowName(hand)} na mão {(hand == Hand.Left ? "esquerda" : "direita")} · {HandsLine()}" : PlayerBagWindow.HandHint(status));
        RefreshBag(); RefreshDoorAction();
    }

    private void PutAwayFromBag(Hand hand)
    {
        if (_campaign == null) return;
        int logged = _combat?.Log.Count ?? 0;
        HandStatus status = _campaign.Unwield(hand, _walker?.Moving == true);
        // In a fight the change shows in the combat log (WeaponsChanged) and the HUD names the new blow.
        if (status == HandStatus.Ready && _combat != null) { _combatWeapon = NamedWeapon(_combat.Get("player").Weapon); ApplyCombatEvents(_combat.Log.Skip(logged).ToArray()); }
        NavigationStatus(status == HandStatus.Ready ? $"Item guardado na bolsa · {HandsLine()}" : PlayerBagWindow.HandHint(status));
        RefreshBag(); RefreshDoorAction();
    }

    // FO2's armor slot: in a fight the change shows in the combat log (ArmorChanged).
    private void WearFromBag(CarriedItem item)
    {
        if (_campaign == null) return;
        int logged = _combat?.Log.Count ?? 0;
        HandStatus status = _campaign.Wear(item, _walker?.Moving == true);
        if (status == HandStatus.Ready && _combat != null) ApplyCombatEvents(_combat.Log.Skip(logged).ToArray());
        NavigationStatus(status == HandStatus.Ready ? $"Armadura vestida · {HandsLine()}" : PlayerBagWindow.WearHint(status));
        RefreshBag(); RefreshDoorAction();
    }

    private void TakeOffFromBag()
    {
        if (_campaign == null) return;
        int logged = _combat?.Log.Count ?? 0;
        HandStatus status = _campaign.TakeOff(_walker?.Moving == true);
        if (status == HandStatus.Ready && _combat != null) ApplyCombatEvents(_combat.Log.Skip(logged).ToArray());
        NavigationStatus(status == HandStatus.Ready ? $"Armadura tirada · CA {_campaign.PlayerStats().ArmorClass} · {HandsLine()}" : PlayerBagWindow.WearHint(status));
        RefreshBag(); RefreshDoorAction();
    }

    // Out of a fight: X makes the other hand the active one (FO2's swap; B already opens the bag here).
    private void SwitchUnarmedHand()
    {
        if (_campaign == null || InCombat) return;
        _campaign.SetActiveHand(WorldSession.Other(_campaign.ActiveHand));
        CombatWeapon weapon = _campaign.PlayerWeapon();
        int meleeDamage = weapon.AddsMeleeDamage ? _campaign.PlayerStats().MeleeDamage : 0;
        NavigationStatus($"Mão {(_campaign.ActiveHand == Hand.Left ? "esquerda" : "direita")} ativa · {HandBlowName(_campaign.ActiveHand)} ({weapon.ActionPoints} AP, dano "
            + $"{weapon.BonusDamage + weapon.MinDamage}–{weapon.BonusDamage + weapon.MaxDamage + meleeDamage}) · {HandsLine()}");
        RefreshDoorAction();
    }
}
