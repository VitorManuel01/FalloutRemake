using System;

namespace FalloutRemake.Runtime.Combat;

/// <summary>
/// One round of CE attackComputeDamage on normal difficulty, no perks or criticals:
/// roll(min, max + Melee Damage for melee/unarmed) x 2 x ammo multiplier / ammo divisor / 2, minus DT, then
/// damage -= damage x DR / 100 with integer truncation (DR includes the ammo modifier, clamped 0..100).
/// </summary>
public static class DamageCalculator
{
    // CE passes 2 for a normal hit; criticals replace it with the critical table's multiplier (not modeled yet).
    public const int NormalHitMultiplier = 2;

    public static (int Min, int Max) RollRange(CombatantState attacker, CombatWeapon weapon) =>
        (weapon.MinDamage, weapon.MaxDamage + (weapon.AddsMeleeDamage ? attacker.Stats.MeleeDamage : 0));

    public static int Threshold(CombatantState target, CombatWeapon weapon) => target.Stats.Threshold(weapon.DamageType);

    public static int Resistance(CombatantState target, CombatWeapon weapon) =>
        Math.Clamp(target.Stats.Resistance(weapon.DamageType) + (weapon.Ammo?.DamageResistanceModifier ?? 0), 0, 100);

    public static int Apply(int roll, CombatWeapon weapon, int threshold, int resistance, int multiplier = NormalHitMultiplier)
    {
        if (threshold < 0 || resistance is < 0 or > 100 || multiplier < 0) throw new ArgumentOutOfRangeException(nameof(threshold));
        long damage = (long)roll * multiplier * (weapon.Ammo?.DamageMultiplier ?? 1);
        int divisor = weapon.Ammo?.DamageDivisor ?? 1;
        if (divisor != 0) damage /= divisor;
        damage /= 2;
        damage -= threshold;
        if (damage > 0) damage -= damage * resistance / 100;
        return (int)Math.Max(0, damage);
    }

    public static DamageEstimate Estimate(CombatantState attacker, CombatantState target)
    {
        CombatWeapon weapon = attacker.Weapon;
        var (min, max) = RollRange(attacker, weapon);
        int threshold = Threshold(target, weapon), resistance = Resistance(target, weapon);
        // Apply is monotonic in the roll, so the range ends are the extremes.
        return new DamageEstimate(weapon.DamageType, min, max, threshold, resistance,
            Apply(min, weapon, threshold, resistance), Apply(max, weapon, threshold, resistance));
    }
}
