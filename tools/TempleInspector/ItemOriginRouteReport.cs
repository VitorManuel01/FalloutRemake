using System.Text.Json;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class ItemOriginRouteReport
{
    public static object Create(GameData data)
    {
        var catalog = new MapCatalog(data.Read("data/maps.txt")); int loads = 0;
        var world = new WorldSession(id => { loads++; return catalog.Load(data, id); }, new[] { 3, 4, 126 });
        world.Current.Travel.MoveTo(24508);
        MapObject door = world.Current.Map.AllObjects.Single(o => o.Offset == 244040);
        if (world.Current.Doors.Toggle(door.Key, world.Location.Hex, 0, false) != DoorUseStatus.Ready) throw new InvalidDataException("First-door profile changed.");
        MapObject pot = world.Current.Items.Containers.Single(o => o.Offset == 206348);
        var nav = world.Current.Doors.Navigation(0); var reach = nav.ReachableFrom(world.Location.Hex);
        int approach = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(pot.Header.Tile, d)).Append(pot.Header.Tile)
            .Where(t => t.HasValue && reach.Distance(t.Value) >= 0 && !nav.IsExit(t.Value)).Select(t => t!.Value).OrderBy(reach.Distance).ThenBy(t => t).First();
        world.Current.Travel.MoveTo(approach); var powder = world.Current.Items.Contents(pot.Key).Single();
        if (world.TakeItem(pot.Key, powder.Source.Key, 1, false) != ItemTakeStatus.Ready) throw new InvalidDataException("Real item transfer unavailable.");
        reach = nav.ReachableFrom(world.Location.Hex);
        int exit = world.Current.Map.ObjectLayers.SelectMany(l => l.Objects).Where(o => o.Header.Elevation == 0
            && o.Header.Pid is >= 0x05000010 and <= 0x05000017 && o.State.GetValueOrDefault("destinationMap", -1) == 126 && reach.Distance(o.Header.Tile) >= 0)
            .Select(o => o.Header.Tile).OrderBy(reach.Distance).ThenBy(t => t).First();
        world.Current.Travel.MoveTo(exit);
        if (world.UseExit(false).Status != ExitStatus.Ready) throw new InvalidDataException("Exterior transition unavailable.");
        string before = JsonSerializer.Serialize(world.Capture(catalog.Source.Sha256)); int loadsBefore = loads;
        ItemOriginPlan plan = ItemOriginRoute.Find(world, world.Carried.Single(), false) ?? throw new InvalidDataException("No return route from the real exterior.");
        if (plan.Transitions != 1 || plan.Legs[0].Destination?.Map != 3 || loads != loadsBefore
            || before != JsonSerializer.Serialize(world.Capture(catalog.Source.Sha256))) throw new InvalidDataException("Planning altered state or lost the real return route.");
        foreach (WorldArea area in world.Areas) MapLayoutWriter.Verify(area.Map);
        return new { Status = "ITEM_ORIGIN_ROUTE_OK", Actor = world.Location, Item = new { powder.Source.Key, powder.Source.Header.Id },
            plan.WalkingSteps, plan.Transitions, Legs = plan.Legs.Select(l => new { l.Map, l.Elevation, From = l.Tiles[0], To = l.Tiles[^1], Steps = l.Tiles.Length - 1, l.Destination }),
            LoadsDuringPlanning = loads - loadsBefore, WorldStateUnchanged = true, OriginalReconstruction = "verified" };
    }
}
