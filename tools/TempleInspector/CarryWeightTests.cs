using System.Buffers.Binary;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class CarryWeightTests
{
    private static MapDocument Map(int weight)
    {
        var map = ItemTransferTests.Map(); var proto = map.Prototypes[1]; byte[] raw = proto.RawBytes.ToArray(); BinaryPrimitives.WriteInt32BigEndian(raw.AsSpan(44), weight);
        return map with { Prototypes = new[] { map.Prototypes[0], proto with { RawBytes = raw } } };
    }
    private static WorldSession World(MapDocument map)
    { var w = new WorldSession(_ => map, new[] { 3 }); w.Current.Travel.Arrive(new(3, 0, 20500, 0)); return w; }
    public static void Run(Action<string, Action> check)
    {
        check("neutral carry profile uses strength five and the verified 25*strength+25 capacity", () =>
        {
            Assert(CarryWeight.TestCapacity == 150 && CarryWeight.CapacityForStrength(1) == 50 && CarryWeight.CapacityForStrength(10) == 275);
            foreach (int strength in new[] { 0, 11 })
            { bool rejected = false; try { CarryWeight.CapacityForStrength(strength); } catch (ArgumentOutOfRangeException) { rejected = true; } Assert(rejected); }
        });
        check("carry limit accepts the exact boundary, refuses excess atomically and return frees capacity", () =>
        {
            var world = World(Map(50));
            Assert(world.TakeItem("owner", "item", 3, false) == ItemTakeStatus.Ready && world.CarriedWeight == 150);
            Assert(world.CanTakeItem("owner", "item", 1, false) == ItemTakeStatus.OverCapacity);
            Assert(world.TakeItem("owner", "item", 1, false) == ItemTakeStatus.OverCapacity && world.CarriedCount == 3);
            world.ReturnItem(3, "owner", "item", 1, false);
            Assert(world.CarriedWeight == 100 && world.TakeItem("owner", "item", 1, false) == ItemTakeStatus.Ready && world.CarriedWeight == 150);
        });
        check("old over-capacity checkpoint contents remain recoverable but cannot take additional items", () =>
        {
            var map = Map(50); var world = World(map);
            world.Current.Items.Restore(new[] { new TakenItem("owner", "item", 4) });
            var fresh = WorldSession.Restore(_ => map, new[] { 3 }, CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog"))), "catalog");
            Assert(fresh.CarriedWeight == 200 && fresh.TakeItem("owner", "item", 1, false) == ItemTakeStatus.OverCapacity);
            Assert(fresh.ReturnItem(3, "owner", "item", 2, false) == ItemTakeStatus.Ready && fresh.CarriedWeight == 100);
        });
        check("weight arithmetic handles zero, large quantities and overflow without wrapping or estimating unknown rules", () =>
        {
            Assert(CarryWeight.Total(new[] { ((long?)0, int.MaxValue), ((long?)2, 3) }) == 6);
            Assert(CarryWeight.Total(new[] { ((long?)int.MaxValue, int.MaxValue) }) == (long)int.MaxValue * int.MaxValue);
            Assert(CarryWeight.Total(new[] { ((long?)long.MaxValue, 2) }) == null);
            Assert(CarryWeight.Total(new[] { ((long?)null, 1) }) == null);
            var map = Map(10); var item = map.ObjectLayers[0].Objects[0].Inventory[0].Object;
            var weapon = item with { Subtype = 3, State = new Dictionary<string, int> { ["ammoQuantity"] = 12 } };
            Assert(CarryWeight.UnitWeight(map.Prototypes[1] with { Subtype = 3 }, weapon) == null);
        });
        check("weight preserves CE hidden-item and power-armor adjustments without modifying source fields", () =>
        {
            var map = Map(101); var item = map.ObjectLayers[0].Objects[0].Inventory[0].Object;
            foreach (int pid in new[] { 3, 232, 348, 349 })
            {
                var armor = item with { Subtype = 0, Header = item.Header with { Pid = pid } };
                Assert(CarryWeight.UnitWeight(map.Prototypes[1] with { Pid = pid, Subtype = 0 }, armor) == 50);
            }
            byte[] hidden = map.Prototypes[1].RawBytes.ToArray(); BinaryPrimitives.WriteInt32BigEndian(hidden.AsSpan(24), 0x08000000);
            Assert(CarryWeight.UnitWeight(map.Prototypes[1] with { RawBytes = hidden }, item) == 0);
        });
        check("unknown legacy carried weight blocks new pickup while still allowing return to its exact source", () =>
        {
            var map = Map(10); var owner = map.ObjectLayers[0].Objects[0]; var item = owner.Inventory[0].Object;
            item = item with { Subtype = 3, State = new Dictionary<string, int> { ["ammoQuantity"] = 12 } };
            owner = owner with { Inventory = new[] { owner.Inventory[0] with { Object = item } } };
            map = map with { ObjectLayers = new[] { new ObjectLayer(0, 0, 1, new[] { owner }) }, Prototypes = new[] { map.Prototypes[0], map.Prototypes[1] with { Subtype = 3 } } };
            var world = World(map); Assert(world.TakeItem("owner", "item", 1, false) == ItemTakeStatus.WeightUnavailable);
            world.Current.Items.Restore(new[] { new TakenItem("owner", "item", 1) });
            Assert(world.CarriedWeight == null && world.ReturnItem(3, "owner", "item", 1, false) == ItemTakeStatus.Ready && world.CarriedWeight == 0);
        });
        check("capacity accounts for carried items from inactive areas after travel and checkpoint restoration", () =>
        {
            MapDocument Area(int id)
            {
                var source = Map(40); var owner = source.ObjectLayers[0].Objects[0];
                owner = owner with { Key = $"{id}:owner", Inventory = new[] { owner.Inventory[0] with { Object = owner.Inventory[0].Object with { Key = $"{id}:item" } } } };
                var exit = WorldTests.Map(id, id == 3 ? 126 : 3).ObjectLayers[0].Objects[0];
                return source with { Source = source.Source with { Sha256 = $"hash{id}" }, Prefix = source.Prefix with { Header = source.Prefix.Header with { MapIndex = id } },
                    ObjectLayers = new[] { new ObjectLayer(0, 0, 2, new[] { owner, exit }) } };
            }
            var world = new WorldSession(Area, new[] { 3, 126 }); world.Current.Travel.Arrive(new(3, 0, 20500, 0));
            world.TakeItem("3:owner", "3:item", 2, false); world.Current.Travel.Arrive(new(3, 0, 20100, 0)); world.UseExit(false);
            world.Current.Travel.Arrive(new(126, 0, 20500, 0));
            var restored = WorldSession.Restore(Area, new[] { 3, 126 }, CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog"))), "catalog");
            Assert(restored.CarriedWeight == 80 && restored.TakeItem("126:owner", "126:item", 1, false) == ItemTakeStatus.Ready);
            Assert(restored.CarriedWeight == 120 && restored.TakeItem("126:owner", "126:item", 1, false) == ItemTakeStatus.OverCapacity && restored.CarriedCount == 3);
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Carry weight assertion failed."); }
}
