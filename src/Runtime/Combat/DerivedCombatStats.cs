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
        CriticalChanceStat = 15, BetterCriticalsStat = 16, ThresholdStat = 17, ResistanceStat = 24;
    // CE skills 0–5: small guns, big guns, energy weapons, unarmed, melee weapons, throwing.
    public const int SmallGuns = 0, BigGuns = 1, EnergyWeapons = 2, Unarmed = 3, MeleeWeapons = 4, Throwing = 5;
    static readonly (int Default, int Multiplier, int Stat1, int Stat2)[] SkillRules =
        { (5, 4, 5, -1), (0, 2, 5, -1), (0, 2, 5, -1), (30, 2, 5, 0), (20, 2, 5, 0), (0, 4, 5, -1) };

    /// <summary>Better Criticals (stat 16, -60..100): added to the critical effect roll. Heavy Handed gives the player -30.</summary>
    public int BetterCriticals { get; init; }
    /// <summary>The player's Finesse trait: CE adds 30 to the target's DR on every hit that does not bypass armor.</summary>
    public bool Finesse { get; init; }
    /// <summary>The player's Jinxed trait or perk: CE turns half of every plain miss in the fight, anyone's, into a critical failure.</summary>
    public bool Jinxed { get; init; }
    /// <summary>The player's chosen perks (combat rules read their ranks); none for critters.</summary>
    public FalloutRemake.Runtime.PerkRanks Perks { get; init; } = FalloutRemake.Runtime.PerkRanks.None;
    /// <summary>The player's One Hander trait: CE attackDetermineToHit gives -40 with a two-handed weapon and +20 with any other.</summary>
    public bool OneHander { get; init; }
    /// <summary>The player's Fast Shot trait: no aimed attacks (critterCanAim) and 1 AP less with a weapon of range above 2.</summary>
    public bool FastShot { get; init; }

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
            Skills(special, critter.Skill)) { BetterCriticals = Stat(BetterCriticalsStat, -60, 100) };
    }

    /// <summary>Character without traits, perks or tags (CE critterUpdateDerivedStats + skillGetValue), with optional worn armor.
    /// Each level above 1 adds EN/2+2 max HP (CharacterProgression); unspent skill points do not change skills.</summary>
    /// <summary>The player's sheet (traits, tags, skill points) at a level, with optional worn armor.</summary>
    public static DerivedCombatStats ForPlayer(FalloutRemake.Runtime.PlayerCharacter character, int level = 1, ArmorPrototypeData? armor = null)
    {
        ArgumentNullException.ThrowIfNull(character);
        int[] threshold = armor?.DamageThreshold ?? new int[7], resistance = armor?.DamageResistance ?? new int[7];
        return new(character.Effective, FalloutRemake.Runtime.CharacterProgression.MaxHitPoints(character, level), character.MaxActionPoints,
            character.ArmorClass + (armor?.ArmorClass ?? 0), character.MeleeDamage, character.Sequence, character.CriticalChance,
            threshold.Select((v, i) => Math.Clamp(v + character.ThresholdBonus(i), 0, 999)).ToArray(),
            resistance.Select((v, i) => Math.Clamp(v + character.ResistanceBonus(i), 0, 100)).ToArray(), character.Skills.Take(6).ToArray())
        {
            // CE traitGetStatModifier: Heavy Handed -30 Better Criticals (the perk adds 20); attackComputeDamage: Finesse +30 target DR.
            BetterCriticals = character.BetterCriticals, Perks = character.Perks,
            Jinxed = character.Has(FalloutRemake.Runtime.CharacterTrait.Jinxed) || character.Perks.Has(FalloutRemake.Runtime.Perk.Jinxed),
            Finesse = character.Has(FalloutRemake.Runtime.CharacterTrait.Finesse),
            OneHander = character.Has(FalloutRemake.Runtime.CharacterTrait.OneHander),
            FastShot = character.Has(FalloutRemake.Runtime.CharacterTrait.FastShot)
        };
    }

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

/// <summary>One of CE's unarmed hit modes (unarmedInitVanilla): requirements, bonus damage and critical chance, AP.</summary>
public sealed record UnarmedBlow(string Name, int Level, int Skill, int Strength, int Agility, int BonusDamage, int BonusCriticalChance, int ActionPoints,
    bool Penetrates, bool Secondary);

/// <summary>
/// CE unarmedGetPunchHitMode / unarmedGetKickHitMode: the last advanced blow of the range whose level, Unarmed skill and
/// ST/AG requirements the dude meets, else the plain punch or kick. The left hand punches, the right one kicks.
/// </summary>
public static class UnarmedBlows
{
    public static readonly UnarmedBlow Punch = new("Desarmado", 0, 0, 0, 0, 0, 0, 3, false, false), Kick = new("Chute", 0, 0, 0, 0, 0, 0, 3, false, false);
    public static readonly UnarmedBlow[] Punches =
    {
        new("Strong Punch", 0, 55, 0, 6, 3, 0, 3, false, false), new("Hammer Punch", 6, 75, 5, 6, 5, 5, 3, false, false),
        new("Lightning Punch", 9, 100, 5, 7, 7, 15, 3, false, false), new("Chop Punch", 5, 75, 5, 7, 3, 10, 3, false, true),
        new("Dragon Punch", 12, 115, 5, 7, 7, 20, 6, true, true), new("Force Punch", 16, 130, 5, 7, 10, 40, 8, true, true)
    };
    public static readonly UnarmedBlow[] Kicks =
    {
        new("Strong Kick", 0, 40, 0, 6, 5, 0, 4, false, false), new("Snap Kick", 6, 60, 0, 6, 7, 0, 4, false, false),
        new("Roundhouse Kick", 9, 80, 6, 6, 9, 5, 4, false, false), new("Kip Kick", 6, 60, 6, 7, 7, 0, 7, false, true),
        new("Jump Kick", 12, 100, 6, 7, 9, 10, 7, true, true), new("Death Blossom Kick", 15, 125, 6, 8, 12, 50, 9, true, true)
    };

    public static UnarmedBlow Best(bool kick, bool secondary, int unarmedSkill, int level, Special special)
    {
        UnarmedBlow chosen = kick ? Kick : Punch;
        foreach (UnarmedBlow blow in kick ? Kicks : Punches)
            if (blow.Secondary == secondary && unarmedSkill >= blow.Skill && level >= blow.Level && special.Strength >= blow.Strength && special.Agility >= blow.Agility)
                chosen = blow;
        return chosen;
    }
}

/// <summary>One attack mode a combatant can use. Natural attacks of critters without weapons use the CE punch profile.</summary>
public sealed record CombatWeapon(string Name, int? Pid, WeaponAttackType AttackType, int MinDamage, int MaxDamage, DamageType DamageType,
    int Range, int ActionPoints, int MinStrength, int SkillIndex, AmmoPrototypeData? Ammo = null)
{
    // CE: no weapon costs 3 AP; unarmed reach 1 (2 with long limbs); punch damage 1–2, Melee Damage adds to the maximum.
    public static CombatWeapon Unarmed(DamageType natural = DamageType.Normal) =>
        new("Desarmado", null, WeaponAttackType.Unarmed, 1, 2, natural, 1, 3, 0, DerivedCombatStats.Unarmed);

    /// <summary>The player's bare-handed blow: the best punch (or kick) its Unarmed skill, level and SPECIAL allow (CE unarmed hit modes).</summary>
    public static CombatWeapon UnarmedFor(DerivedCombatStats stats, int level, bool kick = false)
    {
        UnarmedBlow blow = UnarmedBlows.Best(kick, secondary: false, stats.Skill(DerivedCombatStats.Unarmed), level, stats.Special);
        return new(blow.Name, null, WeaponAttackType.Unarmed, 1, 2, DamageType.Normal, 1, blow.ActionPoints, 0, DerivedCombatStats.Unarmed)
            { BonusDamage = blow.BonusDamage, BonusCriticalChance = blow.BonusCriticalChance, Penetrates = blow.Penetrates, Kick = kick };
    }

    /// <summary>The right hand's blow (a kick) rather than the left hand's punch.</summary>
    public bool Kick { get; init; }

    /// <summary>Unarmed hit mode bonus damage, added to both ends of the roll (weaponGetDamage).</summary>
    public int BonusDamage { get; init; }
    /// <summary>After a plain hit by the player, d100 at or below it makes the blow critical (attackCompute, any game day).</summary>
    public int BonusCriticalChance { get; init; }
    /// <summary>Keeps only 20% of the target's DT, as the Penetrate perk (unarmedIsPenetrating).</summary>
    public bool Penetrates { get; init; }

    /// <param name="rounds">Rounds loaded (the item's ammoQuantity); <paramref name="ammo"/> is the PRO of its ammoTypePid.</param>
    public static CombatWeapon Primary(string name, WeaponPrototypeData weapon, AmmoPrototypeData? ammo = null, int rounds = 0)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        WeaponAttackType type = WeaponPrototypeData.TypeOf(weapon.PrimaryMode);
        if (type == WeaponAttackType.None) throw new InvalidDataException($"Weapon PRO {weapon.Pid:X8} has no primary attack.");
        if (ammo != null && ammo.Caliber != weapon.Caliber) throw new InvalidDataException("Ammunition caliber does not match the weapon.");
        if (weapon.PrimaryMode == WeaponAttackMode.Flame) throw new InvalidDataException($"Weapon PRO {weapon.Pid:X8} fires continuously, which is not supported.");
        return Mode(name, weapon, type, weapon.PrimaryRange, weapon.PrimaryActionPoints, ammo, weapon.PrimaryMode, rounds);
    }

    /// <summary>
    /// The weapon's second attack (CE secondary hit mode: maxRange2, actionPointCost2), such as the spear's throw or the
    /// SMG's burst; null when the PRO names none. A continuous (flamer) mode is not supported.
    /// </summary>
    public static CombatWeapon? Secondary(string name, WeaponPrototypeData weapon, AmmoPrototypeData? ammo = null, int rounds = 0)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        WeaponAttackType type = WeaponPrototypeData.TypeOf(weapon.SecondaryMode);
        if (ammo != null && ammo.Caliber != weapon.Caliber) throw new InvalidDataException("Ammunition caliber does not match the weapon.");
        return type is WeaponAttackType.Throw or WeaponAttackType.Melee || type == WeaponAttackType.Ranged && weapon.SecondaryMode != WeaponAttackMode.Flame
            ? Mode(name, weapon, type, weapon.SecondaryRange, weapon.SecondaryActionPoints, ammo, weapon.SecondaryMode, rounds) with { IsSecondary = true } : null;
    }

    private static CombatWeapon Mode(string name, WeaponPrototypeData weapon, WeaponAttackType type, int range, int actionPoints, AmmoPrototypeData? ammo,
        WeaponAttackMode mode, int rounds)
    {
        // CE weaponGetSkillForHitMode: laser, plasma and electrical guns use Energy Weapons, Big Gun PROs Big Guns, other guns Small Guns.
        int skill = type switch
        {
            WeaponAttackType.Unarmed => DerivedCombatStats.Unarmed, WeaponAttackType.Melee => DerivedCombatStats.MeleeWeapons,
            WeaponAttackType.Throw => DerivedCombatStats.Throwing,
            _ => weapon.DamageType is 1 or 3 or 4 ? DerivedCombatStats.EnergyWeapons : weapon.BigGun ? DerivedCombatStats.BigGuns : DerivedCombatStats.SmallGuns
        };
        if (rounds < 0 || rounds > Math.Max(0, weapon.AmmoCapacity)) throw new InvalidDataException($"Weapon PRO {weapon.Pid:X8} cannot hold {rounds} rounds.");
        return new(name, weapon.Pid, type, weapon.MinDamage, weapon.MaxDamage, (DamageType)weapon.DamageType,
            Math.Max(1, range), Math.Max(1, actionPoints), weapon.MinStrength, skill, ammo)
        {
            CriticalFailureType = weapon.CriticalFailureType, TwoHanded = weapon.TwoHanded, AnimationCode = weapon.AnimationCode,
            AmmoCapacity = Math.Max(0, weapon.AmmoCapacity), Caliber = weapon.Caliber, AmmoPid = ammo?.Pid ?? weapon.AmmoPid, Rounds = rounds,
            WeaponPerk = weapon.Perk, Penetrates = weapon.Perk == FalloutRemake.Runtime.Perk.WeaponPenetrate,
            BurstRounds = mode == WeaponAttackMode.Burst ? Math.Max(1, weapon.BurstRounds) : 0
        };
    }

    /// <summary>CE ammoGetCapacity of the weapon: 0 when it takes no ammunition.</summary>
    public int AmmoCapacity { get; init; }
    /// <summary>CE ammoGetQuantity: rounds loaded, shared by the item's attack modes.</summary>
    public int Rounds { get; init; }
    /// <summary>The weapon PRO's caliber, which ammunition must match (weaponCanBeReloadedWith).</summary>
    public int Caliber { get; init; }
    /// <summary>CE weapon.ammoTypePid: the ammunition loaded last (the PRO's default until then); <see cref="Ammo"/> holds its PRO.</summary>
    public int AmmoPid { get; init; } = -1;
    /// <summary>The weapon PRO's perk (Long Range, Accurate, Penetrate, Scope Range, Fast Reload…); -1 for none.</summary>
    public int WeaponPerk { get; init; } = -1;
    /// <summary>Rounds a burst fires at most (weaponGetBurstRounds); 0 for a single shot or a blow.</summary>
    public int BurstRounds { get; init; }
    /// <summary>The mode's animation is ANIM_FIRE_BURST (CE _compute_spray).</summary>
    public bool Burst => BurstRounds > 0;
    public bool UsesAmmo => AmmoCapacity > 0;

    /// <summary>The weapon PRO's animation code: 0 (WEAPON_ANIMATION_NONE) for bare hands and fist weapons (HtH Evade counts them as empty).</summary>
    public int AnimationCode { get; init; }

    /// <summary>The weapon's second attack mode rather than its first.</summary>
    public bool IsSecondary { get; init; }
    /// <summary>A thrown attack: the weapon itself leaves the hand and lands where the throw ends.</summary>
    public bool Throws => AttackType == WeaponAttackType.Throw;

    /// <summary>PRO critical failure table row; -1 for natural attacks and bare hands (CE weaponGetCriticalFailureType of no weapon).</summary>
    public int CriticalFailureType { get; init; } = -1;
    public bool TwoHanded { get; init; }
    /// <summary>A carried weapon, not bare hands or a natural attack: crippled arms only matter then (CE _combat_check_bad_shot).</summary>
    public bool IsItem => Pid != null;

    public bool AddsMeleeDamage => AttackType is WeaponAttackType.Unarmed or WeaponAttackType.Melee;
    public bool IsRanged => AttackType is WeaponAttackType.Ranged or WeaponAttackType.Throw;
}
