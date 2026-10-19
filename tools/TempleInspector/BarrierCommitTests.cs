using System.Text;
using System.Text.Json.Nodes;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class BarrierCommitTests
{
    private static WorldSession World(int taken = 1, Func<int, Prototype>? prototype = null)
    {
        var world = new WorldSession(_ => BarrierEffectTests.Fixture(), new[] { 3 }, runtimePrototype: prototype ?? (_ => BarrierEffectTests.Replacement()));
        world.Current.Items.Restore(new[] { new TakenItem("owner", "item", taken) }); return world;
    }
    private static TempleBarrierEffectPlan Due(WorldSession world)
    {
        var use = world.CommitExplosiveUse(ExplosiveUseProposal.Create(world, world.Carried.Single(), 60, SkillRollOutcome.Success, false).Plan, false);
        world.AdvanceUseTime(600);
        return TempleBarrierEffectProposal.Create(world, use.Use!.Allocation.Id, BarrierEffectTests.Replacement()).Plan!;
    }
    private static WorldSession Restore(WorldCheckpoint saved, Func<int, Prototype>? prototype = null) => WorldSession.Restore(_ => BarrierEffectTests.Fixture(), new[] { 3 }, saved, "catalog", runtimePrototype: prototype ?? (_ => BarrierEffectTests.Replacement()));
    private static byte[] State(WorldSession world) => CheckpointFile.Encode(world.Capture("catalog"));
    public static void Run(Action<string, Action> check)
    {
        check("barrier commit consumes exactly its due unit and installs separate runtime scenery without XP or source mutation", () =>
        {
            var world = World(); var plan = Due(world); var actor = world.Location; var source = world.Current.Map.AllObjects.Last(); Guid id = Guid.NewGuid();
            var result = world.CommitBarrierEffect(plan, id, false);
            Assert(result.Status == BarrierCommitStatus.Ready && result.Replacement!.Id == id && result.Replacement.CauseUseId == plan.UseId
                && result.Replacement.Location == new ActorLocation(3, 1, 14322, 0) && result.Replacement.ReplacesKey == source.Key);
            Assert(world.Current.Items.Balance("owner", "item") == new ItemBalance(5, 4, 0, 0, 1) && world.Current.Doors.Navigation(1).Walkable(14322)
                && world.Current.Travel.CanArrive(new(3, 1, 14322, 0)) && world.Location == actor && world.Experience == 0 && source.Header.Flags == 0 && source.Header.Id == 44);
        });
        check("v4 restores replacement geometry before an actor standing on the former blocker and owns snapshot arrays", () =>
        {
            var world = World(); world.CommitBarrierEffect(Due(world), Guid.NewGuid(), false); world.Current.Travel.MoveTo(14322);
            var saved = CheckpointFile.Decode(State(world)); var restored = Restore(saved);
            Assert(saved.Version == 4 && saved.Profile == "neutral-v4" && restored.Location.Hex == 14322 && State(restored).SequenceEqual(State(world)));
            saved.Areas[0].Replacements![0] = saved.Areas[0].Replacements![0] with { Id = Guid.NewGuid() };
            Assert(world.SceneryReplacements.Single().Id == restored.SceneryReplacements.Single().Id);
            restored.Reset(); Assert(restored.SceneryReplacements.Length == 0 && !restored.Current.Doors.Navigation(1).Walkable(14322) && restored.Current.Items.Balance("owner", "item") == new ItemBalance(5, 5, 0, 0, 0));
        });
        check("repeated competing invalid and moving barrier commits preserve the entire prior checkpoint", () =>
        {
            var world = World(2);
            var first = world.CommitExplosiveUse(ExplosiveUseProposal.Create(world, world.Carried.Single(), 60, SkillRollOutcome.Success, false).Plan, false).Use!;
            var second = world.CommitExplosiveUse(ExplosiveUseProposal.Create(world, world.Carried.Single(), 60, SkillRollOutcome.Success, false).Plan, false).Use!;
            world.AdvanceUseTime(600);
            var a = TempleBarrierEffectProposal.Create(world, first.Allocation.Id, BarrierEffectTests.Replacement()).Plan!;
            var b = TempleBarrierEffectProposal.Create(world, second.Allocation.Id, BarrierEffectTests.Replacement()).Plan!;
            byte[] before = State(world);
            foreach (var invalid in new TempleBarrierEffectPlan?[] { null, a with { SessionToken = Guid.NewGuid() }, a with { SourceRevision = a.SourceRevision - 1 },
                a with { UseId = Guid.NewGuid() }, a with { TargetKey = null! }, a with { TargetKey = "other" }, a with { Center = null! }, a with { ReplacementHash = "changed" } })
            { Assert(world.CommitBarrierEffect(invalid, Guid.NewGuid(), false).Status != BarrierCommitStatus.Ready); Assert(State(world).SequenceEqual(before)); }
            Assert(world.CommitBarrierEffect(a, Guid.NewGuid(), true).Status == BarrierCommitStatus.Busy && State(world).SequenceEqual(before));
            foreach (Guid id in new[] { Guid.Empty, a.UseId }) Assert(world.CommitBarrierEffect(a, id, false).Status == BarrierCommitStatus.InvalidIdentity && State(world).SequenceEqual(before));
            Guid replacement = Guid.NewGuid(); Assert(world.CommitBarrierEffect(a, replacement, false).Status == BarrierCommitStatus.Ready); before = State(world);
            Assert(world.CommitBarrierEffect(a, replacement, false).Status == BarrierCommitStatus.InvalidIdentity && State(world).SequenceEqual(before));
            Assert(world.CommitBarrierEffect(b, Guid.NewGuid(), false).Status == BarrierCommitStatus.AlreadyReplaced && State(world).SequenceEqual(before));
            Assert(TempleBarrierEffectProposal.Create(world, b.UseId, BarrierEffectTests.Replacement()).Status == BarrierEffectStatus.AlreadyReplaced && world.ItemUses.Single(u => u.Allocation.Id == b.UseId).Allocation.Phase == ItemUsePhase.Placed);
        });
        check("prototype resolution and changed runtime resources refuse before consuming a due explosive", () =>
        {
            foreach (Func<int, Prototype> resolver in new Func<int, Prototype>[] { _ => throw new FileNotFoundException(),
                _ => BarrierEffectTests.Replacement() with { Resource = BarrierEffectTests.Replacement().Resource with { Sha256 = "changed" } } })
            {
                var world = World(prototype: resolver); var plan = Due(world); byte[] before = State(world);
                Assert(world.CommitBarrierEffect(plan, Guid.NewGuid(), false).Status == BarrierCommitStatus.UnsupportedProfile && State(world).SequenceEqual(before));
            }
            var missing = new WorldSession(_ => BarrierEffectTests.Fixture(), new[] { 3 }); missing.Current.Items.Restore(new[] { new TakenItem("owner", "item", 1) });
            var early = missing.CommitExplosiveUse(ExplosiveUseProposal.Create(missing, missing.Carried.Single(), 60, SkillRollOutcome.Success, false).Plan, false);
            missing.AdvanceUseTime(600); byte[] snapshot = State(missing);
            Assert(missing.CommitBarrierEffect(TempleBarrierEffectProposal.Create(missing, early.Use!.Allocation.Id, BarrierEffectTests.Replacement()).Plan, Guid.NewGuid(), false).Status == BarrierCommitStatus.UnsupportedProfile && State(missing).SequenceEqual(snapshot));
        });
        check("v4 rejects forged replacement records missing causes and self-authorizing placement on the blocked tile", () =>
        {
            var world = World(); var plan = Due(world); world.CommitBarrierEffect(plan, Guid.NewGuid(), false); var saved = world.Capture("catalog"); var record = saved.Areas[0].Replacements!.Single(); byte[] before = State(world);
            foreach (var invalid in new SceneryReplacement?[] { null, record with { Id = Guid.Empty }, record with { Id = plan.UseId }, record with { CauseUseId = Guid.NewGuid() },
                record with { ReplacesKey = "other" }, record with { Location = null! }, record with { Location = record.Location with { Map = 126 } },
                record with { Location = record.Location with { Hex = 14323 } }, record with { Location = record.Location with { Rotation = 1 } },
                record with { Pid = 85 }, record with { Fid = 1 }, record with { PrototypeHash = "changed" } })
                Reject(() => Restore(saved with { Areas = new[] { saved.Areas[0] with { Replacements = new[] { invalid! } } } }));
            foreach (var records in new SceneryReplacement[]?[] { null, new[] { record, record with { Id = Guid.NewGuid() } } })
                Reject(() => Restore(saved with { Areas = new[] { saved.Areas[0] with { Replacements = records } } }));
            foreach (var changedUse in new[] { saved.Areas[0].Uses![0] with { Phase = ItemUsePhase.Placed }, saved.Areas[0].Uses![0] with { Placement = new(3, 0, 14521, 0) },
                saved.Areas[0].Uses![0] with { Placement = new(3, 1, 14322, 0) }, saved.Areas[0].Uses![0] with { Placement = new(3, 1, 17000, 0) } })
                Reject(() => Restore(saved with { Areas = new[] { saved.Areas[0] with { Uses = new[] { changedUse } } } }));
            Reject(() => WorldSession.Restore(_ => BarrierEffectTests.Fixture(), new[] { 3 }, saved, "catalog"));
            Reject(() => Restore(saved, _ => BarrierEffectTests.Replacement() with { ScriptId = 34 }));
            Assert(State(world).SequenceEqual(before));
        });
        check("replacement JSON requires every identity position prototype and cause field", () =>
        {
            var world = World(); world.CommitBarrierEffect(Due(world), Guid.NewGuid(), false); byte[] bytes = State(world);
            foreach (string field in new[] { "Id", "CauseUseId", "ReplacesKey", "Location", "Pid", "Fid", "PrototypeHash" })
            { var node = JsonNode.Parse(bytes)!; node["Areas"]![0]!["Replacements"]![0]!.AsObject().Remove(field); Reject(() => CheckpointFile.Decode(Encoding.UTF8.GetBytes(node.ToJsonString()))); }
            Assert(!Encoding.UTF8.GetString(bytes).Contains("RawBytes"));
        });
        check("legacy v3 retains carried placed and consumed state with no invented replacement and upgrades to v4", () =>
        {
            var world = World(); var plan = Due(world); var saved = world.Capture("catalog");
            var legacy = saved with { Version = 3, Profile = "neutral-v3", Areas = saved.Areas.Select(a => a with { Replacements = null }).ToArray() };
            var restored = Restore(CheckpointFile.Decode(CheckpointFile.Encode(legacy)));
            Assert(restored.ItemUses.Single().Allocation.Phase == ItemUsePhase.Placed && restored.SceneryReplacements.Length == 0 && restored.Capture("catalog").Version == 4);
            world.CompleteDueUse(plan.UseId); saved = world.Capture("catalog");
            legacy = saved with { Version = 3, Profile = "neutral-v3", Areas = saved.Areas.Select(a => a with { Replacements = null }).ToArray() };
            restored = Restore(legacy); Assert(restored.ItemUses.Single().Allocation.Phase == ItemUsePhase.Consumed && !restored.Current.Doors.Navigation(1).Walkable(14322));
            Reject(() => Restore(legacy with { Areas = legacy.Areas.Select(a => a with { Replacements = Array.Empty<SceneryReplacement>() }).ToArray() }));
        });
        check("new placements may use an opened barrier tile and restore against the runtime overlay", () =>
        {
            var world = World(2); var plan = Due(world); world.CommitBarrierEffect(plan, Guid.NewGuid(), false); world.Current.Travel.MoveTo(14322);
            var use = world.CommitExplosiveUse(ExplosiveUseProposal.Create(world, world.Carried.Single(), 60, SkillRollOutcome.Success, false).Plan, false);
            Assert(use.Status == ExplosiveCommitStatus.Ready && use.Use!.Allocation.Placement.Hex == 14322);
            var restored = Restore(CheckpointFile.Decode(State(world))); Assert(restored.Location.Hex == 14322 && restored.SceneryReplacements.Length == 1 && restored.DueItemUses.Length == 0);
        });
        check("loaded worlds reject old effect tokens while the ground event is independent of actor movement", () =>
        {
            var world = World(); var plan = Due(world); var restored = Restore(world.Capture("catalog")); byte[] before = State(restored);
            Assert(restored.CommitBarrierEffect(plan, Guid.NewGuid(), false).Status == BarrierCommitStatus.StalePlan && State(restored).SequenceEqual(before));
            var fresh = TempleBarrierEffectProposal.Create(restored, plan.UseId, BarrierEffectTests.Replacement()).Plan!;
            restored.Current.Travel.MoveTo(14522); var actor = restored.Location;
            Assert(restored.CommitBarrierEffect(fresh, Guid.NewGuid(), false).Status == BarrierCommitStatus.Ready && restored.Location == actor && restored.SceneryReplacements.Single().Location.Hex == 14322);
        });
        check("inactive placement area scenery and consumption restore while the exterior remains active", () =>
        {
            var map = BarrierEffectTests.Fixture(); var exit = WorldTests.Map(3, 126).ObjectLayers[0].Objects[0];
            exit = exit with { State = exit.State.ToDictionary(p => p.Key, p => p.Key == "destinationTile" ? 20101 : p.Value) };
            map = map with { ObjectLayers = new[] { map.ObjectLayers[0] with { Objects = map.ObjectLayers[0].Objects.Append(exit).ToArray() }, map.ObjectLayers[1] } };
            MapDocument Load(int id) => id == 3 ? map : WorldTests.Map(126, 3);
            var world = new WorldSession(Load, new[] { 3, 126 }, runtimePrototype: _ => BarrierEffectTests.Replacement()); world.Current.Items.Restore(new[] { new TakenItem("owner", "item", 1) });
            var plan = Due(world); world.Current.Travel.Arrive(new(3, 0, 20100, 0)); Assert(world.UseExit(false).Status == ExitStatus.Ready);
            Assert(world.CommitBarrierEffect(plan, Guid.NewGuid(), false).Status == BarrierCommitStatus.Ready && world.CurrentMap == 126);
            var restored = WorldSession.Restore(Load, new[] { 3, 126 }, CheckpointFile.Decode(State(world)), "catalog", runtimePrototype: _ => BarrierEffectTests.Replacement());
            Assert(restored.CurrentMap == 126 && restored.Areas.Single(a => a.Map.Prefix.Header.MapIndex == 3).Doors.Navigation(1).Walkable(14322) && State(restored).SequenceEqual(State(world)));
        });
        check("replacement cause can belong to an inventory in a different area and is resolved after all inventories restore", () =>
        {
            var outside = ExplosiveProposalTests.Fixture(); var exit = WorldTests.Map(126, 3).ObjectLayers[0].Objects[0];
            exit = exit with { State = exit.State.ToDictionary(p => p.Key, p => p.Key switch { "destinationTile" => 14521, "destinationElevation" => 1, _ => p.Value }) };
            outside = outside with { Source = outside.Source with { Sha256 = "outside", Path = "maps/outside.map" },
                Prefix = outside.Prefix with { Header = outside.Prefix.Header with { MapIndex = 126 } },
                ObjectLayers = new[] { outside.ObjectLayers[0] with { Objects = outside.ObjectLayers[0].Objects.Append(exit).ToArray() } } };
            MapDocument Load(int id) => id == 126 ? outside : BarrierEffectTests.Fixture();
            var world = new WorldSession(Load, new[] { 3, 126 }, 126, _ => BarrierEffectTests.Replacement());
            world.Current.Items.Restore(new[] { new TakenItem("owner", "item", 1) }); Assert(world.UseExit(false).Status == ExitStatus.Ready);
            var plan = Due(world); Assert(plan.SourceMap == 126 && world.CommitBarrierEffect(plan, Guid.NewGuid(), false).Status == BarrierCommitStatus.Ready);
            var saved = CheckpointFile.Decode(State(world));
            var restored = WorldSession.Restore(Load, new[] { 3, 126 }, saved, "catalog", 126, _ => BarrierEffectTests.Replacement());
            Assert(restored.CurrentMap == 3 && restored.Current.Scenery.Capture().Length == 1 && restored.Current.Items.CaptureUses().Length == 0
                && restored.ItemUses.Single().SourceMap == 126 && restored.ItemUses.Single().Allocation.Phase == ItemUsePhase.Consumed && State(restored).SequenceEqual(State(world)));
        });
    }
    private static void Assert(bool condition) { if (!condition) throw new Exception("Barrier commit/checkpoint assertion failed."); }
    private static void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Invalid scenery checkpoint accepted."); }
}
