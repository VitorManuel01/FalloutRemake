using System;
using FalloutRemake.Geometry;

namespace FalloutRemake.Runtime.Combat;

/// <summary>
/// Hit chance from CE attackDetermineToHit, torso target, no perks, traits, lighting, line-of-fire crowding or
/// knockdown. The only project rule is the final clamp: CE caps at 95 with no floor; the roadmap fixes 5..95.
/// </summary>
public static class CombatResolver
{
    public const int MinimumChance = 5, MaximumChance = 95;

    public static int Reach(CombatantState attacker, CombatantState target) => FalloutGrid.Distance(attacker.Hex, target.Hex);

    public static ToHitBreakdown ToHit(CombatantState attacker, CombatantState target)
    {
        CombatWeapon weapon = attacker.Weapon;
        int skill = attacker.Stats.Skill(weapon.SkillIndex), distance = 0;
        if (weapon.IsRanged)
        {
            // Default weapons (no long-range/scope perk): perception multiplier 2, no minimum effective distance.
            int perception = attacker.Stats.Special.Perception;
            int modifier = Reach(attacker, target) - (attacker.IsPlayer ? 2 * (perception - 2) : 2 * perception);
            distance = Math.Max(modifier, -2 * perception) * -4;
        }
        int strength = Math.Max(0, weapon.MinStrength - attacker.Stats.Special.Strength) * 20;
        int armorClass = Math.Max(0, target.EffectiveArmorClass + (weapon.Ammo?.ArmorClassModifier ?? 0));
        int raw = skill + distance - strength - armorClass;
        return new ToHitBreakdown(skill, weapon.SkillIndex, distance, strength, armorClass, raw, Math.Clamp(raw, MinimumChance, MaximumChance));
    }

    /// <summary>Percentile hit: rolls 1–100 and hits when the roll does not exceed the chance.</summary>
    public static bool Hits(int chance, int roll) => roll is >= 1 and <= 100 ? roll <= chance : throw new ArgumentOutOfRangeException(nameof(roll));
}
