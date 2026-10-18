using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private void ClearRouteVisual()
    { if (_routeVisual != null) { _routeVisual.Visible = false; _routeVisual.Mesh = null; } }

    private void StopWalking()
    {
        if (_walker?.Moving != true || _walker.StopRequested) return;
        _walker.RequestStop();
        if (_walker.Moving)
        {
            DrawRoute(new[] { _walker.CurrentHex, _walker.NextHex });
            NavigationStatus("Parando · completando o passo atual");
        }
        else
        {
            ClearRouteVisual(); UpdateWalkerPosition(); RefreshReachability(); RefreshDoorAction();
            NavigationStatus($"Caminhada interrompida no hex {_walker.CurrentHex} · escolha outro destino");
        }
    }

    private async Task CheckStopWalking()
    {
        _checkingNavigation = true;
        try
        {
            ResetWalker(); ToggleWalkMode(); FocusCharacter();
            var doors = _doors!.Capture(); int xp = _campaign!.Experience;
            var stop = _hud.FindChild("StopWalkingAction", true, false) as Button ?? throw new InvalidDataException("Stop button missing.");
            var save = _hud.FindChild("SaveAction", true, false) as Button ?? throw new InvalidDataException("Save button missing.");
            if (!stop.Disabled) throw new InvalidDataException("Idle stop button should be disabled.");
            RequestWalk(24508); _walker!.Advance(.05); UpdateWalkerPosition();
            var before = _walker.Position; int next = _walker.NextHex;
            using var escape = new InputEventKey { Pressed = true, PhysicalKeycode = Key.Escape };
            _UnhandledInput(escape); _UnhandledInput(escape);
            if (!_walker.StopRequested || !_walker.Moving || _walker.Position != before || !stop.Disabled || !save.Disabled || !_followActor)
                throw new InvalidDataException("Stop request jumped actor, allowed mid-step save or lost follow.");
            _checkingNavigation = false;
            using var timer = GetTree().CreateTimer(.35);
            await ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
            _checkingNavigation = true;
            if (_walker.Moving || _walker.CurrentHex != next || _campaign.Location.Hex != next || _routeVisual!.Visible || _routeVisual.Mesh != null
                || !stop.Disabled || save.Disabled || _walkerNode!.Position.DistanceTo(Point(FalloutGrid.HexCenter(next) - _layout!.Origin, 0)) > .001f)
                throw new InvalidDataException("Stop failed to finish exactly one step and clear the route.");
            RequestWalk(24508);
            if (!_walker.Moving || stop.Disabled) throw new InvalidDataException("Route could not restart after stop.");
            stop.EmitSignal(Button.SignalName.Pressed);
            if (_walker.Moving || _campaign.Location.Hex != next || _routeVisual.Visible)
                throw new InvalidDataException("Button failed to stop before the next step.");
            if (_campaign.Experience != xp || !_doors.Capture().Doors.SequenceEqual(doors.Doors))
                throw new InvalidDataException("Stopping altered doors or XP.");
            NavigationStatus($"Parado no hex {next} · portas e progresso preservados");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "stop.png"));
            RequestWalk(24508); _checkingNavigation = false; AdvanceWalker(100); _checkingNavigation = true;
            if (_walker.Moving || _routeVisual.Visible || _routeVisual.Mesh != null)
                throw new InvalidDataException("Completed route remained visible.");
            MapLayoutWriter.Verify(_map!); ResetWalker();
            GD.Print("STOP_WALKING_OK: Esc/button, mid-step continuity, one-step completion, repeated stop, save gating, follow, route restart/cleanup, doors, XP and source bytes verified.");
        }
        finally { _checkingNavigation = false; }
    }
}
