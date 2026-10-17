using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using FalloutRemake.Import;

internal static class StructuralTests
{
    public static void Run(Action<string, Action> check)
    {
        check("script blocks: 17 spatial records, timed record, padding and raw fields", () =>
        {
            var f = new Fixture();
            f.Scripts[1] = 17;
            f.Scripts[2] = 1;
            MapDocument map = f.Parse();
            Assert(map.ActiveScripts.Count() == 18 && map.ScriptGroups[1].Blocks.Length == 2);
            MapScript spatial = map.ActiveScripts.First();
            Assert(spatial.Sid == 0x01000000 && spatial.TypeWords.SequenceEqual(new[] { 20100, 5 }));
            Assert(spatial.Flags == 1 && spatial.LocalVariablesCount == 2 && spatial.Fields[2] == 0x12345678);
            Assert(map.ScriptGroups[1].Blocks[1].Slots[1].TypeWords.Length == 1); // padding SID is timed.
            Assert(map.ActiveScripts.Last().TypeWords[0] == 1234);
            MapLayoutWriter.Verify(map);
        });
        check("objects: duplicate IDs, nested inventory, quantities, offsets and byte preservation", () =>
        {
            var f = new Fixture();
            Node ammo = f.Node(0, 4, 37);
            Node weapon = f.Node(0, 3, 37, new[] { (11, ammo) });
            f.Roots.Add(f.Node(0, 1, 37, new[] { (2, weapon) }));
            f.Roots.Add(f.Node(3, 0, 37));
            MapDocument map = f.Parse();
            Assert(map.DeclaredRootObjects == 2 && map.AllObjects.Count() == 4);
            Assert(map.AllObjects.Select(o => o.Key).Distinct().Count() == 4);
            MapObject first = map.ObjectLayers[0].Objects[0];
            Assert(first.Inventory[0].Quantity == 2 && first.Inventory[0].Object.Inventory[0].Quantity == 11);
            Assert(first.Header.X == -7 && first.Header.ScreenY == 13 && first.Header.RawOutline == 999);
            Assert(first.InventoryCapacity == -123 && first.RawInventoryPointer == 0x12345678);
            Assert(first.Inventory[0].Object.State["ammoQuantity"] == -9); // no runtime correction.
            Assert(first.Inventory[0].Object.Inventory[0].Object.State["ammoQuantity"] == 44);
            MapLayoutWriter.Verify(map);
            using var json = JsonDocument.Parse(MapExport.ToJson(map));
            Assert(json.RootElement.GetProperty("SchemaVersion").GetInt32() == 2);
            Assert(json.RootElement.GetProperty("OriginalMap").GetBytesFromBase64().SequenceEqual(f.Bytes()));
        });
        check("all item/scenery shapes, critter state, exit destination and all elevations", () =>
        {
            var f = new Fixture();
            for (int subtype = 0; subtype < 7; subtype++) f.Roots.Add(f.Node(0, subtype, subtype));
            for (int subtype = 0; subtype < 6; subtype++) f.Roots.Add(f.Node(2, subtype, subtype + 10));
            f.Roots.Add(f.Node(1, 0, 30));
            f.Roots.Add(f.Node(4, 0, 31) with { Elevation = 1 });
            f.Roots.Add(f.Node(5, 0, 32, number: 16) with { Elevation = 2 });
            MapDocument map = f.Parse();
            Assert(map.AllObjects.Single(o => o.Header.Id == 10).State["openFlags"] == 42);
            Assert(map.AllObjects.Single(o => o.Header.Id == 11).State["destinationBuiltTile"] == 20100);
            Assert(map.AllObjects.Single(o => o.Header.Id == 30).State["hp"] == 80);
            Assert(map.AllObjects.Single(o => o.Header.Id == 32).State["destinationMap"] == 126);
            Assert(map.ObjectLayers[1].DeclaredCount == 1 && map.ObjectLayers[2].DeclaredCount == 1);
            MapLayoutWriter.Verify(map);
        });
        check("prototype list indices stay one-based, blank lines retained, PID mismatch rejected", () =>
        {
            var f = new Fixture();
            f.Roots.Add(f.Node(3, 0, 1, number: 2)); // blank first line; second line must remain index 2.
            Assert(f.Parse().Prototypes[0].Pid == 0x03000002);
            var catalog = new ResourceCatalog(p => new GameResource(p, "synthetic", Encoding.ASCII.GetBytes(";fake.pro\nreal.pro\n")));
            Reject(() => catalog.ListEntry("test.lst", 0, ".pro"));
            Assert(catalog.ListEntry("test.lst", 1, ".pro") == "real.pro");
            f.Files["proto/walls/00000002.pro"] = Pro(0x03000003, 0);
            Reject(() => f.Parse());
        });
        check("prototype optional critter word, unknown subtype and truncated PRO", () =>
        {
            byte[] critter = Pro(0x01000001, 0);
            Assert(PrototypeCatalog.Read(new GameResource("a.pro", "synthetic", critter[..412]), 0x01000001).RawBytes.Length == 412);
            Reject(() => PrototypeCatalog.Read(new GameResource("a.pro", "synthetic", critter[..411]), 0x01000001));
            byte[] bad = Pro(1, 0);
            Put(bad, 32, 99);
            Reject(() => PrototypeCatalog.Read(new GameResource("b.pro", "synthetic", bad), 1));
        });
        check("script binding uses SID; index mismatch and missing SID are explicit diagnostics", () =>
        {
            var f = new Fixture();
            f.Scripts[3] = 1;
            f.Roots.Add(f.Node(0, 0, 77) with { Sid = 0x03000000, ScriptIndex = 8 });
            f.Roots.Add(f.Node(0, 0, 78) with { Sid = 0x03000001 });
            MapDocument map = f.Parse();
            Assert(map.ScriptBindings[0].Program.Path == "scripts/test.int");
            Assert(map.Diagnostics.Any(d => d.Code == "script-index") && map.Diagnostics.Any(d => d.Code == "missing-sid"));
            Assert(map.AllObjects.First().Header.ScriptIndex == 8);
            MapLayoutWriter.Verify(map);
            byte[] bytes = f.Bytes();
            Put(bytes, 36, 1); // Map header script index is one-based, unlike active script indices.
            Assert(f.Parse(bytes).MapProgram!.Path == "scripts/test.int");
            Put(bytes, 36, 2);
            Reject(() => f.Parse(bytes));
        });
        check("reject inconsistent script counts, active SID type and duplicate SID", () =>
        {
            var f = new Fixture();
            f.Scripts[3] = 2;
            MapDocument good = f.Parse();
            ScriptBlock block = good.ScriptGroups[3].Blocks[0];
            byte[] bytes = f.Bytes();
            Put(bytes, block.Offset + block.Length - 8, 1);
            Reject(() => f.Parse(bytes));
            bytes = f.Bytes();
            Put(bytes, block.Slots[1].Offset, block.Slots[0].Sid);
            Reject(() => f.Parse(bytes));
            bytes = f.Bytes();
            Put(bytes, block.Slots[0].Offset, 0x04000000);
            Reject(() => f.Parse(bytes));
        });
        check("reject root/inventory count corruption, zero quantity, truncation and trailing bytes", () =>
        {
            var f = new Fixture();
            f.Roots.Add(f.Node(0, 1, 1, new[] { (3, f.Node(0, 6, 2)) }));
            MapDocument map = f.Parse();
            byte[] bytes = f.Bytes();
            Put(bytes, map.ObjectsOffset, 2);
            Reject(() => f.Parse(bytes));
            bytes = f.Bytes();
            Put(bytes, map.ObjectLayers[0].Objects[0].Offset + 72, int.MaxValue);
            Reject(() => f.Parse(bytes));
            bytes = f.Bytes();
            Put(bytes, map.ObjectLayers[0].Objects[0].Inventory[0].QuantityOffset, 0);
            Reject(() => f.Parse(bytes));
            Reject(() => f.Parse(f.Bytes()[..^1]));
            Reject(() => f.Parse(f.Bytes().Concat(new byte[4]).ToArray()));
        });
        check("inventory depth and missing dependencies fail explicitly", () =>
        {
            var f = new Fixture();
            Node node = f.Node(0, 1, 0);
            for (int i = 0; i < 33; i++) node = f.Node(0, 1, i + 1, new[] { (1, node) });
            f.Roots.Add(node);
            Reject(() => f.Parse());
            var missing = new Fixture();
            missing.Roots.Add(missing.Node(3, 0, 1));
            missing.Files.Remove("proto/walls/00000001.pro");
            Reject(() => missing.Parse());
        });
        check("reconstruction detects a changed parsed value rather than reusing source bytes", () =>
        {
            var f = new Fixture();
            f.Scripts[3] = 1;
            MapDocument map = f.Parse();
            map.ActiveScripts.First().Fields[13]++;
            Reject(() => MapLayoutWriter.Verify(map));
        });
    }

    private sealed record Node(int Pid, int Subtype, int Id, (int Quantity, Node Child)[] Inventory)
    {
        public int Elevation { get; init; }
        public int Sid { get; init; } = -1;
        public int ScriptIndex { get; init; } = -1;
    }

    private sealed class Fixture
    {
        public Dictionary<string, byte[]> Files { get; } = new()
        {
            ["scripts/scripts.lst"] = Encoding.ASCII.GetBytes("test.int ; synthetic\n"),
            ["scripts/test.int"] = new byte[] { 1, 2, 3, 4 }
        };
        public int[] Scripts { get; } = new int[5];
        public List<Node> Roots { get; } = new();
        private readonly Dictionary<int, List<string>> _lists = new();
        private static readonly string[] Types = { "items", "critters", "scenery", "walls", "tiles", "misc" };
        public Node Node(int type, int subtype, int id, (int, Node)[]? children = null, int? number = null)
        {
            if (!_lists.TryGetValue(type, out List<string>? list)) _lists[type] = list = new();
            int index = number ?? list.Count + 1;
            while (list.Count < index) list.Add("");
            string name = $"{index:D8}.pro";
            list[index - 1] = name;
            string folder = "proto/" + Types[type] + "/";
            Files[folder + Types[type] + ".lst"] = Encoding.ASCII.GetBytes(string.Join('\n', list) + "\n");
            int pid = type << 24 | index;
            Files[folder + name] = Pro(pid, subtype);
            return new Node(pid, subtype, id, children ?? Array.Empty<(int, Node)>());
        }
        public MapDocument Parse(byte[]? bytes = null) => MapDocument.Read(new GameResource("maps/synthetic.map", "synthetic", bytes ?? Bytes()),
            path => Files.TryGetValue(path, out byte[]? data) ? new GameResource(path, "synthetic", data) : throw new FileNotFoundException(path));
        public byte[] Bytes()
        {
            using var stream = new MemoryStream();
            void W(params int[] values)
            {
                foreach (int value in values) { byte[] word = new byte[4]; Put(word, 0, value); stream.Write(word); }
            }
            W(20);
            stream.Write(Encoding.ASCII.GetBytes("synthetic.map\0\0\0"));
            W(20100, 0, 0, 0, 0, 14, 0, 0, 3, 0);
            W(new int[44]);
            for (int type = 0; type < 5; type++)
            {
                W(Scripts[type]);
                for (int first = 0; first < Scripts[type]; first += 16)
                {
                    int used = Math.Min(16, Scripts[type] - first);
                    for (int slot = 0; slot < 16; slot++)
                    {
                        bool padding = slot >= used;
                        int sid = padding ? 0x0200FFFF : type << 24 | (first + slot);
                        W(sid, -99);
                        if ((sid >> 24) == 1) W(20100, 5);
                        if ((sid >> 24) == 2) W(1234);
                        W(1, 0, 0x12345678, 77, 1, 2, -5, 6, 7, 8, 9, 10, 11, 12);
                    }
                    W(used, 0x12345678);
                }
            }
            void O(Node node)
            {
                W(node.Id, 20100, -7, 8, 12, 13, 0, 5, 1, 0x55, node.Elevation, node.Pid, -1, 4, 65536, 999, node.Sid, node.ScriptIndex);
                W(node.Inventory.Length, -123, 0x12345678);
                int type = node.Pid >> 24;
                if (type == 1) W(-1, 1, 2, 3, 4, 5, 6, 7, 80, 0, 0);
                else
                {
                    W(unchecked((int)0xCCCCCCCC));
                    if (type == 0 && node.Subtype == 3) W(-9, 4);
                    else if (type == 0 && node.Subtype >= 4) W(44);
                    else if (type == 2 && node.Subtype == 0) W(42);
                    else if (type == 2 && node.Subtype == 1) W(20100, 3);
                    else if (type == 2 && node.Subtype == 2) W(7, 2);
                    else if (type == 2 && node.Subtype is 3 or 4) W(3, 20100);
                    else if (node.Pid is >= 0x05000010 and <= 0x05000017) W(126, 18492, 0, 2);
                }
                foreach ((int quantity, Node child) in node.Inventory) { W(quantity); O(child); }
            }
            W(Roots.Count);
            for (int elevation = 0; elevation < 3; elevation++)
            {
                Node[] nodes = Roots.Where(n => n.Elevation == elevation).ToArray();
                W(nodes.Length);
                foreach (Node node in nodes) O(node);
            }
            return stream.ToArray();
        }
    }
    private static byte[] Pro(int pid, int subtype)
    {
        int type = pid >> 24;
        int length = type switch { 0 => new[] { 129, 65, 125, 122, 81, 69, 61 }[subtype], 1 => 416,
            2 => subtype <= 2 ? 49 : 45, 3 => 36, _ => 28 };
        byte[] bytes = new byte[length];
        Put(bytes, 0, pid);
        Put(bytes, 8, 1);
        if (type is 0 or 2) Put(bytes, 32, subtype);
        return bytes;
    }
    private static void Put(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(offset), value);
    private static void Assert(bool value) { if (!value) throw new InvalidOperationException("Structural fixture assertion failed."); }
    private static void Reject(Action test)
    {
        try { test(); }
        catch (Exception e) when (e is IOException or InvalidDataException) { return; }
        throw new InvalidOperationException("Malformed structural fixture was accepted.");
    }
}
