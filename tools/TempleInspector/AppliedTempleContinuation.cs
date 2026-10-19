using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class AppliedTempleContinuation
{
    public static object Create(WorldSession world, TempleBarrierEffectPlan plan, MapCatalog catalog, GameData data)
    {
        var prototypes = new PrototypeCatalog(new ResourceCatalog(data.Read)); int steps = 0;
        WorldSession Resume(byte[] bytes) => WorldSession.Restore(id => catalog.Load(data, id), new[] { 3, 4, 126 }, CheckpointFile.Decode(bytes), catalog.Source.Sha256, runtimePrototype: prototypes.Get);
        byte[] State(WorldSession value) => CheckpointFile.Encode(value.Capture(catalog.Source.Sha256));
        void Walk(WorldSession value, int hex)
        {
            var route = value.Current.Doors.Navigation(value.Location.Elevation).FindRoute(value.Location.Hex, hex);
            if (route.Status is not (RouteStatus.Ready or RouteStatus.AlreadyThere)) throw new InvalidDataException("Applied continuation attempted an unreachable walk.");
            steps += Math.Max(0, route.Tiles.Length - 1); value.Current.Travel.MoveTo(hex);
        }
        void TravelTo(WorldSession value, int elevation)
        {
            var reach = value.Current.Doors.Navigation(value.Location.Elevation).ReachableFrom(value.Location.Hex);
            int exit = value.Current.Map.ObjectLayers.SelectMany(l => l.Objects).Where(o => o.Header.Elevation == value.Location.Elevation
                && o.Header.Pid is >= 0x05000010 and <= 0x05000017 && o.State.GetValueOrDefault("destinationMap", -1) == 3
                && o.State.GetValueOrDefault("destinationElevation", -1) == elevation && reach.Distance(o.Header.Tile) >= 0)
                .Select(o => o.Header.Tile).OrderBy(reach.Distance).ThenBy(t => t).First();
            Walk(value, exit);
            if (value.UseExit(false).Status != ExitStatus.Ready || value.Location.Elevation != elevation) throw new InvalidDataException("Applied continuation exit failed.");
        }
        ActorLocation beforeActor = world.Location; int beforeXp = world.Experience; Guid id = Guid.NewGuid();
        var effect = world.CommitBarrierEffect(plan, id, false);
        if (effect.Status != BarrierCommitStatus.Ready || world.Location != beforeActor || world.Experience != beforeXp
            || world.Current.Doors.Navigation(1).ReachableFrom(29097).Count != 1258 || world.ItemUses.Single().Allocation.Phase != ItemUsePhase.Consumed)
            throw new InvalidDataException("Real barrier commit failed conservation or geometry checks.");
        byte[] after = State(world);
        if (world.CommitBarrierEffect(plan, Guid.NewGuid(), false).Status != BarrierCommitStatus.AlreadyReplaced || !after.SequenceEqual(State(world)))
            throw new InvalidDataException("Repeated real effect changed state.");
        Walk(world, 14322); var resumed = Resume(State(world));
        if (resumed.Location.Hex != 14322 || resumed.SceneryReplacements.Single().Id != id) throw new InvalidDataException("Real checkpoint failed on the former blocked tile.");
        TravelTo(resumed, 2); ActorLocation upperArrival = resumed.Location;
        int upperReachable = resumed.Current.Doors.Navigation(2).ReachableFrom(upperArrival.Hex).Count;
        var owner = resumed.Current.Items.Containers.Single(o => o.Offset == 458120 && o.Header.Id == 376);
        var navigation = resumed.Current.Doors.Navigation(2); var reach = navigation.ReachableFrom(resumed.Location.Hex);
        int approach = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(owner.Header.Tile, d)).Append(owner.Header.Tile)
            .Where(t => t.HasValue && reach.Distance(t.Value) >= 0 && !navigation.IsExit(t.Value)).Select(t => t!.Value).OrderBy(reach.Distance).ThenBy(t => t).First();
        Walk(resumed, approach);
        foreach (var item in resumed.Current.Items.Contents(owner.Key).Where(i => i.Quantity > 0))
            if (resumed.TakeItem(owner.Key, item.Source.Key, item.Quantity, false) != ItemTakeStatus.Ready) throw new InvalidDataException("Real upper-chest collection failed.");
        var collected = resumed.Carried.Select(i => new { i.Source.Key, Id = i.Source.Header.Id, i.Source.Header.Pid, i.Quantity }).ToArray();
        if (upperArrival != new ActorLocation(3, 2, 23914, 0) || upperReachable != 974 || resumed.CarriedCount != 2 || resumed.CarriedWeight != 2
            || !collected.Select(i => (i.Id, i.Pid)).ToHashSet().SetEquals(new[] { (126, 273), (100, 49) })) throw new InvalidDataException("Real upper continuation lost original arrivals/items/weights.");
        var scripted = resumed.Current.Items.Containers.Single(o => o.Offset == 465604);
        if (resumed.Current.Items.ContainerStatus(scripted.Key) != ItemTakeStatus.ScriptRequired || resumed.Current.Items.Contents(scripted.Key).Length != 0)
            throw new InvalidDataException("Real continuation bypassed the scripted chest.");
        resumed = Resume(State(resumed)); TravelTo(resumed, 1); ActorLocation returnArrival = resumed.Location; Walk(resumed, 14322);
        var final = Resume(State(resumed));
        if (final.Location.Hex != 14322 || final.CarriedCount != 2 || final.SceneryReplacements.Single().Id != id || final.Experience != beforeXp
            || !final.Current.Doors.Navigation(1).Walkable(14322)) throw new InvalidDataException("Real return checkpoint lost the passage, inventory or XP.");
        foreach (WorldArea area in final.Areas) MapLayoutWriter.Verify(area.Map);
        return new { Status = "APPLIED_TEMPLE_CONTINUATION_OK", Replacement = final.SceneryReplacements.Single(),
            CheckpointVersion = final.Capture(catalog.Source.Sha256).Version, ReachableAfterEffect = 1258, UpperArrival = upperArrival, UpperReachable = upperReachable,
            Collected = collected, final.CarriedCount, final.CarriedWeight, ScriptedChest = "empty and ScriptRequired", ReturnArrival = returnArrival, FinalLocation = final.Location,
            ExtraWalkingSteps = steps, ExperienceUnchangedByEffect = beforeXp, RepeatedEffect = "refused without mutation", Checkpoints = "former blocker, upper loot and return verified", OriginalReconstruction = "verified" };
    }
}
