using System.Buffers.Binary;
using System.Text;
using FalloutRemake.Geometry;
using FalloutRemake.Import;

internal static class FloorArtTests
{
    public static void Run(Action<string, Action> check)
    {
        check("static FRM palette decoding preserves indexed order, transparency and six-bit colors", () =>
        {
            byte[] bytes = Frame(), palette = Palette();
            bytes[74] = 0; bytes[75] = 1; bytes[76] = 2;
            palette[3] = 63; palette[4] = 32; palette[5] = 1; palette[6] = 64;
            FloorArt tile = FloorArt.Decode(2237, Resource(bytes), Resource(palette));
            byte[] expected = { 0, 0, 0, 0, 252, 128, 4, 255, 0, 0, 0, 255 };
            Assert(tile.Id == 2237 && tile.Rgba.Span[..12].SequenceEqual(expected) && tile.Source.Size == 2954);
            bytes[75] = 0; palette[3] = 0;
            Assert(tile.Rgba.Span[4] == 252); // output owns its pixel buffer.
        });
        check("static FRM rejects truncation, trailing data, wrong size, version and multiple frames", () =>
        {
            foreach (int size in new[] { 0, 61, 74, 2953, 2955 }) Reject(() => FloorArt.Decode(0, Resource(new byte[size]), Resource(Palette())));
            foreach (var (offset, value) in new[] { (0, 3), (58, 0), (66, 2881) })
            { byte[] bytes = Frame(); BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(offset), value); Reject(() => Decode(bytes)); }
            foreach (var (offset, value) in new[] { (6, 1), (8, 2), (62, 81), (64, 35), (70, -1), (72, 1) })
            { byte[] bytes = Frame(); BinaryPrimitives.WriteInt16BigEndian(bytes.AsSpan(offset), (short)value); Reject(() => Decode(bytes)); }
        });
        check("static FRM rejects all directional displacements and nonzero frame offsets", () =>
        {
            for (int direction = 0; direction < 6; direction++)
            {
                foreach (int start in new[] { 10, 22 })
                { byte[] bytes = Frame(); bytes[start + direction * 2 + 1] = 1; Reject(() => Decode(bytes)); }
                byte[] offset = Frame(); offset[34 + direction * 4 + 3] = 1; Reject(() => Decode(offset));
            }
        });
        check("floor artwork catalog keeps zero-based list IDs and source fingerprints without writing assets", () =>
        {
            int reads = 0;
            var catalog = new FloorArtCatalog(path =>
            {
                reads++;
                return new GameResource(path, "synthetic", path switch
                { "art/tiles/tiles.lst" => Encoding.ASCII.GetBytes("reserved.frm\ntest.frm ; comment\n"), "color.pal" => Palette(), "art/tiles/test.frm" => Frame(), _ => throw new InvalidDataException() });
            });
            FloorArt tile = catalog.Get(1);
            Assert(ReferenceEquals(tile, catalog.Get(1)) && reads == 3 && tile.Source.Path == "art/tiles/test.frm");
            Assert(catalog.Dependencies.Length == 3 && catalog.Dependencies.All(d => d.Sha256.Length == 64));
            Reject(() => catalog.Get(2)); Reject(() => catalog.Get(-1)); Reject(() => catalog.Get(4096));
        });
        check("palette truncation and invalid floor IDs fail without mutating source buffers", () =>
        {
            byte[] bytes = Frame(), original = (byte[])bytes.Clone();
            foreach (int length in new[] { 0, 767, 768, 33535 }) Reject(() => FloorArt.Decode(0, Resource(bytes), Resource(new byte[length])));
            Reject(() => FloorArt.Decode(-1, Resource(bytes), Resource(Palette())));
            Reject(() => FloorArt.Decode(4096, Resource(bytes), Resource(Palette())));
            Decode(bytes); Assert(bytes.SequenceEqual(original));
        });
        check("full floor image corners project back to native sprite bounds and preserve adjacent cell offsets", () =>
        {
            foreach (var (u, v) in new[] { (0d, 0d), (1d, 0d), (1d, 1d), (0d, 1d), (.5, .5) })
            {
                var screen = FalloutGrid.OriginalScreen(FloorArtwork.Corner(u, v));
                Assert(Math.Abs(screen.X - (24 + u * 80 - 40)) < 1e-8 && Math.Abs(screen.Y - (16 + v * 36 - 18)) < 1e-8);
            }
            foreach (int square in new[] { 0, 99, 100, 5076, 9999 })
            {
                GroundPoint center = FalloutGrid.SquareCenter(square), corner = FloorArtwork.Corner(0, 0);
                var screen = FalloutGrid.OriginalScreen(new GroundPoint(center.X + corner.X, center.Z + corner.Z));
                int col = 99 - square % 100, row = square / 100;
                Assert(Math.Abs(screen.X - (-16 + 48 * col + 32 * row)) < 1e-8 && Math.Abs(screen.Y - (-2 - 12 * col + 24 * row)) < 1e-8);
            }
        });
    }
    private static byte[] Frame()
    {
        var bytes = new byte[2954];
        BinaryPrimitives.WriteInt32BigEndian(bytes, 4);
        BinaryPrimitives.WriteInt16BigEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteInt16BigEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(58), 2892);
        BinaryPrimitives.WriteInt16BigEndian(bytes.AsSpan(62), 80);
        BinaryPrimitives.WriteInt16BigEndian(bytes.AsSpan(64), 36);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(66), 2880);
        return bytes;
    }
    private static byte[] Palette() => new byte[33536];
    private static GameResource Resource(byte[] bytes) => new("synthetic", "synthetic", bytes);
    private static FloorArt Decode(byte[] bytes) => FloorArt.Decode(0, Resource(bytes), Resource(Palette()));
    private static void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Invalid floor artwork accepted."); }
    private static void Assert(bool condition) { if (!condition) throw new Exception("Floor artwork assertion failed."); }
}
