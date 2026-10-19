using System.Buffers.Binary;
using System.Text;
using FalloutRemake.Import;

internal static class InventoryArtTests
{
    private static byte[] Frame(int width = 3, int height = 2)
    {
        var bytes = new byte[74 + width * height];
        Word(bytes, 0, 4); Half(bytes, 8, 1); Word(bytes, 58, 12 + width * height);
        Half(bytes, 62, width); Half(bytes, 64, height); Word(bytes, 66, width * height); return bytes;
    }
    private static void Word(byte[] b, int o, int v) => BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(o), v);
    private static void Half(byte[] b, int o, int v) => BinaryPrimitives.WriteInt16BigEndian(b.AsSpan(o), (short)v);
    private static GameResource Resource(byte[] bytes) => new("synthetic.frm", "synthetic", bytes);
    private static InventoryArt Decode(byte[] bytes, byte[]? palette = null) => InventoryArt.Decode(0x07000001, Resource(bytes), Resource(palette ?? new byte[33536]));
    public static void Run(Action<string, Action> check)
    {
        check("inventory FRM retains dimensions, signed displacements, transparent index and six-bit palette", () =>
        {
            byte[] bytes = Frame(), colors = new byte[33536]; Half(bytes, 70, -7); Half(bytes, 72, 9); Half(bytes, 10, -3); Half(bytes, 22, 4);
            bytes[75] = 1; bytes[76] = 2; colors[3] = 63; colors[4] = 1; colors[6] = 64;
            InventoryArt art = Decode(bytes, colors);
            Assert(art.Width == 3 && art.Height == 2 && art.OffsetX == -7 && art.OffsetY == 9 && art.DirectionOffsetX == -3 && art.DirectionOffsetY == 4);
            Assert(art.Rgba.Span[..12].SequenceEqual(new byte[] { 0, 0, 0, 0, 252, 4, 0, 255, 0, 0, 0, 255 }));
            bytes[75] = 0; colors[3] = 0; Assert(art.Rgba.Span[4] == 252);
        });
        check("inventory FRM refuses incomplete data, wrong sizes, animation and directional data", () =>
        {
            foreach (int length in new[] { 0, 61, 73, 79, 81 }) Reject(() => Decode(new byte[length]));
            foreach (var (offset, value) in new[] { (0, 3), (58, 0), (66, 7), (34, 1), (54, -1) })
            { byte[] bytes = Frame(); Word(bytes, offset, value); Reject(() => Decode(bytes)); }
            foreach (var (offset, value) in new[] { (6, 1), (8, 2), (62, 0), (62, 513), (64, -1) })
            { byte[] bytes = Frame(); Half(bytes, offset, value); Reject(() => Decode(bytes)); }
            Reject(() => Decode(Frame(), new byte[33535]));
            var large = Decode(Frame(512, 512)); Assert(large.Rgba.Length == 512 * 512 * 4);
        });
        check("inventory FID uses inventory list index rather than world FID and caches exact identities", () =>
        {
            int reads = 0;
            var catalog = new InventoryArtCatalog(path => { reads++; return new(path, "synthetic", path switch
                { "art/inven/inven.lst" => Encoding.ASCII.GetBytes("reserved.frm\nicon.frm\n"), "art/inven/icon.frm" => Frame(), "color.pal" => new byte[33536], _ => throw new InvalidDataException() }); });
            Assert(catalog.Get(-1) == null && reads == 0);
            var icon = catalog.Get(0x07000001)!;
            Assert(ReferenceEquals(icon, catalog.Get(0x07000001)) && reads == 3 && icon.Source.Path == "art/inven/icon.frm" && catalog.Dependencies.Length == 3);
            foreach (int fid in new[] { 1, 0x06000001, 0x17000001, 0x07001001 }) Reject(() => catalog.Get(fid));
            Reject(() => catalog.Get(0x07000002));
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Inventory art assertion failed."); }
    private static void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Invalid inventory art accepted."); }
}
