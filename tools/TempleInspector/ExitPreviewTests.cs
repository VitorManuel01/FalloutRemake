using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class ExitPreviewTests
{
    public static void Run(Action<string, Action> check)
    {
        check("remote exit inspection leaves the actor at its own position and validates source coordinates", () =>
        {
            MapDocument map = WorldTests.Map(3, 126); MapObject exit = map.ObjectLayers[0].Objects[0];
            MapObject remote = exit with { Header = exit.Header with { Tile = 20200 } };
            map = map with { ObjectLayers = new[] { new ObjectLayer(0, 0, 2, new[] { remote, map.ObjectLayers[0].Objects[1] }) } };
            var doors = DoorSession.FromMap(map); var travel = new TempleTravel(map, doors); ActorLocation before = travel.Location;
            Assert(travel.InspectExit(false).Status == ExitStatus.NoExit);
            Assert(travel.InspectExitAt(0, 20200) is { Status: ExitStatus.ExternalMap, Destination.Map: 126, SourceKeys.Count: 1 });
            Assert(travel.Location == before && travel.InspectExit(true).Status == ExitStatus.Busy);
            foreach (var (e, h) in new[] { (-1, 20200), (3, 20200), (1, 20200), (0, -1), (0, 40000) })
                Assert(travel.InspectExitAt(e, h).Status == ExitStatus.Invalid);
        });
        check("world hover does not call the area loader or grow the checkpoint area set", () =>
        {
            int loads = 0;
            var world = new WorldSession(id => { loads++; return WorldTests.Map(id, id == 3 ? 126 : 3); }, new[] { 3, 126 });
            WorldCheckpoint before = world.Capture("catalog"); WorldArea current = world.Current;
            for (int i = 0; i < 20; i++) Assert(world.PreviewExitAt(0, 20100).Status == ExitStatus.ExternalMap);
            Assert(loads == 1 && world.Areas.Count == 1 && world.Capture("catalog").Areas.Length == before.Areas.Length
                && world.Location == before.Areas[0].Location && ReferenceEquals(world.Current, current));
            Assert(world.InspectExit(false).Status == ExitStatus.Ready && loads == 2);
            ActorLocation cached = world.Areas.Single(a => a.Map.Prefix.Header.MapIndex == 126).Travel.Location;
            Assert(world.PreviewExitAt(0, 20100).Status == ExitStatus.Ready && loads == 2
                && world.Areas.Single(a => a.Map.Prefix.Header.MapIndex == 126).Travel.Location == cached);
        });
        check("unsupported and cached blocked destinations remain explicit without new loads", () =>
        {
            int loads = 0;
            var unsupported = new WorldSession(id => { loads++; return WorldTests.Map(id, 35); }, new[] { 3, 126 });
            Assert(unsupported.PreviewExitAt(0, 20100) is { Status: ExitStatus.UnavailableMap, Destination.Map: 35 } && loads == 1);
            var blocked = new WorldSession(id => WorldTests.Map(id, id == 3 ? 126 : 3, id == 126), new[] { 3, 126 });
            Assert(blocked.PreviewExitAt(0, 20100).Status == ExitStatus.ExternalMap);
            Assert(blocked.InspectExit(false).Status == ExitStatus.BlockedDestination);
            Assert(blocked.PreviewExitAt(0, 20100).Status == ExitStatus.BlockedDestination && blocked.CurrentMap == 3);
        });
        check("remote exit preview retains malformed/ambiguous guards and all overlapping equal source keys", () =>
        {
            MapDocument map = WorldTests.Map(3, 126); MapObject exit = map.ObjectLayers[0].Objects[0];
            ExitOption Inspect(MapObject second)
            {
                MapDocument changed = map with { ObjectLayers = new[] { new ObjectLayer(0, 0, 2, new[] { exit, second }) } };
                return new TempleTravel(changed, DoorSession.FromMap(changed)).InspectExitAt(0, 20100);
            }
            Assert(Inspect(exit with { Key = "other" }).SourceKeys!.Count == 2);
            Assert(Inspect(exit with { Key = "other", State = new Dictionary<string, int>(exit.State) { ["destinationMap"] = 4 } }).Status == ExitStatus.Ambiguous);
            Assert(Inspect(exit with { Key = "other", State = new Dictionary<string, int>() }).Status == ExitStatus.Invalid);
        });
        check("remote elevation preview compares reload lifecycle to the source layer rather than the actor layer", () =>
        {
            MapDocument map = WorldTests.Map(3, 3); MapObject source = map.ObjectLayers[0].Objects[0];
            source = source with { ContainerElevation = 1, Header = source.Header with { Elevation = 1 } };
            ExitOption Inspect(int destinationElevation)
            {
                MapObject exit = source with { State = new Dictionary<string, int>(source.State) { ["destinationElevation"] = destinationElevation } };
                var changed = map with
                {
                    Prefix = map.Prefix with { Layers = new[] { new MapLayer(0, Array.Empty<SquareTile>()), new MapLayer(1, Array.Empty<SquareTile>()) } },
                    ObjectLayers = new[] { new ObjectLayer(0, 0, 1, new[] { map.ObjectLayers[0].Objects[1] }), new ObjectLayer(1, 0, 1, new[] { exit }) }
                };
                var travel = new TempleTravel(changed, DoorSession.FromMap(changed));
                ExitOption option = travel.InspectExitAt(1, 20100);
                Assert(travel.Location.Elevation == 0); return option;
            }
            Assert(Inspect(0).Status == ExitStatus.Ready && Inspect(1).Status == ExitStatus.ReloadRequired);
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Exit preview assertion failed."); }
}
