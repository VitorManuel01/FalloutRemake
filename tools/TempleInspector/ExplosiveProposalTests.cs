using System.Buffers.Binary;
using System.Text.Json;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class ExplosiveProposalTests
{
    private static MapDocument Fixture()
    {
        var map = ItemTransferTests.Map(); var owner = map.ObjectLayers[0].Objects[0]; var item = owner.Inventory[0].Object;
        byte[] raw = new byte[69]; BinaryPrimitives.WriteInt32BigEndian(raw.AsSpan(44), 4);
        var prototype = new Prototype(85, 0, 5, 125, -1, new("proto/items/00000085.pro", "synthetic trusted-identity fixture", 69, ExplosiveUseProposal.SourcePrototypeHash), raw);
        owner = owner with { Inventory = new[] { owner.Inventory[0] with { Object = item with { Subtype = 5, Header = item.Header with { Pid = 85 } } } } };
        return map with { ObjectLayers = new[] { new ObjectLayer(0, 0, 1, new[] { owner }) }, Prototypes = new[] { map.Prototypes[0], prototype } };
    }
    private static WorldSession World(MapDocument? map = null)
    { var source = map ?? Fixture(); var world = new WorldSession(_ => source, new[] { 3 }); world.Current.Items.Restore(new[] { new TakenItem("owner", "item", 2) }); return world; }
    public static void Run(Action<string, Action> check)
    {
        check("explosive use proposal binds actual carried identity and leaves ownership and world unchanged", () =>
        {
            var world = World(); string before = JsonSerializer.Serialize(world.Capture("catalog")); var carried = world.Carried.Single();
            var result = ExplosiveUseProposal.Create(world, carried with { Quantity = 999 }, 60, SkillRollOutcome.Success, false);
            Assert(result.Status == ExplosiveProposalStatus.Ready && result.Plan is { ActivePid: 209, MinimumDamage: 40, MaximumDamage: 80, SceneryRadius: 3 });
            Assert(result.Plan!.Source.Quantity == 2 && result.Plan.Quantity == 1 && result.Plan.Source.Source.Key == "item" && result.Plan.Placement == world.Location && result.Plan.Schedule.DelayTicks == 600);
            Assert(before == JsonSerializer.Serialize(world.Capture("catalog")) && world.CarriedCount == 2 && world.Current.Items.Contents("owner").Single().Quantity == 3);
        });
        check("cancelled, moving, invalid timer and stale source proposals never produce a usable plan", () =>
        {
            var world = World(); var carried = world.Carried.Single(); string before = JsonSerializer.Serialize(world.Capture("catalog"));
            Assert(ExplosiveUseProposal.Create(world, carried, -1, SkillRollOutcome.Success, false).Status == ExplosiveProposalStatus.Canceled);
            Assert(ExplosiveUseProposal.Create(world, carried, 60, SkillRollOutcome.Success, true).Status == ExplosiveProposalStatus.Busy);
            Assert(ExplosiveUseProposal.Create(world, carried, 11, SkillRollOutcome.Success, false).Status == ExplosiveProposalStatus.InvalidTimer);
            Assert(ExplosiveUseProposal.Create(world, carried, 60, (SkillRollOutcome)999, false).Status == ExplosiveProposalStatus.InvalidRoll);
            Assert(ExplosiveUseProposal.Create(world, carried with { OwnerKey = "foreign" }, 60, SkillRollOutcome.Success, false).Status == ExplosiveProposalStatus.NoItem);
            Assert(before == JsonSerializer.Serialize(world.Capture("catalog")));
            world.Current.Items.Reset(); Assert(ExplosiveUseProposal.Create(world, carried, 60, SkillRollOutcome.Success, false).Plan == null);
        });
        check("modified explosive prototype identity is refused and exit placement is unavailable", () =>
        {
            var map = Fixture(); var modified = map.Prototypes[1] with { Resource = map.Prototypes[1].Resource with { Sha256 = "modified" } };
            var world = World(map with { Prototypes = new[] { map.Prototypes[0], modified } });
            Assert(ExplosiveUseProposal.Create(world, world.Carried.Single(), 60, SkillRollOutcome.Success, false).Status == ExplosiveProposalStatus.Unsupported);
            var exit = WorldTests.Map(3, 126).ObjectLayers[0].Objects[0];
            world = World(map with { ObjectLayers = new[] { new ObjectLayer(0, 0, 2, map.ObjectLayers[0].Objects.Append(exit).ToArray()) } });
            Assert(ExplosiveUseProposal.Create(world, world.Carried.Single(), 60, SkillRollOutcome.Success, false).Status == ExplosiveProposalStatus.AtExit);
            var obstacle = WorldTests.Map(3, 126, true).ObjectLayers[0].Objects[1];
            world = World(map with { ObjectLayers = new[] { new ObjectLayer(0, 0, 2, map.ObjectLayers[0].Objects.Append(obstacle).ToArray()) } });
            Assert(ExplosiveUseProposal.Create(world, world.Carried.Single(), 60, SkillRollOutcome.Success, false).Status == ExplosiveProposalStatus.Unavailable);
        });
        check("explosive proposal preserves premature timing and demolition expert override without detonation", () =>
        {
            var world = World(); var item = world.Carried.Single();
            Assert(ExplosiveUseProposal.Create(world, item, 60, SkillRollOutcome.CriticalFailure, false).Plan!.Schedule.DelayTicks == 0);
            Assert(ExplosiveUseProposal.Create(world, item, 60, SkillRollOutcome.Failure, false).Plan!.Schedule.DelayTicks == 300);
            Assert(ExplosiveUseProposal.Create(world, item, 60, SkillRollOutcome.CriticalFailure, false, true).Plan!.Schedule.DelayTicks == 600);
            Assert(world.CarriedCount == 2 && world.Carried.Single().Source.Header.Pid == 85);
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Explosive proposal assertion failed."); }
}
