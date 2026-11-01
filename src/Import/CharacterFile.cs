using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;

namespace FalloutRemake.Import;

/// <summary>
/// Character record of a GCD file (premade/*.gcd, CE _pc_load_data): the critter data block as protoCritterDataWrite
/// stores it (flags, 35 base stats, 35 bonus stats, 18 skill point counts, body type, experience, kill type, damage
/// type), a 32-byte name, 4 tagged skills, 2 traits and the unspent character points. Big-endian; -1 marks an empty
/// tag or trait slot. Base stats 7–34 hold the derived values the character editor computed.
/// </summary>
public sealed class CharacterFile
{
    public const int StatCount = 35, SkillCount = 18, TagSlots = 4, TraitSlots = 2, TraitCount = 16, Length = 432;
    private readonly int[] _baseStats, _bonusStats, _skills, _tags, _traits;
    public ResourceIdentity Source { get; }
    public int Flags { get; }
    public int[] BaseStats => (int[])_baseStats.Clone();
    public int[] BonusStats => (int[])_bonusStats.Clone();
    /// <summary>Points put into each skill (CE critter skills array), not the skill value.</summary>
    public int[] SkillPoints => (int[])_skills.Clone();
    public int BodyType { get; }
    public int Experience { get; }
    public int KillType { get; }
    public int DamageType { get; }
    public string Name { get; }
    public int[] TaggedSkills => (int[])_tags.Clone();
    public int[] Traits => (int[])_traits.Clone();
    public int CharacterPoints { get; }

    private CharacterFile(GameResource resource)
    {
        byte[] b = resource.Bytes;
        int offset = 0;
        int Next() { int value = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(offset, 4)); offset += 4; return value; }
        int[] Many(int count) => Enumerable.Range(0, count).Select(_ => Next()).ToArray();
        Source = ResourceIdentity.From(resource);
        Flags = Next(); _baseStats = Many(StatCount); _bonusStats = Many(StatCount); _skills = Many(SkillCount);
        BodyType = Next(); Experience = Next(); KillType = Next(); DamageType = Next();
        int end = Array.IndexOf(b, (byte)0, offset, 32);
        Name = Encoding.Latin1.GetString(b, offset, (end < 0 ? offset + 32 : end) - offset); offset += 32;
        _tags = Many(TagSlots); _traits = Many(TraitSlots); CharacterPoints = Next();
    }

    public static CharacterFile Read(GameResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (resource.Bytes.Length != Length) throw new InvalidDataException($"GCD {resource.Path} must be {Length} bytes.");
        var file = new CharacterFile(resource);
        if (file._tags.Any(t => t is < -1 or >= SkillCount) || file._traits.Any(t => t is < -1 or >= TraitCount)
            || file._skills.Any(s => s < 0) || file.Name.Length == 0 || file.Name.Any(char.IsControl))
            throw new InvalidDataException($"GCD {resource.Path} has an invalid tag, trait, skill or name.");
        return file;
    }
}
