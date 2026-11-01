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
    public const int SpecialTotal = 40, MaxTraits = 2, Tags = 3, MinAge = 16, MaxAge = 35, MaxNameLength = 31;
    private readonly int[] _skillPoints;
    private readonly CharacterTrait[] _traits;
    private readonly int[] _tags;
    public CharacterKind Kind { get; }
    public string Name { get; }
    public Special Base { get; }
    public IReadOnlyList<CharacterTrait> Traits => _traits;
    public IReadOnlyList<int> TaggedSkills => _tags;
    public IReadOnlyList<int> SkillPoints => _skillPoints;
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

    // CE traitGetStatModifier for SPECIAL; critterGetStat clamps SPECIAL to 1–10.
    public Special Effective
    {
        get
        {
            int gifted = Has(CharacterTrait.Gifted) ? 1 : 0;
            int Clamp(int v) => Math.Clamp(v, 1, 10);
            return new(Clamp(Base.Strength + gifted + (Has(CharacterTrait.Bruiser) ? 2 : 0)), Clamp(Base.Perception + gifted), Clamp(Base.Endurance + gifted),
                Clamp(Base.Charisma + gifted), Clamp(Base.Intelligence + gifted), Clamp(Base.Agility + gifted + (Has(CharacterTrait.SmallFrame) ? 1 : 0)),
                Clamp(Base.Luck + gifted));
        }
    }

    // CE critterUpdateDerivedStats from the trait-adjusted SPECIAL, then traitGetStatModifier.
    public int BaseMaxHitPoints => 15 + Effective.Strength + 2 * Effective.Endurance;
    public int MaxActionPoints => 5 + Effective.Agility / 2 - (Has(CharacterTrait.Bruiser) ? 2 : 0);
    public int ArmorClass => Has(CharacterTrait.Kamikaze) ? 0 : Effective.Agility;
    public int MeleeDamage => Math.Max(Effective.Strength - 5, 1) + (Has(CharacterTrait.HeavyHanded) ? 4 : 0);
    public int CarryWeight => 25 + 25 * Effective.Strength - (Has(CharacterTrait.SmallFrame) ? 10 * Effective.Strength : 0);
    public int Sequence => 2 * Effective.Perception + (Has(CharacterTrait.Kamikaze) ? 5 : 0);
    public int HealingRate => Math.Max(Effective.Endurance / 3, 1) + (Has(CharacterTrait.FastMetabolism) ? 2 : 0);
    public int CriticalChance => Effective.Luck + (Has(CharacterTrait.Finesse) ? 10 : 0);
    public int RadiationResistance => Has(CharacterTrait.FastMetabolism) ? 0 : 2 * Effective.Endurance;
    public int PoisonResistance => Has(CharacterTrait.FastMetabolism) ? 0 : 5 * Effective.Endurance;

    /// <summary>CE skillGetValue for the player (normal difficulty, no perks), capped at 300.</summary>
    public int Skill(int index)
    {
        if (index is < 0 or >= CharacterSkill.Count) throw new ArgumentOutOfRangeException(nameof(index));
        var rule = CharacterSkill.Rules[index]; int[] stats = Effective.ToArray(); int points = _skillPoints[index];
        int value = rule.Default + rule.Multiplier * (stats[rule.Stat1] + (rule.Stat2 < 0 ? 0 : stats[rule.Stat2])) + points;
        if (IsTagged(index)) value += points + 20;
        if (Has(CharacterTrait.Gifted)) value -= 10;
        if (Has(CharacterTrait.GoodNatured))
            value += index is CharacterSkill.FirstAid or CharacterSkill.Doctor or CharacterSkill.Speech or CharacterSkill.Barter ? 15
                : index <= CharacterSkill.Throwing ? -10 : 0;
        return Math.Min(value, 300);
    }
    public int[] Skills => Enumerable.Range(0, CharacterSkill.Count).Select(Skill).ToArray();

    // CE pcAddExperience / character editor: HP and skill points per level, perk interval.
    public int HitPointsPerLevel => Effective.Endurance / 2 + 2;
    public int SkillPointsPerLevel => 5 + 2 * Effective.Intelligence + (Has(CharacterTrait.Skilled) ? 5 : 0) - (Has(CharacterTrait.Gifted) ? 5 : 0);
    public int PerkInterval => Has(CharacterTrait.Skilled) ? 4 : 3;
}

internal static class SavedCharacterComparison
{
    public static bool Matches(this SavedCharacter a, SavedCharacter b) => a.Kind == b.Kind && a.Name == b.Name && a.Special.SequenceEqual(b.Special)
        && a.Traits.SequenceEqual(b.Traits) && a.TaggedSkills.SequenceEqual(b.TaggedSkills) && a.SkillPoints.SequenceEqual(b.SkillPoints)
        && a.Age == b.Age && a.Gender == b.Gender && a.Premade == b.Premade;
}
