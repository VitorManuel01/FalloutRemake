using System.Buffers.Binary;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class ItemTransferTests
{
    internal static MapDocument Map()
    {
        MapDocument map = WorldTests.Map(3, 126);
        MapObject template = map.ObjectLayers[0].Objects[0];
        MapObject item = template with { Key = "item", Offset = 300, Subtype = 2, State = new Dictionary<string, int> { ["flags"] = 0 },
            Header = template.Header with { Id = 30, Tile = -1, Pid = 49, Fid = 15, Flags = 8 } };
        MapObject owner = item with { Key = "owner", Offset = 200, Subtype = 1, Header = item.Header with { Id = 20, Pid = 243, Tile = 20500, Flags = 0 },
            InventoryCount = 1, Inventory = new[] { new InventoryEntry(0, 5, item) } };
        Prototype Proto(int pid, int subtype)
        { byte[] raw = new byte[125]; BinaryPrimitives.WriteInt32BigEndian(raw.AsSpan(24), 0); return new(pid, 0, subtype, 0, -1, new("test.pro", "synthetic", raw.Length, "hash"), raw); }
        return map with { ObjectLayers = new[] { new ObjectLayer(0, 0, 1, new[] { owner }) }, Prototypes = new[] { Proto(243, 1), Proto(49, 2) } };
    }
    private static ActorLocation Nearby(MapDocument map) => new(3, 0, FalloutGrid.Neighbor(20500, 0)!.Value, 0);
    public static void Run(Action<string, Action> check)
    {
        check("container transfer conserves quantities and exact source identities without changing MAP records", () =>
        {
            var map = Map(); var session = new ItemTransferSession(map); var nav = HexNavigation.FromMap(map, 0); ActorLocation actor = Nearby(map);
            Assert(session.Take("owner", "item", 2, actor, nav, false) == ItemTakeStatus.Ready);
            Assert(session.Contents("owner").Single().Quantity == 3 && session.Carried.Single().Quantity == 2 && session.Carried[0].Source.Header.Id == 30);
            Assert(session.Take("owner", "item", 3, actor, nav, false) == ItemTakeStatus.Ready);
            Assert(session.Take("owner", "item", 1, actor, nav, false) == ItemTakeStatus.Empty && session.Carried.Single().Quantity == 5);
            Assert(map.ObjectLayers[0].Objects[0].Inventory[0].Quantity == 5 && session.Carried[0].Source.Header.Tile == -1);
            session.Reset(); Assert(session.Carried.Length == 0 && session.Contents("owner").Single().Quantity == 5);
        });
        check("container transfer rejects far, moving, foreign-map, wrong-elevation and invalid quantities without mutation", () =>
        {
            var map = Map(); var s = new ItemTransferSession(map); var nav = HexNavigation.FromMap(map, 0); var actor = Nearby(map);
            Assert(s.Take("owner", "item", 1, actor with { Hex = 100 }, nav, false) == ItemTakeStatus.TooFar);
            Assert(s.Take("owner", "item", 1, actor, nav, true) == ItemTakeStatus.Busy);
            foreach (var a in new[] { actor with { Map = 126 }, actor with { Elevation = 1 } }) Assert(s.Take("owner", "item", 1, a, nav, false) == ItemTakeStatus.Unavailable);
            foreach (int qty in new[] { 0, -1, 6, int.MaxValue }) Assert(s.Take("owner", "item", qty, actor, nav, false) == ItemTakeStatus.InvalidQuantity);
            Assert(s.Carried.Length == 0 && s.Take("owner", "foreign", 1, actor, nav, false) == ItemTakeStatus.NoItem);
        });
        check("container scripts, locks, hidden items and natural weapons remain unavailable", () =>
        {
            var map = Map(); MapObject owner = map.ObjectLayers[0].Objects[0];
            foreach (var altered in new[] { owner with { Header = owner.Header with { Sid = 123 } },
                owner with { State = new Dictionary<string, int> { ["flags"] = DoorSession.Locked } },
                owner with { Header = owner.Header with { Flags = HexNavigation.Hidden } },
                owner with { Inventory = new[] { owner.Inventory[0] with { Object = owner.Inventory[0].Object with { Header = owner.Inventory[0].Object.Header with { Sid = 123 } } } } } })
            {
                var modified = map with { ObjectLayers = new[] { new ObjectLayer(0, 0, 1, new[] { altered }) } };
                var s = new ItemTransferSession(modified); Assert(s.Take("owner", "item", 1, Nearby(map), HexNavigation.FromMap(modified, 0), false) != ItemTakeStatus.Ready && s.Carried.Length == 0);
            }
            var proto = map.Prototypes[1]; byte[] raw = proto.RawBytes.ToArray(); BinaryPrimitives.WriteInt32BigEndian(raw.AsSpan(24), 0x08000000);
            var natural = map with { Prototypes = new[] { map.Prototypes[0], proto with { RawBytes = raw } } };
            Assert(new ItemTransferSession(natural).CanTake("owner", "item", 1, Nearby(map), HexNavigation.FromMap(natural, 0), false) == ItemTakeStatus.Unsupported);
        });
        check("collected-item restore is atomic and rejects duplicates, foreign source keys and invented quantities", () =>
        {
            var map = Map(); var s = new ItemTransferSession(map); s.Restore(new[] { new TakenItem("owner", "item", 2) });
            foreach (TakenItem[] rows in new[] { new[] { new TakenItem("owner", "item", 6) }, new[] { new TakenItem("owner", "foreign", 1) },
                new[] { new TakenItem("owner", "item", 0) }, new[] { new TakenItem("other", "item", 1) },
                new[] { new TakenItem("owner", "item", 1), new TakenItem("owner", "item", 1) } })
            { Reject(() => s.Restore(rows)); Assert(s.Carried.Single().Quantity == 2); }
            var snapshot = s.Capture(); snapshot[0] = snapshot[0] with { Quantity = 4 }; Assert(s.Carried.Single().Quantity == 2);
            var restored = new ItemTransferSession(map); restored.Restore(s.Capture()); Assert(restored.Contents("owner").Single().Quantity + restored.Carried.Single().Quantity == 5);
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Item transfer assertion failed."); }
    private static void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Invalid item transfer restored."); }
}
