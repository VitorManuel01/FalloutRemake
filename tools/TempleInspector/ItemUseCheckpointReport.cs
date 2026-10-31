using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class ItemUseCheckpointReport
{
    public static object Create(GameData data)
    {
        var catalog = new MapCatalog(data.Read("data/maps.txt")); var world = new WorldSession(id => catalog.Load(data, id), new[] { 3, 4, 126 });
        MapObject owner = world.Current.Items.Containers.Single(o => o.Offset == 337852 && o.Header.Id == 358);
        var item = world.Current.Items.Contents(owner.Key).Single(i => i.Source.Header.Id == 364 && i.Source.Header.Pid == 85);
        world.Current.Items.Restore(new[] { new TakenItem(owner.Key, item.Source.Key, 1) });
        WorldCheckpoint legacySource = world.Capture(catalog.Source.Sha256);
        world.Current.Travel.MoveTo(31306);
        if (world.UseExit(false).Status != ExitStatus.Ready) throw new InvalidDataException("Fixture exterior route unavailable.");
        var plan = ExplosiveUseProposal.Create(world, world.Carried.Single(), 60, SkillRollOutcome.Success, false).Plan!
            with { Id = Guid.Parse("ef860af8-5495-484c-a35e-974d4a418c51") };
        ExplosiveCommitResult commit = world.CommitExplosiveUse(plan, false);
        if (commit.Status != ExplosiveCommitStatus.Ready) throw new InvalidDataException("Original item commit failed.");
        ItemUseAllocation use = commit.Use!.Allocation;
        byte[] after = CheckpointFile.Encode(world.Capture(catalog.Source.Sha256));
        if (world.CommitExplosiveUse(plan, false).Status != ExplosiveCommitStatus.StalePlan || !after.SequenceEqual(CheckpointFile.Encode(world.Capture(catalog.Source.Sha256))))
            throw new InvalidDataException("Repeated use proposal changed the fixture.");
        byte[] encoded = CheckpointFile.Encode(world.Capture(catalog.Source.Sha256));
        var restored = WorldSession.Restore(id => catalog.Load(data, id), new[] { 3, 4, 126 }, CheckpointFile.Decode(encoded), catalog.Source.Sha256);
        if (restored.CurrentMap != 126 || restored.CarriedCount != 0 || restored.CarriedWeight != 0 || restored.ItemUses.Single().Allocation != use
            || !encoded.SequenceEqual(CheckpointFile.Encode(restored.Capture(catalog.Source.Sha256)))) throw new InvalidDataException("Placed item did not round-trip.");
        restored.AdvanceUseTime(175);
        var elapsed = WorldSession.Restore(id => catalog.Load(data, id), new[] { 3, 4, 126 },
            CheckpointFile.Decode(CheckpointFile.Encode(restored.Capture(catalog.Source.Sha256))), catalog.Source.Sha256);
        if (elapsed.ItemUses.Single().Allocation.RemainingTicks != 425 || elapsed.AdvanceUseTime(425).Due.Length != 1 || elapsed.CompleteDueUse(use.Id) != ItemUseStatus.Ready)
            throw new InvalidDataException("Partially elapsed logical use failed to complete.");
        var consumed = WorldSession.Restore(id => catalog.Load(data, id), new[] { 3, 4, 126 },
            CheckpointFile.Decode(CheckpointFile.Encode(elapsed.Capture(catalog.Source.Sha256))), catalog.Source.Sha256);
        var legacy = legacySource with { Entries = null, Combat = null, Version = 2, Profile = "neutral-v2", Areas = legacySource.Areas.Select(a => a with { Uses = null, Replacements = null }).ToArray() };
        var upgraded = WorldSession.Restore(id => catalog.Load(data, id), new[] { 3, 4, 126 }, CheckpointFile.Decode(CheckpointFile.Encode(legacy)), catalog.Source.Sha256);
        if (upgraded.CarriedCount != 1 || upgraded.ItemUses.Length != 0 || consumed.CarriedCount != 0 || consumed.ItemUses.Single().Allocation.Phase != ItemUsePhase.Consumed)
            throw new InvalidDataException("Legacy or consumed ownership restoration failed.");
        foreach (var area in consumed.Areas) MapLayoutWriter.Verify(area.Map);
        return new { Status = "ITEM_USE_CHECKPOINT_OK", Fixture = "isolated session seeded from original inventory; no gameplay placement, clock or explosion",
            Version = consumed.Capture(catalog.Source.Sha256).Version, Profile = consumed.Capture(catalog.Source.Sha256).Profile, SourceMap = 3, SourceItem = item.Source.Key, ItemId = item.Source.Header.Id, use.Placement,
            PlacedRoundTrip = "verified", PartialTimerRoundTrip = "verified: 175 elapsed, 425 remaining", ConsumedRoundTrip = "verified", RepeatedCommit = "refused without state change", LegacyV2Carried = upgraded.CarriedCount, UpgradedVersion = upgraded.Capture(catalog.Source.Sha256).Version,
            Balance = consumed.Areas.Single(a => a.Map.Prefix.Header.MapIndex == 3).Items.Balance(owner.Key, item.Source.Key),
            RuntimeActivationEnabled = false, OriginalReconstruction = "verified" };
    }
}
