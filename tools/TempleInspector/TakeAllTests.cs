using System.Buffers.Binary;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class TakeAllTests
{
    private static MapDocument Map(int weight)
    {
        MapDocument map = ItemTransferTests.Map(); MapObject owner = map.ObjectLayers[0].Objects[0];
        InventoryEntry entry = owner.Inventory[0];
        owner = owner with { InventoryCount = 2, Inventory = new[] { entry with { Quantity = 2 },
            entry with { Quantity = 3, Object = entry.Object with { Key = "second", Offset = 400, Header = entry.Object.Header with { Id = 31 } } } } };
        byte[] raw = map.Prototypes[1].RawBytes.ToArray(); BinaryPrimitives.WriteInt32BigEndian(raw.AsSpan(44), weight);
        return map with { ObjectLayers = new[] { new ObjectLayer(0, 0, 1, new[] { owner }) },
            Prototypes = new[] { map.Prototypes[0], map.Prototypes[1] with { RawBytes = raw } } };
    }
    private static WorldSession World(MapDocument map)
    { var world = new WorldSession(_ => map, new[] { 3 }); world.Current.Travel.Arrive(new(3, 0, 20500, 0)); return world; }
    private static byte[] State(WorldSession world) => CheckpointFile.Encode(world.Capture("catalog"));
    public static void Run(Action<string, Action> check)
    {
        check("take all combines source stacks atomically at the exact carry boundary and survives checkpoints", () =>
        {
            var map = Map(30); var world = World(map);
            Assert(world.TakeItem("owner", "item", 1, false) == ItemTakeStatus.Ready);
            byte[] before = State(world); long revision = world.Current.Items.Revision;
            Assert(world.CanTakeAllItems("owner", false) == ItemTakeStatus.Ready && before.SequenceEqual(State(world)));
            Assert(world.TakeAllItems("owner", false) == ItemTakeStatus.Ready && world.Current.Items.Revision == revision + 1
                && world.CarriedCount == 5 && world.CarriedWeight == 150 && world.Carried.Length == 2);
            Assert(world.Current.Items.Contents("owner").All(i => i.Quantity == 0) && map.ObjectLayers[0].Objects[0].Inventory.Sum(i => i.Quantity) == 5);
            byte[] full = State(world);
            Assert(world.TakeAllItems("owner", false) == ItemTakeStatus.Empty && full.SequenceEqual(State(world)));
            var restored = WorldSession.Restore(_ => map, new[] { 3 }, CheckpointFile.Decode(full), "catalog");
            Assert(full.SequenceEqual(State(restored)) && restored.ReturnItem(3, "owner", "second", 2, false) == ItemTakeStatus.Ready
                && restored.Current.Items.Contents("owner").Single(i => i.Source.Key == "second").Quantity == 2);
        });
        check("take all rejects aggregate excess although either stack fits and never partially collects", () =>
        {
            var world = World(Map(31)); byte[] before = State(world); long revision = world.Current.Items.Revision;
            Assert(world.CanTakeItem("owner", "item", 2, false) == ItemTakeStatus.Ready
                && world.CanTakeItem("owner", "second", 3, false) == ItemTakeStatus.Ready);
            Assert(world.CanTakeAllItems("owner", false) == ItemTakeStatus.OverCapacity
                && world.TakeAllItems("owner", false) == ItemTakeStatus.OverCapacity && before.SequenceEqual(State(world))
                && world.Current.Items.Revision == revision && world.CarriedCount == 0);
        });
        check("take all validates every remaining source before moving any eligible stack", () =>
        {
            var map = Map(1); var owner = map.ObjectLayers[0].Objects[0];
            foreach (var altered in new[] { owner.Inventory[1].Object with { Header = owner.Inventory[1].Object.Header with { Sid = 123 } },
                owner.Inventory[1].Object with { Inventory = new[] { owner.Inventory[0] } } })
            {
                var changed = map with { ObjectLayers = new[] { new ObjectLayer(0, 0, 1, new[] { owner with {
                    Inventory = new[] { owner.Inventory[0], owner.Inventory[1] with { Object = altered } } } }) } };
                var world = World(changed); byte[] before = State(world);
                Assert(world.TakeAllItems("owner", false) != ItemTakeStatus.Ready && before.SequenceEqual(State(world)));
            }
            var locked = map with { ObjectLayers = new[] { new ObjectLayer(0, 0, 1, new[] { owner with {
                State = new Dictionary<string, int> { ["flags"] = DoorSession.Locked } } }) } };
            Assert(World(locked).TakeAllItems("owner", false) == ItemTakeStatus.Locked);
        });
        check("take all rechecks movement location and unknown weights without changing ownership", () =>
        {
            var map = Map(1); var world = World(map); byte[] before = State(world);
            Assert(world.CanTakeAllItems("owner", false) == ItemTakeStatus.Ready
                && world.TakeAllItems("owner", true) == ItemTakeStatus.Busy && before.SequenceEqual(State(world)));
            world.Current.Travel.Arrive(new(3, 0, 100, 0)); before = State(world);
            Assert(world.TakeAllItems("owner", false) == ItemTakeStatus.TooFar && before.SequenceEqual(State(world)));
            var missing = map with { Prototypes = new[] { map.Prototypes[0], map.Prototypes[1] with { RawBytes = new byte[125] } } };
            BinaryPrimitives.WriteInt32BigEndian(missing.Prototypes[1].RawBytes.AsSpan(44), -1);
            world = World(missing); before = State(world);
            Assert(world.TakeAllItems("owner", false) == ItemTakeStatus.WeightUnavailable && before.SequenceEqual(State(world)));
        });
        check("take all includes inactive-area inventory in its capacity decision after restoring the world", () =>
        {
            MapDocument Area(int id)
            {
                var source = Map(40); var owner = source.ObjectLayers[0].Objects[0];
                var exit = WorldTests.Map(id, id == 3 ? 126 : 3).ObjectLayers[0].Objects[0];
                return source with { Source = source.Source with { Sha256 = $"hash{id}" },
                    Prefix = source.Prefix with { Header = source.Prefix.Header with { MapIndex = id } },
                    ObjectLayers = new[] { new ObjectLayer(0, 0, 2, new[] { owner, exit }) } };
            }
            var world = new WorldSession(Area, new[] { 3, 126 }); world.Current.Travel.Arrive(new(3, 0, 20500, 0));
            Assert(world.TakeItem("owner", "item", 2, false) == ItemTakeStatus.Ready);
            world.Current.Travel.Arrive(new(3, 0, 20100, 0)); Assert(world.UseExit(false).Status == ExitStatus.Ready);
            world.Current.Travel.Arrive(new(126, 0, 20500, 0));
            world = WorldSession.Restore(Area, new[] { 3, 126 }, CheckpointFile.Decode(State(world)), "catalog");
            byte[] before = State(world);
            Assert(world.CarriedWeight == 80 && world.TakeAllItems("owner", false) == ItemTakeStatus.OverCapacity
                && before.SequenceEqual(State(world)) && world.CarriedCount == 2);
        });
    }
    private static void Assert(bool condition) { if (!condition) throw new Exception("Take-all conservation assertion failed."); }
}
