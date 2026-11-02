using System;
using System.Buffers.Binary;
using System.IO;

namespace FalloutRemake.Import;

/// <summary>Original attack modes stored in an item's extended-flag nibbles (CE hit-mode attack types).</summary>
public enum WeaponAttackMode { None, Punch, Kick, Swing, Thrust, Throw, Single, Burst, Flame }
public enum WeaponAttackType { None, Unarmed, Melee, Throw, Ranged }

/// <summary>Serialized weapon PRO fields (CE protoItemDataRead, ITEM_TYPE_WEAPON). No perks, traits or ammo rules applied.</summary>
public sealed class WeaponPrototypeData
{
    public const int Subtype = 3;
    public ResourceIdentity Source { get; }
    public int Pid { get; }
    public int AnimationCode { get; }
    public int MinDamage { get; }
    public int MaxDamage { get; }
    public int DamageType { get; }
    public int PrimaryRange { get; }
    public int SecondaryRange { get; }
    public int ProjectilePid { get; }
    public int MinStrength { get; }
    public int PrimaryActionPoints { get; }
    public int SecondaryActionPoints { get; }
    public int CriticalFailureType { get; }
    public int Perk { get; }
    public int BurstRounds { get; }
    public int Caliber { get; }
    public int AmmoPid { get; }
    public int AmmoCapacity { get; }
    public int SoundCode { get; }
    public WeaponAttackMode PrimaryMode { get; }
    public WeaponAttackMode SecondaryMode { get; }
    /// <summary>CE WEAPON_TWO_HAND (extended flag 0x200): a crippled arm cannot wield it.</summary>
    public bool TwoHanded { get; }
    public const int TwoHandedFlag = 0x200;
    /// <summary>CE ItemProtoExtendedFlags_BigGun (0x100): a non-energy firearm trained by Big Guns.</summary>
    public bool BigGun { get; }
    public const int BigGunFlag = 0x100;

    private WeaponPrototypeData(Prototype prototype)
    {
        byte[] b = prototype.RawBytes;
        int At(int offset) => BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(offset, 4));
        Source = prototype.Resource; Pid = prototype.Pid;
        // 57-byte item header (14 words and the sound byte), then the weapon block.
        AnimationCode = At(57); MinDamage = At(61); MaxDamage = At(65); DamageType = At(69);
        PrimaryRange = At(73); SecondaryRange = At(77); ProjectilePid = At(81); MinStrength = At(85);
        PrimaryActionPoints = At(89); SecondaryActionPoints = At(93); CriticalFailureType = At(97); Perk = At(101);
        BurstRounds = At(105); Caliber = At(109); AmmoPid = At(113); AmmoCapacity = At(117); SoundCode = b[121];
        int extended = prototype.ExtendedFlags;
        PrimaryMode = Mode(extended & 0xF); SecondaryMode = Mode((extended >> 4) & 0xF);
        TwoHanded = (extended & TwoHandedFlag) != 0; BigGun = (extended & BigGunFlag) != 0;
        if (MinDamage < 0 || MaxDamage < MinDamage || DamageType is < 0 or > 6)
            throw new InvalidDataException($"Weapon PRO {Pid:X8} has an unsupported damage range or type.");
    }

    static WeaponAttackMode Mode(int nibble) => nibble <= (int)WeaponAttackMode.Flame ? (WeaponAttackMode)nibble
        : throw new InvalidDataException("Unknown weapon attack mode nibble.");

    // CE _attack_subtype: punch/kick unarmed, swing/thrust melee, throw, single/burst/flame ranged.
    public static WeaponAttackType TypeOf(WeaponAttackMode mode) => mode switch
    {
        WeaponAttackMode.Punch or WeaponAttackMode.Kick => WeaponAttackType.Unarmed,
        WeaponAttackMode.Swing or WeaponAttackMode.Thrust => WeaponAttackType.Melee,
        WeaponAttackMode.Throw => WeaponAttackType.Throw,
        WeaponAttackMode.Single or WeaponAttackMode.Burst or WeaponAttackMode.Flame => WeaponAttackType.Ranged,
        _ => WeaponAttackType.None
    };

    public static WeaponPrototypeData Read(Prototype prototype) => new(ItemBlock(prototype, Subtype, 122));

    internal static Prototype ItemBlock(Prototype prototype, int subtype, int length)
    {
        if (prototype == null || prototype.Type != 0 || prototype.Subtype != subtype || prototype.RawBytes == null || prototype.RawBytes.Length != length)
            throw new InvalidDataException($"Expected a serialized item prototype of subtype {subtype}.");
        var resource = new GameResource(prototype.Resource.Path, prototype.Resource.Source, (byte[])prototype.RawBytes.Clone());
        Prototype canonical = PrototypeCatalog.Read(resource, prototype.Pid);
        if (canonical.Resource != prototype.Resource) throw new InvalidDataException("Item prototype bytes differ from their source fingerprint.");
        return canonical;
    }
}
