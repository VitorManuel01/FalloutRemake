using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class TempleContinuationAudit
{
    public static object Create(WorldSession world, TempleBarrierProjection projection)
    {
        MapDocument map = world.Current.Map;
        var reach = projection.Navigation.ReachableFrom(world.Location.Hex);
        MapObject exit = map.ObjectLayers.SelectMany(l => l.Objects).Where(o => o.Header.Elevation == 1
            && o.Header.Pid is >= 0x05000010 and <= 0x05000017 && o.State.GetValueOrDefault("destinationMap", -1) == 3
            && o.State.GetValueOrDefault("destinationElevation", -1) == 2 && reach.Distance(o.Header.Tile) >= 0)
            .OrderBy(o => reach.Distance(o.Header.Tile)).ThenBy(o => o.Offset).First();
        var option = world.Current.Travel.InspectExitAt(1, exit.Header.Tile);
        if (option.Status != ExitStatus.Ready || !world.Current.Travel.CanArrive(option.Destination!)) throw new InvalidDataException("Projected elevation-2 arrival is unavailable.");
        ActorLocation arrival = option.Destination!; var navigation = world.Current.Doors.Navigation(2); var upperReach = navigation.ReachableFrom(arrival.Hex);
        var containers = world.Current.Items.Containers.Where(o => o.Header.Elevation == 2).Select(owner =>
        {
            int? approach = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(owner.Header.Tile, d)).Append(owner.Header.Tile)
                .Where(t => t.HasValue && upperReach.Distance(t.Value) >= 0 && !navigation.IsExit(t.Value))
                .OrderBy(t => upperReach.Distance(t!.Value)).ThenBy(t => t).FirstOrDefault();
            return new { Owner = owner, Approach = approach, Contents = world.Current.Items.Contents(owner.Key) };
        }).ToArray();
        var candidates = containers.Where(c => c.Approach.HasValue).SelectMany(c => c.Contents.Where(i => i.Quantity > 0).Select(item => new {
            OwnerId = c.Owner.Header.Id, OwnerKey = c.Owner.Key, ItemId = item.Source.Header.Id, ItemKey = item.Source.Key, item.Source.Header.Pid,
            item.Quantity, Approach = c.Approach!.Value, Steps = upperReach.Distance(c.Approach.Value),
            Status = world.Current.Items.CanTake(c.Owner.Key, item.Source.Key, item.Quantity, arrival with { Hex = c.Approach.Value }, navigation, false).ToString(),
            UnitWeight = world.UnitWeight(3, item.Source), Path = upperReach.PathTo(c.Approach.Value) })).ToArray();
        if (candidates.Length != 2 || candidates.Any(c => c.OwnerId != 376 || c.Status != "Ready" || c.Quantity != 1 || c.UnitWeight != 1)
            || !candidates.Select(c => (c.ItemId, c.Pid)).ToHashSet().SetEquals(new[] { (126, 273), (100, 49) }))
            throw new InvalidDataException("Projected continuation differs from the two original static items on elevation 2.");
        var scripted = containers.Single(c => c.Owner.Offset == 465604);
        if (scripted.Owner.Header.Id != 100 || scripted.Contents.Length != 0 || world.Current.Items.ContainerStatus(scripted.Owner.Key) != ItemTakeStatus.ScriptRequired)
            throw new InvalidDataException("Projected continuation bypassed the known scripted empty chest.");
        return new { Mode = "read-only continuation on detached barrier geometry; no travel or pickup applied", SourceExit = new { exit.Key, Hex = exit.Header.Tile, Path = reach.PathTo(exit.Header.Tile) },
            Arrival = arrival, ReachableOnElevation2 = upperReach.Count, CandidatePickups = candidates,
            ScriptedChest = new { scripted.Owner.Key, Id = scripted.Owner.Header.Id, Contents = 0, Status = "ScriptRequired", ReachableApproach = scripted.Approach },
            ProjectedStaticUnits = candidates.Sum(c => c.Quantity), ProjectedStaticWeight = 2 };
    }
}
