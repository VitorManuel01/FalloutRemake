using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class InitialGateAudit
{
    public static object Create(WorldSession original, MapCatalog catalog, GameData data)
    {
        byte[] State(WorldSession w) => CheckpointFile.Encode(w.Capture(catalog.Source.Sha256));
        var prototypes = new PrototypeCatalog(new ResourceCatalog(data.Read));
        WorldSession Restore(byte[] bytes) => WorldSession.Restore(id => catalog.Load(data, id), new[] { 3, 4, 126 }, CheckpointFile.Decode(bytes), catalog.Source.Sha256, runtimePrototype: prototypes.Get);
        byte[] before = State(original); var world = Restore(before); var map = world.Current.Map;
        if (world.Location.Elevation != 2 || world.Current.Doors.InitialTempleGateKey is not string key) throw new InvalidDataException("Initial gate profile unavailable.");
        MapObject gate = map.AllObjects.Single(o => o.Key == key), actor = map.AllObjects.Single(o => o.Offset == 425776);
        int Approach(MapObject obj)
        {
            var nav = world.Current.Doors.Navigation(2); var reach = nav.ReachableFrom(world.Location.Hex);
            return Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(obj.Header.Tile, d))
                .Where(t => t.HasValue && reach.Distance(t.Value) >= 0 && !nav.IsExit(t.Value)).Select(t => t!.Value)
                .OrderBy(reach.Distance).ThenBy(t => t).First();
        }
        int Walk(int hex)
        {
            var route = world.Current.Doors.Navigation(2).FindRoute(world.Location.Hex, hex);
            if (route.Status is not (RouteStatus.Ready or RouteStatus.AlreadyThere)) throw new InvalidDataException("Initial gate audit tried an unreachable walk.");
            world.Current.Travel.MoveTo(hex); return route.Tiles.Length - 1;
        }
        int approach = Approach(gate); int steps = Walk(approach); int beforeReach = world.Current.Doors.Navigation(2).ReachableFrom(world.Location.Hex).Count;
        if (world.Current.Doors.Toggle(key, world.Location.Hex, 2, false) != DoorUseStatus.Ready) throw new InvalidDataException("Initial native gate did not open.");
        int openReach = world.Current.Doors.Navigation(2).ReachableFrom(world.Location.Hex).Count;
        steps += Walk(gate.Header.Tile); world = Restore(State(world));
        if (!world.Current.Doors.Get(key)!.IsOpen || world.Current.Doors.CanToggle(key, world.Location.Hex, 2, false) != DoorUseStatus.Occupied)
            throw new InvalidDataException("Open gate checkpoint or occupied-close guard failed.");
        int actorApproach = Approach(actor); steps += Walk(actorApproach); var nearActor = world.Location; world = Restore(State(world));
        if (world.Location != nearActor || !world.Current.Doors.Get(key)!.IsOpen || world.CarriedCount != original.CarriedCount || world.Experience != original.Experience)
            throw new InvalidDataException("NPC approach checkpoint changed inventory, XP or actor.");
        if (world.Current.Doors.CanLockpick(key, approach, 2, false) != DoorUseStatus.ScriptRequired) throw new InvalidDataException("Initial gate enabled lockpick.");
        if (!before.SequenceEqual(State(original)) || gate.State["openFlags"] != 0) throw new InvalidDataException("Detached initial gate audit changed its source.");
        MapLayoutWriter.Verify(map);
        return new { Status = "INITIAL_GATE_AUDIT_OK", Gate = gate.Key, GateApproach = approach, ClosedReachable = beforeReach,
            OpenReachable = openReach, NpcApproach = nearActor, WalkingSteps = steps, CheckpointVersion = 5,
            SourceAndCallerUnchanged = true, DialogueOrCombatExecuted = false, LockpickEnabled = false };
    }
}
