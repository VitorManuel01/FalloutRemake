using System;
using System.Buffers.Binary;
using System.Linq;

namespace FalloutRemake.Import;

/// <summary>Serialized armor PRO fields (CE protoItemDataRead, ITEM_TYPE_ARMOR). Arrays follow the 7 CE damage types.</summary>
public sealed class ArmorPrototypeData
{
    public const int Subtype = 0;
    private readonly int[] _resistance, _threshold;
    public ResourceIdentity Source { get; }
    public int Pid { get; }
    public int ArmorClass { get; }
    public int[] DamageResistance => (int[])_resistance.Clone();
    public int[] DamageThreshold => (int[])_threshold.Clone();
    public int Perk { get; }
    public int MaleFid { get; }
    public int FemaleFid { get; }

    private ArmorPrototypeData(Prototype prototype)
    {
        byte[] b = prototype.RawBytes;
        int At(int offset) => BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(offset, 4));
        Source = prototype.Resource; Pid = prototype.Pid; ArmorClass = At(57);
        _resistance = Enumerable.Range(0, 7).Select(i => At(61 + 4 * i)).ToArray();
        _threshold = Enumerable.Range(0, 7).Select(i => At(89 + 4 * i)).ToArray();
        Perk = At(117); MaleFid = At(121); FemaleFid = At(125);
    }

    public static ArmorPrototypeData Read(Prototype prototype) => new(WeaponPrototypeData.ItemBlock(prototype, Subtype, 129));
}
