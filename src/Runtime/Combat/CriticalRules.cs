using System;
using static FalloutRemake.Runtime.Combat.DamageFlags;

namespace FalloutRemake.Runtime.Combat;

/// <summary>CE damage flags: a critter's lasting combat results and the per-attack flags of the critical tables.</summary>
[Flags]
public enum DamageFlags
{
    None = 0, KnockedOut = 0x01, KnockedDown = 0x02, CrippledLeftLeg = 0x04, CrippledRightLeg = 0x08, CrippledLeftArm = 0x10,
    CrippledRightArm = 0x20, Blind = 0x40, Dead = 0x80, OnFire = 0x400, Bypass = 0x800, Explode = 0x1000, Destroy = 0x2000,
    Drop = 0x4000, LoseTurn = 0x8000, HitSelf = 0x10000, LoseAmmo = 0x20000, Dud = 0x40000, HurtSelf = 0x80000,
    RandomHit = 0x100000, CrippleRandom = 0x200000,
    CrippledLegs = CrippledLeftLeg | CrippledRightLeg, CrippledArms = CrippledLeftArm | CrippledRightArm,
    Crippled = CrippledLegs | CrippledArms | Blind,
    /// <summary>What CE _set_new_results keeps on the critter after an attack.</summary>
    Results = KnockedOut | KnockedDown | Crippled | Dead | LoseTurn
}

/// <summary>One row of CE gCriticalHitTables / gPlayerCriticalHitTable (CriticalHitDescription); MassiveStat -1 means no massive check.</summary>
public sealed record CriticalEffect(int Multiplier, DamageFlags Flags, int MassiveStat, int MassiveModifier, DamageFlags MassiveFlags, int Message, int MassiveMessage);

/// <summary>
/// CE critical hits and failures. randomTranslateRoll upgrades a hit or a miss only from game day 1 (the clock starts on
/// day 0 at 302400 ticks), the dude's failures only bite from day 6 (attackComputeCriticalFailure). Attacks are uncalled,
/// which CE turns into the torso, so only the torso rows apply. Every row, the failure table and the start time were
/// checked against fallout2.exe (--critical-report).
/// </summary>
public static class CriticalRules
{
    private const DamageFlags N = None;
    public const long GameStartTicks = 302400, TicksPerDay = 864000;
    /// <summary>CE ends the game in year 13 (gameTimeAddTicks); the clock never reaches it in the supported maps.</summary>
    public const long GameEndTicks = 13L * 365 * TicksPerDay;
    public const int CriticalsFromDay = 1, PlayerFailuresFromDay = 6;
    // hit_location_penalty[HIT_LOCATION_TORSO]; attackDetermineToHit bonus against a prone target; _combat_standup cost.
    public const int TorsoPenalty = 0, ProneTargetBonus = 40, StandUpActionPoints = 3;

    public static bool CriticalsAllowed(long gameTime) => gameTime / TicksPerDay >= CriticalsFromDay;
    public static bool PlayerFailuresAllowed(long gameTime) => gameTime / TicksPerDay >= PlayerFailuresFromDay;
    /// <summary>_set_new_results: the knockout event fires 10 x (35 - 3 x EN) ticks later.</summary>
    public static int KnockoutTicks(int endurance) => 10 * (35 - 3 * endurance);
    /// <summary>attackComputeCriticalHit: d100 + Better Criticals picks one of six rows.</summary>
    public static int Effect(int roll) => roll <= 20 ? 0 : roll <= 45 ? 1 : roll <= 70 ? 2 : roll <= 90 ? 3 : roll <= 100 ? 4 : 5;
    /// <summary>attackComputeCriticalFailure: d100 - 5 x (LK - 5) picks one of five columns.</summary>
    public static int FailureEffect(int roll) => roll <= 20 ? 0 : roll <= 50 ? 1 : roll <= 75 ? 2 : roll <= 95 ? 3 : 4;
    /// <summary>Movement AP per hex (critterGetMovementPointCostAdjustedForCrippledLegs).</summary>
    public static int ActionPointsPerHex(DamageFlags results) =>
        (results & CrippledLegs) == CrippledLegs ? 8 : (results & CrippledLegs) != 0 ? 4 : 1;
    /// <summary>_do_random_cripple: 0 left leg, 1 right leg, 2 left arm, 3 right arm.</summary>
    public static DamageFlags RandomCripple(int roll) => roll switch
    {
        0 => CrippledLeftLeg, 1 => CrippledRightLeg, 2 => CrippledLeftArm, 3 => CrippledRightArm,
        _ => throw new ArgumentOutOfRangeException(nameof(roll))
    };

    public const int KillTypeCount = 19;
    /// <summary>Torso row for the defender: the player's own table, or the critter PRO kill type.</summary>
    public static CriticalEffect Torso(bool player, int killType, int effect)
    {
        if (effect is < 0 or > 5) throw new ArgumentOutOfRangeException(nameof(effect));
        if (player) return PlayerTorso[effect];
        if (killType is < 0 or >= KillTypeCount) throw new ArgumentOutOfRangeException(nameof(killType));
        return KillTypeTorso[killType][effect];
    }

    /// <summary>_cf_table row by the weapon PRO critical failure type (-1, no weapon, uses row 0).</summary>
    public static DamageFlags Failure(int type, int effect)
    {
        int row = type == -1 ? 0 : type;
        if (row is < 0 or >= 7 || effect is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(type));
        return FailureTable[row, effect];
    }

    // gPlayerCriticalHitTable[HIT_LOCATION_TORSO].
    internal static readonly CriticalEffect[] PlayerTorso =
        { new(3, N, -1, 0, N, 6512, 5000), new(3, N, -1, 0, N, 6512, 5000), new(3, Bypass, -1, 0, N, 6508, 5000), new(3, KnockedDown | Bypass, -1, 0, N, 6503, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 6503, 5000), new(4, KnockedDown | Bypass, 6, 2, Dead, 6503, 6513) };

    // gCriticalHitTables[killType][HIT_LOCATION_TORSO], kill types in CE order.
    internal static readonly CriticalEffect[][] KillTypeTorso =
    {
        // 0 Man
        new CriticalEffect[] { new(3, N, -1, 0, N, 5016, 5000), new(3, Bypass, -1, 0, N, 5017, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 5019, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 5019, 5000), new(6, KnockedOut | Bypass, -1, 0, N, 5020, 5000), new(6, Dead, -1, 0, N, 5021, 5000) },
        // 1 Woman
        new CriticalEffect[] { new(3, N, -1, 0, N, 5116, 5100), new(3, Bypass, -1, 0, N, 5117, 5100), new(4, KnockedDown | Bypass, -1, 0, N, 5119, 5100), new(4, KnockedDown | Bypass, -1, 0, N, 5119, 5100), new(6, KnockedOut | Bypass, -1, 0, N, 5120, 5100), new(6, Dead, -1, 0, N, 5121, 5100) },
        // 2 Child
        new CriticalEffect[] { new(3, N, -1, 0, N, 5210, 5000), new(4, Bypass, -1, 0, N, 5211, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 5212, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 5212, 5000), new(4, KnockedOut | Bypass, -1, 0, N, 5213, 5000), new(6, Dead, -1, 0, N, 5214, 5000) },
        // 3 Super Mutant
        new CriticalEffect[] { new(3, N, -1, 0, N, 5300, 5000), new(3, Bypass, -1, 0, N, 5301, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 5302, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 5302, 5000), new(4, KnockedOut | Bypass, -1, 0, N, 5310, 5000), new(6, Dead, -1, 0, N, 5311, 5000) },
        // 4 Ghoul
        new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, Bypass, -1, 0, N, 5017, 5000), new(3, N, -1, 0, N, 5018, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 5004, 5000), new(4, KnockedOut | Bypass, -1, 0, N, 5003, 5000), new(6, Dead, -1, 0, N, 5007, 5000) },
        // 5 Brahmin
        new CriticalEffect[] { new(3, N, -1, 0, N, 5504, 5000), new(3, N, -1, 0, N, 5504, 5000), new(4, N, -1, 0, N, 5504, 5000), new(4, Bypass, -1, 0, N, 5505, 5000), new(4, Bypass, -1, 0, N, 5505, 5000), new(6, Dead, -1, 0, N, 5506, 5000) },
        // 6 Radscorpion
        new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, Bypass, -1, 0, N, 5604, 5000), new(4, N, -1, 0, N, 5016, 5000), new(4, Bypass, -1, 0, N, 5605, 5000), new(4, Bypass, 5, 0, KnockedDown, 5605, 5606), new(4, Dead, -1, 0, N, 5607, 5000) },
        // 7 Rat
        new CriticalEffect[] { new(3, N, -1, 0, N, 5706, 5000), new(3, KnockedDown, -1, 0, N, 5707, 5000), new(3, KnockedDown, -1, 0, N, 5707, 5000), new(4, KnockedDown, -1, 0, N, 5707, 5000), new(4, KnockedDown, -1, 0, N, 5707, 5000), new(4, Dead, -1, 0, N, 5708, 5000) },
        // 8 Floater
        new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, 5, 0, KnockedDown, 5001, 5800), new(3, N, 5, -2, KnockedDown, 5001, 5800), new(4, KnockedDown, -1, 0, N, 5800, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 5804, 5000), new(4, Dead, -1, 0, N, 5805, 5000) },
        // 9 Centaur
        new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(4, N, -1, 0, N, 5001, 5000), new(4, Bypass, -1, 0, N, 5901, 5000), new(4, Bypass, 2, 2, N, 5901, 5900), new(5, KnockedDown | Bypass, -1, 0, N, 5900, 5000), new(5, Dead, -1, 0, N, 5902, 5000) },
        // 10 Robot
        new CriticalEffect[] { new(3, N, -1, 0, N, 6000, 5000), new(3, Bypass, -1, 0, N, 6006, 5000), new(4, N, -1, 0, N, 6007, 5000), new(4, Bypass, -1, 0, N, 6008, 5000), new(6, Bypass, -1, 0, N, 6009, 5000), new(6, Dead, -1, 0, N, 6010, 5000) },
        // 11 Dog
        new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, 5, -1, KnockedDown, 5001, 6100), new(4, N, -1, 0, N, 5016, 5000), new(4, N, 5, -3, KnockedDown, 5016, 6100), new(4, KnockedDown, -1, 0, N, 6100, 5000), new(6, Dead, -1, 0, N, 6103, 5000) },
        // 12 Mantis
        new CriticalEffect[] { new(3, N, -1, 0, N, 1000, 5000), new(3, N, 2, 0, Bypass, 5001, 6205), new(3, N, 2, -2, Bypass, 5001, 6205), new(4, N, 2, -2, Bypass, 5016, 6205), new(4, N, 2, -4, Bypass, 5016, 6205), new(6, Dead, -1, 0, N, 6206, 5000) },
        // 13 Deathclaw
        new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, 2, -1, Bypass, 5001, 6300), new(4, N, -1, 0, N, 5016, 5000), new(4, N, 2, -1, Bypass, 5016, 6300), new(5, KnockedDown | Bypass, -1, 0, N, 5004, 5000), new(5, KnockedDown | Bypass | LoseTurn, -1, 0, N, 5005, 5000) },
        // 14 Plant
        new CriticalEffect[] { new(3, N, -1, 0, N, 6405, 5000), new(3, N, -1, 0, N, 6405, 5000), new(3, Bypass, -1, 0, N, 6400, 5000), new(4, N, -1, 0, N, 6401, 5000), new(4, N, -1, 0, N, 6401, 5000), new(4, Bypass, -1, 0, N, 6402, 5000) },
        // 15 Gecko
        new CriticalEffect[] { new(3, N, -1, 0, N, 6701, 5000), new(3, Bypass, -1, 0, N, 6701, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 6704, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 6704, 5000), new(6, KnockedOut | Bypass, -1, 0, N, 6704, 5000), new(6, Dead, -1, 0, N, 6704, 5000) },
        // 16 Alien
        new CriticalEffect[] { new(3, N, -1, 0, N, 6800, 5000), new(3, Bypass, -1, 0, N, 6800, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 6800, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 6800, 5000), new(6, KnockedOut | Bypass, -1, 0, N, 6800, 5000), new(6, Dead, -1, 0, N, 6800, 5000) },
        // 17 Giant Ant
        new CriticalEffect[] { new(3, N, -1, 0, N, 6900, 5000), new(3, Bypass, -1, 0, N, 6900, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 6904, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 6904, 5000), new(6, KnockedOut | Bypass, -1, 0, N, 6904, 5000), new(6, Dead, -1, 0, N, 6904, 5000) },
        // 18 Big Bad Boss
        new CriticalEffect[] { new(3, N, -1, 0, N, 7106, 7100), new(3, N, -1, 0, N, 7106, 7100), new(3, N, -1, 0, N, 7106, 7100), new(4, N, -1, 0, N, 7106, 7100), new(4, KnockedDown, -1, 0, N, 7106, 7100), new(5, KnockedDown, -1, 0, N, 7106, 7100) },
    };

    // _cf_table[weapon critical failure type][effect].
    internal static readonly DamageFlags[,] FailureTable =
    {
        { N, LoseTurn, LoseTurn, KnockedDown | HurtSelf, CrippleRandom },
        { N, LoseTurn, Drop, RandomHit, HitSelf },
        { N, LoseAmmo, Drop, RandomHit, Destroy },
        { LoseTurn, LoseTurn | LoseAmmo, Drop | LoseTurn, RandomHit, Explode | LoseTurn },
        { Dud, Drop, Drop | HurtSelf, RandomHit, Explode },
        { LoseTurn, Dud, Destroy, RandomHit, KnockedDown | Explode | LoseTurn },
        { N, LoseTurn, RandomHit, Destroy, OnFire | Explode | LoseTurn },
    };
}
