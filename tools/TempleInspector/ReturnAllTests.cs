using System.Buffers.Binary;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class ReturnAllTests
{
    private static byte[] State(WorldSession world) => CheckpointFile.Encode(world.Capture("catalog"));
    private static WorldSession World(MapDocument map)
    { var world = new WorldSession(_ => map, new[] { 3 }); world.Current.Travel.Arrive(new(3, 0, 20500, 0)); return world; }
    public static void Run(Action<string, Action> check)
    {
        check("return all restores only the chosen source container in one revision and preserves other origins through checkpoints", () =>
        {
            var map = TakeAllTests.Map(1); var owner = map.ObjectLayers[0].Objects[0];
            var other = owner with { Key = "other-owner", InventoryCount = 1, Inventory = new[] { owner.Inventory[0] with {
                Quantity = 1, Object = owner.Inventory[0].Object with { Key = "other-item" } } } };
            map = map with { ObjectLayers = new[] { new ObjectLayer(0, 0, 2, new[] { owner, other }) } };
            var world = World(map); Assert(world.TakeAllItems("owner", false) == ItemTakeStatus.Ready && world.TakeAllItems("other-owner", false) == ItemTakeStatus.Ready);
            byte[] before = State(world); long revision = world.Current.Items.Revision;
            Assert(world.CanReturnAllItems(3, "owner", false) == ItemTakeStatus.Ready && before.SequenceEqual(State(world)));
            Assert(world.ReturnAllItems(3, "owner", false) == ItemTakeStatus.Ready && world.Current.Items.Revision == revision + 1
                && world.CarriedCount == 1 && world.Carried.Single().OwnerKey == "other-owner"
                && world.Current.Items.Contents("owner").Sum(i => i.Quantity) == 5);
            byte[] after = State(world);
            Assert(world.ReturnAllItems(3, "owner", false) == ItemTakeStatus.Empty && after.SequenceEqual(State(world)));
            var restored = WorldSession.Restore(_ => map, new[] { 3 }, CheckpointFile.Decode(after), "catalog");
            Assert(after.SequenceEqual(State(restored)) && restored.TakeAllItems("owner", false) == ItemTakeStatus.Ready && restored.CarriedCount == 6);
        });
        check("return all rechecks moving distance area and exits without partial returns", () =>
        {
            var map = TakeAllTests.Map(1); var world = World(map); world.TakeAllItems("owner", false); byte[] before = State(world);
            Assert(world.ReturnAllItems(3, "owner", true) == ItemTakeStatus.Busy && world.ReturnAllItems(126, "owner", false) == ItemTakeStatus.Unavailable
                && before.SequenceEqual(State(world)));
            world.Current.Travel.Arrive(new(3, 0, 100, 0)); before = State(world);
            Assert(world.ReturnAllItems(3, "owner", false) == ItemTakeStatus.TooFar && before.SequenceEqual(State(world)));
            var exit = WorldTests.Map(3, 126).ObjectLayers[0].Objects[0]; exit = exit with { Header = exit.Header with { Tile = 20500 } };
            var session = new ItemTransferSession(map); session.Restore(new[] { new TakenItem("owner", "item", 2), new TakenItem("owner", "second", 3) });
            Assert(session.ReturnAll("owner", new(3, 0, 20500, 0), new HexNavigation(map.ObjectLayers[0].Objects.Append(exit), 0), false) == ItemTakeStatus.AtExit
                && session.Carried.Sum(i => i.Quantity) == 5);
        });
        check("return all never returns placed or consumed units and preserves their ownership after restoration", () =>
        {
            var map = ExplosiveProposalTests.Fixture(); var session = new ItemTransferSession(map);
            session.Restore(new[] { new TakenItem("owner", "item", 4) }, new[] { ItemUseTests.Use(), ItemUseTests.Use(2) with {
                Phase = ItemUsePhase.Consumed, RemainingTicks = 0 } });
            var nav = HexNavigation.FromMap(map, 0); var actor = new ActorLocation(3, 0, 20500, 0);
            Assert(session.ReturnAll("owner", actor, nav, false) == ItemTakeStatus.Ready
                && session.Balance("owner", "item") == new ItemBalance(5, 3, 0, 1, 1));
            var restored = new ItemTransferSession(map); restored.Restore(session.Capture(), session.CaptureUses());
            Assert(restored.Balance("owner", "item") == session.Balance("owner", "item")
                && restored.ReturnAll("owner", actor, nav, false) == ItemTakeStatus.Empty);
        });
        check("return all permits reducing a legacy inventory with unknown weight without inventing a measured weight", () =>
        {
            var map = TakeAllTests.Map(1); byte[] raw = map.Prototypes[1].RawBytes.ToArray(); BinaryPrimitives.WriteInt32BigEndian(raw.AsSpan(44), -1);
            map = map with { Prototypes = new[] { map.Prototypes[0], map.Prototypes[1] with { RawBytes = raw } } };
            var world = World(map); world.Current.Items.Restore(new[] { new TakenItem("owner", "item", 2), new TakenItem("owner", "second", 3) });
            Assert(world.CarriedWeight == null && world.ReturnAllItems(3, "owner", false) == ItemTakeStatus.Ready
                && world.CarriedWeight == 0 && world.CarriedCount == 0 && world.Current.Items.Contents("owner").Sum(i => i.Quantity) == 5);
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Return-all source conservation assertion failed."); }
}
