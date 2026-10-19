using System.Text.Json;
using System.Text;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class TempleCollectionReport
{
    public static object Create(GameData data)
    {
        var catalog = new MapCatalog(data.Read("data/maps.txt")); var world = new WorldSession(id => catalog.Load(data, id), new[] { 3, 4, 126 });
        var map = world.Current.Map; var moves = new List<object>(); int steps = 0;
        var names = new PrototypeTextCatalog(data.Read, data.FindPaths("pro_"));
        (int Target, int Steps)? Approach(MapObject owner)
        {
            var nav = world.Current.Doors.Navigation(world.Location.Elevation); var reach = nav.ReachableFrom(world.Location.Hex);
            return Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(owner.Header.Tile, d)).Append(owner.Header.Tile)
                .Where(t => t.HasValue && reach.Distance(t.Value) >= 0 && !nav.IsExit(t.Value)).Select(t => ((int Target, int Steps)?)(t!.Value, reach.Distance(t.Value)))
                .OrderBy(t => t!.Value.Steps).ThenBy(t => t!.Value.Target).FirstOrDefault();
        }
        void Walk(int target)
        {
            var nav = world.Current.Doors.Navigation(world.Location.Elevation); var walker = new HexWalker(nav, world.Location.Hex);
            HexRoute route = walker.WalkTo(target);
            if (route.Status is not (RouteStatus.Ready or RouteStatus.AlreadyThere)) throw new InvalidDataException("Collection audit attempted an unreachable walk.");
            walker.Advance(10000); steps += route.Tiles.Length - 1; world.Current.Travel.MoveTo(walker.CurrentHex);
        }
        foreach (int offset in new[] { 244040, 137396 })
        {
            MapObject door = map.AllObjects.Single(o => o.Offset == offset); Walk(Approach(door)!.Value.Target);
            if (offset == 137396 && world.Current.Doors.UseLockpick(door.Key, world.Location.Hex, 0, false, _ => SkillRollOutcome.Success).ExperienceAwarded != 25)
                throw new InvalidDataException("Verified test lockpick failed.");
            if (world.Current.Doors.Toggle(door.Key, world.Location.Hex, 0, false) != DoorUseStatus.Ready) throw new InvalidDataException("Verified door failed to open.");
        }
        string? boundary = null;
        for (int elevation = 0; elevation < 3; elevation++)
        {
            if (world.Location.Elevation != elevation) break;
            while (true)
            {
                var next = world.Current.Items.Containers.Where(o => o.Header.Elevation == elevation && world.Current.Items.ContainerStatus(o.Key) == ItemTakeStatus.Ready
                    && world.Current.Items.Contents(o.Key).Any(i => i.Quantity > 0))
                    .Select(o => new { Owner = o, Approach = Approach(o) }).Where(o => o.Approach.HasValue)
                    .OrderBy(o => o.Approach!.Value.Steps).ThenBy(o => o.Owner.Offset).FirstOrDefault();
                if (next == null) break;
                Walk(next.Approach!.Value.Target);
                foreach (var item in world.Current.Items.Contents(next.Owner.Key).Where(i => i.Quantity > 0))
                {
                    ItemTakeStatus status = world.TakeItem(next.Owner.Key, item.Source.Key, item.Quantity, false);
                    if (status != ItemTakeStatus.Ready) throw new InvalidDataException($"Reachable static item failed the collection profile: {status}.");
                    moves.Add(new { next.Owner.Key, OwnerId = next.Owner.Header.Id, ItemId = item.Source.Header.Id, item.Source.Header.Pid,
                        Name = names.Resolve(map.Prototypes.Single(p => p.Pid == item.Source.Header.Pid))?.Name, item.Quantity, Elevation = elevation });
                }
            }
            if (elevation == 2) break;
            var navigation = world.Current.Doors.Navigation(elevation); var reach = navigation.ReachableFrom(world.Location.Hex);
            int? exit = map.ObjectLayers.SelectMany(l => l.Objects).Where(o => o.Header.Elevation == elevation && o.Header.Pid is >= 0x05000010 and <= 0x05000017
                && o.State.GetValueOrDefault("destinationMap", -1) == 3 && o.State.GetValueOrDefault("destinationElevation", -1) == elevation + 1 && reach.Distance(o.Header.Tile) >= 0)
                .Select(o => o.Header.Tile).OrderBy(reach.Distance).ThenBy(t => t).Cast<int?>().FirstOrDefault();
            if (!exit.HasValue) { boundary = $"No supported reachable exit from elevation {elevation} to {elevation + 1}."; break; }
            Walk(exit.Value);
            if (world.UseExit(false).Status != ExitStatus.Ready) throw new InvalidDataException("Collection audit exit was rejected.");
        }
        string snapshot = JsonSerializer.Serialize(world.Capture(catalog.Source.Sha256));
        var restored = WorldSession.Restore(id => catalog.Load(data, id), new[] { 3, 4, 126 }, CheckpointFile.Decode(Encoding.UTF8.GetBytes(snapshot)), catalog.Source.Sha256);
        if (JsonSerializer.Serialize(restored.Capture(catalog.Source.Sha256)) != snapshot || restored.CarriedCount != world.CarriedCount || restored.CarriedWeight != world.CarriedWeight)
            throw new InvalidDataException("Collection checkpoint lost inventory or area state.");
        MapLayoutWriter.Verify(map);
        return new { Status = "TEMPLE_COLLECTION_AUDIT_OK", Profile = "Navigation and static collection only; known lockpick outcome forced successful; no combat, traps or general scripts",
            Collected = moves, world.CarriedCount, world.CarriedWeight, world.Experience, WalkingSteps = steps, world.Location, Boundary = boundary,
            Remaining = world.Current.Items.Containers.Select(o => new { o.Offset, o.Header.Id, o.Header.Elevation,
                Status = world.Current.Items.ContainerStatus(o.Key).ToString(), Quantity = world.Current.Items.Contents(o.Key).Sum(i => i.Quantity) }),
            ReachableClosedDoors = map.ObjectLayers.SelectMany(l => l.Objects).Where(o => o.Header.Elevation == world.Location.Elevation && o.Header.Pid >> 24 == 2 && o.Subtype == 0)
                .Where(o => world.Current.Doors.Get(o.Key)?.IsOpen == false && Approach(o).HasValue)
                .Select(o => new { o.Key, o.Offset, o.Header.Id, o.Header.Tile, Status = world.Current.Doors.CanToggle(o.Key, Approach(o)!.Value.Target, world.Location.Elevation, false).ToString(),
                    Binding = map.ScriptBindings.SingleOrDefault(b => b.Sid == o.Header.Sid) }),
            ReachableScriptedBlockers = map.ObjectLayers.SelectMany(l => l.Objects).Where(o => o.Header.Elevation == world.Location.Elevation && o.Header.Sid >= 0
                && world.Current.Doors.Navigation(world.Location.Elevation).BlockersAt(o.Header.Tile).Any(b => b.Key == o.Key) && Approach(o).HasValue)
                .Select(o => new { o.Key, o.Offset, o.Header.Id, o.Header.Pid, o.Header.Tile, o.Subtype,
                    Binding = map.ScriptBindings.SingleOrDefault(b => b.Sid == o.Header.Sid) }),
            CheckpointRoundTrip = "verified", OriginalReconstruction = "verified" };
    }
}
