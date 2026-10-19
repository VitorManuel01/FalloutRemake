using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private PlayerBagWindow? _bag;
    private void CloseBag() { if (_bag != null) { _bag.Hide(); _bag.QueueFree(); _bag = null; } }
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
        CloseInventory(); CloseLoot(); ClearRouteHover();
        GetTree().Root.GuiEmbedSubwindows = true;
        var dialog = new PlayerBagWindow { Size = new Vector2I(960, 680), Theme = _hud.GetChild<Control>(0).Theme };
        _bag = dialog; _hud.AddChild(dialog);
        dialog.Confirmed += CloseBag; dialog.Canceled += CloseBag; dialog.CloseRequested += CloseBag;
        dialog.ReturnRequested = ReturnFromBag;
        dialog.ApproachRequested = ApproachBagOrigin;
        RefreshBag(); dialog.PopupCentered(new Vector2I(960, 680));
    }
    private void RefreshBag() => _bag?.Refresh(_campaign!.Carried, CarriedText, MapDisplayName, _campaign.CarriedWeight, _campaign.CarryCapacity,
        item => _campaign.UnitWeight(item.Map, item.Source),
        (item, qty) => _campaign.CanReturnItem(item.Map, item.OwnerKey, item.Source.Key, qty, _walker?.Moving == true),
        item => ItemOriginRoute.Find(_campaign, item, _walker?.Moving == true), item => InventoryTexture(item.Map, item.Source));
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
            foreach (WorldArea area in _campaign.Areas) MapLayoutWriter.Verify(area.Map);
            ResetWalker();
            GD.Print("PLAYER_BAG_OK: B/button, empty bag, original powder name/ID/quantity/origin after exterior checkpoint reload, readable modal, movement/camera isolation, unchanged items and source bytes verified.");
        }
        finally { CloseBag(); _checkingNavigation = false; }
    }
}
