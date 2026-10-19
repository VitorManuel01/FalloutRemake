using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class PendingUseClockTests
{
    private static WorldSession World()
    { var world = new WorldSession(_ => ExplosiveProposalTests.Fixture(), new[] { 3 }); world.Current.Items.Restore(new[] { new TakenItem("owner", "item", 3) }); return world; }
    private static WorldItemUse Place(WorldSession world, SkillRollOutcome outcome = SkillRollOutcome.Success)
    {
        var proposal = ExplosiveUseProposal.Create(world, world.Carried.Single(), 60, outcome, false);
        return world.CommitExplosiveUse(proposal.Plan, false).Use ?? throw new Exception("Cannot prepare pending clock fixture.");
    }
    private static byte[] State(WorldSession world) => CheckpointFile.Encode(world.Capture("catalog"));
    public static void Run(Action<string, Action> check)
    {
        check("logical use clock reaches deadlines without consuming items or changing actors doors and source quantities", () =>
        {
            var world = World(); var a = Place(world); var b = Place(world); var position = world.Location; var doors = world.Current.Doors.Capture();
            Assert(world.AdvanceUseTime(599).Length == 0 && world.ItemUses.All(u => u.Allocation.RemainingTicks == 1));
            var due = world.AdvanceUseTime(1);
            Assert(due.Length == 2 && due.Select(u => u.Allocation.Id).ToHashSet().SetEquals(new[] { a.Allocation.Id, b.Allocation.Id }));
            Assert(world.ItemUses.All(u => u.Allocation.Phase == ItemUsePhase.Placed) && world.Current.Items.Balance("owner", "item") == new ItemBalance(5, 2, 1, 2, 0));
            Assert(world.Location == position && world.Experience == 0 && world.Current.Doors.Capture().Doors.SequenceEqual(doors.Doors));
        });
        check("a due use remains pending until explicit completion and cannot be completed early or twice", () =>
        {
            var world = World(); var use = Place(world); byte[] before = State(world);
            Assert(world.CompleteDueUse(use.Allocation.Id) == ItemUseStatus.InvalidState && State(world).SequenceEqual(before));
            world.AdvanceUseTime(600); before = State(world);
            Assert(world.AdvanceUseTime(0).Single().Allocation.Id == use.Allocation.Id && world.AdvanceUseTime(5000).Length == 1 && State(world).SequenceEqual(before));
            Assert(world.CompleteDueUse(use.Allocation.Id) == ItemUseStatus.Ready && world.DueItemUses.Length == 0);
            Assert(world.Current.Items.Balance("owner", "item") == new ItemBalance(5, 2, 2, 0, 1)); before = State(world);
            Assert(world.CompleteDueUse(use.Allocation.Id) == ItemUseStatus.Empty && world.CompleteDueUse(Guid.Empty) == ItemUseStatus.NoItem && State(world).SequenceEqual(before));
        });
        check("ordinary and critical failure deadlines preserve their shortened and immediate schedules", () =>
        {
            var world = World(); var normal = Place(world); var failed = Place(world, SkillRollOutcome.Failure); var critical = Place(world, SkillRollOutcome.CriticalFailure);
            Assert(world.DueItemUses.Single().Allocation.Id == critical.Allocation.Id);
            var due = world.AdvanceUseTime(299); Assert(due.Length == 1 && world.ItemUses.Single(u => u.Allocation.Id == failed.Allocation.Id).Allocation.RemainingTicks == 1);
            due = world.AdvanceUseTime(1); Assert(due.Length == 2 && !due.Any(u => u.Allocation.Id == normal.Allocation.Id));
            Assert(world.CarriedCount == 0 && world.Current.Items.Balance("owner", "item")!.Placed == 3);
        });
        check("partially elapsed ticks survive save load and complete only after the remaining duration", () =>
        {
            var world = World(); var use = Place(world); world.AdvanceUseTime(175);
            var restored = WorldSession.Restore(_ => ExplosiveProposalTests.Fixture(), new[] { 3 }, CheckpointFile.Decode(State(world)), "catalog");
            Assert(restored.ItemUses.Single().Allocation.RemainingTicks == 425 && restored.AdvanceUseTime(424).Length == 0);
            Assert(restored.AdvanceUseTime(1).Single().Allocation.Id == use.Allocation.Id && restored.CompleteDueUse(use.Allocation.Id) == ItemUseStatus.Ready);
            var consumed = WorldSession.Restore(_ => ExplosiveProposalTests.Fixture(), new[] { 3 }, CheckpointFile.Decode(State(restored)), "catalog");
            Assert(consumed.DueItemUses.Length == 0 && consumed.ItemUses.Single().Allocation.Phase == ItemUsePhase.Consumed && consumed.CarriedCount == 2);
        });
        check("negative time is rejected atomically and large elapsed intervals clamp without overflow", () =>
        {
            var world = World(); Place(world); byte[] before = State(world); bool rejected = false;
            try { world.AdvanceUseTime(-1); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Assert(rejected && State(world).SequenceEqual(before));
            Assert(world.AdvanceUseTime(int.MaxValue).Length == 1 && world.ItemUses.Single().Allocation.RemainingTicks == 0 && world.CarriedCount == 2);
            var snapshot = world.DueItemUses; snapshot[0] = snapshot[0] with { Allocation = snapshot[0].Allocation with { RemainingTicks = 999 } };
            Assert(world.DueItemUses.Single().Allocation.RemainingTicks == 0);
        });
        check("elapsed ownership revision invalidates earlier proposals but zero time preserves them", () =>
        {
            var world = World(); Place(world);
            var old = ExplosiveUseProposal.Create(world, world.Carried.Single(), 60, SkillRollOutcome.Success, false).Plan!;
            long revision = world.Current.Items.Revision; world.AdvanceUseTime(0); Assert(world.Current.Items.Revision == revision);
            world.AdvanceUseTime(1); byte[] before = State(world);
            Assert(world.CommitExplosiveUse(old, false).Status == ExplosiveCommitStatus.StalePlan && State(world).SequenceEqual(before));
        });
        check("clock updates allocations owned by inactive source areas and leaves the current area unchanged", () =>
        {
            var temple = ExplosiveProposalTests.Fixture(); var exit = WorldTests.Map(3, 126).ObjectLayers[0].Objects[0];
            temple = temple with { ObjectLayers = new[] { new ObjectLayer(0, 0, 2, temple.ObjectLayers[0].Objects.Append(exit).ToArray()) } };
            var world = new WorldSession(id => id == 3 ? temple : WorldTests.Map(126, 3), new[] { 3, 126 });
            world.Current.Items.Restore(new[] { new TakenItem("owner", "item", 1) }); world.UseExit(false); world.Current.Travel.Arrive(new(126, 0, 20101, 0));
            var use = Place(world); var location = world.Location;
            Assert(world.AdvanceUseTime(600).Single().SourceMap == 3 && world.Location == location && world.CurrentMap == 126);
            Assert(world.CompleteDueUse(use.Allocation.Id) == ItemUseStatus.Ready && world.Areas.Single(a => a.Map.Prefix.Header.MapIndex == 3).Items.Balance("owner", "item")!.Consumed == 1);
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Pending-use clock assertion failed."); }
}
