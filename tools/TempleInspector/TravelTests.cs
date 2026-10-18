using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class TravelTests
{
    public static void Run(Action<string, Action> check)
    {
        check("internal exits preserve absolute target map, elevation, hex and rotation, including return", () =>
        {
            var (_, _, travel) = Fixture(Exit(), Exit(20200, 1, 3, 0, 20101, 2, "back"));
            Assert(travel.UseExit(false) is { Status: ExitStatus.Ready, SourceKeys.Count: 1 });
            Assert(travel.Location == new ActorLocation(3, 1, 20200, 4));
            travel.UseExit(false); Assert(travel.Location == new ActorLocation(3, 0, 20101, 2));
            travel.Reset(); Assert(travel.Location == new ActorLocation(3, 0, 20100, 0));
        });
        check("moving actors and positions without exit grids never transition", () =>
        {
            var (_, _, travel) = Fixture(Exit()); ActorLocation original = travel.Location;
            Assert(travel.UseExit(true).Status == ExitStatus.Busy && travel.Location == original);
            var (_, _, empty) = Fixture(); Assert(empty.UseExit(false).Status == ExitStatus.NoExit && empty.Location == original);
        });
        check("overlapping equal exits retain all source keys and conflicting destinations are refused", () =>
        {
            MapObject first = Exit(), second = Exit(key: "second");
            var (_, _, same) = Fixture(first, second);
            Assert(same.InspectExit(false).SourceKeys!.SequenceEqual(new[] { first.Key, second.Key }));
            second = Exit(destinationHex: 20201, key: "second");
            var (_, _, conflict) = Fixture(first, second);
            Assert(conflict.UseExit(false).Status == ExitStatus.Ambiguous && conflict.Location.Elevation == 0);
        });
        check("malformed, hidden and wrong-FID exits cannot teleport the actor", () =>
        {
            MapObject source = Exit();
            foreach (MapObject invalid in new[]
            {
                Exit(destinationHex: -1), Exit(destinationHex: 0), Exit(destinationHex: 40000),
                Exit(destinationElevation: 3), Exit(rotation: 6),
                source with { State = new Dictionary<string, int>() },
                source with { Header = source.Header with { Fid = 0x02000001 } }
            })
            {
                var (_, _, travel) = Fixture(invalid);
                Assert(travel.UseExit(false).Status == ExitStatus.Invalid && travel.Location.Elevation == 0);
            }
            var (_, _, hidden) = Fixture(source with { Header = source.Header with { Flags = HexNavigation.Hidden } });
            Assert(hidden.UseExit(false).Status == ExitStatus.NoExit);
        });
        check("external maps and same-elevation reloads remain explicit unsupported destinations", () =>
        {
            var (_, _, external) = Fixture(Exit(destinationMap: 126));
            Assert(external.UseExit(false) is { Status: ExitStatus.ExternalMap, Destination.Map: 126 } && external.Location.Map == 3);
            var (_, _, reload) = Fixture(Exit(destinationElevation: 0));
            Assert(reload.UseExit(false).Status == ExitStatus.ReloadRequired && reload.Location.Hex == 20100);
        });
        check("blocked source or destination and absent elevation fail without partial location changes", () =>
        {
            MapObject blocker = Exit(key: "blocker") with
            { Header = Exit().Header with { Pid = 0x03000001, Fid = 0x03000001, Tile = 20200, Elevation = 1, Flags = 0 } };
            var (_, _, blocked) = Fixture(Exit(), blocker);
            Assert(blocked.UseExit(false).Status == ExitStatus.BlockedDestination && blocked.Location.Elevation == 0);
            var (map, doors, _) = Fixture(Exit());
            map = map with { Prefix = map.Prefix with { Layers = new[] { map.Prefix.Layers[0] } } };
            var absent = new TempleTravel(map, doors);
            Assert(absent.UseExit(false).Status == ExitStatus.BlockedDestination && absent.Location.Elevation == 0);
            blocker = blocker with { Header = blocker.Header with { Tile = 20100, Elevation = 0 } };
            var (_, _, sourceBlocked) = Fixture(Exit(), blocker);
            Assert(sourceBlocked.UseExit(false).Status == ExitStatus.Invalid);
        });
        check("travel movement uses valid routes and exit endpoints cannot be walked through", () =>
        {
            int exitHex = FalloutGrid.Neighbor(20100, 2)!.Value;
            var (_, _, travel) = Fixture(Exit(tile: exitHex));
            travel.MoveTo(exitHex); ActorLocation onExit = travel.Location;
            Reject(() => travel.MoveTo(20100)); Assert(travel.Location == onExit);
            travel.UseExit(false); Assert(travel.Location.Elevation == 1);
            Reject(() => travel.MoveTo(40000)); Assert(travel.Location.Hex == 20200);
        });
        check("changing travel location or resetting it never replaces door state or mutates source exit records", () =>
        {
            MapObject source = Exit();
            MapObject door = Exit(tile: 20102, key: "door") with
            { Subtype = 0, Header = Exit().Header with { Pid = 0x02000001, Fid = 0x02000001, Tile = 20102, Flags = 0 }, State = new Dictionary<string, int> { ["openFlags"] = 0 } };
            var (_, doors, travel) = Fixture(source, door);
            int adjacent = FalloutGrid.Neighbor(20102, 2)!.Value;
            Assert(doors.Toggle(door.Key, adjacent, 0, false) == DoorUseStatus.Ready);
            DoorState opened = doors.Get(door.Key)!;
            travel.UseExit(false); travel.Reset();
            Assert(ReferenceEquals(opened, doors.Get(door.Key)) && source.Header.Tile == 20100 && source.State["destinationElevation"] == 1);
        });
    }
    private static MapObject Exit(int tile = 20100, int elevation = 0, int destinationMap = 3, int destinationElevation = 1,
        int destinationHex = 20200, int rotation = 4, string key = "exit") => new(key, 100, 104, 104, elevation,
        new ObjectHeader(42, tile, 0, 0, 0, 0, 0, 0, 0x05000001, HexNavigation.NoBlock, elevation, 0x05000010, -1, 0, 0, 0, -1, -1),
        0, 0, 0, null, new Dictionary<string, int>
        { ["destinationMap"] = destinationMap, ["destinationElevation"] = destinationElevation, ["destinationTile"] = destinationHex, ["destinationRotation"] = rotation }, Array.Empty<InventoryEntry>());
    private static (MapDocument, DoorSession, TempleTravel) Fixture(params MapObject[] objects)
    {
        var header = new MapHeader(20, Array.Empty<byte>(), 20100, 0, 0, 0, 0, 0, 0, 0, 3, 0, new int[44]);
        var prefix = new MapPrefix(header, Array.Empty<int>(), Array.Empty<int>(),
            Enumerable.Range(0, 3).Select(e => new MapLayer(e, Array.Empty<SquareTile>())).ToArray(), 0, ReadOnlyMemory<byte>.Empty);
        var map = new MapDocument(new ResourceIdentity("synthetic.map", "synthetic", 0, ""), prefix, Array.Empty<ScriptGroup>(), 0, objects.Length,
            Enumerable.Range(0, 3).Select(e => new ObjectLayer(e, 0, objects.Count(o => o.Header.Elevation == e), objects.Where(o => o.Header.Elevation == e).ToArray())).ToArray(),
            Array.Empty<Prototype>(), Array.Empty<ScriptBinding>(), null, Array.Empty<ResourceIdentity>(), Array.Empty<ImportDiagnostic>(), 0);
        DoorSession doors = DoorSession.FromMap(map);
        return (map, doors, new TempleTravel(map, doors));
    }
    private static void Reject(Action action)
    { try { action(); } catch (InvalidOperationException) { return; } throw new Exception("Invalid travel movement accepted."); }
    private static void Assert(bool condition) { if (!condition) throw new Exception("Temple travel assertion failed."); }
}
