using System;
using System.IO;
using System.Linq;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime.Combat;

/// <summary>SPECIAL order of the CE stat block.</summary>
public readonly record struct Special(int Strength, int Perception, int Endurance, int Charisma, int Intelligence, int Agility, int Luck)
{
    public static Special Neutral { get; } = new(5, 5, 5, 5, 5, 5, 5);
    public int[] ToArray() => new[] { Strength, Perception, Endurance, Charisma, Intelligence, Agility, Luck };
    public static Special From(int[] values) => values.Length == 7 && values.All(v => v is >= 1 and <= 10)
        ? new(values[0], values[1], values[2], values[3], values[4], values[5], values[6])
        : throw new InvalidDataException("SPECIAL values must be seven integers between 1 and 10.");
}

/// <summary>
/// Effective combat stats. Critters: PRO base + bonus per stat, clamped like CE critterGetStat (the PRO base already
/// holds the critterUpdateDerivedStats values; the bonus is the creature's own adjustment). Player: derived from SPECIAL.
/// </summary>
public sealed record DerivedCombatStats(Special Special, int MaxHitPoints, int MaxActionPoints, int ArmorClass, int MeleeDamage,
    int Sequence, int CriticalChance, int[] DamageThreshold, int[] DamageResistance, int[] CombatSkills)
{
    // CE stat indices.
    public const int MaxHitPointsStat = 7, MaxActionPointsStat = 8, ArmorClassStat = 9, MeleeDamageStat = 11, SequenceStat = 13,
        CriticalChanceStat = 15, ThresholdStat = 17, ResistanceStat = 24;
    // CE skills 0–5: small guns, big guns, energy weapons, unarmed, melee weapons, throwing.
    public const int SmallGuns = 0, BigGuns = 1, EnergyWeapons = 2, Unarmed = 3, MeleeWeapons = 4, Throwing = 5;
    static readonly (int Default, int Multiplier, int Stat1, int Stat2)[] SkillRules =
        { (5, 4, 5, -1), (0, 2, 5, -1), (0, 2, 5, -1), (30, 2, 5, 0), (20, 2, 5, 0), (0, 4, 5, -1) };

    public int Threshold(DamageType type) => DamageThreshold[(int)type];
    public int Resistance(DamageType type) => DamageResistance[(int)type];
    public int Skill(int index) => CombatSkills[index];

    // CE skillGetValue without tags, traits or perks: default + multiplier x stats + points stored in the PRO.
    static int[] Skills(Special s, Func<int, int> points)
    {
        int[] stats = s.ToArray();
        return SkillRules.Select((r, i) => Math.Min(300, r.Default + r.Multiplier * (stats[r.Stat1] + (r.Stat2 < 0 ? 0 : stats[r.Stat2])) + points(i))).ToArray();
    }

    public static DerivedCombatStats FromCritter(CritterPrototypeData critter)
    {
        ArgumentNullException.ThrowIfNull(critter);
        int Stat(int index, int min, int max) => Math.Clamp(critter.BaseStat(index) + critter.BonusStat(index), min, max);
        var special = Special.From(Enumerable.Range(0, 7).Select(i => Stat(i, 1, 10)).ToArray());
        return new(special, Stat(MaxHitPointsStat, 0, 999), Stat(MaxActionPointsStat, 1, 99), Stat(ArmorClassStat, 0, 999),
            Stat(MeleeDamageStat, 0, 500), Stat(SequenceStat, 0, 999), Stat(CriticalChanceStat, 0, 100),
            Enumerable.Range(0, 7).Select(i => Stat(ThresholdStat + i, 0, 999)).ToArray(),
            Enumerable.Range(0, 7).Select(i => Stat(ResistanceStat + i, 0, 100)).ToArray(),
            Skills(special, critter.Skill));
    }

    /// <summary>Character without traits, perks or tags (CE critterUpdateDerivedStats + skillGetValue), with optional worn armor.
    /// Each level above 1 adds EN/2+2 max HP (CharacterProgression); unspent skill points do not change skills.</summary>
    public static DerivedCombatStats ForCharacter(Special special, ArmorPrototypeData? armor = null, int level = 1)
    {
        _ = Special.From(special.ToArray());
        int[] threshold = armor?.DamageThreshold ?? new int[7], resistance = armor?.DamageResistance ?? new int[7];
        return new(special, FalloutRemake.Runtime.CharacterProgression.MaxHitPoints(special, level), 5 + special.Agility / 2, special.Agility + (armor?.ArmorClass ?? 0),
            Math.Max(special.Strength - 5, 1), 2 * special.Perception, special.Luck,
            threshold.Select(v => Math.Clamp(v, 0, 999)).ToArray(), resistance.Select(v => Math.Clamp(v, 0, 100)).ToArray(),
            Skills(special, _ => 0));
    }
}

/// <summary>One attack mode a combatant can use. Natural attacks of critters without weapons use the CE punch profile.</summary>
public sealed record CombatWeapon(string Name, int? Pid, WeaponAttackType AttackType, int MinDamage, int MaxDamage, DamageType DamageType,
    int Range, int ActionPoints, int MinStrength, int SkillIndex, AmmoPrototypeData? Ammo = null)
{
    // CE: no weapon costs 3 AP; unarmed reach 1 (2 with long limbs); punch damage 1–2, Melee Damage adds to the maximum.
    public static CombatWeapon Unarmed(DamageType natural = DamageType.Normal) =>
        new("Desarmado", null, WeaponAttackType.Unarmed, 1, 2, natural, 1, 3, 0, DerivedCombatStats.Unarmed);

    public static CombatWeapon Primary(string name, WeaponPrototypeData weapon, AmmoPrototypeData? ammo = null)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        WeaponAttackType type = WeaponPrototypeData.TypeOf(weapon.PrimaryMode);
        if (type == WeaponAttackType.None) throw new InvalidDataException($"Weapon PRO {weapon.Pid:X8} has no primary attack.");
        if (ammo != null && ammo.Caliber != weapon.Caliber) throw new InvalidDataException("Ammunition caliber does not match the weapon.");
        // Project mapping until CE weaponGetSkillValue is ported: energy damage uses Energy Weapons, other guns Small Guns.
        int skill = type switch
        {
            WeaponAttackType.Unarmed => DerivedCombatStats.Unarmed, WeaponAttackType.Melee => DerivedCombatStats.MeleeWeapons,
            WeaponAttackType.Throw => DerivedCombatStats.Throwing,
            _ => weapon.DamageType is 1 or 3 or 4 ? DerivedCombatStats.EnergyWeapons : DerivedCombatStats.SmallGuns
        };
        return new(name, weapon.Pid, type, weapon.MinDamage, weapon.MaxDamage, (DamageType)weapon.DamageType,
            Math.Max(1, weapon.PrimaryRange), Math.Max(1, weapon.PrimaryActionPoints), weapon.MinStrength, skill, ammo);
    }

    public bool AddsMeleeDamage => AttackType is WeaponAttackType.Unarmed or WeaponAttackType.Melee;
    public bool IsRanged => AttackType is WeaponAttackType.Ranged or WeaponAttackType.Throw;
}
