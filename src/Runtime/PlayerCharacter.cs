using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using FalloutRemake.Import;
using FalloutRemake.Runtime.Combat;

namespace FalloutRemake.Runtime;

/// <summary>Fallout 2 traits in CE order (trait.msg names are 100 + index).</summary>
public enum CharacterTrait
{
    FastMetabolism, Bruiser, SmallFrame, OneHander, Finesse, Kamikaze, HeavyHanded, FastShot,
    BloodyMess, Jinxed, GoodNatured, ChemReliant, ChemResistant, SexAppeal, Skilled, Gifted
}

/// <summary>Fallout 2 skills in CE order (skill.msg names are 100 + index).</summary>
public static class CharacterSkill
{
    public const int SmallGuns = 0, BigGuns = 1, EnergyWeapons = 2, Unarmed = 3, MeleeWeapons = 4, Throwing = 5, FirstAid = 6,
        Doctor = 7, Sneak = 8, Lockpick = 9, Steal = 10, Traps = 11, Science = 12, Repair = 13, Speech = 14, Barter = 15,
        Gambling = 16, Outdoorsman = 17, Count = 18;
    // CE gSkillDescriptions: default value, stat multiplier, first and second stat (-1 none); stats in SPECIAL order.
    internal static readonly (int Default, int Multiplier, int Stat1, int Stat2)[] Rules =
    {
        (5, 4, 5, -1), (0, 2, 5, -1), (0, 2, 5, -1), (30, 2, 5, 0), (20, 2, 5, 0), (0, 4, 5, -1),
        (0, 2, 1, 4), (5, 1, 1, 4), (5, 3, 5, -1), (10, 1, 1, 5), (0, 3, 5, -1), (10, 1, 1, 5),
        (0, 4, 4, -1), (0, 3, 4, -1), (0, 5, 3, -1), (0, 4, 3, -1), (0, 5, 6, -1), (0, 2, 2, 4)
    };
}

public enum CharacterKind { Premade, Created, TestNeutral, Draft }

/// <summary>Saved form of a <see cref="PlayerCharacter"/> (checkpoint v12).</summary>
public sealed record SavedCharacter([property: JsonRequired] CharacterKind Kind, [property: JsonRequired] string Name,
    [property: JsonRequired] int[] Special, [property: JsonRequired] int[] Traits, [property: JsonRequired] int[] TaggedSkills,
    [property: JsonRequired] int[] SkillPoints, [property: JsonRequired] int Age, [property: JsonRequired] int Gender, string? Premade);

/// <summary>
/// The player's character sheet, with the CE rules of stat.cc, skill.cc and trait.cc for a character without perks,
/// drugs or armor: effective SPECIAL = base + trait modifier (Gifted +1 all, Bruiser +2 ST, Small Frame +1 AG,
/// clamped 1–10), derived stats from the effective SPECIAL plus trait modifiers, skills from default + multiplier x
/// stats + points, tagged skills +20 with their points counted twice, Gifted −10% and Good Natured ±.
/// Creation follows the character editor: 40 SPECIAL points (35 + 5 to spend), up to 2 traits, exactly 3 tags.
/// </summary>
public sealed class PlayerCharacter
{
    public const int SpecialTotal = 40, MaxTraits = 2, Tags = 3, MinAge = 16, MaxAge = 35, MaxNameLength = 31, MaxSkill = 300;
    private readonly int[] _skillPoints;
    // Points bought with level-up skill points (CE skillAdd raises the same base value as the creation points).
    private readonly int[] _raises = new int[CharacterSkill.Count];
    private readonly CharacterTrait[] _traits;
    private readonly int[] _tags;
    public CharacterKind Kind { get; }
    public string Name { get; }
    public Special Base { get; }
    public IReadOnlyList<CharacterTrait> Traits => _traits;
    public IReadOnlyList<int> TaggedSkills => _tags;
    public IReadOnlyList<int> SkillPoints => _skillPoints;
    /// <summary>Base points added by spending level-up skill points; the creation sheet (<see cref="Capture"/>) leaves them out.</summary>
    public IReadOnlyList<int> SkillRaises => _raises;
    public int Age { get; }
    /// <summary>0 male, 1 female (CE STAT_GENDER).</summary>
    public int Gender { get; }
    /// <summary>Source GCD of a premade character (premade/combat.gcd …).</summary>
    public string? Premade { get; }

    private PlayerCharacter(CharacterKind kind, string name, Special special, IEnumerable<CharacterTrait> traits, IEnumerable<int> tags,
        IEnumerable<int> skillPoints, int age, int gender, string? premade)
    {
        Kind = kind; Name = name; Base = special; _traits = traits.ToArray(); _tags = tags.ToArray(); _skillPoints = skillPoints.ToArray();
        Age = age; Gender = gender; Premade = premade;
    }

    /// <summary>The profile every earlier milestone used: SPECIAL 5, no traits, tags or points (not creatable: 5 points unspent).</summary>
    public static PlayerCharacter Neutral { get; } = new(CharacterKind.TestNeutral, "Escolhido", Special.Neutral, Array.Empty<CharacterTrait>(),
        Array.Empty<int>(), new int[CharacterSkill.Count], 25, 0, null);

    /// <summary>The same character with these level-up raises; all zero gives the creation sheet itself.</summary>
    public PlayerCharacter WithSkillRaises(IReadOnlyList<int> raises)
    {
        ArgumentNullException.ThrowIfNull(raises);
        if (raises.Count != CharacterSkill.Count || raises.Any(r => r is < 0 or > MaxSkill)) throw new ArgumentOutOfRangeException(nameof(raises));
        if (raises.SequenceEqual(_raises)) return this;
        PlayerCharacter sheet = Creation;
        if (raises.All(r => r == 0)) return sheet;
        var raised = new PlayerCharacter(Kind, Name, Base, _traits, _tags, _skillPoints, Age, Gender, Premade) { Creation = sheet };
        for (int i = 0; i < raises.Count; i++) raised._raises[i] = raises[i];
        return raised;
    }
    /// <summary>The character as created or loaded from its GCD, without level-up raises.</summary>
    public PlayerCharacter Creation { get => _creation ?? this; private init => _creation = value; }
    private PlayerCharacter? _creation;

    public bool Has(CharacterTrait trait) => _traits.Contains(trait);
    public bool IsTagged(int skill) => _tags.Contains(skill);
    public int SpentSpecial => Base.ToArray().Sum();
    /// <summary>
    /// Speech tag as the Temple dialogue checks it (TempleSpeechDraft: IN 4+ and Speech tagged). The neutral test profile
    /// always ran with that branch open, so it keeps it; every real character needs the tag.
    /// </summary>
    public bool TempleSpeechTag => IsTagged(CharacterSkill.Speech) || Kind == CharacterKind.TestNeutral;
    public bool QualifiesForTempleSpeech => Effective.Intelligence >= 4 && TempleSpeechTag;

    /// <summary>Why a new character cannot start yet, or null when it is complete (character editor rules).</summary>
    public static string? CreationProblem(string? name, Special special, IReadOnlyCollection<CharacterTrait> traits, IReadOnlyCollection<int> tags, int age, int gender)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength || name.Any(c => char.IsControl(c) || c > 'ÿ')) return "nome";
        if (special.ToArray().Any(v => v is < 1 or > 10)) return "atributo fora de 1–10";
        int left = SpecialTotal - special.ToArray().Sum();
        if (left > 0) return $"{left} ponto(s) de atributo sem usar";
        if (left < 0) return "pontos de atributo demais";
        if (traits.Count > MaxTraits || traits.Distinct().Count() != traits.Count || traits.Any(t => !Enum.IsDefined(t))) return "traits";
        if (tags.Distinct().Count() != tags.Count || tags.Any(t => t is < 0 or >= CharacterSkill.Count)) return "tags";
        if (tags.Count != Tags) return $"escolha {Tags} perícias marcadas ({tags.Count} agora)";
        if (age is < MinAge or > MaxAge) return "idade";
        if (gender is not (0 or 1)) return "sexo";
        return null;
    }

    public static PlayerCharacter Create(string name, Special special, IEnumerable<CharacterTrait> traits, IEnumerable<int> tags, int age, int gender)
    {
        CharacterTrait[] t = traits.ToArray(); int[] g = tags.ToArray();
        if (CreationProblem(name, special, t, g, age, gender) is { } problem) throw new InvalidDataException($"Character cannot be created: {problem}.");
        return new(CharacterKind.Created, name.Trim(), special, t, g, new int[CharacterSkill.Count], age, gender, null);
    }

    /// <summary>
    /// An unfinished editor choice, only to preview its numbers (SPECIAL clamped to 1–10, the first traits and tags kept).
    /// A draft never starts a game nor enters a checkpoint.
    /// </summary>
    public static PlayerCharacter Draft(string? name, Special special, IEnumerable<CharacterTrait> traits, IEnumerable<int> tags, int age, int gender) =>
        new(CharacterKind.Draft, string.IsNullOrWhiteSpace(name) ? "Novo personagem" : name.Trim(),
            Special.From(special.ToArray().Select(v => Math.Clamp(v, 1, 10)).ToArray()), traits.Distinct().Take(MaxTraits),
            tags.Where(t => t is >= 0 and < CharacterSkill.Count).Distinct().Take(Tags), new int[CharacterSkill.Count],
            Math.Clamp(age, MinAge, MaxAge), gender is 0 or 1 ? gender : 0, null);

    /// <summary>A premade GCD (Narg, Mingan, Chitsa): its base SPECIAL, traits, tags, skill points, age and gender.</summary>
    public static PlayerCharacter FromPremade(CharacterFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        int[] stats = file.BaseStats;
        var special = Special.From(stats.Take(7).ToArray());
        CharacterTrait[] traits = file.Traits.Where(t => t >= 0).Select(t => (CharacterTrait)t).ToArray();
        int[] tags = file.TaggedSkills.Where(t => t >= 0).ToArray();
        if (file.CharacterPoints != 0 || CreationProblem(file.Name, special, traits, tags, stats[33], stats[34]) is { } problem)
            throw new InvalidDataException($"Premade {file.Source.Path} is not a complete character.");
        if (file.SkillPoints.Any(p => p is < 0 or > 300)) throw new InvalidDataException("Premade skill points out of range.");
        return new(CharacterKind.Premade, file.Name, special, traits, tags, file.SkillPoints, stats[33], stats[34], file.Source.Path);
    }

    public SavedCharacter Capture() => new(Kind, Name, Base.ToArray(), _traits.Select(t => (int)t).ToArray(), _tags.ToArray(),
        _skillPoints.ToArray(), Age, Gender, Premade);

    /// <summary>Rebuilds a saved character; a premade must still match its original GCD (<paramref name="premade"/>).</summary>
    public static PlayerCharacter Restore(SavedCharacter saved, Func<string, CharacterFile>? premade)
    {
        if (saved == null || saved.Name == null || saved.Special is not { Length: 7 } || saved.Traits == null || saved.TaggedSkills == null
            || saved.SkillPoints is not { Length: CharacterSkill.Count }) throw new InvalidDataException("Checkpoint character is malformed.");
        switch (saved.Kind)
        {
            case CharacterKind.TestNeutral:
                if (!Neutral.Capture().Matches(saved)) throw new InvalidDataException("Checkpoint test profile differs from the neutral profile.");
                return Neutral;
            case CharacterKind.Premade:
                if (premade == null || saved.Premade == null) throw new InvalidDataException("Checkpoint premade character has no source.");
                PlayerCharacter original = FromPremade(premade(saved.Premade));
                if (!original.Capture().Matches(saved)) throw new InvalidDataException("Checkpoint premade character differs from its GCD.");
                return original;
            case CharacterKind.Created:
                if (saved.Premade != null || saved.SkillPoints.Any(p => p != 0) || saved.Traits.Any(t => t is < 0 or >= 16))
                    throw new InvalidDataException("Checkpoint created character is invalid.");
                try { return Create(saved.Name, Special.From(saved.Special), saved.Traits.Select(t => (CharacterTrait)t), saved.TaggedSkills, saved.Age, saved.Gender); }
                catch (InvalidDataException error) { throw new InvalidDataException("Checkpoint created character breaks the creation rules.", error); }
            default: throw new InvalidDataException("Checkpoint character kind is unknown.");
        }
    }

    /// <summary>The worn armor's perk (CE _adjust_ac → perkAddEffect): its Strength joins the bonus stats while worn.</summary>
    public ArmorPerk Armor { get; private set; } = ArmorPerk.None;

    /// <summary>This sheet wearing armor with that perk; <see cref="ArmorPerk.None"/> takes it off. Never saved with the character.</summary>
    public PlayerCharacter Wearing(ArmorPerk perk)
    {
        ArgumentNullException.ThrowIfNull(perk);
        if (perk == Armor) return this;
        var worn = (PlayerCharacter)MemberwiseClone();
        // The creation sheet stays the unworn character (skill spending checks it).
        worn._creation = Creation; worn.Armor = perk;
        return worn;
    }

    /// <summary>The chosen perks (CE perkAdd): their stat and skill bonuses join this sheet. Saved apart from the character.</summary>
    public PerkRanks Perks { get; private set; } = PerkRanks.None;

    /// <summary>This sheet with these chosen perks.</summary>
    public PlayerCharacter WithPerks(PerkRanks perks)
    {
        ArgumentNullException.ThrowIfNull(perks);
        if (ReferenceEquals(perks, Perks)) return this;
        var sheet = (PlayerCharacter)MemberwiseClone();
        sheet._creation = Creation; sheet.Perks = perks;
        return sheet;
    }

    // CE traitGetStatModifier for SPECIAL; critterGetStat adds the bonus stats (the armor perk's) and, for the dude, one
    // point for each Gain perk, then clamps SPECIAL to 1–10.
    public Special Effective => Adjusted(Armor.Strength, gains: true);

    private Special Adjusted(int strengthBonus, bool gains = false)
    {
        int gifted = Has(CharacterTrait.Gifted) ? 1 : 0;
        int Gain(int perk) => gains && Perks.Has(perk) ? 1 : 0;
        int Clamp(int v) => Math.Clamp(v, 1, 10);
        return new(Clamp(Base.Strength + gifted + (Has(CharacterTrait.Bruiser) ? 2 : 0) + strengthBonus + Gain(Perk.GainStrength)),
            Clamp(Base.Perception + gifted + Gain(Perk.GainPerception)), Clamp(Base.Endurance + gifted + Gain(Perk.GainEndurance)),
            Clamp(Base.Charisma + gifted + Gain(Perk.GainCharisma)), Clamp(Base.Intelligence + gifted + Gain(Perk.GainIntelligence)),
            Clamp(Base.Agility + gifted + (Has(CharacterTrait.SmallFrame) ? 1 : 0) + Gain(Perk.GainAgility)), Clamp(Base.Luck + gifted + Gain(Perk.GainLuck)));
    }

    // CE critterUpdateDerivedStats from the SPECIAL critterGetStat gives, then traitGetStatModifier, plus the perks' bonus
    // stats (perkAddEffect); max HP alone takes the base SPECIAL with traits (critterGetBaseStatWithTraitModifier), so
    // armor and Gain perks never change it.
    public int BaseMaxHitPoints => 15 + Adjusted(0).Strength + 2 * Adjusted(0).Endurance;
    public int MaxActionPoints => 5 + Effective.Agility / 2 - (Has(CharacterTrait.Bruiser) ? 2 : 0) + Perks.StatBonus(8);
    public int ArmorClass => (Has(CharacterTrait.Kamikaze) ? 0 : Effective.Agility) + Perks.StatBonus(9);
    public int MeleeDamage => Math.Max(Effective.Strength - 5, 1) + (Has(CharacterTrait.HeavyHanded) ? 4 : 0) + Perks.StatBonus(11);
    public int CarryWeight => 25 + 25 * Effective.Strength - (Has(CharacterTrait.SmallFrame) ? 10 * Effective.Strength : 0) + Perks.StatBonus(12);
    public int Sequence => 2 * Effective.Perception + (Has(CharacterTrait.Kamikaze) ? 5 : 0) + Perks.StatBonus(13);
    public int HealingRate => Math.Max(Effective.Endurance / 3, 1) + (Has(CharacterTrait.FastMetabolism) ? 2 : 0) + Perks.StatBonus(14);
    public int CriticalChance => Effective.Luck + (Has(CharacterTrait.Finesse) ? 10 : 0) + Perks.StatBonus(15);
    /// <summary>CE STAT_BETTER_CRITICALS: Heavy Handed -30, the Better Criticals perk +20.</summary>
    public int BetterCriticals => (Has(CharacterTrait.HeavyHanded) ? -30 : 0) + Perks.StatBonus(16);
    /// <summary>The perks' bonus to a damage type's threshold (stat 17 + type) and resistance (stat 24 + type: Toughness).</summary>
    public int ThresholdBonus(int damageType) => Perks.StatBonus(17 + damageType);
    public int ResistanceBonus(int damageType) => Perks.StatBonus(24 + damageType);
    // Fast Metabolism removes the base value only; the armor perk's bonus still counts, clamped to CE's 0–95.
    public int RadiationResistance => Math.Clamp((Has(CharacterTrait.FastMetabolism) ? 0 : 2 * Effective.Endurance) + Armor.RadiationResistance + Perks.StatBonus(31), 0, 95);
    public int PoisonResistance => Math.Clamp((Has(CharacterTrait.FastMetabolism) ? 0 : 5 * Effective.Endurance) + Perks.StatBonus(32), 0, 95);

    /// <summary>CE skillGetValue for the player (normal difficulty, traits and perkGetSkillModifier), capped at 300.</summary>
    public int Skill(int index)
    {
        if (index is < 0 or >= CharacterSkill.Count) throw new ArgumentOutOfRangeException(nameof(index));
        var rule = CharacterSkill.Rules[index]; int[] stats = Effective.ToArray(); int points = _skillPoints[index] + _raises[index];
        int value = rule.Default + rule.Multiplier * (stats[rule.Stat1] + (rule.Stat2 < 0 ? 0 : stats[rule.Stat2])) + points;
        if (IsTagged(index)) value += points + 20;
        if (Has(CharacterTrait.Gifted)) value -= 10;
        if (Has(CharacterTrait.GoodNatured))
            value += index is CharacterSkill.FirstAid or CharacterSkill.Doctor or CharacterSkill.Speech or CharacterSkill.Barter ? 15
                : index <= CharacterSkill.Throwing ? -10 : 0;
        value += Perks.SkillModifier(index);
        return Math.Min(value, 300);
    }
    public int[] Skills => Enumerable.Range(0, CharacterSkill.Count).Select(Skill).ToArray();

    // CE pcAddExperience / character editor: HP and skill points per level from the base EN and IN with traits (Gain perks
    // and armor do not count), perk interval. Lifegiver and Educated add to the levels gained after they were chosen.
    public int HitPointsPerLevel => Adjusted(0).Endurance / 2 + 2;
    public int SkillPointsPerLevel => 5 + 2 * Adjusted(0).Intelligence + (Has(CharacterTrait.Skilled) ? 5 : 0) - (Has(CharacterTrait.Gifted) ? 5 : 0);
    /// <summary>What reaching <paramref name="level"/> grants: HP per level plus 4 per Lifegiver rank chosen before it.</summary>
    public int HitPointsForLevel(int level) => HitPointsPerLevel + 4 * Perks.RankBefore(Perk.Lifegiver, level);
    /// <summary>Skill points reaching <paramref name="level"/> grants: per level plus 2 per Educated rank chosen before it (Gifted's floor at 0).</summary>
    public int SkillPointsForLevel(int level) => Math.Max(0, SkillPointsPerLevel + 2 * Perks.RankBefore(Perk.Educated, level));
    public int PerkInterval => Has(CharacterTrait.Skilled) ? 4 : 3;
}

internal static class SavedCharacterComparison
{
    public static bool Matches(this SavedCharacter a, SavedCharacter b) => a.Kind == b.Kind && a.Name == b.Name && a.Special.SequenceEqual(b.Special)
        && a.Traits.SequenceEqual(b.Traits) && a.TaggedSkills.SequenceEqual(b.TaggedSkills) && a.SkillPoints.SequenceEqual(b.SkillPoints)
        && a.Age == b.Age && a.Gender == b.Gender && a.Premade == b.Premade;
}
