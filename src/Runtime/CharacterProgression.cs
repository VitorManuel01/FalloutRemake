using System;
using System.Collections.Generic;
using FalloutRemake.Runtime.Combat;

namespace FalloutRemake.Runtime;

/// <summary>What one new level grants (CE character editor and pcAddExperience, no perks or traits).</summary>
public sealed record LevelGain(int Level, int HitPoints, int SkillPoints, bool PerkAvailable);

/// <summary>
/// Fallout 2 level progression for the neutral character, as CE: thresholds of pcGetExperienceForLevel
/// (1000 x triangular numbers), +EN/2+2 max HP per level with current HP rising by the same amount,
/// 5 + 2 x IN skill points per level (unspent total capped at 99) and a perk every third level.
/// Spending skill points and choosing perks are not modeled yet, so both are derived totals.
/// </summary>
public static class CharacterProgression
{
    public const int MaxLevel = 99, MaxUnspentSkillPoints = 99, PerkInterval = 3;

    public static int ExperienceForLevel(int level)
    {
        if (level is < 1 or > MaxLevel) throw new ArgumentOutOfRangeException(nameof(level));
        int half = level / 2;
        return (level & 1) != 0 ? 1000 * half * level : 1000 * half * (level - 1);
    }

    public static int Level(int experience)
    {
        if (experience < 0) throw new ArgumentOutOfRangeException(nameof(experience));
        int level = 1;
        while (level < MaxLevel && experience >= ExperienceForLevel(level + 1)) level++;
        return level;
    }

    public static int HitPointsPerLevel(Special special) => special.Endurance / 2 + 2;
    public static int SkillPointsPerLevel(Special special) => 5 + 2 * special.Intelligence;
    public static int MaxHitPoints(Special special, int level) =>
        15 + special.Strength + 2 * special.Endurance + (CheckedLevel(level) - 1) * HitPointsPerLevel(special);
    public static int UnspentSkillPoints(Special special, int level) =>
        Math.Min(MaxUnspentSkillPoints, (CheckedLevel(level) - 1) * SkillPointsPerLevel(special));
    public static int PerksAvailable(int level) => CheckedLevel(level) / PerkInterval;

    public static IReadOnlyList<LevelGain> Gains(Special special, int fromLevel, int toLevel)
    {
        var gains = new List<LevelGain>();
        for (int level = CheckedLevel(fromLevel) + 1; level <= CheckedLevel(toLevel); level++)
            gains.Add(new LevelGain(level, HitPointsPerLevel(special), SkillPointsPerLevel(special), level % PerkInterval == 0));
        return gains;
    }

    static int CheckedLevel(int level) => level is >= 1 and <= MaxLevel ? level : throw new ArgumentOutOfRangeException(nameof(level));
}
