using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class BarrierEffectReport
{
    public static object Create(GameData data, bool apply = false)
    {
        var catalog = new MapCatalog(data.Read("data/maps.txt")); var prototypes = new PrototypeCatalog(new ResourceCatalog(data.Read));
        var world = new WorldSession(id => catalog.Load(data, id), new[] { 3, 4, 126 }, runtimePrototype: prototypes.Get);
        MapDocument map = world.Current.Map; int steps = 0;
        int Approach(MapObject obj)
        {
            var nav = world.Current.Doors.Navigation(world.Location.Elevation); var reach = nav.ReachableFrom(world.Location.Hex);
            return Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(obj.Header.Tile, d)).Append(obj.Header.Tile)
                .Where(t => t.HasValue && reach.Distance(t.Value) >= 0 && !nav.IsExit(t.Value)).Select(t => t!.Value)
                .OrderBy(reach.Distance).ThenBy(t => t).First();
        }
        void Walk(int hex)
        {
            var route = world.Current.Doors.Navigation(world.Location.Elevation).FindRoute(world.Location.Hex, hex);
            if (route.Status is not (RouteStatus.Ready or RouteStatus.AlreadyThere)) throw new InvalidDataException("Barrier effect audit attempted an unreachable walk.");
            steps += Math.Max(0, route.Tiles.Length - 1); world.Current.Travel.MoveTo(hex);
        }
        foreach (int offset in new[] { 244040, 137396 })
        {
            var door = map.AllObjects.Single(o => o.Offset == offset); Walk(Approach(door));
            if (offset == 137396 && world.Current.Doors.UseLockpick(door.Key, world.Location.Hex, 0, false, _ => SkillRollOutcome.Success).ExperienceAwarded != 25)
                throw new InvalidDataException("Known lockpick fixture failed.");
            if (world.Current.Doors.Toggle(door.Key, world.Location.Hex, 0, false) != DoorUseStatus.Ready) throw new InvalidDataException("Audit door failed to open.");
        }
        var reached = world.Current.Doors.Navigation(0).ReachableFrom(world.Location.Hex);
        int exit = map.ObjectLayers[0].Objects.Where(o => o.State.GetValueOrDefault("destinationMap", -1) == 3
            && o.State.GetValueOrDefault("destinationElevation", -1) == 1 && reached.Distance(o.Header.Tile) >= 0)
            .Select(o => o.Header.Tile).OrderBy(reached.Distance).ThenBy(t => t).First();
        Walk(exit); if (world.UseExit(false).Status != ExitStatus.Ready) throw new InvalidDataException("Audit elevation exit failed.");
        MapObject owner = world.Current.Items.Containers.Single(o => o.Offset == 337852 && o.Header.Id == 358);
        MapObject item = world.Current.Items.Contents(owner.Key).Single(i => i.Source.Header.Id == 364 && i.Source.Header.Pid == 85).Source;
        Walk(Approach(owner));
        if (world.TakeItem(owner.Key, item.Key, 1, false) != ItemTakeStatus.Ready) throw new InvalidDataException("Original explosive could not be collected.");
        var nav = world.Current.Doors.Navigation(1); var reach = nav.ReachableFrom(world.Location.Hex);
        int placement = Enumerable.Range(1, 39999).Where(t => reach.Distance(t) >= 0 && !nav.IsExit(t) && FalloutGrid.Distance(t, 14322) <= 3)
            .OrderBy(t => FalloutGrid.Distance(t, 14322)).ThenBy(reach.Distance).ThenBy(t => t).First();
        Walk(placement);
        var usePlan = ExplosiveUseProposal.Create(world, world.Carried.Single(), 60, SkillRollOutcome.Success, false).Plan!;
        var committed = world.CommitExplosiveUse(usePlan, false);
        if (committed.Status != ExplosiveCommitStatus.Ready) throw new InvalidDataException("Audit use commit failed.");
        Prototype replacement = new PrototypeCatalog(new ResourceCatalog(data.Read)).Get(TempleBarrierEffectProposal.ReplacementPid);
        if (TempleBarrierEffectProposal.Create(world, usePlan.Id, replacement).Status != BarrierEffectStatus.NotDue) throw new InvalidDataException("Effect was proposed before the deadline.");
        world.AdvanceUseTime(600); byte[] before = CheckpointFile.Encode(world.Capture(catalog.Source.Sha256));
        var result = TempleBarrierEffectProposal.Create(world, usePlan.Id, replacement);
        if (result.Status != BarrierEffectStatus.Ready || result.Plan!.TargetKey != TempleBarrierEffectProposal.MapHash + ":303324"
            || !before.SequenceEqual(CheckpointFile.Encode(world.Capture(catalog.Source.Sha256))) || nav.ReachableFrom(29097).Count != 1245
            || world.Current.Doors.Navigation(1).Walkable(14322)) throw new InvalidDataException("Barrier proposal changed state or missed its verified target.");
        var restored = WorldSession.Restore(id => catalog.Load(data, id), new[] { 3, 4, 126 }, CheckpointFile.Decode(before), catalog.Source.Sha256);
        var restoredResult = TempleBarrierEffectProposal.Create(restored, usePlan.Id, replacement);
        if (restoredResult.Status != BarrierEffectStatus.Ready || restoredResult.Plan!.TargetKey != result.Plan.TargetKey
            || restoredResult.Plan.SessionToken == result.Plan.SessionToken || !before.SequenceEqual(CheckpointFile.Encode(restored.Capture(catalog.Source.Sha256))))
            throw new InvalidDataException("Restored due effect proposal was inconsistent.");
        var projected = TempleBarrierProjection.Create(world, result.Plan, replacement, Guid.NewGuid());
        if (projected.Status != BarrierProjectionStatus.Ready) throw new InvalidDataException("Barrier scene projection failed.");
        var projectedReach = projected.Projection!.Navigation.ReachableFrom(29097);
        int[] projectedExits = map.ObjectLayers.SelectMany(l => l.Objects).Where(o => o.Header.Elevation == 1
            && o.Header.Pid is >= 0x05000010 and <= 0x05000017 && o.State.GetValueOrDefault("destinationMap", -1) == 3
            && o.State.GetValueOrDefault("destinationElevation", -1) == 2 && projectedReach.Distance(o.Header.Tile) >= 0)
            .Select(o => o.Header.Tile).Distinct().OrderBy(t => t).ToArray();
        if (projectedReach.Count != 1258 || projectedExits.Length != 6 || world.Current.Doors.Navigation(1).Walkable(14322)
            || !before.SequenceEqual(CheckpointFile.Encode(world.Capture(catalog.Source.Sha256)))) throw new InvalidDataException("Detached projection changed the live world or failed to expose the verified passage.");
        object continuation = TempleContinuationAudit.Create(world, projected.Projection);
        if (!before.SequenceEqual(CheckpointFile.Encode(world.Capture(catalog.Source.Sha256)))) throw new InvalidDataException("Projected continuation mutated the source session.");
        object? applied = apply ? AppliedTempleContinuation.Create(world, result.Plan, catalog, data) : null;
        MapLayoutWriter.Verify(map);
        return new { Status = apply ? "BARRIER_COMMIT_OK" : "BARRIER_EFFECT_PROPOSAL_OK", Fixture = "original geometry and real collection; forced successful test lockpick and timer roll; restricted scenery effect only when requested; no general combat",
            WalkingSteps = steps, Source = new { Map = 3, OwnerId = owner.Header.Id, ItemId = item.Header.Id, item.Key },
            Proposal = result.Plan, Replacement = replacement.Resource, ReachableUnchanged = 1245, world.Location, world.Experience,
            DetachedProjection = new { projected.Projection.Replacement, Reachable = projectedReach.Count, ExitsToElevation2 = projectedExits, AppliedToWorld = false, Persisted = false },
            ProjectedContinuation = continuation,
            AppliedContinuation = applied,
            Ownership = world.Current.Items.Balance(owner.Key, item.Key), OriginalReconstruction = "verified",
            CheckpointRoundTrip = "verified", MutationDuringProposal = false, RuntimeEffectsEnabled = apply };
    }
}
