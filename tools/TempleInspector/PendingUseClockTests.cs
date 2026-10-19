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
            Assert(world.AdvanceUseTime(599).Due.Length == 0 && world.ItemUses.All(u => u.Allocation.RemainingTicks == 1));
            var due = world.AdvanceUseTime(1).Due;
            Assert(due.Length == 2 && due.Select(u => u.Allocation.Id).ToHashSet().SetEquals(new[] { a.Allocation.Id, b.Allocation.Id }));
            Assert(world.ItemUses.All(u => u.Allocation.Phase == ItemUsePhase.Placed) && world.Current.Items.Balance("owner", "item") == new ItemBalance(5, 2, 1, 2, 0));
            Assert(world.Location == position && world.Experience == 0 && world.Current.Doors.Capture().Doors.SequenceEqual(doors.Doors));
        });
        check("a due use remains pending until explicit completion and cannot be completed early or twice", () =>
        {
            var world = World(); var use = Place(world); byte[] before = State(world);
            Assert(world.CompleteDueUse(use.Allocation.Id) == ItemUseStatus.InvalidState && State(world).SequenceEqual(before));
            world.AdvanceUseTime(600); before = State(world);
            Assert(world.AdvanceUseTime(0).Due.Single().Allocation.Id == use.Allocation.Id && world.AdvanceUseTime(5000).Due.Length == 1 && State(world).SequenceEqual(before));
            Assert(world.CompleteDueUse(use.Allocation.Id) == ItemUseStatus.Ready && world.DueItemUses.Length == 0);
            Assert(world.Current.Items.Balance("owner", "item") == new ItemBalance(5, 2, 2, 0, 1)); before = State(world);
            Assert(world.CompleteDueUse(use.Allocation.Id) == ItemUseStatus.Empty && world.CompleteDueUse(Guid.Empty) == ItemUseStatus.NoItem && State(world).SequenceEqual(before));
        });
        check("ordinary and critical failure deadlines preserve their shortened and immediate schedules", () =>
        {
            var world = World(); var normal = Place(world); var failed = Place(world, SkillRollOutcome.Failure); var critical = Place(world, SkillRollOutcome.CriticalFailure);
            Assert(world.DueItemUses.Single().Allocation.Id == critical.Allocation.Id);
            var blocked = world.AdvanceUseTime(299);
            Assert(blocked.AppliedTicks == 0 && blocked.UnusedTicks == 299 && blocked.Due.Single().Allocation.Id == critical.Allocation.Id);
            Assert(world.CompleteDueUse(critical.Allocation.Id) == ItemUseStatus.Ready);
            Assert(world.AdvanceUseTime(299).Due.Length == 0 && world.ItemUses.Single(u => u.Allocation.Id == failed.Allocation.Id).Allocation.RemainingTicks == 1);
            var step = world.AdvanceUseTime(301); Assert(step.AppliedTicks == 1 && step.UnusedTicks == 300 && step.Due.Single().Allocation.Id == failed.Allocation.Id);
            Assert(world.CompleteDueUse(failed.Allocation.Id) == ItemUseStatus.Ready);
            Assert(world.AdvanceUseTime(step.UnusedTicks).Due.Single().Allocation.Id == normal.Allocation.Id);
            Assert(world.CarriedCount == 0 && world.Current.Items.Balance("owner", "item")!.Placed == 1);
        });
        check("partially elapsed ticks survive save load and complete only after the remaining duration", () =>
        {
            var world = World(); var use = Place(world); world.AdvanceUseTime(175);
            var restored = WorldSession.Restore(_ => ExplosiveProposalTests.Fixture(), new[] { 3 }, CheckpointFile.Decode(State(world)), "catalog");
            Assert(restored.ItemUses.Single().Allocation.RemainingTicks == 425 && restored.AdvanceUseTime(424).Due.Length == 0);
            Assert(restored.AdvanceUseTime(1).Due.Single().Allocation.Id == use.Allocation.Id && restored.CompleteDueUse(use.Allocation.Id) == ItemUseStatus.Ready);
            var consumed = WorldSession.Restore(_ => ExplosiveProposalTests.Fixture(), new[] { 3 }, CheckpointFile.Decode(State(restored)), "catalog");
            Assert(consumed.DueItemUses.Length == 0 && consumed.ItemUses.Single().Allocation.Phase == ItemUsePhase.Consumed && consumed.CarriedCount == 2);
        });
        check("negative time is rejected atomically and large elapsed intervals clamp without overflow", () =>
        {
            var world = World(); Place(world); byte[] before = State(world); bool rejected = false;
            try { world.AdvanceUseTime(-1); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Assert(rejected && State(world).SequenceEqual(before));
            Assert(world.AdvanceUseTime(int.MaxValue).Due.Length == 1 && world.ItemUses.Single().Allocation.RemainingTicks == 0 && world.CarriedCount == 2);
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
            Assert(world.AdvanceUseTime(600).Due.Single().SourceMap == 3 && world.Location == location && world.CurrentMap == 126);
            Assert(world.CompleteDueUse(use.Allocation.Id) == ItemUseStatus.Ready && world.Areas.Single(a => a.Map.Prefix.Header.MapIndex == 3).Items.Balance("owner", "item")!.Consumed == 1);
        });
        check("a long time budget stops at the earliest deadline and preserves the unused budget for the next event", () =>
        {
            var world = World(); var normal = Place(world); var failed = Place(world, SkillRollOutcome.Failure);
            var first = world.AdvanceUseTime(600);
            Assert(first.AppliedTicks == 300 && first.UnusedTicks == 300 && first.Due.Single().Allocation.Id == failed.Allocation.Id);
            Assert(world.ItemUses.Single(u => u.Allocation.Id == normal.Allocation.Id).Allocation.RemainingTicks == 300);
            byte[] before = State(world); var held = world.AdvanceUseTime(first.UnusedTicks);
            Assert(held.AppliedTicks == 0 && held.UnusedTicks == 300 && State(world).SequenceEqual(before));
            world.CompleteDueUse(failed.Allocation.Id); var second = world.AdvanceUseTime(held.UnusedTicks);
            Assert(second.AppliedTicks == 300 && second.UnusedTicks == 0 && second.Due.Single().Allocation.Id == normal.Allocation.Id);
        });
        check("placements created later keep their own relative deadlines and survive a blocked-time checkpoint", () =>
        {
            var world = World(); var a = Place(world); Assert(world.AdvanceUseTime(100).AppliedTicks == 100); var b = Place(world);
            var first = world.AdvanceUseTime(600); Assert(first.AppliedTicks == 500 && first.UnusedTicks == 100 && first.Due.Single().Allocation.Id == a.Allocation.Id);
            var restored = WorldSession.Restore(_ => ExplosiveProposalTests.Fixture(), new[] { 3 }, CheckpointFile.Decode(State(world)), "catalog");
            Assert(restored.AdvanceUseTime(first.UnusedTicks).AppliedTicks == 0 && restored.DueItemUses.Single().Allocation.Id == a.Allocation.Id);
            restored.CompleteDueUse(a.Allocation.Id); var second = restored.AdvanceUseTime(first.UnusedTicks);
            Assert(second.AppliedTicks == 100 && second.UnusedTicks == 0 && second.Due.Single().Allocation.Id == b.Allocation.Id);
        });
        check("simultaneous due events block later deadlines until every due event is acknowledged", () =>
        {
            var world = World(); var a = Place(world, SkillRollOutcome.Failure); var b = Place(world, SkillRollOutcome.Failure); var later = Place(world);
            var first = world.AdvanceUseTime(600); Assert(first.Due.Length == 2 && first.UnusedTicks == 300);
            world.CompleteDueUse(a.Allocation.Id); var held = world.AdvanceUseTime(first.UnusedTicks);
            Assert(held.AppliedTicks == 0 && held.Due.Single().Allocation.Id == b.Allocation.Id);
            world.CompleteDueUse(b.Allocation.Id); Assert(world.AdvanceUseTime(held.UnusedTicks).Due.Single().Allocation.Id == later.Allocation.Id);
        });
        check("a world without pending uses accepts the full time budget without altering ownership", () =>
        {
            var world = World(); byte[] before = State(world); var step = world.AdvanceUseTime(int.MaxValue);
            Assert(step.AppliedTicks == int.MaxValue && step.UnusedTicks == 0 && step.Due.Length == 0 && State(world).SequenceEqual(before));
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Pending-use clock assertion failed."); }
}
