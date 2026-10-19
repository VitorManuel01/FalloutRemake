using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class BarrierProjectionTests
{
    private static (WorldSession World, TempleBarrierEffectPlan Plan) Fixture(MapDocument? map = null)
    {
        var world = new WorldSession(_ => map ?? BarrierEffectTests.Fixture(), new[] { 3 }); world.Current.Items.Restore(new[] { new TakenItem("owner", "item", 1) });
        var use = world.CommitExplosiveUse(ExplosiveUseProposal.Create(world, world.Carried.Single(), 60, SkillRollOutcome.Success, false).Plan, false);
        world.AdvanceUseTime(600); return (world, TempleBarrierEffectProposal.Create(world, use.Use!.Allocation.Id, BarrierEffectTests.Replacement()).Plan!);
    }
    private static string State(WorldSession world) => System.Text.Json.JsonSerializer.Serialize(world.Capture("catalog"));
    public static void Run(Action<string, Action> check)
    {
        check("projected barrier replacement has its own identity and original location without changing live navigation", () =>
        {
            var (world, plan) = Fixture(); string before = State(world); Guid id = Guid.NewGuid();
            var result = TempleBarrierProjection.Create(world, plan, BarrierEffectTests.Replacement(), id);
            Assert(result.Status == BarrierProjectionStatus.Ready); var projection = result.Projection!;
            Assert(projection.Replacement.Id == id && projection.Replacement.CauseUseId == plan.UseId && id != plan.UseId
                && projection.Replacement.ReplacesKey == plan.TargetKey && projection.Replacement.Location == new ActorLocation(3, 1, 14322, 0)
                && projection.Replacement.Pid == 33555365 && projection.Replacement.Fid == 33555376);
            Assert(projection.Navigation.Walkable(14322) && !world.Current.Doors.Navigation(1).Walkable(14322) && State(world) == before);
        });
        check("projection rejects changed session revision target location parameters and invalid runtime identities", () =>
        {
            var (world, plan) = Fixture(); var prototype = BarrierEffectTests.Replacement(); string before = State(world);
            foreach (var invalid in new TempleBarrierEffectPlan?[] { null, plan with { SessionToken = Guid.NewGuid() }, plan with { SourceRevision = plan.SourceRevision + 1 },
                plan with { UseId = Guid.NewGuid() }, plan with { TargetKey = "another" }, plan with { SourceMap = 126 }, plan with { Distance = 3 },
                plan with { Center = plan.Center with { Hex = 14522 } }, plan with { ReplacementPid = 1 }, plan with { ReplacementFid = 1 }, plan with { ReplacementHash = "changed" } })
                Assert(TempleBarrierProjection.Create(world, invalid, prototype, Guid.NewGuid()).Status == BarrierProjectionStatus.StalePlan);
            foreach (Guid id in new[] { Guid.Empty, plan.UseId }) Assert(TempleBarrierProjection.Create(world, plan, prototype, id).Status == BarrierProjectionStatus.InvalidIdentity);
            Assert(State(world) == before);
            world.CompleteDueUse(plan.UseId); before = State(world);
            Assert(TempleBarrierProjection.Create(world, plan, prototype, Guid.NewGuid()).Status == BarrierProjectionStatus.StalePlan && State(world) == before);
        });
        check("projection preserves unrelated closed doors walls exits and live source object counts", () =>
        {
            var map = BarrierEffectTests.Fixture(); var barrier = map.ObjectLayers[1].Objects.Single();
            var wall = barrier with { Key = "wall", Offset = 1, Subtype = null, Header = barrier.Header with { Id = 77, Tile = 14722, Sid = -1, ScriptIndex = -1, Pid = 0x03000001, Fid = 0x03000001 } };
            var door = wall with { Key = "door", Offset = 2, Subtype = 0, Header = wall.Header with { Tile = 14922, Pid = 0x02000001, Fid = 0x02000001 }, State = new Dictionary<string, int> { ["openFlags"] = 0 } };
            var exit = WorldTests.Map(3, 126).ObjectLayers[0].Objects[0]; exit = exit with { Header = exit.Header with { Tile = 15122, Elevation = 1 } };
            map = map with { ObjectLayers = new[] { map.ObjectLayers[0], map.ObjectLayers[1] with { Objects = new[] { barrier, wall, door, exit } } } };
            var (world, plan) = Fixture(map); string before = State(world); int count = world.Current.Map.AllObjects.Count();
            var projection = TempleBarrierProjection.Create(world, plan, BarrierEffectTests.Replacement(), Guid.NewGuid()).Projection!;
            Assert(projection.Navigation.Walkable(14322) && !projection.Navigation.Walkable(14722) && !projection.Navigation.Walkable(14922)
                && projection.Navigation.IsExit(15122) && world.Current.Map.AllObjects.Count() == count && State(world) == before);
            var live = world.Current.Doors.Navigation(1);
            int approach = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(14922, d)!.Value).First(t => live.Walkable(t) && !live.IsExit(t));
            Assert(world.Current.Doors.Toggle("door", approach, 1, false) == DoorUseStatus.Ready);
            // The detached graph remains a snapshot even after a later live door change.
            Assert(!projection.Navigation.Walkable(14922) && world.Current.Doors.Navigation(1).Walkable(14922));
        });
        check("loaded worlds require a fresh proposal and retain replacement identity only in the caller projection", () =>
        {
            var (world, plan) = Fixture(); var restored = WorldSession.Restore(_ => BarrierEffectTests.Fixture(), new[] { 3 }, world.Capture("catalog"), "catalog");
            var prototype = BarrierEffectTests.Replacement(); Guid id = Guid.NewGuid();
            Assert(TempleBarrierProjection.Create(restored, plan, prototype, id).Status == BarrierProjectionStatus.StalePlan);
            var fresh = TempleBarrierEffectProposal.Create(restored, plan.UseId, prototype).Plan!;
            var a = TempleBarrierProjection.Create(restored, fresh, prototype, id).Projection!;
            var b = TempleBarrierProjection.Create(restored, fresh, prototype, id).Projection!;
            Assert(a.Replacement == b.Replacement && a.Navigation.Walkable(14322) && restored.ItemUses.Single().Allocation.Phase == ItemUsePhase.Placed);
        });
    }
    private static void Assert(bool condition) { if (!condition) throw new Exception("Barrier scene projection assertion failed."); }
}
