using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private bool _followActor;
    private void SetCameraFollow(bool following)
    { if (_followActor == following) return; _followActor = following; _hud.CameraFollow(following); }
    private void DragCamera(Vector2 relative)
    {
        SetCameraFollow(false);
        float scale = _camera.Size / GetViewport().GetVisibleRect().Size.Y;
        _focus -= (new Vector3(Mathf.Cos(_yaw), 0, -Mathf.Sin(_yaw)) * relative.X
            + new Vector3(Mathf.Sin(_yaw), 0, Mathf.Cos(_yaw)) * relative.Y * 1.4f) * scale;
        UpdateCamera();
    }
    private void FocusCharacter()
    {
        if (_campaign == null) return;
        if (_elevation != _campaign.Location.Elevation) ChangeElevation(_campaign.Location.Elevation);
        if (_walkerNode == null) return;
        SetCameraFollow(true); _focus = _walkerNode.Position; _camera.Size = 32; UpdateCamera();
        NavigationStatus("Câmera seguindo o personagem · WASD ou arrasto para explorar livremente");
    }
    private async Task CheckCharacterCamera()
    {
        _checkingNavigation = true;
        try
        {
            ResetWalker(); ChangeElevation(2);
            var location = _campaign!.Location;
            using var center = new InputEventKey { Pressed = true, PhysicalKeycode = Key.C };
            _UnhandledInput(center);
            if (_elevation != location.Elevation || _campaign.Location != location || !_followActor || _camera.Size != 32)
                throw new InvalidDataException("Character focus teleported actor or failed to return its inspection elevation.");
            int destination = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(_walker!.CurrentHex, d)!.Value)
                .First(t => _navigation!.Walkable(t) && !_navigation.IsExit(t));
            RequestWalk(destination); _checkingNavigation = false;
            using var timer = GetTree().CreateTimer(.4);
            await ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
            _checkingNavigation = true;
            if (_walker!.Moving || _walker.CurrentHex != destination || _focus.DistanceTo(_walkerNode!.Position) > .01f)
                throw new InvalidDataException("Camera failed to follow frame-driven movement.");
            var before = _campaign.Location;
            // Exercise the same drag handler without depending on the OS cursor position in headless mode.
            DragCamera(new Vector2(30, 10));
            if (_followActor || _campaign.Location != before) throw new InvalidDataException("Manual camera drag changed the actor or retained follow lock.");
            _UnhandledInput(center); RequestWalk(24508);
            if (!_walker.Moving) throw new InvalidDataException("Camera route did not start.");
            _UnhandledInput(center);
            if (!_walker.Moving) throw new InvalidDataException("Centering cancelled the current route.");
            using var overview = new InputEventKey { Pressed = true, PhysicalKeycode = Key.Home };
            _UnhandledInput(overview);
            if (_followActor) throw new InvalidDataException("Overview did not release character follow.");
            ChangeElevation(1);
            var stationary = _campaign.Location;
            var button = _hud.FindChild("CharacterFocusAction", true, false) as Button ?? throw new InvalidDataException("Character focus button missing.");
            button.EmitSignal(Button.SignalName.Pressed);
            if (_elevation != stationary.Elevation || _campaign.Location != stationary || !_followActor
                || _focus.DistanceTo(_walkerNode!.Position) > .01f)
                throw new InvalidDataException("HUD character focus changed actor state or failed.");
            NavigationStatus("Câmera no personagem · acompanhamento ativo");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "camera.png"));
            foreach (var area in _campaign.Areas) MapLayoutWriter.Verify(area.Map);
            ResetWalker();
            GD.Print("CHARACTER_CAMERA_OK: C and HUD; actor focus, frame-driven follow, free drag, route preservation, overview, inspection and source bytes verified.");
        }
        finally { _checkingNavigation = false; }
    }
}
