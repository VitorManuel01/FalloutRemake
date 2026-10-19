using FalloutRemake.Geometry;
using FalloutRemake.Runtime;

internal static class ItemReturnTests
{
    public static void Run(Action<string, Action> check)
    {
        check("partial and full returns conserve exact container and carried quantities through repeated round trips", () =>
        {
            var map = ItemTransferTests.Map(); var s = new ItemTransferSession(map); var nav = HexNavigation.FromMap(map, 0); var actor = new ActorLocation(3, 0, 20500, 0);
            Assert(s.Take("owner", "item", 5, actor, nav, false) == ItemTakeStatus.Ready);
            Assert(s.Return("owner", "item", 2, actor, nav, false) == ItemTakeStatus.Ready && s.Carried.Single().Quantity == 3 && s.Contents("owner").Single().Quantity == 2);
            Assert(s.Return("owner", "item", 3, actor, nav, false) == ItemTakeStatus.Ready && s.Carried.Length == 0 && s.Contents("owner").Single().Quantity == 5);
            Assert(s.Return("owner", "item", 1, actor, nav, false) == ItemTakeStatus.Empty);
            for (int i = 0; i < 10; i++)
            { Assert(s.Take("owner", "item", 5, actor, nav, false) == ItemTakeStatus.Ready); Assert(s.Return("owner", "item", 5, actor, nav, false) == ItemTakeStatus.Ready); }
            Assert(s.Capture().Length == 0 && map.ObjectLayers[0].Objects[0].Inventory[0].Quantity == 5);
        });
        check("returns refuse far, moving, wrong-map, wrong-owner and excessive quantities without changing either side", () =>
        {
            var map = ItemTransferTests.Map(); var s = new ItemTransferSession(map); var nav = HexNavigation.FromMap(map, 0); var actor = new ActorLocation(3, 0, 20500, 0);
            s.Take("owner", "item", 2, actor, nav, false);
            Assert(s.Return("owner", "item", 1, actor with { Hex = 100 }, nav, false) == ItemTakeStatus.TooFar);
            Assert(s.Return("owner", "item", 1, actor, nav, true) == ItemTakeStatus.Busy);
            Assert(s.Return("owner", "item", 1, actor with { Map = 126 }, nav, false) == ItemTakeStatus.Unavailable);
            Assert(s.Return("foreign", "item", 1, actor, nav, false) == ItemTakeStatus.NoItem);
            foreach (int qty in new[] { -1, 0, 3, int.MaxValue }) Assert(s.Return("owner", "item", qty, actor, nav, false) == ItemTakeStatus.InvalidQuantity);
            Assert(s.Carried.Single().Quantity == 2 && s.Contents("owner").Single().Quantity == 3);
        });
        check("pickup and return both reject interaction while standing on an exit", () =>
        {
            var map = ItemTransferTests.Map(); var s = new ItemTransferSession(map); var actor = new ActorLocation(3, 0, 20500, 0);
            s.Take("owner", "item", 2, actor, HexNavigation.FromMap(map, 0), false);
            var exit = WorldTests.Map(3, 126).ObjectLayers[0].Objects[0]; exit = exit with { Header = exit.Header with { Tile = 20500 } };
            var nav = new HexNavigation(map.ObjectLayers[0].Objects.Append(exit), 0);
            Assert(s.Return("owner", "item", 1, actor, nav, false) == ItemTakeStatus.AtExit && s.Take("owner", "item", 1, actor, nav, false) == ItemTakeStatus.AtExit);
        });
        check("world checkpoint persists the net carried amount after return and never transfers to an inactive area", () =>
        {
            var map = ItemTransferTests.Map(); var world = new WorldSession(_ => map, new[] { 3 }); world.Current.Travel.Arrive(new(3, 0, 20500, 0));
            world.TakeItem("owner", "item", 4, false);
            Assert(world.ReturnItem(126, "owner", "item", 1, false) == ItemTakeStatus.Unavailable);
            Assert(world.ReturnItem(3, "owner", "item", 3, false) == ItemTakeStatus.Ready);
            var fresh = WorldSession.Restore(_ => map, new[] { 3 }, CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog"))), "catalog");
            Assert(fresh.CarriedCount == 1 && fresh.Current.Items.Contents("owner").Single().Quantity == 4 && fresh.Carried.Single().Source.Header.Id == 30);
            fresh.ReturnItem(3, "owner", "item", 1, false);
            var empty = WorldSession.Restore(_ => map, new[] { 3 }, fresh.Capture("catalog"), "catalog");
            Assert(empty.CarriedCount == 0 && empty.Current.Items.Contents("owner").Single().Quantity == 5);
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Item return assertion failed."); }
}
