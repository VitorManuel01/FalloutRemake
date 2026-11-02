using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private PlayerBagWindow? _bag;
    private void CloseBag()
    {
        if (_bag != null) { _bag.Hide(); _bag.QueueFree(); _bag = null; }
        // Closing the bag in a fight closes the inventory the turn paid for.
        if (_combat is { InventoryOpen: true } && _combat.Active is { IsPlayer: true } player) ApplyCombatResult(_combat.Execute(CombatActionProposal.CloseInventory(player.Id)));
    }
    private PrototypeText? CarriedText(CarriedItem item)
    {
        if (_objectText == null || _campaign == null) return null;
        try
        {
            WorldArea source = _campaign.Areas.Single(a => a.Map.Prefix.Header.MapIndex == item.Map);
            return _objectText.Resolve(source.Map.Prototypes.Single(p => p.Pid == item.Source.Header.Pid));
        }
        catch (Exception error) when (error is IOException or InvalidDataException)
        { GD.Print("CARRIED_TEXT_UNAVAILABLE: " + error.Message); return null; }
    }
    private void OpenBag()
    {
        if (_bag?.Visible == true) { CloseBag(); return; }
        if (_campaign == null) return;
        // In a fight the bag is CE's inventory: only on the player's turn, for 4 AP (inventoryOpen).
        if (_combat != null)
        {
            if (_combat.Active is not { IsPlayer: true } fighter) { NavigationStatus("Aguarde: é o turno da criatura."); return; }
            CombatActionResult opened = _combat.Execute(CombatActionProposal.OpenInventory(fighter.Id));
            if (opened.Status != CombatActionStatus.Ready) { NavigationStatus($"Bolsa fechada · abrir em combate custa {CombatSession.InventoryCost(fighter)} AP"); return; }
            ApplyCombatResult(opened);
        }
        CloseBarrierEffectReview(); CloseUseClock(); CloseExplosivePreparation(); CloseInventory(); CloseLoot(); ClearRouteHover();
        GetTree().Root.GuiEmbedSubwindows = true;
        var dialog = new PlayerBagWindow { Size = new Vector2I(960, 680), Theme = _hud.GetChild<Control>(0).Theme };
        _bag = dialog; _hud.AddChild(dialog);
        dialog.Confirmed += CloseBag; dialog.Canceled += CloseBag; dialog.CloseRequested += CloseBag;
        dialog.ReturnRequested = ReturnFromBag;
        Guid sessionToken = _campaign.SessionToken;
        dialog.ReturnAllRequested = item =>
        {
            if (_bag != dialog || _campaign?.SessionToken != sessionToken || dialog.SelectedItem is not { } selected
                || selected.Map != item.Map || selected.OwnerKey != item.OwnerKey || selected.Source.Key != item.Source.Key) return;
            ReturnAllToOrigin(item);
        };
        dialog.ApproachRequested = ApproachBagOrigin;
        dialog.PrepareExplosiveRequested = OpenExplosivePreparation;
        dialog.UseDrugRequested = item => { if (_bag == dialog && _campaign?.SessionToken == sessionToken) UseDrugFromBag(item); };
        dialog.BarrierApproachRequested = ApproachBarrierForPlacement;
        dialog.WieldRequested = (item, hand) => { if (_bag == dialog && _campaign?.SessionToken == sessionToken) WieldFromBag(item, hand); };
        dialog.PutAwayRequested = hand => { if (_bag == dialog && _campaign?.SessionToken == sessionToken) PutAwayFromBag(hand); };
        dialog.WearRequested = item => { if (_bag == dialog && _campaign?.SessionToken == sessionToken) WearFromBag(item); };
        dialog.TakeOffRequested = () => { if (_bag == dialog && _campaign?.SessionToken == sessionToken) TakeOffFromBag(); };
        RefreshBag(); dialog.PopupCenteredClamped(new Vector2I(960, 680), .92f);
    }
    private void RefreshBag() => _bag?.Refresh(_campaign!.Carried, CarriedText, MapDisplayName, _campaign.CarriedWeight, _campaign.CarryCapacity,
        item => _campaign.UnitWeight(item.Map, item.Source),
        (item, qty) => _campaign.CanReturnItem(item.Map, item.OwnerKey, item.Source.Key, qty, _walker?.Moving == true),
        // No walking back to a container in the middle of a fight.
        item => InCombat ? null : ItemOriginRoute.Find(_campaign, item, _walker?.Moving == true), item => InventoryTexture(item.Map, item.Source),
        item => ProposeExplosivePreparation(item, ExplosiveTimer.DefaultSeconds).Status, PreparationHint, item => BarrierPlacementApproach(item).HasValue,
        item => _campaign.CanReturnAllItems(item.Map, item.OwnerKey, _walker?.Moving == true), item => _campaign.CanUseDrug(item, _walker?.Moving == true),
        (_campaign.Character.Kind == CharacterKind.TestNeutral ? $"Força {CarryWeight.TestStrength} (perfil de teste)" : $"Força {_campaign.Character.Effective.Strength} · {_campaign.Character.Name}")
            + (_campaign.TempleChestCustody.Length == 0 ? "" : $" · {_campaign.TempleChestCustody.Sum(c => c.Quantity)} unidade(s) no baú do desafio (J ao lado do baú)"),
        HeldIn, (item, hand) => _campaign.CanWield(item, hand, _walker?.Moving == true), IsWorn, item => _campaign.CanWear(item, _walker?.Moving == true));
    // Outside combat a drug costs no AP (CE); the roll uses the fight seed in automated checks, the clock otherwise.
    private void UseDrugFromBag(CarriedItem item)
    {
        if (_campaign == null) return;
        DrugUseResult result = _campaign.UseDrug(item, new Runtime.Combat.SeededCombatRandom(_combatSeed ?? (ulong)DateTime.UtcNow.Ticks), _walker?.Moving == true);
        NavigationStatus(result.Status == DrugUseStatus.Ready
            ? $"Healing Powder usado · +{result.Healed} HP ({result.HitPoints}/{_campaign.MaxHitPoints}){(result.Healed < result.Rolled ? $" · {result.Rolled - result.Healed} além do máximo se perderam" : "")} · K para salvar"
            : PlayerBagWindow.DrugHint(result.Status));
        RefreshBag(); RefreshDoorAction();
    }
    private void ApproachBagOrigin(CarriedItem item)
    {
        if (_campaign == null) return;
        ItemOriginPlan? plan = ItemOriginRoute.Find(_campaign, item, _walker?.Moving == true);
        if (plan == null) { RefreshBag(); return; }
        ItemOriginLeg leg = plan.Legs[0];
        CloseBag();
        if (_elevation != _campaign.Location.Elevation) ChangeElevation(_campaign.Location.Elevation);
        if (!_walkMode) ToggleWalkMode();
        if (leg.Destination == null) ApproachContainerFromClick(_layout!.Objects.Single(o => o.Source.Key == item.OwnerKey));
        else
        {
            int target = leg.Tiles[^1];
            SelectObject(_layout!.Objects.Where(o => o.Kind == Geometry.PreviewKind.Exit && o.Source.Header.Tile == target).OrderBy(o => o.Source.Offset).First());
            if (target == _walker!.CurrentHex) NavigationStatus(ExitHint(CurrentExit()));
            else { RequestWalk(target); NavigationStatus($"Caminhando até a passagem · Enter ao chegar · abra B depois para continuar"); }
        }
    }
    private void ReturnFromBag(CarriedItem item, int quantity)
    {
        if (_campaign == null) return;
        ItemTakeStatus status = _campaign.ReturnItem(item.Map, item.OwnerKey, item.Source.Key, quantity, _walker?.Moving == true);
        NavigationStatus(status == ItemTakeStatus.Ready ? $"Item devolvido ao recipiente · bolsa com {_campaign.CarriedCount} unidades · K para salvar" : ContainerLootWindow.Hint(status));
        RefreshBag();
        RefreshContainerVisuals();
    }
    private void ReturnAllToOrigin(CarriedItem item)
    {
        if (_bag?.Visible != true || _campaign == null) return;
        long before = _campaign.CarriedCount;
        ItemTakeStatus status = _campaign.ReturnAllItems(item.Map, item.OwnerKey, _walker?.Moving == true);
        NavigationStatus(status == ItemTakeStatus.Ready ? $"Devolvidas {before - _campaign.CarriedCount} unidade(s) à mesma origem · K para salvar" : ContainerLootWindow.Hint(status));
        RefreshBag(); RefreshContainerVisuals();
    }
    private async Task CheckPlayerBag()
    {
        _checkingNavigation = true;
        try
        {
            ResetWalker(); using var key = new InputEventKey { Pressed = true, PhysicalKeycode = Key.B };
            _UnhandledInput(key);
            if (_bag == null || _bag.RowCount != 0 || !_bag.DetailText.Contains("Recolha")) throw new InvalidDataException("Empty bag was not available with B.");
            _bag.EmitSignal(Window.SignalName.WindowInput, key);
            ToggleWalkMode(); RequestWalk(24508); _walker!.Advance(1000); UpdateWalkerPosition(); UseDoor(_layout!.Objects.Single(o => o.Source.Offset == 244040).Source.Key);
            var pot = _campaign!.Current.Items.Containers.Single(o => o.Offset == 206348); var powder = _campaign.Current.Items.Contents(pot.Key).Single();
            RequestWalk(ContainerApproach(pot)!.Value); _walker!.Advance(1000); UpdateWalkerPosition();
            if (_campaign.TakeItem(pot.Key, powder.Source.Key, 1, false) != ItemTakeStatus.Ready) throw new InvalidDataException("Bag fixture transfer failed.");
            var name = CarriedText(_campaign.Carried.Single())?.Name ?? throw new InvalidDataException("Original carried item name missing.");
            RequestWalk(ClosestExitTo(126)); _walker!.Advance(1000); UpdateWalkerPosition(); UseExit(); SaveCheckpoint();
            ResetWalker(); LoadCheckpoint();
            var before = _campaign.Location; long count = _campaign.CarriedCount;
            ((Button)_hud.FindChild("BagAction", true, false)).EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Vector2 viewport = GetViewport().GetVisibleRect().Size;
            if (_bag == null || _bag.RowCount != 1 || _bag.TotalQuantity != 1 || _campaign.CarriedWeight != 1 || _campaign.CarryCapacity != 150 || _bag.SelectedItem?.Source.Header.Id != 106
                || _bag.SelectedItem.Map != 3 || !_bag.DetailText.Contains(name) || !_bag.DetailText.Contains(MapDisplayName(3)) || !_bag.SummaryText.Contains("1 / 150 lb") || _bag.SelectedWeightText != "1 lb" || _bag.SelectedArtFid != 0x070000AA
                || !_bag.ReadableLayout || _bag.Size.X > viewport.X - 32 || _bag.Size.Y > viewport.Y - 32 || _campaign.Location != before
                || !((Button)_bag.FindChild("ReturnOriginAction", true, false)).Disabled
                || ((Button)_hud.FindChild("LoadAction", true, false)).GetGlobalRect().End.X > viewport.X - 15)
                throw new InvalidDataException("Bag lost original item identity/name from an inactive area or its readable layout.");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "bag.png"));
            _bag.EmitSignal(AcceptDialog.SignalName.Confirmed);
            RequestWalk(ClosestExitTo(3)); _walker!.Advance(.05); UpdateWalkerPosition(); var position = _walker.Position; float yaw = _yaw;
            _UnhandledInput(key); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            using var rotate = new InputEventKey { Pressed = true, PhysicalKeycode = Key.E }; _UnhandledInput(rotate);
            if (_bag == null || _walker.Position != position || _yaw != yaw || _campaign.CarriedCount != count)
                throw new InvalidDataException("Bag modal changed movement, camera or collected items.");
            _bag.EmitSignal(Window.SignalName.WindowInput, key);
            if (_bag != null) throw new InvalidDataException("B did not close bag.");
            // The original powder can be taken from the bag only while standing; outside combat it costs no AP and is used up.
            OpenBag(); var use = (Button)_bag!.FindChild("UseDrugAction", true, false);
            if (!use.Visible || !use.Disabled || !use.TooltipText.Contains("andar")) throw new InvalidDataException("Healing Powder use was offered while walking.");
            CloseBag(); _walker.Advance(1000); UpdateWalkerPosition(); OpenBag(); use = (Button)_bag!.FindChild("UseDrugAction", true, false);
            if (use.Disabled) throw new InvalidDataException("Healing Powder use unavailable while standing: " + use.TooltipText);
            use.EmitSignal(Button.SignalName.Pressed);
            if (_campaign.CarriedCount != 0 || _campaign.HitPoints != _campaign.MaxHitPoints || _bag?.RowCount != 0
                || _campaign.Areas.Single(a => a.Map.Prefix.Header.MapIndex == 3).Items.Balance(pot.Key, powder.Source.Key) is not { Consumed: 1, Carried: 0 })
                throw new InvalidDataException("Using the Healing Powder from the bag did not use up exactly one unit.");
            CloseBag();
            foreach (WorldArea area in _campaign.Areas) MapLayoutWriter.Verify(area.Map);
            ResetWalker();
            GD.Print("PLAYER_BAG_OK: B/button, empty bag, original powder name/ID/quantity/origin after exterior checkpoint reload, readable modal, movement/camera isolation, unchanged items, Healing Powder used from the bag only while standing; source bytes verified.");
        }
        finally { CloseBag(); _checkingNavigation = false; }
    }
}
