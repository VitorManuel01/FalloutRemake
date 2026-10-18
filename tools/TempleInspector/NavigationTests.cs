using FalloutRemake.Geometry;
using FalloutRemake.Import;

internal static class NavigationTests
{
    public static void Run(Action<string, Action> check)
    {
        check("blocking uses the FID nibble, flags and elevation, not the PID", () =>
        {
            var roots = new[] { Obj(20100, 0, fid: 0x71000000), Obj(20101, 3, fid: 0),
                Obj(20102, 3, HexNavigation.Hidden), Obj(20103, 2, HexNavigation.NoBlock),
                Obj(20104, 3) with { Header = Obj(20104, 3).Header with { Elevation = 1 } } };
            var nav = new HexNavigation(roots, 0);
            Assert(!nav.Walkable(20100) && nav.Walkable(20101) && nav.Walkable(20102) && nav.Walkable(20103) && nav.Walkable(20104));
            Assert(!nav.Walkable(-1) && !nav.Walkable(40000));
        });
        check("multihex footprints include six neighbors and never wrap across edges", () =>
        {
            var nav = new HexNavigation(new[] { Obj(20100, 1, HexNavigation.Multihex) }, 0);
            Assert(nav.BlockedHexCount == 7);
            for (int d = 0; d < 6; d++) Assert(!nav.Walkable(FalloutGrid.Neighbor(20100, d)!.Value));
            nav = new HexNavigation(new[] { Obj(0, 3, HexNavigation.Multihex) }, 0);
            Assert(nav.BlockedHexCount == 4 && nav.Walkable(199) && nav.Walkable(39999));
        });
        check("overlapping blockers retain distinct source keys and inventory stays unplaced", () =>
        {
            MapObject first = Obj(20100, 3), second = Obj(20100, 3) with { Key = "fixture:2" };
            MapObject item = Obj(20101, 0) with { Inventory = new[] { new InventoryEntry(0, 1, Obj(20102, 3)) }, InventoryCount = 1 };
            var nav = new HexNavigation(new[] { first, second, item }, 0);
            Assert(nav.BlockersAt(20100).Count == 2 && nav.BlockersAt(20100).Select(b => b.Key).Distinct().Count() == 2);
            Assert(nav.Walkable(20102));
        });
        check("closed doors block; open passable doors use NO_BLOCK; inconsistent state is reported", () =>
        {
            var closed = Obj(20100, 2) with { Subtype = 0, State = new Dictionary<string, int> { ["openFlags"] = 0x02000000 } };
            var open = Obj(20101, 2, HexNavigation.NoBlock) with { Subtype = 0, State = new Dictionary<string, int> { ["openFlags"] = 1 } };
            var mismatch = Obj(20102, 2) with { Subtype = 0, State = new Dictionary<string, int> { ["openFlags"] = 1 } };
            var nav = new HexNavigation(new[] { closed, open, mismatch }, 0);
            Assert(!nav.Walkable(20100) && nav.Walkable(20101) && !nav.Walkable(20102));
            Assert(nav.DoorFlagDisagreements == 1 && nav.BlockersAt(20100)[0].IsDoor);
        });
        check("routes avoid blockers, detect unreachable targets and stay inside the grid", () =>
        {
            int start = 20100, obstacle = FalloutGrid.Neighbor(start, 0)!.Value;
            int end = FalloutGrid.Neighbor(obstacle, 0)!.Value;
            var nav = new HexNavigation(new[] { Obj(obstacle, 3) }, 0);
            HexRoute path = nav.FindRoute(start, end);
            Assert(path.Status == RouteStatus.Ready && path.Tiles.Length == 4 && !path.Tiles.Contains(obstacle));
            Assert(nav.FindRoute(start, obstacle).Status == RouteStatus.Blocked);
            Assert(nav.FindRoute(start, start).Status == RouteStatus.AlreadyThere);
            Assert(nav.FindRoute(-1, end).Status == RouteStatus.OutsideGrid);
            var sealedNav = new HexNavigation(Enumerable.Range(0, 6).Select(d => Obj(FalloutGrid.Neighbor(start, d)!.Value, 3)), 0);
            Assert(sealedNav.ReachableFrom(start).Count == 1 && sealedNav.FindRoute(start, end).Status == RouteStatus.Unreachable);
        });
        check("exit hexes are terminal endpoints and do not silently cross maps", () =>
        {
            int start = 20100, exit = FalloutGrid.Neighbor(start, 0)!.Value, beyond = FalloutGrid.Neighbor(exit, 0)!.Value;
            var roots = Enumerable.Range(1, 5).Select(d => Obj(FalloutGrid.Neighbor(start, d)!.Value, 3)).ToList();
            roots.Add(Obj(exit, 5) with { Header = Obj(exit, 5).Header with { Pid = 0x05000010 } });
            var nav = new HexNavigation(roots, 0);
            Assert(nav.FindRoute(start, exit).Status == RouteStatus.Ready && nav.FindRoute(start, beyond).Status == RouteStatus.Unreachable);
            Assert(nav.FindRoute(exit, start).Status == RouteStatus.Unreachable);
        });
        check("marker advances continuously through valid adjacent hexes and rejects rerouting mid-step", () =>
        {
            var nav = new HexNavigation(Array.Empty<MapObject>(), 0);
            var walker = new HexWalker(nav, 20100);
            HexRoute route = walker.WalkTo(20103);
            Assert(route.Status == RouteStatus.Ready && walker.WalkTo(20104).Status == RouteStatus.Busy);
            route.Tiles[1] = 39999; // A caller cannot mutate the marker's private route.
            walker.Advance(.1); Assert(walker.CurrentHex == 20100 && Math.Abs(walker.Progress - .5) < 1e-8);
            var initial = FalloutGrid.HexCenter(20100); var position = walker.Position;
            Assert(Math.Abs(Math.Sqrt(Math.Pow(position.X - initial.X, 2) + Math.Pow(position.Z - initial.Z, 2)) - .5) < 1e-8);
            walker.Advance(100); Assert(walker.CurrentHex == 20103 && !walker.Moving && walker.Progress == 0);
            try { walker.Advance(double.NaN); throw new Exception("Invalid delta accepted."); } catch (ArgumentOutOfRangeException) { }
        });
        check("world picking round-trips every original hex and rejects virtual edge cells", () =>
        {
            for (int tile = 0; tile < 40000; tile++) Assert(FalloutGrid.HexAt(FalloutGrid.HexCenter(tile)) == tile);
            GroundPoint p = FalloutGrid.HexCenter(0);
            Assert(FalloutGrid.HexAt(new GroundPoint(p.X, p.Z - 1)) == null);
            Assert(FalloutGrid.HexAt(new GroundPoint(double.NaN, 0)) == null);
        });
        check("stopping mid-step preserves position and completes only the immediate next hex", () =>
        {
            var walker = new HexWalker(new HexNavigation(Array.Empty<MapObject>(), 0), 20100);
            walker.WalkTo(20103); walker.Advance(.07);
            var position = walker.Position; int next = walker.NextHex; double progress = walker.Progress;
            Assert(walker.RequestStop() && walker.RequestStop() && walker.StopRequested && walker.Moving
                && walker.Position == position && walker.Progress == progress);
            walker.Advance(100);
            Assert(!walker.Moving && !walker.StopRequested && walker.CurrentHex == next && walker.Progress == 0
                && walker.Position == FalloutGrid.HexCenter(next) && !walker.RequestStop());
            Assert(walker.WalkTo(20103).Status == RouteStatus.Ready);
            walker.Advance(100); Assert(walker.CurrentHex == 20103);
        });
        check("stopping at a step boundary never advances an extra hex or blocks a new route", () =>
        {
            var walker = new HexWalker(new HexNavigation(Array.Empty<MapObject>(), 0), 20100);
            walker.WalkTo(20103); Assert(walker.RequestStop() && !walker.Moving && walker.CurrentHex == 20100);
            HexRoute route = walker.WalkTo(20103); walker.Advance(.2); int current = walker.CurrentHex;
            Assert(current == route.Tiles[1] && walker.Progress == 0 && walker.RequestStop() && !walker.Moving);
            walker.Advance(100); Assert(walker.CurrentHex == current && walker.Position == FalloutGrid.HexCenter(current));
            Assert(walker.WalkTo(20103).Status == RouteStatus.Ready && !walker.StopRequested);
        });
    }
    private static MapObject Obj(int tile, int type, int flags = 0, int? fid = null) => new($"fixture:{tile}", tile, 0, 0, 0,
        new ObjectHeader(42, tile, 0, 0, 0, 0, 0, 0, fid ?? type << 24, flags, 0, (type << 24) | 1, -1, 0, 0, 0, -1, -1),
        0, 0, 0, null, new Dictionary<string, int>(), Array.Empty<InventoryEntry>());
    private static void Assert(bool condition) { if (!condition) throw new Exception("Navigation assertion failed."); }
}
