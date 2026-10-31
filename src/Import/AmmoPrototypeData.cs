using System;
using System.Buffers.Binary;

namespace FalloutRemake.Import;

/// <summary>Serialized ammunition PRO fields (CE protoItemDataRead, ITEM_TYPE_AMMO).</summary>
public sealed class AmmoPrototypeData
{
    public const int Subtype = 4;
    public ResourceIdentity Source { get; }
    public int Pid { get; }
    public int Caliber { get; }
    public int Quantity { get; }
    public int ArmorClassModifier { get; }
    public int DamageResistanceModifier { get; }
    public int DamageMultiplier { get; }
    public int DamageDivisor { get; }

    private AmmoPrototypeData(Prototype prototype)
    {
        byte[] b = prototype.RawBytes;
        int At(int offset) => BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(offset, 4));
        Source = prototype.Resource; Pid = prototype.Pid; Caliber = At(57); Quantity = At(61);
        ArmorClassModifier = At(65); DamageResistanceModifier = At(69); DamageMultiplier = At(73); DamageDivisor = At(77);
    }

    public static AmmoPrototypeData Read(Prototype prototype) => new(WeaponPrototypeData.ItemBlock(prototype, Subtype, 81));
}
