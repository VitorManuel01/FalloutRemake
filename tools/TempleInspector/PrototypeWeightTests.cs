using System.Buffers.Binary;
using FalloutRemake.Import;

internal static class PrototypeWeightTests
{
    private static GameResource Item(int subtype, int weight)
    {
        int[] sizes = { 129, 65, 125, 122, 81, 69, 61 }; byte[] raw = new byte[sizes[subtype]];
        void Word(int offset, int value) => BinaryPrimitives.WriteInt32BigEndian(raw.AsSpan(offset), value);
        Word(0, 49); Word(28, -1); Word(32, subtype); Word(36, 7); Word(40, 99); Word(44, weight); Word(48, 1234); Word(52, 42);
        return new("proto/items/synthetic.pro", "synthetic", raw);
    }
    public static void Run(Action<string, Action> check)
    {
        check("all seven item PRO layouts read base weight at offset 44 independently of material, size and cost", () =>
        {
            for (int subtype = 0; subtype < 7; subtype++)
            {
                var source = Item(subtype, 17 + subtype); byte[] before = source.Bytes.ToArray();
                Prototype p = PrototypeCatalog.Read(source, 49);
                Assert(p.BaseWeight == 17 + subtype && p.InventoryFid == 42 && p.Fid == 0 && p.Subtype == subtype && before.SequenceEqual(source.Bytes));
            }
        });
        check("zero and maximum signed base weights remain exact while negative weights are refused", () =>
        {
            foreach (int weight in new[] { 0, int.MaxValue }) Assert(PrototypeCatalog.Read(Item(2, weight), 49).BaseWeight == weight);
            try { PrototypeCatalog.Read(Item(2, -1), 49); } catch (InvalidDataException) { return; }
            throw new Exception("Negative item weight accepted.");
        });
        check("non-item PRO records expose no item base weight", () =>
        {
            var raw = new byte[36]; BinaryPrimitives.WriteInt32BigEndian(raw, 0x03000001);
            var proto = PrototypeCatalog.Read(new("proto/walls/synthetic.pro", "synthetic", raw), 0x03000001);
            Assert(proto.BaseWeight == null && proto.InventoryFid == null);
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Base weight assertion failed."); }
}
