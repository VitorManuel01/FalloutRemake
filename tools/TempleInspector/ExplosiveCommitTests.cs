using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class ExplosiveCommitTests
{
    private static WorldSession World(MapDocument? map = null)
    { var source = map ?? ExplosiveProposalTests.Fixture(); var world = new WorldSession(_ => source, new[] { 3 }); world.Current.Items.Restore(new[] { new TakenItem("owner", "item", 2) }); return world; }
    private static ExplosiveUsePlan Plan(WorldSession world) => ExplosiveUseProposal.Create(world, world.Carried.Single(), 60, SkillRollOutcome.Success, false).Plan!;
    private static byte[] State(WorldSession world) => CheckpointFile.Encode(world.Capture("catalog"));
    public static void Run(Action<string, Action> check)
    {
        check("explosive commit atomically reserves one source unit with a persistent placement and unchanged actor doors and source", () =>
        {
            var world = World(); var position = world.Location; var doors = world.Current.Doors.Capture(); var plan = Plan(world);
            var result = world.CommitExplosiveUse(plan, false);
            Assert(result.Status == ExplosiveCommitStatus.Ready && result.Use!.SourceMap == 3 && result.Use.Allocation.Id == plan.Id
                && result.Use.Allocation.RemainingTicks == 600 && world.CarriedCount == 1 && world.CarriedWeight == 4);
            Assert(world.Location == position && world.Current.Items.Balance("owner", "item") == new ItemBalance(5, 3, 1, 1, 0)
                && world.Current.Doors.Capture().Doors.SequenceEqual(doors.Doors) && world.Experience == 0);
            Assert(plan.Source.Source.Header.Pid == 85 && plan.Source.Source.Header.Tile == -1 && world.Current.Map.ObjectLayers[0].Objects[0].Inventory[0].Quantity == 5);
            var restored = WorldSession.Restore(_ => ExplosiveProposalTests.Fixture(), new[] { 3 }, CheckpointFile.Decode(State(world)), "catalog");
            Assert(restored.ItemUses.Single().Allocation.Id == plan.Id && restored.CarriedCount == 1 && restored.CarriedWeight == 4);
        });
        check("reapplying a committed proposal or a concurrent old proposal cannot reserve a second unit", () =>
        {
            var world = World(); var one = Plan(world); var concurrent = Plan(world);
            Assert(world.CommitExplosiveUse(one, false).Status == ExplosiveCommitStatus.Ready); byte[] before = State(world);
            Assert(world.CommitExplosiveUse(one, false).Status == ExplosiveCommitStatus.StalePlan && world.CommitExplosiveUse(concurrent, false).Status == ExplosiveCommitStatus.StalePlan);
            Assert(State(world).SequenceEqual(before)); var next = Plan(world);
            Assert(world.CommitExplosiveUse(next, false).Status == ExplosiveCommitStatus.Ready && world.CarriedCount == 0 && world.ItemUses.Length == 2);
        });
        check("proposal session token and source revision reject load reset and intervening inventory changes", () =>
        {
            var world = World(); var old = Plan(world); var saved = world.Capture("catalog");
            var restored = WorldSession.Restore(_ => ExplosiveProposalTests.Fixture(), new[] { 3 }, saved, "catalog"); byte[] before = State(restored);
            Assert(restored.SessionToken != world.SessionToken && restored.CommitExplosiveUse(old, false).Status == ExplosiveCommitStatus.StalePlan && State(restored).SequenceEqual(before));
            world.Reset(); Assert(world.CommitExplosiveUse(old, false).Status == ExplosiveCommitStatus.StalePlan);
            world = World(); world.Current.Travel.Arrive(new(3, 0, FalloutGrid.Neighbor(20500, 0)!.Value, 0)); old = Plan(world);
            Assert(world.ReturnItem(3, "owner", "item", 1, false) == ItemTakeStatus.Ready); before = State(world);
            Assert(world.CommitExplosiveUse(old, false).Status == ExplosiveCommitStatus.StalePlan && State(world).SequenceEqual(before));
            old = Plan(world); Assert(world.TakeItem("owner", "item", 1, false) == ItemTakeStatus.Ready); before = State(world);
            Assert(world.CommitExplosiveUse(old, false).Status == ExplosiveCommitStatus.StalePlan && State(world).SequenceEqual(before));
        });
        check("moving or relocated actor refuses a proposal without changing the pending state", () =>
        {
            var world = World(); var plan = Plan(world); byte[] before = State(world);
            Assert(world.CommitExplosiveUse(plan, true).Status == ExplosiveCommitStatus.Busy && State(world).SequenceEqual(before));
            world.Current.Travel.MoveTo(20101); before = State(world);
            Assert(world.CommitExplosiveUse(plan, false).Status == ExplosiveCommitStatus.StalePlan && State(world).SequenceEqual(before));
        });
        check("commit rechecks plan quantity timing identity and canonical explosive values", () =>
        {
            var world = World(); var plan = Plan(world); byte[] before = State(world);
            foreach (var invalid in new[] { plan with { Id = Guid.Empty }, plan with { Quantity = 2 }, plan with { ActivePid = 85 },
                plan with { MinimumDamage = 1 }, plan with { MaximumDamage = 999 }, plan with { SceneryRadius = 99 },
                plan with { Schedule = new(60, 1, ExplosiveExpiry.Normal) }, plan with { Schedule = null! }, plan with { Source = null! }, null! })
            { Assert(world.CommitExplosiveUse(invalid, false).Status == ExplosiveCommitStatus.InvalidPlan && State(world).SequenceEqual(before)); }
            Assert(world.CommitExplosiveUse(plan with { Source = plan.Source with { OwnerKey = "foreign" } }, false).Status == ExplosiveCommitStatus.NoItem);
            Assert(world.CommitExplosiveUse(plan with { Source = plan.Source with { Quantity = 999 } }, false).Status == ExplosiveCommitStatus.StalePlan);
            var map = ExplosiveProposalTests.Fixture(); var p = map.Prototypes[1];
            var modified = World(map with { Prototypes = new[] { map.Prototypes[0], p with { Resource = p.Resource with { Sha256 = "changed" } } } });
            var forged = plan with { SessionToken = modified.SessionToken, SourceRevision = modified.Current.Items.Revision };
            Assert(modified.CommitExplosiveUse(forged, false).Status == ExplosiveCommitStatus.Unsupported && modified.ItemUses.Length == 0);
        });
        check("commit rechecks occupied geometry and exit status rather than trusting the proposal position", () =>
        {
            var map = ExplosiveProposalTests.Fixture(); var door = WorldTests.Map(3, 126).ObjectLayers[0].Objects[1];
            door = door with { Key = "placement-door", Header = door.Header with { Tile = 20101 } };
            map = map with { ObjectLayers = new[] { new ObjectLayer(0, 0, 2, map.ObjectLayers[0].Objects.Append(door).ToArray()) } };
            var world = World(map); var closed = world.Current.Doors.Capture(); world.Current.Doors.Toggle(door.Key, 20100, 0, false); world.Current.Travel.MoveTo(20101);
            var plan = Plan(world); world.Current.Doors.Restore(closed);
            Assert(world.CommitExplosiveUse(plan, false).Status == ExplosiveCommitStatus.Unavailable && world.ItemUses.Length == 0 && world.CarriedCount == 2);
            map = ExplosiveProposalTests.Fixture(); var exit = WorldTests.Map(3, 126).ObjectLayers[0].Objects[0];
            world = World(map with { ObjectLayers = new[] { new ObjectLayer(0, 0, 2, map.ObjectLayers[0].Objects.Append(exit).ToArray()) } });
            plan = plan with { SessionToken = world.SessionToken, SourceRevision = world.Current.Items.Revision, Placement = world.Location };
            Assert(world.CommitExplosiveUse(plan, false).Status == ExplosiveCommitStatus.AtExit && world.ItemUses.Length == 0);
        });
        check("committing a carried source from another visited area records its true origin and current placement", () =>
        {
            var temple = ExplosiveProposalTests.Fixture(); var exit = WorldTests.Map(3, 126).ObjectLayers[0].Objects[0];
            temple = temple with { ObjectLayers = new[] { new ObjectLayer(0, 0, 2, temple.ObjectLayers[0].Objects.Append(exit).ToArray()) } };
            var world = new WorldSession(id => id == 3 ? temple : WorldTests.Map(126, 3), new[] { 3, 126 });
            world.Current.Items.Restore(new[] { new TakenItem("owner", "item", 2) }); world.UseExit(false); world.Current.Travel.Arrive(new(126, 0, 20101, 0));
            var result = world.CommitExplosiveUse(Plan(world), false);
            Assert(result.Status == ExplosiveCommitStatus.Ready && result.Use!.SourceMap == 3 && result.Use.Allocation.Placement.Map == 126 && world.CarriedCount == 1);
            var restored = WorldSession.Restore(id => id == 3 ? temple : WorldTests.Map(126, 3), new[] { 3, 126 }, CheckpointFile.Decode(State(world)), "catalog");
            Assert(restored.CurrentMap == 126 && restored.ItemUses.Single().SourceMap == 3 && restored.ItemUses.Single().Allocation.Placement.Hex == 20101);
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Explosive commit assertion failed."); }
}
