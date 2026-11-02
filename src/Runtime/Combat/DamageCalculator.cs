using System;

namespace FalloutRemake.Runtime.Combat;

/// <summary>
/// One round of CE attackComputeDamage on normal difficulty, no perks:
/// roll(min, max + Melee Damage for melee/unarmed) x multiplier x ammo multiplier / ammo divisor / 2, minus DT, then
/// damage -= damage x DR / 100 with integer truncation (DR includes the ammo modifier, clamped 0..100).
/// A critical that bypasses armor keeps 20% of DT and DR (not against EMP); otherwise the player's Finesse adds 30 DR.
/// </summary>
public static class DamageCalculator
{
    // CE passes 2 for a normal hit; a critical passes its table row's multiplier instead.
    public const int NormalHitMultiplier = 2;

    // weaponGetDamage: bonus + min .. bonus + Melee Damage + max (the bonus of an advanced unarmed blow).
    public static (int Min, int Max) RollRange(CombatantState attacker, CombatWeapon weapon) =>
        (weapon.BonusDamage + weapon.MinDamage, weapon.BonusDamage + weapon.MaxDamage + (weapon.AddsMeleeDamage ? attacker.Stats.MeleeDamage : 0));

    static bool Bypasses(CombatWeapon weapon, bool bypass) => bypass && weapon.DamageType != DamageType.Emp;

    public static int Threshold(CombatantState target, CombatWeapon weapon, bool bypass = false)
    {
        int threshold = target.Stats.Threshold(weapon.DamageType);
        // A penetrating blow keeps 20% of DT on any hit; a bypassing critical keeps 20% of both DT and DR.
        return Bypasses(weapon, bypass) || weapon.Penetrates ? 20 * threshold / 100 : threshold;
    }

    /// <param name="finesse">The attacker is the player with Finesse (CE applies it to whatever the attack damages).</param>
    public static int Resistance(CombatantState target, CombatWeapon weapon, bool bypass = false, bool finesse = false)
    {
        int resistance = target.Stats.Resistance(weapon.DamageType);
        if (Bypasses(weapon, bypass)) resistance = 20 * resistance / 100;
        else if (finesse) resistance += 30;
        return Math.Clamp(resistance + (weapon.Ammo?.DamageResistanceModifier ?? 0), 0, 100);
    }

    public static bool HasFinesse(CombatantState attacker) => attacker.IsPlayer && attacker.Stats.Finesse;

    /// <param name="bonus">Added to the roll first (Bonus Ranged Damage: 2 per rank on the dude's firearm shots).</param>
    /// <param name="after">Added to the result (Living Anatomy, Pyromaniac: +5 each).</param>
    public static int Apply(int roll, CombatWeapon weapon, int threshold, int resistance, int multiplier = NormalHitMultiplier, int bonus = 0, int after = 0)
    {
        if (threshold < 0 || resistance is < 0 or > 100 || multiplier < 0) throw new ArgumentOutOfRangeException(nameof(threshold));
        long damage = (long)(roll + bonus) * multiplier * (weapon.Ammo?.DamageMultiplier ?? 1);
        int divisor = weapon.Ammo?.DamageDivisor ?? 1;
        if (divisor != 0) damage /= divisor;
        damage /= 2;
        damage -= threshold;
        if (damage > 0) damage -= damage * resistance / 100;
        return (int)Math.Max(0, damage) + after;
    }

    /// <summary>attackComputeDamage's damage bonus: Bonus Ranged Damage, 2 per rank, on the dude's firearm (not thrown) attacks.</summary>
    public static int RollBonus(CombatantState attacker) => attacker.IsPlayer && attacker.Weapon.AttackType == FalloutRemake.Import.WeaponAttackType.Ranged
        ? 2 * attacker.Stats.Perks.Rank(FalloutRemake.Runtime.Perk.BonusRangedDamage) : 0;

    /// <summary>
    /// What CE adds after the rounds: Living Anatomy +5 against anything but robots and aliens (kill types 10 and 16),
    /// Pyromaniac +5 with fire damage, both for the dude.
    /// </summary>
    public static int AfterBonus(CombatantState attacker, CombatantState target)
    {
        if (!attacker.IsPlayer) return 0;
        int bonus = 0;
        if (attacker.Stats.Perks.Has(FalloutRemake.Runtime.Perk.LivingAnatomy) && target.KillType is not (10 or 16)) bonus += 5;
        if (attacker.Stats.Perks.Has(FalloutRemake.Runtime.Perk.Pyromaniac) && attacker.Weapon.DamageType == DamageType.Fire) bonus += 5;
        return bonus;
    }

    public static DamageEstimate Estimate(CombatantState attacker, CombatantState target)
    {
        CombatWeapon weapon = attacker.Weapon;
        var (min, max) = RollRange(attacker, weapon);
        int threshold = Threshold(target, weapon), resistance = Resistance(target, weapon, finesse: HasFinesse(attacker));
        int bonus = RollBonus(attacker), after = AfterBonus(attacker, target);
        // Apply is monotonic in the roll, so the range ends are the extremes.
        return new DamageEstimate(weapon.DamageType, min, max, threshold, resistance,
            Apply(min, weapon, threshold, resistance, bonus: bonus, after: after), Apply(max, weapon, threshold, resistance, bonus: bonus, after: after));
    }
}
