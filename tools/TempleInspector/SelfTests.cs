using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using FalloutRemake.Import;

internal static class SelfTests
{
    public static int Run()
    {
        // Entirely synthetic fixtures; no game content is needed by these checks.
        string testParent = Path.GetFullPath("work");
        string root = Path.Combine(testParent, "synthetic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        int passed = 0;
        void Check(string name, Action test)
        {
            test();
            passed++;
            Console.WriteLine("PASS " + name);
        }
        try
        {
            byte[] payload = Encoding.ASCII.GetBytes("synthetic resource");
            Check("DAT plain and zlib entries, case/slashes, prefixed archive", () =>
            {
                string path = Path.Combine(root, "sample.dat");
                WriteDat(path, [("Maps/Test.map", payload, true), ("plain.txt", payload, false)], 7);
                var dat = new DatArchive(path);
                Equal(payload, dat.Read("MAPS\\TEST.MAP"));
                Equal(payload, dat.Read("plain.txt"));
            });
            Check("DAT truncated footer/index and invalid bounds rejected", () =>
            {
                string path = Path.Combine(root, "bad.dat");
                File.WriteAllBytes(path, new byte[7]);
                Reject(() => new DatArchive(path));
                WriteDat(path, [("a", payload, false)]);
                byte[] bytes = File.ReadAllBytes(path);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(bytes.Length - 8), int.MaxValue);
                File.WriteAllBytes(path, bytes);
                Reject(() => new DatArchive(path));
                WriteDat(path, [("a", payload, false)]);
                bytes = File.ReadAllBytes(path);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(bytes.Length - 12), int.MaxValue);
                File.WriteAllBytes(path, bytes);
                Reject(() => new DatArchive(path));
            });
            Check("DAT path traversal and duplicate names rejected", () =>
            {
                Reject(() => DatArchive.NormalizePath("../master.dat"));
                Reject(() => DatArchive.NormalizePath("C:\\master.dat"));
                string path = Path.Combine(root, "duplicates.dat");
                WriteDat(path, [("A", payload, false), ("a", payload, false)]);
                Reject(() => new DatArchive(path));
            });
            Check("DAT decompressed size mismatch and corrupt zlib rejected", () =>
            {
                string path = Path.Combine(root, "compressed.dat");
                WriteDat(path, [("a", payload, true)]);
                byte[] bytes = File.ReadAllBytes(path);
                int treeSize = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(bytes.Length - 8));
                int sizeOffset = bytes.Length - 8 - treeSize + 4 + 4 + 1 + 1;
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(sizeOffset), payload.Length - 1);
                File.WriteAllBytes(path, bytes);
                Reject(() => new DatArchive(path).Read("a"));
                WriteDat(path, [("a", payload, true)]);
                bytes = File.ReadAllBytes(path);
                bytes[0] = 0;
                File.WriteAllBytes(path, bytes);
                Reject(() => new DatArchive(path).Read("a"));
            });
            Check("source priority: high-res, numbered patches, loose, critter, master", () =>
            {
                string install = Path.Combine(root, "install");
                Directory.CreateDirectory(Path.Combine(install, "data", "MAPS"));
                WriteDat(Path.Combine(install, "master.dat"), [("maps/test.map", [1], false), ("master.txt", [1], false)]);
                WriteDat(Path.Combine(install, "critter.dat"), [("maps/test.map", [2], false), ("critter.txt", [2], false)]);
                File.WriteAllBytes(Path.Combine(install, "data", "MAPS", "TEST.MAP"), [3]);
                Equal([3], new GameData(install).Read("maps/test.map").Bytes);
                WriteDat(Path.Combine(install, "patch000.dat"), [("maps/test.map", [4], false)]);
                Equal([4], new GameData(install).Read("maps/test.map").Bytes);
                WriteDat(Path.Combine(install, "PATCH001.DAT"), [("maps/test.map", [5], false)]);
                Equal([5], new GameData(install).Read("maps/test.map").Bytes);
                WriteDat(Path.Combine(install, "f2_res.dat"), [("maps/test.map", [6], false)]);
                var data = new GameData(install);
                Equal([6], data.Read("maps/test.map").Bytes);
                Equal([2], data.Read("critter.txt").Bytes);
                Equal([1], data.Read("master.txt").Bytes);
                Reject(() => data.Read("missing.txt"));
            });
            Check("MAP big-endian header, variables, two elevations and raw tile flags", () =>
            {
                byte[] bytes = MapFixture(4); // elevation 1 omitted, 0 and 2 present.
                MapPrefix map = MapPrefix.Read(bytes);
                Assert(map.Header.MapIndex == 42 && map.Header.EnteringTile == 20100 && map.Header.ScriptIndex == 123);
                Assert(map.Header.Reserved[43] == -77 && map.GlobalVariables[0] == 0x12345678 && map.LocalVariables[0] == -2);
                Assert(map.Layers.Count == 2 && map.Layers[1].Elevation == 2);
                SquareTile tile = map.Layers[0].Tiles[0];
                Assert(tile.Raw == 0xB456A123 && tile.FloorId == 0x123 && tile.RoofId == 0x456 && tile.FloorFlags == 10 && tile.RoofFlags == 11);
                Assert(map.UnparsedOffset == 236 + 8 + 80000);
                Equal([0xDE, 0xAD, 0xBE, 0xEF], map.UnparsedScriptsAndObjects.ToArray());
                Equal(bytes, map.OriginalBytes.ToArray());
                bytes[0] = 255;
                Assert(map.OriginalBytes.Span[0] == 0);
            });
            Check("MAP absent elevations retain their actual indices", () =>
            {
                MapPrefix map = MapPrefix.Read(MapFixture(10)); // only elevation 1.
                Assert(map.Layers.Count == 1 && map.Layers[0].Elevation == 1);
                Assert(MapPrefix.Read(MapFixture(14)).Layers.Count == 0);
            });
            Check("MAP unsupported version, negative counts and truncated tiles rejected", () =>
            {
                byte[] bytes = MapFixture(12);
                BinaryPrimitives.WriteInt32BigEndian(bytes, 19);
                Reject(() => MapPrefix.Read(bytes));
                bytes = MapFixture(12);
                BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(32), -1);
                Reject(() => MapPrefix.Read(bytes));
                bytes = MapFixture(12);
                Reject(() => MapPrefix.Read(bytes[..240]));
                Reject(() => MapPrefix.Read(bytes[..235]));
                Reject(() => MapPrefix.Read(bytes[..^5]));
            });
            StructuralTests.Run(Check);
            GeometryTests.Run(Check);
            FramingTests.Run(Check);
            VisualPreferencesTests.Run(Check, root);
            ItemTransferTests.Run(Check);
            ItemOriginRouteTests.Run(Check);
            InventoryArtTests.Run(Check);
            HexDistanceTests.Run(Check);
            ExplosiveTimerTests.Run(Check);
            ExplosiveProposalTests.Run(Check);
            ItemUseTests.Run(Check);
            ItemUseCheckpointTests.Run(Check);
            ExplosiveCommitTests.Run(Check);
            PendingUseClockTests.Run(Check);
            BarrierEffectTests.Run(Check);
            BarrierProjectionTests.Run(Check);
            ItemCheckpointTests.Run(Check);
            ItemReturnTests.Run(Check);
            PrototypeWeightTests.Run(Check);
            CarryWeightTests.Run(Check);
            OcclusionTests.Run(Check);
            MessageTextTests.Run(Check);
            InventoryViewTests.Run(Check);
            FloorArtTests.Run(Check);
            NavigationTests.Run(Check);
            DoorTests.Run(Check);
            IntTests.Run(Check);
            TempleAdapterTests.Run(Check);
            CharacterSkillTests.Run(Check);
            TravelTests.Run(Check);
            ExitPreviewTests.Run(Check);
            WorldTests.Run(Check);
            CheckpointTests.Run(Check);
            Console.WriteLine($"{passed} synthetic test groups passed.");
            return 0;
        }
        finally
        {
            // Check containment before deleting our unique synthetic fixture directory.
            string resolved = Path.GetFullPath(root);
            if (Path.GetDirectoryName(resolved) != testParent || !Path.GetFileName(resolved).StartsWith("synthetic-", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing to remove a directory outside the test workspace.");
            Directory.Delete(resolved, true);
        }
    }

    private static byte[] MapFixture(int flags)
    {
        using var stream = new MemoryStream();
        void Word(int value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(bytes, value);
            stream.Write(bytes);
        }
        Word(20);
        stream.Write(Encoding.ASCII.GetBytes("synthetic.map\0\0\0"));
        foreach (int value in new[] { 20100, 0, 3, 1, 123, flags, 0, 1, 42, 0 }) Word(value);
        for (int i = 0; i < 44; i++) Word(i == 43 ? -77 : 0);
        Word(0x12345678);
        Word(-2);
        for (int elevation = 0; elevation < 3; elevation++)
            if ((flags & (2 << elevation)) == 0)
                for (int i = 0; i < 10000; i++) Word(unchecked((int)0xB456A123));
        stream.Write([0xDE, 0xAD, 0xBE, 0xEF]);
        return stream.ToArray();
    }

    private static void WriteDat(string path, (string Name, byte[] Payload, bool Compressed)[] entries, int prefix = 0)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII);
        writer.Write(new byte[prefix]);
        var rows = new List<(string Name, bool Compressed, int Size, int Stored, int Offset)>();
        foreach (var entry in entries)
        {
            byte[] stored = entry.Payload;
            if (entry.Compressed)
            {
                using var compressed = new MemoryStream();
                using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, true)) zlib.Write(stored);
                stored = compressed.ToArray();
            }
            rows.Add((entry.Name, entry.Compressed, entry.Payload.Length, stored.Length, (int)stream.Position - prefix));
            writer.Write(stored);
        }
        long treeStart = stream.Position;
        writer.Write(rows.Count);
        foreach (var row in rows)
        {
            byte[] name = Encoding.ASCII.GetBytes(row.Name);
            writer.Write(name.Length);
            writer.Write(name);
            writer.Write((byte)(row.Compressed ? 1 : 0));
            writer.Write(row.Size);
            writer.Write(row.Stored);
            writer.Write(row.Offset);
        }
        writer.Write((int)(stream.Position - treeStart));
        writer.Write((int)stream.Position + 4 - prefix);
    }

    private static void Equal(byte[] expected, byte[] actual) => Assert(expected.SequenceEqual(actual));
    private static void Assert(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Synthetic fixture assertion failed.");
    }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception error) when (error is IOException or InvalidDataException) { return; }
        throw new InvalidOperationException("Invalid input was accepted.");
    }
}
