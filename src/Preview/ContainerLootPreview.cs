using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private ContainerLootWindow? _loot;
    private string? _lootOwner;
    private void RefreshLootAction() => _hud.LootAction(_selected?.Source is { Subtype: 1 } source && source.Header.Pid >> 24 == 0);
    private void CloseLoot()
    { if (_loot != null) { _loot.Hide(); _loot.QueueFree(); _loot = null; } _lootOwner = null; }
    private int? ContainerApproach(MapObject owner) => _walker?.Moving == false && owner.Header.Elevation == _elevation && _reachable != null
        ? Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(owner.Header.Tile, d)).Append(owner.Header.Tile)
            .Where(t => t.HasValue && _reachable.Distance(t.Value) >= 0 && !_navigation!.IsExit(t.Value))
            .OrderBy(t => _reachable.Distance(t!.Value)).ThenBy(t => t).FirstOrDefault() : null;
    private void OpenLoot()
    {
        if (_loot?.Visible == true) { CloseLoot(); return; }
        if (_selected?.Source is not { Subtype: 1 } owner || owner.Header.Pid >> 24 != 0 || _campaign == null) return;
        CloseInventory(); ClearRouteHover(); _lootOwner = owner.Key;
        GetTree().Root.GuiEmbedSubwindows = true;
        var dialog = new ContainerLootWindow { Size = new Vector2I(900, 560), Theme = _hud.GetChild<Control>(0).Theme };
        _loot = dialog; _hud.AddChild(dialog);
        dialog.Confirmed += CloseLoot; dialog.Canceled += CloseLoot; dialog.CloseRequested += CloseLoot;
        dialog.TakeRequested = TakeFromContainer; dialog.ApproachRequested = ApproachLootContainer;
        RefreshLootWindow(); dialog.PopupCentered(new Vector2I(900, 560));
    }
    private void RefreshLootWindow()
    {
        if (_loot == null || _lootOwner == null || _campaign == null) return;
        MapObject owner = _campaign.Current.Items.Containers.Single(o => o.Key == _lootOwner);
        _loot.Refresh(OriginalText(owner)?.Name ?? "Recipiente", _campaign.Current.Items.Contents(_lootOwner), OriginalText,
            (key, qty) => _campaign.Current.Items.CanTake(_lootOwner, key, qty, _campaign.Location,
                _doors!.Navigation(_campaign.Location.Elevation), _walker?.Moving == true), _campaign.CarriedCount, ContainerApproach(owner).HasValue);
    }
    private void TakeFromContainer(string key, int quantity)
    {
        if (_lootOwner == null || _campaign == null) return;
        ItemTakeStatus status = _campaign.TakeItem(_lootOwner, key, quantity, _walker?.Moving == true);
        NavigationStatus(status == ItemTakeStatus.Ready ? $"Item recolhido · bolsa com {_campaign.CarriedCount} {(_campaign.CarriedCount == 1 ? "unidade" : "unidades")} · K para salvar" : ContainerLootWindow.Hint(status));
        RefreshLootWindow();
    }
    private void ApproachLootContainer()
    {
        if (_lootOwner == null || _campaign == null) return;
        MapObject owner = _campaign.Current.Items.Containers.Single(o => o.Key == _lootOwner);
        int? target = ContainerApproach(owner); if (!target.HasValue) return;
        CloseLoot(); if (!_walkMode) ToggleWalkMode(); RequestWalk(target.Value);
    }
    private async Task CheckContainerLoot()
    {
        _checkingNavigation = true;
        try
        {
            ResetWalker(); ToggleWalkMode();
            PreviewObject pot = _layout!.Objects.Single(o => o.Source.Offset == 206348); SelectObject(pot);
            using var key = new InputEventKey { Pressed = true, PhysicalKeycode = Key.J };
            _UnhandledInput(key);
            if (_loot == null || !_loot.StatusText.Contains("Sem caminho")) throw new InvalidDataException("Unreachable container offered an approach.");
            CloseLoot(); RequestWalk(24508); _walker!.Advance(1000); UpdateWalkerPosition(); UseDoor(_layout.Objects.Single(o => o.Source.Offset == 244040).Source.Key);
            SelectObject(pot); _UnhandledInput(key);
            var one = (Button)_loot!.FindChild("TakeOneAction", true, false);
            var approach = (Button)_loot.FindChild("ApproachContainerAction", true, false);
            if (!one.Disabled || approach.Disabled || _campaign!.CarriedCount != 0) throw new InvalidDataException("Distant container allowed immediate transfer.");
            approach.EmitSignal(Button.SignalName.Pressed);
            if (_loot != null || !_walker.Moving || !_walkMode) throw new InvalidDataException("Container approach failed to start walking.");
            _walker.Advance(.05); UpdateWalkerPosition(); var position = _walker.Position;
            _UnhandledInput(key); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_loot == null || !_loot.StatusText.Contains("aguarde") || _walker.Position != position) throw new InvalidDataException("Loot window failed to pause movement or gate transfer.");
            _loot.EmitSignal(Window.SignalName.WindowInput, key); _walker.Advance(1000); UpdateWalkerPosition();
            if (_loot != null) throw new InvalidDataException("J did not close the loot window.");
            var location = _campaign.Location; var doors = _doors!.Capture(); int roots = _lookup.Count;
            var hudButton = (Button)_hud.FindChild("LootAction", true, false); hudButton.EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            one = (Button)_loot!.FindChild("TakeOneAction", true, false);
            Vector2 viewport = GetViewport().GetVisibleRect().Size;
            if (one.Disabled || !_loot.ReadableLayout || _loot.Remaining != 1 || _loot.Size.X > viewport.X - 32 || _loot.Size.Y > viewport.Y - 32)
                throw new InvalidDataException($"Nearby original item was not readable or collectable: disabled={one.Disabled}, list={_loot.ListSize}, window={_loot.Size}, remaining={_loot.Remaining}, actor={_campaign.Location.Hex}, moving={_walker.Moving}, status={_loot.StatusText}.");
            one.EmitSignal(Button.SignalName.Pressed);
            if (_campaign.CarriedCount != 1 || _campaign.Carried.Single().Source.Header.Id != 106 || _loot.Remaining != 0
                || !one.Disabled || !_loot.StatusText.Contains("já foi recolhida")) throw new InvalidDataException("Loot action lost quantity/identity or allowed duplication.");
            one.EmitSignal(Button.SignalName.Pressed);
            if (_campaign.CarriedCount != 1 || _campaign.Location != location || _lookup.Count != roots || !_doors.Capture().Doors.SequenceEqual(doors.Doors))
                throw new InvalidDataException("Repeated or modal collection changed unrelated world state.");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "loot.png"));
            _loot.EmitSignal(AcceptDialog.SignalName.Confirmed); SaveCheckpoint(); ResetWalker(); LoadCheckpoint();
            SelectObject(_layout!.Objects.Single(o => o.Source.Offset == 206348)); _UnhandledInput(key);
            if (_loot!.Remaining != 0 || _campaign.CarriedCount != 1) throw new InvalidDataException("Reopened container duplicated checkpoint contents.");
            CloseLoot(); MapLayoutWriter.Verify(_map!); ResetWalker();
            GD.Print("CONTAINER_LOOT_OK: J/button, blocked/distant approach, paused modal movement, original powder ID 106, exact remaining quantity, repeat refusal, checkpoint/reopen, unchanged location/doors/root objects/source bytes verified.");
        }
        finally { CloseLoot(); _checkingNavigation = false; }
    }
}
