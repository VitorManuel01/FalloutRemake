using System;
using FalloutRemake.Geometry;

namespace FalloutRemake.Runtime.Combat;

/// <summary>
/// Hit chance from CE attackDetermineToHit, no perks, traits, lighting or line-of-fire crowding: the hit location's
/// penalty (halved for melee and unarmed), +40 against a prone target, -25 for a blind attacker. The only project rule is
/// the final clamp: CE caps at 95 with no floor; the roadmap fixes 5..95.
/// </summary>
public static class CombatResolver
{
    public const int MinimumChance = 5, MaximumChance = 95, BlindPenalty = 25;

    public static int Reach(CombatantState attacker, CombatantState target) => FalloutGrid.Distance(attacker.Hex, target.Hex);

    /// <param name="fromHex">_determine_to_hit_from_tile: as if the attacker stood there.</param>
    /// <param name="useDistance">False for _determine_to_hit_no_range: no range term, but CE still adds a positive perception bonus.</param>
    public static ToHitBreakdown ToHit(CombatantState attacker, CombatantState target, HitLocation location = HitLocation.Uncalled,
        int? fromHex = null, bool useDistance = true)
    {
        CombatWeapon weapon = attacker.Weapon;
        int skill = attacker.Stats.Skill(weapon.SkillIndex), distance = 0;
        if (weapon.IsRanged)
        {
            // Default weapons (no long-range/scope perk): perception multiplier 2, no minimum effective distance.
            int perception = attacker.Perception;
            int modifier = (useDistance ? FalloutGrid.Distance(fromHex ?? attacker.Hex, target.Hex) : 0)
                - (attacker.IsPlayer ? 2 * (perception - 2) : 2 * perception);
            modifier = Math.Max(modifier, -2 * perception);
            distance = modifier * (modifier >= 0 && (attacker.Conditions & DamageFlags.Blind) != 0 ? -12 : -4);
            if (!useDistance && distance <= 0) distance = 0;
        }
        int strength = Math.Max(0, weapon.MinStrength - attacker.Stats.Special.Strength) * 20;
        int armorClass = Math.Max(0, target.EffectiveArmorClass + (weapon.Ammo?.ArmorClassModifier ?? 0));
        int penalty = weapon.IsRanged ? CriticalRules.Penalty(location) : CriticalRules.Penalty(location) / 2;
        int prone = target.Prone ? CriticalRules.ProneTargetBonus : 0;
        int blind = (attacker.Conditions & DamageFlags.Blind) != 0 ? BlindPenalty : 0;
        // Raw stays uncapped for the HUD's arithmetic; CE's own value is Math.Min(Raw, 95).
        int raw = skill + distance - strength - armorClass + penalty - blind + prone;
        return new ToHitBreakdown(skill, weapon.SkillIndex, distance, strength, armorClass, raw, Math.Clamp(raw, MinimumChance, MaximumChance), prone, penalty, blind);
    }

    /// <summary>Percentile hit: rolls 1–100 and hits when the roll does not exceed the chance.</summary>
    public static bool Hits(int chance, int roll) => roll is >= 1 and <= 100 ? roll <= chance : throw new ArgumentOutOfRangeException(nameof(roll));
}
