using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Geometry;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private async Task CheckSceneReview()
    {
        int containers = 0, lamps = 0;
        float yaw = _yaw;
        // Test target/proxy resolution with walls filtered out, so unrelated solid walls
        // cannot intercept rays from the opposite side of a room.
        _hud.Filters["wall"] = false; ApplyFilters();
        for (int elevation = 0; elevation < 3; elevation++)
        {
            ChangeElevation(elevation);
            foreach (var obj in _layout!.Objects.Where(o => EntityPresentation.Role(o.Source) is EntityVisualRole.Container or EntityVisualRole.LightSource).ToArray())
            {
                var proxy = _layout.Objects.SingleOrDefault(o => o.Source.Header.Tile == obj.Source.Header.Tile && EntityPresentation.Role(o.Source) == EntityVisualRole.Barrier);
                if (proxy == null) continue;
                bool technical = EntityPresentation.IsBlockingProxy(proxy.Source);
                if ((technical ? !IsSharedBlockingProxy(proxy) || _bodies[proxy.Source.Key].GetChild<MeshInstance3D>(0).Visible : !_bodies[proxy.Source.Key].HasMeta("shared_light_obstacle"))
                    || !_doors!.Navigation(elevation).BlockersAt(obj.Source.Header.Tile).Any(b => b.Key == proxy.Source.Key))
                    throw new InvalidDataException("Shared technical blocker lost its floor presentation or original navigation identity.");
                bool container = IsContainer(obj);
                if (container) containers++; else lamps++;
                foreach (float angle in new[] { 0f, 1.57f, 3.14f })
                {
                    _yaw = angle; _focus = Point(obj.Position, 0); _camera.Size = 18; UpdateCamera();
                    await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                    await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                    foreach (float height in container ? new[] { .2f, .5f } : new[] { .5f, 1.03f })
                    {
                        Pick(_camera.UnprojectPosition(Point(obj.Position, height)));
                        if (_selected?.Source.Key != obj.Source.Key) throw new InvalidDataException($"Inspection target {obj.Source.Offset} at {angle}/{height}: picked {_selected?.Source.Offset}/{_selected?.Kind}; screen={_camera.UnprojectPosition(Point(obj.Position, height))}; viewport={GetViewport().GetVisibleRect()}; body={_bodies[obj.Source.Key].CollisionLayer}/{_bodies[obj.Source.Key].GlobalPosition}.");
                    }
                }
                SelectObject(obj);
                if (FindHudButton("CycleAction").Disabled || !_hud.SelectionDetails.Contains("no hex")) throw new InvalidDataException("Shared hex lost cycling or overlap count.");
                CycleSelection();
                if (_selected?.Source.Key != proxy.Source.Key) throw new InvalidDataException("Technical source object cannot be inspected by cycling.");
            }
        }
        if (containers != 7 || lamps != 3) throw new InvalidDataException($"Expected 7 shared containers and 3 shared lamps, got {containers}/{lamps}.");
        var blockingLight = _layout!.Objects.Single(o => o.Source.Offset == 432688);
        SelectObject(blockingLight);
        if (!EntityPresentation.Blocks(blockingLight.Source) || !_hud.SelectionDetails.Contains("também bloqueia")) throw new InvalidDataException("Blocking light lost its independent blocking trait.");
        _yaw = yaw;
        _focus = Point(blockingLight.Position, 0); _camera.Size = 18; UpdateCamera();
        if (_capture != null) await Capture(Path.ChangeExtension(_capture, "review-blocking-light.png"));
        _hud.Select(new string('W', 80) + " · Recipiente · objeto 100", _hud.SelectionDetails);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var actions = (Control)_hud.FindChild("SelectionActions", true, false);
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        if (actions.GetGlobalRect().End.Y > viewport.Y - 64 || actions.GetGlobalRect().End.X > viewport.X - 16 || !_hud.NavigationFits)
            throw new InvalidDataException($"HUD overlaps at {viewport}: selection={actions.GetGlobalRect()}, navigation={_hud.NavigationFits}.");
        if (_capture != null) await Capture(Path.ChangeExtension(_capture, "review-responsive.png"));
        foreach (float zoom in new[] { 8f, 18f, 40f, 100f, 220f })
        {
            _camera.Size = zoom; UpdateCamera();
            foreach (Label3D label in _worldLabels.Where(l => l.Visible))
                if (Math.Abs(label.PixelSize * label.FontSize / _camera.Size * viewport.Y - 14) > .01)
                    throw new InvalidDataException("Caption screen size varies with orthographic zoom.");
        }
        SelectObject(_layout.Objects.Single(o => o.Source.Offset == 465604));
        OpenInventory();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        CheckDialogBounds(_inventory!);
        BuildWorld();
        if (InspectorOpen) throw new InvalidDataException("World rebuild retained the inventory modal.");
        ChangeElevation(0); SelectObject(_layout!.Objects.Single(o => o.Source.Offset == 206348));
        OpenLoot(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); CheckDialogBounds(_loot!); CloseLoot();
        OpenBag(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); CheckDialogBounds(_bag!);
        if (_capture != null) await Capture(Path.ChangeExtension(_capture, "review-bag.png"));
        CloseBag();
        ClearSelection(); if (!FindHudButton("CycleAction").Disabled) throw new InvalidDataException("Cycle enabled without selection.");
        GD.Print($"SCENE_REVIEW_OK: 7 containers/3 lamps, 60 unobstructed inspection rays at 3 rotations/elevations, original blocker navigation, cycling, blocking light, long title, HUD/modal bounds at {viewport}, zoom 8..220 and modal cleanup.");
    }

    private void CheckDialogBounds(Window dialog)
    {
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        if (dialog.Position.X < 0 || dialog.Position.Y < 0 || dialog.Position.X + dialog.Size.X > viewport.X
            || dialog.Position.Y + dialog.Size.Y > viewport.Y)
            throw new InvalidDataException($"Embedded dialog outside viewport {viewport}: {dialog.Position}/{dialog.Size}.");
    }
}
