using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Import;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private async Task CheckElevationWalkGuard()
    {
        _checkingNavigation = true;
        try
        {
            ResetWalker(); ToggleWalkMode(); RequestWalk(24508); _walker!.Advance(.05); UpdateWalkerPosition();
            var walker = _walker; var position = walker.Position; var location = _campaign!.Location; var doors = _doors!.Capture();
            int[] route = _committedRoute.ToArray(); int count = _lookup.Count;
            var selector = (OptionButton)_hud.FindChild("ElevationSelection", true, false);
            selector.Select(1); selector.EmitSignal(OptionButton.SignalName.ItemSelected, 1L);
            if (_elevation != 0 || selector.Selected != 0 || !ReferenceEquals(walker, _walker) || _walker.Position != position
                || !_walker.Moving || !_committedRoute.SequenceEqual(route) || _campaign.Location != location || _lookup.Count != count)
                throw new InvalidDataException("Elevation selection lost the route or fractional step.");
            selector.EmitSignal(OptionButton.SignalName.ItemSelected, 0L); GoToEntry();
            if (!ReferenceEquals(walker, _walker) || _walker.Position != position || !_committedRoute.SequenceEqual(route))
                throw new InvalidDataException("Reselecting the same elevation or focusing entry rebuilt the walking actor.");
            _checkingNavigation = false; AdvanceWalker(.05); _checkingNavigation = true;
            if (!_walker.Moving || _walker.Position == position) throw new InvalidDataException("Rejected inspection switch paused or cancelled movement.");
            StopWalking(); _checkingNavigation = false; AdvanceWalker(.2); _checkingNavigation = true;
            var stopped = _campaign.Location;
            selector.Select(1); selector.EmitSignal(OptionButton.SignalName.ItemSelected, 1L);
            if (_elevation != 1 || _walker != null || _campaign.Location != stopped) throw new InvalidDataException("Idle inspection switch moved the actor.");
            GoToEntry();
            if (_walker?.CurrentHex != stopped.Hex || _campaign.Location != stopped || _elevation != 0 || _walker.Moving
                || !_doors.Capture().Doors.SequenceEqual(doors.Doors)) throw new InvalidDataException("Returning from inspection changed the stopped actor or doors.");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            MapLayoutWriter.Verify(_map!); ResetWalker();
            GD.Print("ELEVATION_WALK_GUARD_OK: HUD mid-step elevation change refused, selection restored, same-elevation no rebuild, route/progress preserved, movement continues, Esc then inspection/entry allowed without teleport or source changes.");
        }
        finally { _checkingNavigation = false; }
    }
}
