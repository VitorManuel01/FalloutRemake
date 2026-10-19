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
    private string? _hoverDoorKey;
    private DoorUseStatus DoorApproachStatus(PreviewObject door)
    {
        DoorUseStatus status = DoorStatus(door.Source.Key);
        if (status == DoorUseStatus.Locked && LockpickStatus(door.Source.Key) is DoorUseStatus.TooFar or DoorUseStatus.Busy)
            status = LockpickStatus(door.Source.Key);
        return status;
    }
    private static int? ClosestDoorApproach(PreviewObject door, HexReachability reach, HexNavigation navigation) =>
        Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(door.Source.Header.Tile, d))
            .Where(t => t.HasValue && reach.Distance(t.Value) >= 0 && !navigation.IsExit(t.Value))
            .OrderBy(t => reach.Distance(t!.Value)).ThenBy(t => t).FirstOrDefault();
    private void PreviewDoorRoute(PreviewObject door)
    {
        if (!_walkMode || _walker?.Moving != false || _navigation == null || _reachable == null)
        { ClearRouteHover(); return; }
        if (_hoverDoorKey == door.Source.Key) return;
        ClearRouteHover();
        DoorUseStatus status = DoorApproachStatus(door);
        string name = OriginalText(door.Source)?.Name ?? "Porta";
        if (name.Length > 40) name = name[..39] + "…";
        if (status == DoorUseStatus.TooFar && ClosestDoorApproach(door, _reachable, _navigation) is int target)
        {
            PreviewGroundRoute(target);
            _hud.RouteHint($"{name} · aproximação em {_hoverPath.Length - 1} passos · clique para se aproximar");
        }
        else
        {
            _hoverTargetMaterial!.AlbedoColor = new Color(status is DoorUseStatus.Ready or DoorUseStatus.Locked ? "eacb70bb" : "e4766abb");
            _hoverTarget!.Position = Point(door.Position, .09f); _hoverTarget.Visible = true;
            _hud.RouteHint($"{name} · {(status == DoorUseStatus.TooFar ? "Sem caminho até o lado desta porta" : DoorHint(status))}");
        }
        _hoverDoorKey = door.Source.Key;
    }
    private async Task CheckDoorRouteHover()
    {
        _checkingNavigation = true;
        try
        {
            ResetWalker(); ToggleWalkMode(); SetNavigationGrid(false);
            PreviewObject door = _layout!.Objects.Single(o => o.Source.Offset == 244040);
            PreviewObject locked = _layout.Objects.Single(o => o.Source.Key == _doors!.TempleDoor!.SourceKey);
            var before = _campaign!.Location; var sourceDoors = _doors!.Capture();
            int expected = ClosestDoorApproach(door, _reachable!, _navigation!) ?? throw new InvalidDataException("No real door approach.");
            HexRoute expectedRoute = _navigation!.FindRoute(_walker!.CurrentHex, expected);
            _focus = Point(door.Position, 0); _camera.Size = 30; UpdateCamera();
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Vector2 screen = _camera.UnprojectPosition(Point(door.Position, 1.2f));
            RouteHoverAtScreen(screen);
            if (_hoverDoorKey != door.Source.Key || !_hoverPath.SequenceEqual(expectedRoute.Tiles) || !_hoverLine!.Visible
                || _walker.Moving || _selected != null || _campaign.Location != before || _skills.LastCheck != null
                || !_doors.Capture().Doors.SequenceEqual(sourceDoors.Doors))
                throw new InvalidDataException("Door hover changed state or disagreed with the actual approach route.");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "door-route.png"));
            WalkAtScreen(screen);
            if (!_walker.Moving || _walker.NextHex != expectedRoute.Tiles[1] || _hoverTarget!.Visible)
                throw new InvalidDataException("Door click did not follow the previewed approach.");
            _walker.Advance(1000); UpdateWalkerPosition(); RefreshReachability();
            if (_walker.CurrentHex != expected) throw new InvalidDataException("Door approach ended at another hex.");
            PreviewDoorRoute(door);
            if (_hoverLine.Visible || !_hud.RouteHintText!.Contains("U")) throw new InvalidDataException("Nearby door preview still proposed movement.");
            UseDoor(door.Source.Key);
            if (_hoverDoorKey != null) throw new InvalidDataException("Door change retained a stale preview.");
            PreviewDoorRoute(locked);
            if (_hoverDoorKey != locked.Source.Key || _hoverPath.Length < 2 || !_doors.Get(locked.Source.Key)!.IsLocked
                || _campaign.Experience != 0 || _skills.LastCheck != null)
                throw new InvalidDataException("Locked door preview bypassed the lock, XP or skill roll.");
            int approach = _hoverPath[^1];
            PreviewGroundRoute(approach);
            if (_hoverDoorKey != null || !_hud.RouteHintText!.StartsWith("Prévia"))
                throw new InvalidDataException("Moving from door to floor retained the door hint.");
            MapLayoutWriter.Verify(_map!); SetNavigationGrid(true); ResetWalker();
            GD.Print("DOOR_ROUTE_PREVIEW_OK: screen picking, shared nearest approach, preview/click endpoint, nearby/locked states, cache invalidation, floor switch, actor, selection, skills, XP, doors and source bytes verified.");
        }
        finally { _checkingNavigation = false; }
    }
}
