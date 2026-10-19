using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Import;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private InventoryInspector? _inventory;
    private bool InspectorOpen => _inventory?.Visible == true || _loot?.Visible == true || _bag?.Visible == true;
    private void CloseInventory()
    {
        if (_inventory == null) return;
        _inventory.Hide(); _inventory.QueueFree(); _inventory = null;
    }
    private void OpenInventory()
    {
        if (InspectorOpen) { CloseInventory(); return; }
        if (_selected == null || !CanInspectInventory(_selected.Source)) return;
        ClearRouteHover();
        GetTree().Root.GuiEmbedSubwindows = true;
        var dialog = new InventoryInspector { Size = new Vector2I(1000, 620), Theme = _hud.GetChild<Control>(0).Theme };
        _inventory = dialog; _hud.AddChild(dialog);
        dialog.Confirmed += CloseInventory; dialog.Canceled += CloseInventory; dialog.CloseRequested += CloseInventory;
        dialog.Inspect(_selected.Source, OriginalText);
        dialog.PopupCentered(new Vector2I(1000, 620));
    }
    private static bool CanInspectInventory(MapObject obj) => obj.Inventory.Length > 0
        || obj.Header.Pid >> 24 == 1 || (obj.Header.Pid >> 24 == 0 && obj.Subtype == 1);
    private async Task CheckInventoryInspector()
    {
        ResetWalker(); var location = _campaign!.Location; int roots = _lookup.Count;
        var owner = _layout!.Objects.First(o => o.Source.Inventory.Length > 0);
        SelectObject(owner);
        var button = _hud.FindChild("InventoryAction", true, false) as Button ?? throw new InvalidDataException("Inventory button missing.");
        if (button.Disabled) throw new InvalidDataException("Inventory owner action unavailable.");
        RequestWalk(24508); _walker!.Advance(.05); UpdateWalkerPosition();
        var walkingPosition = _walker.Position;
        using var inspect = new InputEventKey { Pressed = true, PhysicalKeycode = Key.I };
        _UnhandledInput(inspect);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        MapObject first = owner.Source.Inventory[0].Object;
        if (!InspectorOpen || _inventory!.ItemRows != MapDocument.Flatten(owner.Source).Count() - 1 || _inventory.SelectedSourceKey != first.Key
            || !_inventory.DetailText.Contains($"PID  {first.Header.Pid}") || _lookup.Count != roots || _campaign.Location != location)
            throw new InvalidDataException("Inventory inspection lost source identity or spawned its contents into the world.");
        if (_walker.Position != walkingPosition || !_walker.Moving)
            throw new InvalidDataException("Inventory inspection failed to suspend movement without cancelling its route.");
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        if (!_inventory.ReadableLayout || _inventory.Size.X > viewport.X - 32 || _inventory.Size.Y > viewport.Y - 32)
            throw new InvalidDataException($"Inventory layout is unreadable or exceeds viewport: {_inventory.Size}.");
        Vector3 camera = _camera.Position;
        using var rotate = new InputEventKey { Pressed = true, PhysicalKeycode = Key.E };
        _UnhandledInput(rotate); _inventory.EmitSignal(Window.SignalName.WindowInput, inspect);
        if (_camera.Position != camera || InspectorOpen) throw new InvalidDataException("Inventory modal did not isolate keys or close with I.");
        button.EmitSignal(Button.SignalName.Pressed);
        if (!InspectorOpen) throw new InvalidDataException("Inventory button failed.");
        if (_capture != null) await Capture(Path.ChangeExtension(_capture, "inventory.png"));
        _inventory!.EmitSignal(AcceptDialog.SignalName.Confirmed);
        var empty = _layout.Objects.First(o => CanInspectInventory(o.Source) && o.Source.Inventory.Length == 0);
        SelectObject(empty); _UnhandledInput(inspect);
        if (!InspectorOpen || _inventory!.ItemRows != 0 || !_inventory.DetailText.Contains("não contém"))
            throw new InvalidDataException("Empty inventory inspection failed.");
        CloseInventory(); ClearSelection();
        if (!button.Disabled) throw new InvalidDataException("Inventory remained enabled without selection.");
        MapLayoutWriter.Verify(_map!); ResetWalker();
        GD.Print("INVENTORY_INSPECTOR_OK: real owner, original names, quantities, source keys, empty contents, I/button, modal input, close, unchanged actor/root count and source bytes verified.");
    }
}
