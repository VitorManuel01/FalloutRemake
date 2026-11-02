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
/// day 0 at 302400 ticks), the dude's failures only bite from day 6 (attackComputeCriticalFailure). Both sides aim (the
/// player through the called-shot window, creatures through their AI packet), so the player's table and every kill type's
/// nine locations are whole; an uncalled blow reads the torso, as attackCompute converts it. Every row, the failure table
/// and the start time were checked against fallout2.exe (--critical-report).
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
        if (player) return PlayerTable[(int)HitLocation.Torso][effect];
        return Critter(killType, HitLocation.Torso, effect);
    }

    /// <summary>A critter's row for any hit location by its PRO kill type; uncalled reads the torso.</summary>
    public static CriticalEffect Critter(int killType, HitLocation location, int effect)
    {
        if (killType is < 0 or >= KillTypeCount) throw new ArgumentOutOfRangeException(nameof(killType));
        if (effect is < 0 or > 5 || location is < HitLocation.Head or > HitLocation.Uncalled) throw new ArgumentOutOfRangeException(nameof(effect));
        return KillTypeTable[killType][(int)(location == HitLocation.Uncalled ? HitLocation.Torso : location)][effect];
    }

    /// <summary>The player's row for any hit location (an aimed blow from a creature); uncalled reads the torso, as attackCompute converts it.</summary>
    public static CriticalEffect Player(HitLocation location, int effect)
    {
        if (effect is < 0 or > 5 || location is < HitLocation.Head or > HitLocation.Uncalled) throw new ArgumentOutOfRangeException(nameof(effect));
        return PlayerTable[(int)(location == HitLocation.Uncalled ? HitLocation.Torso : location)][effect];
    }

    /// <summary>hit_location_penalty_default: to-hit adjustment (halved for melee and unarmed) and critical chance reduction.</summary>
    public static int Penalty(HitLocation location) => HitLocationPenalty[(int)location];
    internal static readonly int[] HitLocationPenalty = { -40, -30, -30, 0, -20, -20, -60, -30, 0 };

    // gPlayerCriticalHitTable, rows in HitLocation order.
    internal static readonly CriticalEffect[][] PlayerTable =
    {
        // Head
        new CriticalEffect[] { new(3, N, -1, 0, N, 6500, 5000), new(3, Bypass, 2, 3, KnockedDown, 6501, 6503), new(3, Bypass, 2, 0, KnockedDown, 6501, 6503), new(3, KnockedDown | Bypass, 2, 2, KnockedOut, 6503, 6502), new(3, KnockedOut | Bypass, 6, 2, Blind, 6502, 6504), new(6, Bypass, 2, -2, Dead, 6501, 6505) },
        // Left arm
        new CriticalEffect[] { new(2, N, -1, 0, N, 6506, 5000), new(2, LoseTurn, -1, 0, N, 6507, 5000), new(3, N, 2, 0, CrippledLeftArm, 6508, 6509), new(3, Bypass, -1, 0, N, 6501, 5000), new(3, CrippledLeftArm | Bypass, -1, 0, N, 6510, 5000), new(3, CrippledLeftArm | Bypass, -1, 0, N, 6510, 5000) },
        // Right arm
        new CriticalEffect[] { new(2, N, -1, 0, N, 6506, 5000), new(2, LoseTurn, -1, 0, N, 6507, 5000), new(3, N, 2, 0, CrippledRightArm, 6508, 6509), new(3, Bypass, -1, 0, N, 6501, 5000), new(3, CrippledRightArm | Bypass, -1, 0, N, 6511, 5000), new(3, CrippledRightArm | Bypass, -1, 0, N, 6511, 5000) },
        // Torso
        new CriticalEffect[] { new(3, N, -1, 0, N, 6512, 5000), new(3, N, -1, 0, N, 6512, 5000), new(3, Bypass, -1, 0, N, 6508, 5000), new(3, KnockedDown | Bypass, -1, 0, N, 6503, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 6503, 5000), new(4, KnockedDown | Bypass, 6, 2, Dead, 6503, 6513) },
        // Right leg
        new CriticalEffect[] { new(3, N, -1, 0, N, 6512, 5000), new(3, KnockedDown, -1, 0, N, 6514, 5000), new(3, KnockedDown, 2, 0, CrippledRightLeg, 6514, 6515), new(4, KnockedDown | CrippledRightLeg | Bypass, -1, 0, N, 6516, 5000), new(4, KnockedDown | CrippledRightLeg | Bypass, -1, 0, N, 6516, 5000), new(4, KnockedOut | CrippledRightLeg | Bypass, -1, 0, N, 6517, 5000) },
        // Left leg
        new CriticalEffect[] { new(3, N, -1, 0, N, 6512, 5000), new(3, KnockedDown, -1, 0, N, 6514, 5000), new(3, KnockedDown, 2, 0, CrippledLeftLeg, 6514, 6515), new(4, KnockedDown | CrippledLeftLeg | Bypass, -1, 0, N, 6516, 5000), new(4, KnockedDown | CrippledLeftLeg | Bypass, -1, 0, N, 6516, 5000), new(4, KnockedOut | CrippledLeftLeg | Bypass, -1, 0, N, 6517, 5000) },
        // Eyes
        new CriticalEffect[] { new(3, N, -1, 0, N, 6518, 5000), new(3, N, 6, 3, Blind, 6518, 6519), new(3, Bypass, 6, 3, Blind, 6501, 6519), new(4, Bypass | LoseTurn, -1, 0, N, 6520, 5000), new(4, Blind | Bypass | LoseTurn, -1, 0, N, 6521, 5000), new(6, Dead, -1, 0, N, 6522, 5000) },
        // Groin
        new CriticalEffect[] { new(3, N, -1, 0, N, 6523, 5000), new(3, N, 2, 0, KnockedDown, 6523, 6524), new(3, KnockedDown, -1, 0, N, 6524, 5000), new(3, KnockedDown, 2, 4, KnockedOut, 6524, 6525), new(4, KnockedDown, 2, 2, KnockedOut, 6524, 6525), new(4, KnockedOut, -1, 0, N, 6526, 5000) },
        // Uncalled
        new CriticalEffect[] { new(3, N, -1, 0, N, 6512, 5000), new(3, N, -1, 0, N, 6512, 5000), new(3, Bypass, -1, 0, N, 6508, 5000), new(3, KnockedDown | Bypass, -1, 0, N, 6503, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 6503, 5000), new(4, KnockedDown | Bypass, 6, 2, Dead, 6503, 6513) },
    };

    /// <summary>_cf_table row by the weapon PRO critical failure type (-1, no weapon, uses row 0).</summary>
    public static DamageFlags Failure(int type, int effect)
    {
        int row = type == -1 ? 0 : type;
        if (row is < 0 or >= 7 || effect is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(type));
        return FailureTable[row, effect];
    }

    // gCriticalHitTables[killType][location][effect], kill types in CE order and locations in HitLocation order (generated
    // from CE combat.cc; --critical-report compares every row with fallout2.exe).
    internal static readonly CriticalEffect[][][] KillTypeTable =
    {
        // 0 Man
        new[]
        {
            new CriticalEffect[] { new(4, N, -1, 0, N, 5001, 5000), new(4, Bypass, 2, 0, KnockedOut, 5002, 5003), new(5, Bypass, 2, -3, KnockedOut, 5002, 5003), new(5, KnockedDown | Bypass, 2, -3, KnockedOut, 5004, 5003), new(6, KnockedOut | Bypass, 6, 0, Blind, 5005, 5006), new(6, Dead, -1, 0, N, 5007, 5000) }, // Head
            new CriticalEffect[] { new(3, N, -1, 0, N, 5008, 5000), new(3, LoseTurn, -1, 0, N, 5009, 5000), new(4, N, 2, -3, CrippledLeftArm, 5010, 5011), new(4, CrippledLeftArm | Bypass, -1, 0, N, 5012, 5000), new(4, CrippledLeftArm | Bypass, -1, 0, N, 5012, 5000), new(4, CrippledLeftArm | Bypass, -1, 0, N, 5013, 5000) }, // Left arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 5008, 5000), new(3, LoseTurn, -1, 0, N, 5009, 5000), new(4, N, 2, -3, CrippledRightArm, 5014, 5000), new(4, CrippledRightArm | Bypass, -1, 0, N, 5015, 5000), new(4, CrippledRightArm | Bypass, -1, 0, N, 5015, 5000), new(4, CrippledRightArm | Bypass, -1, 0, N, 5013, 5000) }, // Right arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 5016, 5000), new(3, Bypass, -1, 0, N, 5017, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 5019, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 5019, 5000), new(6, KnockedOut | Bypass, -1, 0, N, 5020, 5000), new(6, Dead, -1, 0, N, 5021, 5000) }, // Torso
            new CriticalEffect[] { new(3, KnockedDown, -1, 0, N, 5023, 5000), new(3, KnockedDown, 2, 0, CrippledRightLeg, 5023, 5024), new(4, KnockedDown, 2, -3, CrippledRightLeg, 5023, 5024), new(4, KnockedDown | CrippledRightLeg | Bypass, -1, 0, N, 5025, 5000), new(4, KnockedDown | CrippledRightLeg | Bypass, 2, 0, KnockedOut, 5025, 5026), new(4, KnockedOut | CrippledRightLeg | Bypass, -1, 0, N, 5026, 5000) }, // Right leg
            new CriticalEffect[] { new(3, KnockedDown, -1, 0, N, 5023, 5000), new(3, KnockedDown, 2, 0, CrippledLeftLeg, 5023, 5024), new(4, KnockedDown, 2, -3, CrippledLeftLeg, 5023, 5024), new(4, KnockedDown | CrippledLeftLeg | Bypass, -1, 0, N, 5025, 5000), new(4, KnockedDown | CrippledLeftLeg | Bypass, 2, 0, KnockedOut, 5025, 5026), new(4, KnockedOut | CrippledLeftLeg | Bypass, -1, 0, N, 5026, 5000) }, // Left leg
            new CriticalEffect[] { new(4, N, 6, 4, Blind, 5027, 5028), new(4, Bypass, 6, 3, Blind, 5029, 5028), new(6, Bypass, 6, 2, Blind, 5029, 5028), new(6, Blind | Bypass | LoseTurn, -1, 0, N, 5030, 5000), new(8, KnockedOut | Blind | Bypass, -1, 0, N, 5031, 5000), new(8, Dead, -1, 0, N, 5032, 5000) }, // Eyes
            new CriticalEffect[] { new(3, N, -1, 0, N, 5033, 5000), new(3, Bypass, 2, -3, KnockedDown, 5034, 5035), new(3, KnockedDown, 2, -3, KnockedOut, 5035, 5036), new(3, KnockedOut, -1, 0, N, 5036, 5000), new(4, KnockedDown | Bypass, 2, 0, KnockedOut, 5035, 5036), new(4, KnockedOut | Bypass, -1, 0, N, 5037, 5000) }, // Groin
            new CriticalEffect[] { new(3, N, -1, 0, N, 5016, 5000), new(3, Bypass, -1, 0, N, 5017, 5000), new(4, N, -1, 0, N, 5018, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 5019, 5000), new(6, KnockedOut | Bypass, -1, 0, N, 5020, 5000), new(6, Dead, -1, 0, N, 5021, 5000) }, // Uncalled
        },
        // 1 Woman
        new[]
        {
            new CriticalEffect[] { new(4, N, -1, 0, N, 5101, 5100), new(4, Bypass, 2, 0, KnockedOut, 5102, 5103), new(6, Bypass, 2, -3, KnockedOut, 5102, 5103), new(6, KnockedDown | Bypass, 2, -3, KnockedOut, 5104, 5103), new(6, KnockedOut | Bypass, 6, 0, Blind, 5105, 5106), new(6, Dead, -1, 0, N, 5107, 5000) }, // Head
            new CriticalEffect[] { new(3, N, -1, 0, N, 5108, 5100), new(3, LoseTurn, -1, 0, N, 5109, 5100), new(4, N, 2, -2, CrippledLeftArm, 5110, 5111), new(4, N, 2, -4, CrippledLeftArm, 5110, 5111), new(4, CrippledLeftArm | Bypass, -1, 0, N, 5112, 5100), new(4, CrippledLeftArm | Bypass, -1, 0, N, 5113, 5100) }, // Left arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 5108, 5100), new(3, LoseTurn, -1, 0, N, 5109, 5100), new(4, N, 2, -2, CrippledRightArm, 5114, 5100), new(4, N, 2, -4, CrippledRightArm, 5114, 5100), new(4, CrippledRightArm | Bypass, -1, 0, N, 5115, 5100), new(4, CrippledRightArm | Bypass, -1, 0, N, 5113, 5100) }, // Right arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 5116, 5100), new(3, Bypass, -1, 0, N, 5117, 5100), new(4, KnockedDown | Bypass, -1, 0, N, 5119, 5100), new(4, KnockedDown | Bypass, -1, 0, N, 5119, 5100), new(6, KnockedOut | Bypass, -1, 0, N, 5120, 5100), new(6, Dead, -1, 0, N, 5121, 5100) }, // Torso
            new CriticalEffect[] { new(3, KnockedDown, -1, 0, N, 5123, 5100), new(4, KnockedDown, 2, 0, CrippledRightLeg, 5123, 5124), new(4, KnockedDown, 2, -3, CrippledRightLeg, 5123, 5124), new(4, KnockedDown | CrippledRightLeg | Bypass, -1, 0, N, 5125, 5100), new(4, KnockedDown | CrippledRightLeg | Bypass, 2, 0, KnockedOut, 5125, 5126), new(4, KnockedOut | CrippledRightLeg | Bypass, -1, 0, N, 5126, 5100) }, // Right leg
            new CriticalEffect[] { new(3, KnockedDown, -1, 0, N, 5123, 5100), new(4, KnockedDown, 2, 0, CrippledLeftLeg, 5123, 5124), new(4, KnockedDown, 2, -3, CrippledLeftLeg, 5123, 5124), new(4, KnockedDown | CrippledLeftLeg | Bypass, -1, 0, N, 5125, 5100), new(4, KnockedDown | CrippledLeftLeg | Bypass, 2, 0, KnockedOut, 5125, 5126), new(4, KnockedOut | CrippledLeftLeg | Bypass, -1, 0, N, 5126, 5100) }, // Left leg
            new CriticalEffect[] { new(4, N, 6, 4, Blind, 5127, 5128), new(4, Bypass, 6, 3, Blind, 5129, 5128), new(6, Bypass, 6, 2, Blind, 5129, 5128), new(6, Blind | Bypass | LoseTurn, -1, 0, N, 5130, 5100), new(8, KnockedOut | Blind | Bypass, -1, 0, N, 5131, 5100), new(8, Dead, -1, 0, N, 5132, 5100) }, // Eyes
            new CriticalEffect[] { new(3, N, -1, 0, N, 5133, 5100), new(3, N, 2, 0, KnockedDown, 5133, 5134), new(3, KnockedDown, 2, 0, KnockedOut, 5134, 5135), new(3, KnockedOut, -1, 0, N, 5135, 5100), new(4, KnockedDown | Bypass, 2, 0, KnockedOut, 5134, 5135), new(4, KnockedOut | Bypass, -1, 0, N, 5135, 5100) }, // Groin
            new CriticalEffect[] { new(3, N, -1, 0, N, 5116, 5100), new(3, Bypass, -1, 0, N, 5117, 5100), new(4, KnockedDown | Bypass, -1, 0, N, 5119, 5100), new(4, KnockedDown | Bypass, -1, 0, N, 5119, 5100), new(6, KnockedOut | Bypass, -1, 0, N, 5120, 5100), new(6, Dead, -1, 0, N, 5121, 5100) }, // Uncalled
        },
        // 2 Child
        new[]
        {
            new CriticalEffect[] { new(4, N, 2, 0, KnockedOut, 5200, 5201), new(4, Bypass, 2, -2, KnockedOut, 5202, 5203), new(4, Bypass, 2, -2, KnockedOut, 5202, 5203), new(6, KnockedOut | Bypass, -1, 0, N, 5203, 5000), new(6, KnockedOut | Bypass, -1, 0, N, 5203, 5000), new(6, Dead, -1, 0, N, 5204, 5000) }, // Head
            new CriticalEffect[] { new(3, N, -1, 0, N, 5205, 5000), new(4, LoseTurn, 2, 0, CrippledLeftArm, 5206, 5207), new(4, LoseTurn, 2, -2, CrippledLeftArm, 5206, 5207), new(4, CrippledLeftArm | Bypass, -1, 0, N, 5208, 5000), new(4, CrippledLeftArm | Bypass, -1, 0, N, 5208, 5000), new(4, CrippledLeftArm | Bypass, -1, 0, N, 5208, 5000) }, // Left arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 5209, 5000), new(4, LoseTurn, 2, 0, CrippledRightArm, 5206, 5207), new(4, LoseTurn, 2, -2, CrippledRightArm, 5206, 5207), new(4, CrippledRightArm | Bypass, -1, 0, N, 5208, 5000), new(4, CrippledRightArm | Bypass, -1, 0, N, 5208, 5000), new(4, CrippledRightArm | Bypass, -1, 0, N, 5208, 5000) }, // Right arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 5210, 5000), new(4, Bypass, -1, 0, N, 5211, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 5212, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 5212, 5000), new(4, KnockedOut | Bypass, -1, 0, N, 5213, 5000), new(6, Dead, -1, 0, N, 5214, 5000) }, // Torso
            new CriticalEffect[] { new(3, N, -1, 0, N, 5215, 5000), new(3, KnockedDown | CrippledRightLeg, -1, 0, CrippledRightArm | Blind | OnFire | Explode, 5000, 0), new(3, KnockedDown | CrippledRightLeg, -1, 0, CrippledRightArm | Blind | OnFire | Explode, 5000, 0), new(4, KnockedDown | CrippledRightLeg | Bypass, -1, 0, N, 5217, 5000), new(4, KnockedDown | CrippledRightLeg | Bypass, -1, 0, N, 5217, 5000), new(4, KnockedDown | CrippledRightLeg | Bypass, -1, 0, N, 5217, 5000) }, // Right leg
            new CriticalEffect[] { new(3, N, -1, 0, N, 5215, 5000), new(3, KnockedDown | CrippledLeftLeg, -1, 0, CrippledRightArm | Blind | OnFire | Explode, 5000, 0), new(3, KnockedDown | CrippledLeftLeg, -1, 0, CrippledRightArm | Blind | OnFire | Explode, 5000, 0), new(4, KnockedDown | CrippledLeftLeg | Bypass, -1, 0, N, 5217, 5000), new(4, KnockedDown | CrippledLeftLeg | Bypass, -1, 0, N, 5217, 5000), new(4, KnockedDown | CrippledLeftLeg | Bypass, -1, 0, N, 5217, 5000) }, // Left leg
            new CriticalEffect[] { new(4, N, 6, 5, Blind, 5218, 5219), new(4, Bypass, 6, 2, Blind, 5220, 5221), new(6, Bypass, 6, -1, Blind, 5220, 5221), new(6, Blind | Bypass | LoseTurn, -1, 0, N, 5222, 5000), new(8, KnockedOut | Blind | Bypass, -1, 0, N, 5223, 5000), new(8, Dead, -1, 0, N, 5224, 5000) }, // Eyes
            new CriticalEffect[] { new(3, N, -1, 0, N, 5225, 5000), new(3, N, -1, 0, N, 5225, 5000), new(3, KnockedDown, -1, 0, N, 5226, 5000), new(3, KnockedDown, -1, 0, N, 5226, 5000), new(3, KnockedDown, -1, 0, N, 5226, 5000), new(4, KnockedDown, -1, 0, N, 5226, 5000) }, // Groin
            new CriticalEffect[] { new(3, N, -1, 0, N, 5210, 5000), new(3, Bypass, -1, 0, N, 5211, 5000), new(4, Bypass, -1, 0, N, 5211, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 5212, 5000), new(4, KnockedOut | Bypass, -1, 0, N, 5213, 5000), new(6, Dead, -1, 0, N, 5214, 5000) }, // Uncalled
        },
        // 3 Super Mutant
        new[]
        {
            new CriticalEffect[] { new(4, N, -1, 0, N, 5300, 5000), new(4, Bypass, 2, -1, KnockedDown, 5301, 5302), new(5, Bypass, 2, -4, KnockedDown, 5301, 5302), new(5, KnockedDown | Bypass, 2, -3, KnockedOut, 5302, 5303), new(6, KnockedDown | Bypass, 2, 0, KnockedOut, 5302, 5303), new(6, Dead, -1, 0, N, 5304, 5000) }, // Head
            new CriticalEffect[] { new(3, N, -1, 0, N, 5300, 5000), new(3, N, 5, 0, LoseTurn, 5300, 5306), new(4, Bypass | LoseTurn, 2, -1, CrippledLeftArm, 5307, 5308), new(4, Bypass | LoseTurn, 2, -3, CrippledLeftArm, 5307, 5308), new(4, CrippledLeftArm | Bypass, -1, 0, N, 5308, 5000), new(4, CrippledLeftArm | Bypass, -1, 0, N, 5308, 5000) }, // Left arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 5300, 5000), new(3, N, 5, 0, LoseTurn, 5300, 5006), new(4, Bypass | LoseTurn, 2, -1, CrippledRightArm, 5307, 5309), new(4, Bypass | LoseTurn, 2, -3, CrippledRightArm, 5307, 5309), new(4, CrippledRightArm | Bypass, -1, 0, N, 5309, 5000), new(4, CrippledRightArm | Bypass, -1, 0, N, 5309, 5000) }, // Right arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 5300, 5000), new(3, Bypass, -1, 0, N, 5301, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 5302, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 5302, 5000), new(4, KnockedOut | Bypass, -1, 0, N, 5310, 5000), new(6, Dead, -1, 0, N, 5311, 5000) }, // Torso
            new CriticalEffect[] { new(3, N, -1, 0, N, 5300, 5000), new(3, N, 5, 0, KnockedDown, 5300, 5312), new(4, KnockedDown, 2, -3, CrippledRightLeg, 5312, 5313), new(4, KnockedDown | CrippledRightLeg, -1, 0, N, 5313, 5000), new(4, KnockedDown | CrippledRightLeg | Bypass, -1, 0, N, 5314, 5000), new(4, KnockedOut | CrippledRightLeg | Bypass, -1, 0, N, 5315, 5000) }, // Right leg
            new CriticalEffect[] { new(3, N, -1, 0, N, 5300, 5000), new(3, N, 5, 0, KnockedDown, 5300, 5312), new(4, KnockedDown, 2, -3, CrippledLeftLeg, 5312, 5313), new(4, KnockedDown | CrippledLeftLeg, -1, 0, N, 5313, 5000), new(4, KnockedDown | CrippledLeftLeg | Bypass, -1, 0, N, 5314, 5000), new(4, KnockedOut | CrippledLeftLeg | Bypass, -1, 0, N, 5315, 5000) }, // Left leg
            new CriticalEffect[] { new(4, N, -1, 0, N, 5300, 5000), new(4, Bypass, 6, 5, Blind, 5316, 5317), new(6, Bypass, 6, 3, Blind, 5316, 5317), new(6, Bypass | LoseTurn, 6, 0, Blind, 5318, 5319), new(8, KnockedOut | Blind | Bypass, -1, 0, N, 5320, 5000), new(8, Dead, -1, 0, N, 5321, 5000) }, // Eyes
            new CriticalEffect[] { new(3, N, -1, 0, N, 5300, 5000), new(3, N, 6, 0, Bypass, 5300, 5017), new(3, Bypass, 2, -2, KnockedDown, 5301, 5302), new(3, KnockedDown, -1, 0, N, 5312, 5000), new(4, KnockedDown | Bypass, 2, 0, KnockedOut, 5302, 5303), new(4, KnockedOut | Bypass, -1, 0, N, 5303, 5000) }, // Groin
            new CriticalEffect[] { new(3, N, -1, 0, N, 5300, 5000), new(3, Bypass, -1, 0, N, 5301, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 5302, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 5302, 5000), new(4, KnockedOut | Bypass, -1, 0, N, 5310, 5000), new(6, Dead, -1, 0, N, 5311, 5000) }, // Uncalled
        },
        // 4 Ghoul
        new[]
        {
            new CriticalEffect[] { new(4, N, -1, 0, N, 5001, 5000), new(4, Bypass, 2, 0, KnockedOut, 5400, 5003), new(5, Bypass, 2, -1, KnockedOut, 5400, 5003), new(5, KnockedDown | Bypass, 2, -2, KnockedOut, 5004, 5005), new(6, KnockedOut | Bypass, 0, 0, N, 5005, 5000), new(6, Dead, -1, 0, N, 5401, 5000) }, // Head
            new CriticalEffect[] { new(3, N, -1, 0, N, 5016, 5000), new(3, N, 5, 0, Drop | LoseTurn, 5001, 5402), new(4, Drop | LoseTurn, 2, 0, CrippledLeftArm, 5402, 5012), new(4, Bypass | Drop | LoseTurn, 2, 0, CrippledLeftArm, 5403, 5404), new(4, CrippledLeftArm | Bypass | Drop, -1, 0, N, 5404, 5000), new(4, CrippledLeftArm | Bypass | Drop, -1, 0, N, 5404, 5000) }, // Left arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 5016, 5000), new(3, N, 5, 0, Drop | LoseTurn, 5001, 5402), new(4, Drop | LoseTurn, 2, 0, CrippledRightArm, 5402, 5015), new(4, Bypass | Drop | LoseTurn, 2, 0, CrippledRightArm, 5403, 5404), new(4, CrippledRightArm | Bypass | Drop, -1, 0, N, 5404, 5000), new(4, CrippledRightArm | Bypass | Drop, -1, 0, N, 5404, 5000) }, // Right arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, Bypass, -1, 0, N, 5017, 5000), new(3, N, -1, 0, N, 5018, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 5004, 5000), new(4, KnockedOut | Bypass, -1, 0, N, 5003, 5000), new(6, Dead, -1, 0, N, 5007, 5000) }, // Torso
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, 5, 0, KnockedDown, 5001, 5023), new(4, KnockedDown, 2, 0, CrippledRightLeg, 5023, 5024), new(4, KnockedDown | CrippledRightLeg, -1, 0, N, 5024, 5000), new(4, KnockedDown | CrippledRightLeg | Bypass, 2, 0, KnockedOut, 5024, 5026), new(4, KnockedOut | CrippledRightLeg | Bypass, -1, 0, N, 5026, 5000) }, // Right leg
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, 5, 0, KnockedDown, 5001, 5023), new(4, KnockedDown, 2, 0, CrippledLeftLeg, 5023, 5024), new(4, KnockedDown | CrippledLeftLeg, -1, 0, N, 5024, 5000), new(4, KnockedDown | CrippledLeftLeg | Bypass, 2, 0, KnockedOut, 5024, 5026), new(4, KnockedOut | CrippledLeftLeg | Bypass, -1, 0, N, 5026, 5000) }, // Left leg
            new CriticalEffect[] { new(4, N, 6, 3, Blind, 5001, 5405), new(4, Bypass, 6, 0, Blind, 5406, 5407), new(6, Bypass, 6, -3, Blind, 5406, 5407), new(6, Blind | Bypass | LoseTurn, -1, 0, N, 5030, 5000), new(8, KnockedOut | Blind | Bypass, -1, 0, N, 5031, 5000), new(8, Dead, -1, 0, N, 5408, 5000) }, // Eyes
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, 6, 0, Bypass, 5001, 5033), new(3, Bypass, 2, 0, KnockedDown, 5033, 5035), new(3, KnockedDown, -1, 0, N, 5004, 5000), new(4, KnockedDown | Bypass, 2, 0, KnockedOut, 5035, 5036), new(4, KnockedOut | Bypass, -1, 0, N, 5036, 5000) }, // Groin
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, Bypass, -1, 0, N, 5017, 5000), new(3, N, -1, 0, N, 5018, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 5004, 5000), new(4, KnockedOut | Bypass, -1, 0, N, 5003, 5000), new(6, Dead, -1, 0, N, 5007, 5000) }, // Uncalled
        },
        // 5 Brahmin
        new[]
        {
            new CriticalEffect[] { new(4, N, -1, 0, N, 5016, 5000), new(4, N, -1, 0, N, 5016, 5000), new(5, N, 2, 2, KnockedDown, 5016, 5500), new(5, N, 2, -1, KnockedDown, 5016, 5500), new(6, KnockedOut, 0, 0, N, 5501, 5000), new(6, Dead, -1, 0, N, 5502, 5000) }, // Head
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, -1, 0, N, 5001, 5000), new(4, N, 2, 0, CrippledLeftLeg, 5016, 5503), new(4, N, 2, 0, CrippledLeftLeg, 5016, 5503), new(4, CrippledLeftLeg, -1, 0, N, 5503, 5000), new(4, CrippledLeftLeg, -1, 0, N, 5503, 5000) }, // Left arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, -1, 0, N, 5001, 5000), new(4, N, 2, 0, CrippledRightLeg, 5016, 5503), new(4, N, 2, 0, CrippledRightLeg, 5016, 5503), new(4, CrippledRightLeg, -1, 0, N, 5503, 5000), new(4, CrippledRightLeg, -1, 0, N, 5503, 5000) }, // Right arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 5504, 5000), new(3, N, -1, 0, N, 5504, 5000), new(4, N, -1, 0, N, 5504, 5000), new(4, Bypass, -1, 0, N, 5505, 5000), new(4, Bypass, -1, 0, N, 5505, 5000), new(6, Dead, -1, 0, N, 5506, 5000) }, // Torso
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, -1, 0, N, 5001, 5000), new(4, N, 2, 0, CrippledRightLeg, 5016, 5503), new(4, N, 2, 0, CrippledRightLeg, 5016, 5503), new(4, CrippledRightLeg, -1, 0, N, 5503, 5000), new(4, CrippledRightLeg, -1, 0, N, 5503, 5000) }, // Right leg
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, -1, 0, N, 5001, 5000), new(4, N, 2, 0, CrippledLeftLeg, 5016, 5503), new(4, N, 2, 0, CrippledLeftLeg, 5016, 5503), new(4, CrippledLeftLeg, -1, 0, N, 5503, 5000), new(4, CrippledLeftLeg, -1, 0, N, 5503, 5000) }, // Left leg
            new CriticalEffect[] { new(4, N, -1, 0, N, 5001, 5000), new(4, Bypass, 6, 0, Blind, 5029, 5507), new(6, Bypass, 6, -3, Blind, 5029, 5507), new(6, Blind | Bypass | LoseTurn, -1, 0, N, 5508, 5000), new(8, KnockedOut | Blind | Bypass, -1, 0, N, 5509, 5000), new(8, Dead, -1, 0, N, 5510, 5000) }, // Eyes
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, Bypass, -1, 0, N, 5511, 5000), new(3, Bypass, -1, 0, N, 5511, 5000), new(4, Bypass, -1, 0, N, 5512, 5000), new(4, Bypass, -1, 0, N, 5512, 5000), new(6, Bypass, -1, 0, N, 5513, 5000) }, // Groin
            new CriticalEffect[] { new(3, N, -1, 0, N, 5504, 5000), new(3, N, -1, 0, N, 5504, 5000), new(4, N, -1, 0, N, 5504, 5000), new(4, Bypass, -1, 0, N, 5505, 5000), new(4, Bypass, -1, 0, N, 5505, 5000), new(6, Dead, -1, 0, N, 5506, 5000) }, // Uncalled
        },
        // 6 Radscorpion
        new[]
        {
            new CriticalEffect[] { new(4, N, -1, 0, N, 5001, 5000), new(4, N, 2, 3, KnockedDown, 5001, 5600), new(5, N, 2, 0, KnockedDown, 5001, 5600), new(5, N, 2, -3, KnockedDown, 5001, 5600), new(6, KnockedDown, -1, 0, N, 5600, 5000), new(6, Dead, -1, 0, N, 5601, 5000) }, // Head
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, -1, 0, N, 5001, 5000), new(4, N, -1, 0, N, 5016, 5000), new(4, N, 2, 0, CrippledLeftArm, 5016, 5602), new(4, CrippledLeftArm, -1, 0, N, 5602, 5000), new(4, CrippledLeftArm, -1, 0, N, 5602, 5000) }, // Left arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, -1, 0, N, 5001, 5000), new(4, N, -1, 0, N, 5016, 5000), new(4, N, 2, 2, CrippledRightArm, 5016, 5603), new(4, N, 2, 0, CrippledRightArm, 5016, 5603), new(4, CrippledRightArm, -1, 0, N, 5603, 5000) }, // Right arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, Bypass, -1, 0, N, 5604, 5000), new(4, N, -1, 0, N, 5016, 5000), new(4, Bypass, -1, 0, N, 5605, 5000), new(4, Bypass, 5, 0, KnockedDown, 5605, 5606), new(4, Dead, -1, 0, N, 5607, 5000) }, // Torso
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, 5, 2, N, 5001, 5600), new(4, KnockedDown, 2, 0, CrippledRightLeg, 5600, 5608), new(4, CrippledRightLeg | Bypass, -1, 0, N, 5609, 5000), new(4, KnockedDown | CrippledRightLeg | Bypass, -1, 0, N, 5608, 5000), new(4, KnockedDown | CrippledRightLeg | Bypass, -1, 0, N, 5608, 5000) }, // Right leg
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, 5, 2, N, 5001, 5600), new(4, KnockedDown, 2, 0, CrippledLeftLeg, 5600, 5008), new(4, CrippledLeftLeg | Bypass, -1, 0, N, 5609, 5000), new(4, KnockedDown | CrippledLeftLeg | Bypass, -1, 0, N, 5608, 5000), new(4, KnockedDown | CrippledLeftLeg | Bypass, -1, 0, N, 5608, 5000) }, // Left leg
            new CriticalEffect[] { new(4, N, -1, 0, N, 5001, 5000), new(4, N, 5, 3, Blind, 5001, 5610), new(6, N, 5, 0, Blind, 5016, 5610), new(6, N, 5, -3, Blind, 5016, 5610), new(8, N, 5, -3, Blind, 5611, 5612), new(8, Dead, -1, 0, N, 5613, 5000) }, // Eyes
            new CriticalEffect[] { new(3, N, -1, 0, N, 5614, 5000), new(3, N, -1, 0, N, 5614, 5000), new(4, N, -1, 0, N, 5614, 5000), new(4, KnockedOut, -1, 0, N, 5615, 5000), new(4, KnockedOut, -1, 0, N, 5615, 5000), new(4, Dead, -1, 0, N, 5616, 5000) }, // Groin
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, Bypass, -1, 0, N, 5604, 5000), new(4, N, -1, 0, N, 5016, 5000), new(4, Bypass, -1, 0, N, 5605, 5000), new(4, Bypass, 5, 0, KnockedDown, 5605, 5606), new(4, Dead, -1, 0, N, 5607, 5000) }, // Uncalled
        },
        // 7 Rat
        new[]
        {
            new CriticalEffect[] { new(4, Bypass, -1, 0, N, 5700, 5000), new(4, Bypass, -1, 0, N, 5700, 5000), new(4, Dead, -1, 0, N, 5701, 5000), new(4, Dead, -1, 0, N, 5701, 5000), new(6, Dead, -1, 0, N, 5701, 5000), new(6, Dead, -1, 0, N, 5701, 5000) }, // Head
            new CriticalEffect[] { new(3, CrippledLeftArm, -1, 0, N, 5703, 5000), new(3, CrippledLeftArm, -1, 0, N, 5703, 5000), new(3, CrippledLeftArm, -1, 0, N, 5703, 5000), new(4, CrippledLeftArm, -1, 0, N, 5703, 5000), new(4, CrippledLeftArm, -1, 0, N, 5703, 5000), new(4, CrippledLeftArm, -1, 0, N, 5703, 5000) }, // Left arm
            new CriticalEffect[] { new(3, CrippledRightArm, -1, 0, N, 5705, 5000), new(3, CrippledRightArm, -1, 0, N, 5705, 5000), new(3, CrippledRightArm, -1, 0, N, 5705, 5000), new(4, CrippledRightArm, -1, 0, N, 5705, 5000), new(4, CrippledRightArm, -1, 0, N, 5705, 5000), new(4, CrippledRightArm, -1, 0, N, 5705, 5000) }, // Right arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 5706, 5000), new(3, KnockedDown, -1, 0, N, 5707, 5000), new(3, KnockedDown, -1, 0, N, 5707, 5000), new(4, KnockedDown, -1, 0, N, 5707, 5000), new(4, KnockedDown, -1, 0, N, 5707, 5000), new(4, Dead, -1, 0, N, 5708, 5000) }, // Torso
            new CriticalEffect[] { new(3, CrippledRightLeg, -1, 0, N, 5709, 5000), new(3, CrippledRightLeg, -1, 0, N, 5709, 5000), new(3, CrippledRightLeg, -1, 0, N, 5709, 5000), new(4, CrippledRightLeg, -1, 0, N, 5709, 5000), new(4, CrippledRightLeg, -1, 0, N, 5709, 5000), new(4, CrippledRightLeg, -1, 0, N, 5709, 5000) }, // Right leg
            new CriticalEffect[] { new(3, CrippledLeftLeg, -1, 0, N, 5710, 5000), new(3, CrippledLeftLeg, -1, 0, N, 5710, 5000), new(3, CrippledLeftLeg, -1, 0, N, 5710, 5000), new(4, CrippledLeftLeg, -1, 0, N, 5710, 5000), new(4, CrippledLeftLeg, -1, 0, N, 5710, 5000), new(4, CrippledLeftLeg, -1, 0, N, 5710, 5000) }, // Left leg
            new CriticalEffect[] { new(4, Bypass, -1, 0, N, 5711, 5000), new(4, Dead, -1, 0, N, 5712, 5000), new(4, Dead, -1, 0, N, 5712, 5000), new(6, Dead, -1, 0, N, 5712, 5000), new(6, Dead, -1, 0, N, 5712, 5000), new(6, Dead, -1, 0, N, 5712, 5000) }, // Eyes
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, -1, 0, N, 5001, 5000), new(4, Bypass, -1, 0, N, 5711, 5000), new(4, Bypass, -1, 0, N, 5711, 5000), new(6, Dead, -1, 0, N, 5712, 5000), new(6, Dead, -1, 0, N, 5712, 5000) }, // Groin
            new CriticalEffect[] { new(3, N, -1, 0, N, 5706, 5000), new(3, KnockedDown, -1, 0, N, 5707, 5000), new(3, KnockedDown, -1, 0, N, 5707, 5000), new(4, KnockedDown, -1, 0, N, 5707, 5000), new(4, KnockedDown, -1, 0, N, 5707, 5000), new(4, Dead, -1, 0, N, 5708, 5000) }, // Uncalled
        },
        // 8 Floater
        new[]
        {
            new CriticalEffect[] { new(4, N, -1, 0, N, 5001, 5000), new(4, N, 5, 0, KnockedDown, 5001, 5800), new(5, N, 5, -3, KnockedDown, 5016, 5800), new(5, KnockedDown, 2, 0, KnockedOut, 5800, 5801), new(6, KnockedDown, 2, -3, KnockedOut, 5800, 5801), new(6, Dead, -1, 0, N, 5802, 5000) }, // Head
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, 2, 0, LoseTurn, 5001, 5803), new(4, N, 2, -2, LoseTurn, 5001, 5803), new(3, Bypass | LoseTurn, -1, 0, N, 5804, 5000), new(4, Bypass | LoseTurn, -1, 0, N, 5804, 5000), new(4, Dead, -1, 0, N, 5805, 5000) }, // Left arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, 2, 0, LoseTurn, 5001, 5803), new(4, N, 2, -2, LoseTurn, 5001, 5803), new(3, Bypass | LoseTurn, -1, 0, N, 5804, 5000), new(4, Bypass | LoseTurn, -1, 0, N, 5804, 5000), new(4, Dead, -1, 0, N, 5805, 5000) }, // Right arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, 5, 0, KnockedDown, 5001, 5800), new(3, N, 5, -2, KnockedDown, 5001, 5800), new(4, KnockedDown, -1, 0, N, 5800, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 5804, 5000), new(4, Dead, -1, 0, N, 5805, 5000) }, // Torso
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, 5, 1, KnockedDown, 5001, 5800), new(4, N, 5, -1, KnockedDown, 5001, 5800), new(4, KnockedDown, 2, -1, CrippledLeftLeg | CrippledRightLeg, 5800, 5806), new(4, KnockedDown | Bypass, 2, -3, CrippledLeftLeg | CrippledRightLeg, 5804, 5806), new(6, Dead | OnFire, -1, 0, N, 5807, 5000) }, // Right leg
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, -1, 0, N, 5001, 5000), new(4, LoseTurn, -1, 0, N, 5803, 5000), new(4, LoseTurn, -1, 0, N, 5803, 5000), new(4, CrippledLeftArm | CrippledRightArm | LoseTurn, -1, 0, N, 5808, 5000), new(4, CrippledLeftArm | CrippledRightArm | LoseTurn, -1, 0, N, 5808, 5000) }, // Left leg
            new CriticalEffect[] { new(4, N, -1, 0, N, 5001, 5000), new(4, Bypass, -1, 0, N, 5809, 5000), new(5, N, 2, 0, Blind, 5016, 5810), new(5, Bypass, 2, -3, Blind, 5809, 5810), new(6, Blind | Bypass, -1, 0, N, 5810, 5000), new(6, KnockedDown | Blind | Bypass, -1, 0, N, 5801, 5000) }, // Eyes
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, -1, 0, N, 5001, 5000), new(3, N, 2, 0, KnockedDown, 5001, 5800), new(3, N, 2, -3, KnockedDown, 5001, 5800), new(3, KnockedDown | Bypass, -1, 0, N, 5800, 5000), new(3, KnockedDown | Bypass, -1, 0, N, 5800, 5000) }, // Groin
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, 5, 0, KnockedDown, 5001, 5800), new(3, N, 5, -2, KnockedDown, 5001, 5800), new(4, KnockedDown, -1, 0, N, 5800, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 5804, 5000), new(4, Dead, -1, 0, N, 5805, 5000) }, // Uncalled
        },
        // 9 Centaur
        new[]
        {
            new CriticalEffect[] { new(4, N, -1, 0, N, 5016, 5000), new(4, N, 2, 0, KnockedDown | LoseTurn, 5016, 5900), new(5, N, 2, -3, KnockedDown | LoseTurn, 5016, 5900), new(5, Bypass, 2, -3, KnockedDown | LoseTurn, 5901, 5900), new(6, Bypass, 2, -3, KnockedDown | LoseTurn, 5901, 5900), new(6, Dead, -1, 0, N, 5902, 5000) }, // Head
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, -1, 0, N, 5001, 5000), new(4, N, 2, 0, LoseTurn, 5016, 5903), new(4, N, 2, 0, CrippledLeftArm, 5016, 5904), new(4, CrippledLeftArm, -1, 0, N, 5904, 5000), new(4, CrippledLeftArm | Bypass, -1, 0, N, 5905, 5000) }, // Left arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, -1, 0, N, 5001, 5000), new(4, N, 2, 0, LoseTurn, 5016, 5903), new(4, N, 2, 0, CrippledRightArm, 5016, 5904), new(4, CrippledRightArm, -1, 0, N, 5904, 5000), new(4, CrippledRightArm | Bypass, -1, 0, N, 5905, 5000) }, // Right arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(4, N, -1, 0, N, 5001, 5000), new(4, Bypass, -1, 0, N, 5901, 5000), new(4, Bypass, 2, 2, N, 5901, 5900), new(5, KnockedDown | Bypass, -1, 0, N, 5900, 5000), new(5, Dead, -1, 0, N, 5902, 5000) }, // Torso
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, KnockedDown, -1, 0, N, 5900, 5000), new(4, KnockedDown, 2, 0, CrippledRightLeg, 5900, 5906), new(4, KnockedDown | CrippledRightLeg, -1, 0, N, 5906, 5000), new(4, KnockedDown | CrippledRightLeg, -1, 0, N, 5906, 5000), new(4, KnockedDown | CrippledRightLeg | LoseTurn, -1, 0, N, 5907, 5000) }, // Right leg
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, KnockedDown, -1, 0, N, 5900, 5000), new(4, KnockedDown, 2, 0, CrippledLeftLeg, 5900, 5906), new(4, KnockedDown | CrippledLeftLeg, -1, 0, N, 5906, 5000), new(4, KnockedDown | CrippledLeftLeg, -1, 0, N, 5906, 5000), new(4, KnockedDown | CrippledLeftLeg | LoseTurn, -1, 0, N, 5907, 5000) }, // Left leg
            new CriticalEffect[] { new(4, N, -1, 0, N, 5001, 5000), new(4, N, 2, 1, Blind, 5001, 5908), new(6, Bypass, 2, -1, Blind, 5901, 5908), new(6, Blind | Bypass | LoseTurn, -1, 0, N, 5909, 5000), new(8, KnockedOut | Blind | Bypass, -1, 0, N, 5910, 5000), new(8, Dead, -1, 0, N, 5911, 5000) }, // Eyes
            new CriticalEffect[] { new(2, N, -1, 0, N, 5912, 5000), new(2, N, -1, 0, N, 5912, 5000), new(2, N, -1, 0, N, 5912, 5000), new(2, N, -1, 0, N, 5912, 5000), new(2, N, -1, 0, N, 5912, 5000), new(2, N, -1, 0, N, 5912, 5000) }, // Groin
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(4, N, -1, 0, N, 5001, 5000), new(4, Bypass, -1, 0, N, 5901, 5000), new(4, Bypass, 2, 2, N, 5901, 5900), new(5, KnockedDown | Bypass, -1, 0, N, 5900, 5000), new(5, Dead, -1, 0, N, 5902, 5000) }, // Uncalled
        },
        // 10 Robot
        new[]
        {
            new CriticalEffect[] { new(4, N, -1, 0, N, 6000, 5000), new(4, N, -1, 0, N, 6000, 5000), new(5, N, -1, 0, N, 6000, 5000), new(5, KnockedDown, -1, 0, N, 6001, 5000), new(6, KnockedDown | Bypass, -1, 0, N, 6002, 5000), new(6, Dead, -1, 0, N, 6003, 5000) }, // Head
            new CriticalEffect[] { new(3, N, -1, 0, N, 6000, 5000), new(3, N, -1, 0, N, 6000, 5000), new(3, N, 2, 0, CrippledLeftArm, 6000, 6004), new(3, N, 2, -3, CrippledLeftArm, 6000, 6004), new(4, CrippledLeftArm, -1, 0, N, 6004, 5000), new(4, CrippledLeftArm, 2, 0, CrippledRightArm, 6004, 6005) }, // Left arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 6000, 5000), new(3, N, -1, 0, N, 6000, 5000), new(3, N, 2, 0, CrippledRightArm, 6000, 6004), new(3, N, 2, -3, CrippledRightArm, 6000, 6004), new(4, CrippledRightArm, -1, 0, N, 6004, 5000), new(4, CrippledRightArm, 2, 0, CrippledLeftArm, 6004, 6005) }, // Right arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 6000, 5000), new(3, Bypass, -1, 0, N, 6006, 5000), new(4, N, -1, 0, N, 6007, 5000), new(4, Bypass, -1, 0, N, 6008, 5000), new(6, Bypass, -1, 0, N, 6009, 5000), new(6, Dead, -1, 0, N, 6010, 5000) }, // Torso
            new CriticalEffect[] { new(3, N, -1, 0, N, 6000, 5000), new(4, N, -1, 0, N, 6007, 5000), new(3, N, 2, 0, CrippledRightLeg, 6000, 6004), new(4, N, 2, -4, CrippledRightLeg, 6007, 6004), new(4, CrippledRightLeg, 2, 0, KnockedDown, 6004, 6011), new(4, CrippledRightLeg, 2, -3, KnockedDown | CrippledLeftLeg, 6004, 6012) }, // Right leg
            new CriticalEffect[] { new(3, N, -1, 0, N, 6000, 5000), new(4, N, -1, 0, N, 6007, 5000), new(3, N, 2, 0, CrippledLeftLeg, 6000, 6004), new(4, N, 2, -4, CrippledLeftLeg, 6007, 6004), new(4, CrippledLeftLeg, 2, 0, KnockedDown, 6004, 6011), new(4, CrippledLeftLeg, 2, -3, KnockedDown | CrippledRightLeg, 6004, 6012) }, // Left leg
            new CriticalEffect[] { new(3, N, -1, 0, N, 6000, 5000), new(3, N, 2, 0, Blind, 6000, 6013), new(3, N, 2, -2, Blind, 6000, 6013), new(3, N, 2, -4, Blind, 6000, 6013), new(3, N, 2, -6, Blind, 6000, 6013), new(3, Blind, -1, 0, N, 6013, 5000) }, // Eyes
            new CriticalEffect[] { new(3, N, -1, 0, N, 6000, 5000), new(3, N, -1, 0, N, 6000, 5000), new(3, N, 2, -1, KnockedDown | LoseTurn, 6000, 6002), new(3, N, 2, -4, KnockedDown | LoseTurn, 6000, 6002), new(3, KnockedDown | LoseTurn, 2, 0, CrippleRandom, 6002, 6003), new(3, KnockedDown | LoseTurn, 2, -4, CrippleRandom, 6002, 6003) }, // Groin (fallout2.exe; CE combat.cc has no massive flags here)
            new CriticalEffect[] { new(3, N, -1, 0, N, 6000, 5000), new(3, Bypass, -1, 0, N, 6006, 5000), new(4, N, -1, 0, N, 6007, 5000), new(4, Bypass, -1, 0, N, 6008, 5000), new(6, Bypass, -1, 0, N, 6009, 5000), new(6, Dead, -1, 0, N, 6010, 5000) }, // Uncalled
        },
        // 11 Dog
        new[]
        {
            new CriticalEffect[] { new(4, N, -1, 0, N, 5016, 5000), new(4, N, 2, 0, KnockedDown, 5016, 6100), new(4, N, 2, -3, KnockedDown, 5016, 6100), new(4, N, 2, -6, CrippledLeftArm | CrippledRightArm, 5016, 6101), new(4, KnockedDown, 2, -3, KnockedOut, 6100, 6102), new(4, Dead, -1, 0, N, 6103, 5000) }, // Head
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, 2, -1, CrippledLeftLeg, 5001, 6104), new(3, N, 2, -3, CrippledLeftLeg, 5001, 6104), new(3, N, 2, -5, KnockedDown | CrippledLeftLeg, 5001, 6105), new(3, CrippledLeftLeg, 5, -1, KnockedDown, 6104, 6105), new(3, KnockedDown | CrippledLeftLeg, -1, 0, N, 6105, 5000) }, // Left arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, 2, -1, CrippledRightLeg, 5001, 6104), new(3, N, 2, -3, CrippledRightLeg, 5001, 6104), new(3, N, 2, -5, KnockedDown | CrippledRightLeg, 5001, 6105), new(3, CrippledRightLeg, 5, -1, KnockedDown, 6104, 6105), new(3, KnockedDown | CrippledRightLeg, -1, 0, N, 6105, 5000) }, // Right arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, 5, -1, KnockedDown, 5001, 6100), new(4, N, -1, 0, N, 5016, 5000), new(4, N, 5, -3, KnockedDown, 5016, 6100), new(4, KnockedDown, -1, 0, N, 6100, 5000), new(6, Dead, -1, 0, N, 6103, 5000) }, // Torso
            new CriticalEffect[] { new(3, N, 2, 1, CrippledRightLeg, 5001, 6104), new(3, N, 2, 0, CrippledRightLeg, 5001, 6104), new(3, N, 2, -2, CrippledRightLeg, 5001, 6104), new(3, N, 2, -4, KnockedDown | CrippledRightLeg, 5001, 6105), new(3, CrippledRightLeg, 5, -1, KnockedDown, 6104, 6105), new(3, KnockedDown | CrippledRightLeg, -1, 0, N, 6105, 5000) }, // Right leg
            new CriticalEffect[] { new(3, N, 2, 1, CrippledLeftLeg, 5001, 6104), new(3, N, 2, 0, CrippledLeftLeg, 5001, 6104), new(3, N, 2, -2, CrippledLeftLeg, 5001, 6104), new(3, N, 2, -4, KnockedDown | CrippledLeftLeg, 5001, 6105), new(3, CrippledLeftLeg, 5, -1, KnockedDown, 6104, 6105), new(3, KnockedDown | CrippledLeftLeg, -1, 0, N, 6105, 5000) }, // Left leg
            new CriticalEffect[] { new(4, N, -1, 0, N, 5001, 5000), new(4, Bypass, -1, 0, N, 5018, 5000), new(6, Bypass, -1, 0, N, 5018, 5000), new(6, Bypass, 2, 3, Blind, 5018, 6106), new(8, Bypass, 2, 0, Blind, 5018, 6106), new(8, Dead, -1, 0, N, 6107, 5000) }, // Eyes
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, 5, -2, KnockedDown, 5001, 6100), new(4, N, -1, 0, N, 5016, 5000), new(4, N, 5, -5, KnockedDown, 5016, 6100), new(4, KnockedDown, -1, 0, N, 6100, 5000), new(6, Dead, -1, 0, N, 6103, 5000) }, // Groin
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, 5, -1, KnockedDown, 5001, 6100), new(4, N, -1, 0, N, 5016, 5000), new(4, N, 5, -3, KnockedDown, 5016, 6100), new(4, KnockedDown, -1, 0, N, 6100, 5000), new(6, Dead, -1, 0, N, 6103, 5000) }, // Uncalled
        },
        // 12 Mantis
        new[]
        {
            new CriticalEffect[] { new(4, N, -1, 0, N, 5001, 5000), new(4, N, 2, 0, KnockedDown, 5001, 6200), new(5, N, 2, -3, KnockedDown, 5016, 6200), new(5, KnockedDown, 2, -1, KnockedOut, 6200, 6201), new(6, KnockedDown, 2, -3, KnockedOut, 6200, 6201), new(6, Dead, -1, 0, N, 6202, 5000) }, // Head
            new CriticalEffect[] { new(3, N, 2, 0, CrippledLeftArm, 5001, 6203), new(3, N, 2, 0, CrippledLeftArm, 5001, 6203), new(3, N, 2, -2, CrippledLeftArm, 5001, 6203), new(4, N, 2, -4, CrippledLeftArm, 5016, 6203), new(4, N, 2, -4, CrippledLeftArm, 5016, 6203), new(4, CrippledLeftArm | LoseTurn, -1, 0, N, 6204, 5000) }, // Left arm
            new CriticalEffect[] { new(3, N, 2, 0, CrippledRightArm, 5001, 6203), new(3, N, 2, 0, CrippledRightArm, 5001, 6203), new(3, N, 2, -2, CrippledRightArm, 5001, 6203), new(4, N, 2, -4, CrippledRightArm, 5016, 6203), new(4, N, 2, -4, CrippledRightArm, 5016, 6203), new(4, CrippledRightArm | LoseTurn, -1, 0, N, 6204, 5000) }, // Right arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 1000, 5000), new(3, N, 2, 0, Bypass, 5001, 6205), new(3, N, 2, -2, Bypass, 5001, 6205), new(4, N, 2, -2, Bypass, 5016, 6205), new(4, N, 2, -4, Bypass, 5016, 6205), new(6, Dead, -1, 0, N, 6206, 5000) }, // Torso
            new CriticalEffect[] { new(3, N, 5, 0, KnockedDown, 5001, 6201), new(3, N, 5, -2, KnockedDown, 5001, 6201), new(4, N, 5, -4, KnockedDown, 5001, 6201), new(3, KnockedDown, 2, 0, CrippledRightLeg, 6201, 6203), new(4, KnockedDown, 2, -3, CrippledRightLeg, 6201, 6203), new(4, KnockedDown | CrippledRightLeg, -1, 0, N, 6207, 5000) }, // Right leg
            new CriticalEffect[] { new(3, N, 5, 0, KnockedDown, 5001, 6201), new(3, N, 5, -3, KnockedDown, 5001, 6201), new(3, KnockedDown, 2, -2, CrippledLeftLeg, 6201, 6208), new(3, KnockedDown, 2, -2, CrippledLeftLeg, 6201, 6208), new(3, KnockedDown, 2, -5, CrippledLeftLeg, 6201, 6208), new(3, KnockedDown | CrippledLeftLeg, -1, 0, N, 6208, 5000) }, // Left leg
            new CriticalEffect[] { new(4, N, -1, 0, N, 5001, 5000), new(4, Bypass, 2, 0, LoseTurn, 6205, 6209), new(6, Bypass, 2, -3, LoseTurn, 6205, 6209), new(6, Bypass | LoseTurn, 2, -3, Blind, 6209, 6210), new(8, KnockedDown | Bypass | LoseTurn, 2, -3, Blind, 6209, 6210), new(8, Dead, -1, 0, N, 6202, 5000) }, // Eyes
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, -1, 0, N, 5001, 5000), new(3, Bypass, -1, 0, N, 6205, 5000), new(4, N, -1, 0, N, 5016, 5000), new(4, N, -1, 0, N, 5016, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 6209, 5000) }, // Groin
            new CriticalEffect[] { new(3, N, -1, 0, N, 1000, 5000), new(3, N, 2, 0, Bypass, 5001, 6205), new(3, N, 2, -2, Bypass, 5001, 6205), new(4, N, 2, -2, Bypass, 5016, 6205), new(4, N, 2, -4, Bypass, 5016, 6205), new(6, Dead, -1, 0, N, 6206, 5000) }, // Uncalled
        },
        // 13 Death Claw
        new[]
        {
            new CriticalEffect[] { new(4, N, -1, 0, N, 5016, 5000), new(4, N, 2, 0, KnockedDown, 5016, 5023), new(5, N, 2, -3, KnockedDown, 5016, 5023), new(5, N, 2, -5, KnockedDown, 5016, 5023), new(6, N, 2, -4, KnockedDown | LoseTurn, 5016, 5004), new(6, N, 2, -5, KnockedDown | LoseTurn, 5016, 5004) }, // Head
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, 2, 0, CrippledLeftArm, 5001, 5011), new(3, N, 2, -2, CrippledLeftArm, 5001, 5011), new(3, N, 2, -4, CrippledLeftArm, 5001, 5011), new(3, N, 2, -6, CrippledLeftArm, 5001, 5011), new(3, N, 2, -8, CrippledLeftArm, 5001, 5011) }, // Left arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, 2, 0, CrippledRightArm, 5001, 5014), new(3, N, 2, -2, CrippledRightArm, 5001, 5014), new(3, N, 2, -4, CrippledRightArm, 5001, 5014), new(3, N, 2, -6, CrippledRightArm, 5001, 5014), new(3, N, 2, -8, CrippledRightArm, 5001, 5014) }, // Right arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, 2, -1, Bypass, 5001, 6300), new(4, N, -1, 0, N, 5016, 5000), new(4, N, 2, -1, Bypass, 5016, 6300), new(5, KnockedDown | Bypass, -1, 0, N, 5004, 5000), new(5, KnockedDown | Bypass | LoseTurn, -1, 0, N, 5005, 5000) }, // Torso
            new CriticalEffect[] { new(3, N, 5, 0, KnockedDown, 5001, 5004), new(3, N, 2, 0, CrippledRightLeg, 5001, 5004), new(3, KnockedDown, 2, -2, CrippledRightLeg, 5001, 5004), new(4, KnockedDown, 2, -4, CrippledRightLeg, 5016, 5022), new(4, KnockedDown, 2, -5, CrippledRightLeg, 5023, 5024), new(4, KnockedDown, 2, -6, CrippledRightLeg, 5023, 5024) }, // Right leg
            new CriticalEffect[] { new(3, N, 5, 0, KnockedDown, 5001, 5004), new(3, N, 2, 0, CrippledRightLeg, 5001, 5004), new(3, KnockedDown, 2, -2, CrippledRightLeg, 5001, 5004), new(4, KnockedDown, 2, -4, CrippledRightLeg, 5016, 5022), new(4, KnockedDown, 2, -5, CrippledRightLeg, 5023, 5024), new(4, KnockedDown, 2, -6, CrippledRightLeg, 5023, 5024) }, // Left leg
            new CriticalEffect[] { new(4, N, -1, 0, N, 5001, 5000), new(4, N, 2, -3, LoseTurn, 5001, 6301), new(6, Bypass, 2, -6, LoseTurn, 6300, 6301), new(6, Bypass, 2, -2, Blind, 6301, 6302), new(8, Blind | Bypass, -1, 0, N, 6302, 5000), new(8, Blind | Bypass, -1, 0, N, 6302, 5000) }, // Eyes
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, -1, 0, N, 5001, 5000), new(4, N, -1, 0, N, 5001, 5000), new(4, N, -1, 0, N, 5016, 5000), new(5, N, 5, 0, KnockedDown, 5016, 5004), new(5, N, 5, -3, KnockedDown, 5016, 5004) }, // Groin
            new CriticalEffect[] { new(3, N, -1, 0, N, 5001, 5000), new(3, N, 2, -1, Bypass, 5001, 6300), new(4, N, -1, 0, N, 5016, 5000), new(4, N, 2, -1, Bypass, 5016, 6300), new(5, KnockedDown | Bypass, -1, 0, N, 5004, 5000), new(5, KnockedDown | Bypass | LoseTurn, -1, 0, N, 5005, 5000) }, // Uncalled
        },
        // 14 Plant
        new[]
        {
            new CriticalEffect[] { new(4, N, -1, 0, N, 6405, 5000), new(4, Bypass, -1, 0, N, 6400, 5000), new(5, N, -1, 0, N, 6401, 5000), new(5, Bypass, -1, 0, N, 6402, 5000), new(6, Bypass, 2, -3, LoseTurn, 6402, 6403), new(6, Bypass, 2, -6, LoseTurn, 6402, 6403) }, // Head
            new CriticalEffect[] { new(3, N, -1, 0, N, 6405, 5000), new(3, N, -1, 0, N, 6405, 5000), new(3, N, -1, 0, N, 6405, 5000), new(3, N, -1, 0, N, 6405, 5000), new(3, N, -1, 0, N, 6405, 5000), new(3, N, -1, 0, N, 6405, 5000) }, // Left arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 6405, 5000), new(3, N, -1, 0, N, 6405, 5000), new(3, N, -1, 0, N, 6405, 5000), new(3, N, -1, 0, N, 6405, 5000), new(3, N, -1, 0, N, 6405, 5000), new(3, N, -1, 0, N, 6405, 5000) }, // Right arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 6405, 5000), new(3, N, -1, 0, N, 6405, 5000), new(3, Bypass, -1, 0, N, 6400, 5000), new(4, N, -1, 0, N, 6401, 5000), new(4, N, -1, 0, N, 6401, 5000), new(4, Bypass, -1, 0, N, 6402, 5000) }, // Torso
            new CriticalEffect[] { new(3, N, -1, 0, N, 6405, 5000), new(3, N, -1, 0, N, 6405, 5000), new(3, N, -1, 0, N, 6405, 5000), new(3, N, -1, 0, N, 6405, 5000), new(3, N, -1, 0, N, 6405, 5000), new(3, N, -1, 0, N, 6405, 5000) }, // Right leg
            new CriticalEffect[] { new(3, N, -1, 0, N, 6405, 5000), new(3, N, -1, 0, N, 6405, 5000), new(3, N, -1, 0, N, 6405, 5000), new(3, N, -1, 0, N, 6405, 5000), new(3, N, -1, 0, N, 6405, 5000), new(3, N, -1, 0, N, 6405, 5000) }, // Left leg
            new CriticalEffect[] { new(4, N, -1, 0, N, 6405, 5000), new(4, Bypass, -1, 0, N, 6400, 5000), new(5, N, -1, 0, N, 6401, 5000), new(5, Bypass, -1, 0, N, 6402, 5000), new(6, Bypass, 2, -4, Blind, 6402, 6406), new(6, Blind | Bypass, -1, 0, N, 6406, 6404) }, // Eyes
            new CriticalEffect[] { new(3, N, -1, 0, N, 6405, 5000), new(3, N, -1, 0, N, 6405, 5000), new(4, Bypass, -1, 0, N, 6402, 5000), new(4, Bypass, -1, 0, N, 6402, 5000), new(5, Bypass, 2, -3, LoseTurn, 6402, 6403), new(5, Bypass, 2, -6, LoseTurn, 6402, 6403) }, // Groin
            new CriticalEffect[] { new(3, N, -1, 0, N, 6405, 5000), new(3, N, -1, 0, N, 6405, 5000), new(3, Bypass, -1, 0, N, 6400, 5000), new(4, N, -1, 0, N, 6401, 5000), new(4, N, -1, 0, N, 6401, 5000), new(4, Bypass, -1, 0, N, 6402, 5000) }, // Uncalled
        },
        // 15 Gecko
        new[]
        {
            new CriticalEffect[] { new(4, N, -1, 0, N, 6701, 5000), new(4, Bypass, 2, 0, KnockedOut, 6700, 5003), new(5, Bypass, 2, -3, KnockedOut, 6700, 5003), new(5, KnockedDown | Bypass, 2, -3, KnockedOut, 6700, 5003), new(6, KnockedOut | Bypass, 6, 0, Blind, 6700, 5006), new(6, Dead, -1, 0, N, 6700, 5000) }, // Head
            new CriticalEffect[] { new(3, N, -1, 0, N, 6702, 5000), new(3, LoseTurn, -1, 0, N, 6702, 5000), new(4, N, 2, -3, CrippledLeftArm, 6702, 5011), new(4, CrippledLeftArm | Bypass, -1, 0, N, 6702, 5000), new(4, CrippledLeftArm | Bypass, -1, 0, N, 6702, 5000), new(4, CrippledLeftArm | Bypass, -1, 0, N, 6702, 5000) }, // Left arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 6702, 5000), new(3, LoseTurn, -1, 0, N, 6702, 5000), new(4, N, 2, -3, CrippledRightArm, 6702, 5000), new(4, CrippledRightArm | Bypass, -1, 0, N, 6702, 5000), new(4, CrippledRightArm | Bypass, -1, 0, N, 6702, 5000), new(4, CrippledRightArm | Bypass, -1, 0, N, 6702, 5000) }, // Right arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 6701, 5000), new(3, Bypass, -1, 0, N, 6701, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 6704, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 6704, 5000), new(6, KnockedOut | Bypass, -1, 0, N, 6704, 5000), new(6, Dead, -1, 0, N, 6704, 5000) }, // Torso
            new CriticalEffect[] { new(3, KnockedDown, -1, 0, N, 6705, 5000), new(3, KnockedDown, 2, 0, CrippledRightLeg, 6705, 5024), new(4, KnockedDown, 2, -3, CrippledRightLeg, 6705, 5024), new(4, KnockedDown | CrippledRightLeg | Bypass, -1, 0, N, 6705, 5000), new(4, KnockedDown | CrippledRightLeg | Bypass, 2, 0, KnockedOut, 6705, 5026), new(4, KnockedOut | CrippledRightLeg | Bypass, -1, 0, N, 6705, 5000) }, // Right leg
            new CriticalEffect[] { new(3, KnockedDown, -1, 0, N, 6705, 5000), new(3, KnockedDown, 2, 0, CrippledLeftLeg, 6705, 5024), new(4, KnockedDown, 2, -3, CrippledLeftLeg, 6705, 5024), new(4, KnockedDown | CrippledLeftLeg | Bypass, -1, 0, N, 6705, 5000), new(4, KnockedDown | CrippledLeftLeg | Bypass, 2, 0, KnockedOut, 6705, 5026), new(4, KnockedOut | CrippledLeftLeg | Bypass, -1, 0, N, 6705, 5000) }, // Left leg
            new CriticalEffect[] { new(4, N, 6, 4, Blind, 6700, 5028), new(4, Bypass, 6, 3, Blind, 6700, 5028), new(6, Bypass, 6, 2, Blind, 6700, 5028), new(6, Blind | Bypass | LoseTurn, -1, 0, N, 6700, 5000), new(8, KnockedOut | Blind | Bypass, -1, 0, N, 6700, 5000), new(8, Dead, -1, 0, N, 6700, 5000) }, // Eyes
            new CriticalEffect[] { new(3, N, -1, 0, N, 6703, 5000), new(3, Bypass, 2, -3, KnockedDown, 6703, 5035), new(3, KnockedDown, 2, -3, KnockedOut, 6703, 5036), new(3, KnockedOut, -1, 0, N, 6703, 5000), new(4, KnockedDown | Bypass, 2, 0, KnockedOut, 6703, 5036), new(4, KnockedOut | Bypass, -1, 0, N, 6703, 5000) }, // Groin
            new CriticalEffect[] { new(3, N, -1, 0, N, 6700, 5000), new(3, Bypass, -1, 0, N, 6700, 5000), new(4, N, -1, 0, N, 6700, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 6700, 5000), new(6, KnockedOut | Bypass, -1, 0, N, 6700, 5000), new(6, Dead, -1, 0, N, 6700, 5000) }, // Uncalled
        },
        // 16 Alien
        new[]
        {
            new CriticalEffect[] { new(4, N, -1, 0, N, 6801, 5000), new(4, Bypass, 2, 0, KnockedOut, 6800, 5003), new(5, Bypass, 2, -3, KnockedOut, 6800, 5003), new(5, KnockedDown | Bypass, 2, -3, KnockedOut, 6803, 5003), new(6, KnockedOut | Bypass, 6, 0, Blind, 6804, 5006), new(6, Dead, -1, 0, N, 6804, 5000) }, // Head
            new CriticalEffect[] { new(3, N, -1, 0, N, 6806, 5000), new(3, LoseTurn, -1, 0, N, 6806, 5000), new(4, N, 2, -3, CrippledLeftArm, 6806, 5011), new(4, CrippledLeftArm | Bypass, -1, 0, N, 6806, 5000), new(4, CrippledLeftArm | Bypass, -1, 0, N, 6806, 5000), new(4, CrippledLeftArm | Bypass, -1, 0, N, 6806, 5000) }, // Left arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 6806, 5000), new(3, LoseTurn, -1, 0, N, 6806, 5000), new(4, N, 2, -3, CrippledRightArm, 6806, 5000), new(4, CrippledRightArm | Bypass, -1, 0, N, 6806, 5000), new(4, CrippledRightArm | Bypass, -1, 0, N, 6806, 5000), new(4, CrippledRightArm | Bypass, -1, 0, N, 6806, 5000) }, // Right arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 6800, 5000), new(3, Bypass, -1, 0, N, 6800, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 6800, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 6800, 5000), new(6, KnockedOut | Bypass, -1, 0, N, 6800, 5000), new(6, Dead, -1, 0, N, 6800, 5000) }, // Torso
            new CriticalEffect[] { new(3, KnockedDown, -1, 0, N, 6805, 5000), new(3, KnockedDown, 2, 0, CrippledRightLeg, 6805, 5024), new(4, KnockedDown, 2, -3, CrippledRightLeg, 6805, 5024), new(4, KnockedDown | CrippledRightLeg | Bypass, -1, 0, N, 6805, 5000), new(4, KnockedDown | CrippledRightLeg | Bypass, 2, 0, KnockedOut, 6805, 5026), new(4, KnockedOut | CrippledRightLeg | Bypass, -1, 0, N, 6805, 5000) }, // Right leg
            new CriticalEffect[] { new(3, KnockedDown, -1, 0, N, 6805, 5000), new(3, KnockedDown, 2, 0, CrippledLeftLeg, 6805, 5024), new(4, KnockedDown, 2, -3, CrippledLeftLeg, 6805, 5024), new(4, KnockedDown | CrippledLeftLeg | Bypass, -1, 0, N, 6805, 5000), new(4, KnockedDown | CrippledLeftLeg | Bypass, 2, 0, KnockedOut, 6805, 5026), new(4, KnockedOut | CrippledLeftLeg | Bypass, -1, 0, N, 6805, 5000) }, // Left leg
            new CriticalEffect[] { new(4, N, 6, 4, Blind, 6803, 5028), new(4, Bypass, 6, 3, Blind, 6803, 5028), new(6, Bypass, 6, 2, Blind, 6803, 5028), new(6, Blind | Bypass | LoseTurn, -1, 0, N, 6803, 5000), new(8, KnockedOut | Blind | Bypass, -1, 0, N, 6803, 5000), new(8, Dead, -1, 0, N, 6804, 5000) }, // Eyes
            new CriticalEffect[] { new(3, N, -1, 0, N, 6801, 5000), new(3, Bypass, 2, -3, KnockedDown, 6801, 5035), new(3, KnockedDown, 2, -3, KnockedOut, 6801, 5036), new(3, KnockedOut, -1, 0, N, 6801, 5000), new(4, KnockedDown | Bypass, 2, 0, KnockedOut, 6804, 5036), new(4, KnockedOut | Bypass, -1, 0, N, 6804, 5000) }, // Groin
            new CriticalEffect[] { new(3, N, -1, 0, N, 6800, 5000), new(3, Bypass, -1, 0, N, 6800, 5000), new(4, N, -1, 0, N, 6800, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 6800, 5000), new(6, KnockedOut | Bypass, -1, 0, N, 6800, 5000), new(6, Dead, -1, 0, N, 6800, 5000) }, // Uncalled
        },
        // 17 Giant Ant
        new[]
        {
            new CriticalEffect[] { new(4, N, -1, 0, N, 6901, 5000), new(4, Bypass, 2, 0, KnockedOut, 6901, 5003), new(5, Bypass, 2, -3, KnockedOut, 6902, 5003), new(5, KnockedDown | Bypass, 2, -3, KnockedOut, 6902, 5003), new(6, KnockedOut | Bypass, 6, 0, Blind, 6902, 5006), new(6, Dead, -1, 0, N, 6902, 5000) }, // Head
            new CriticalEffect[] { new(3, N, -1, 0, N, 6906, 5000), new(3, LoseTurn, -1, 0, N, 6906, 5000), new(4, N, 2, -3, CrippledLeftArm, 6906, 5011), new(4, CrippledLeftArm | Bypass, -1, 0, N, 6906, 5000), new(4, CrippledLeftArm | Bypass, -1, 0, N, 6906, 5000), new(4, CrippledLeftArm | Bypass, -1, 0, N, 6906, 5000) }, // Left arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 6906, 5000), new(3, LoseTurn, -1, 0, N, 6906, 5000), new(4, N, 2, -3, CrippledRightArm, 6906, 5000), new(4, CrippledRightArm | Bypass, -1, 0, N, 6906, 5000), new(4, CrippledRightArm | Bypass, -1, 0, N, 6906, 5000), new(4, CrippledRightArm | Bypass, -1, 0, N, 6906, 5000) }, // Right arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 6900, 5000), new(3, Bypass, -1, 0, N, 6900, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 6904, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 6904, 5000), new(6, KnockedOut | Bypass, -1, 0, N, 6904, 5000), new(6, Dead, -1, 0, N, 6904, 5000) }, // Torso
            new CriticalEffect[] { new(3, KnockedDown, -1, 0, N, 6905, 5000), new(3, KnockedDown, 2, 0, CrippledRightLeg, 6905, 5024), new(4, KnockedDown, 2, -3, CrippledRightLeg, 6905, 5024), new(4, KnockedDown | CrippledRightLeg | Bypass, -1, 0, N, 6905, 5000), new(4, KnockedDown | CrippledRightLeg | Bypass, 2, 0, KnockedOut, 6905, 5026), new(4, KnockedOut | CrippledRightLeg | Bypass, -1, 0, N, 6905, 5000) }, // Right leg
            new CriticalEffect[] { new(3, KnockedDown, -1, 0, N, 6905, 5000), new(3, KnockedDown, 2, 0, CrippledLeftLeg, 6905, 5024), new(4, KnockedDown, 2, -3, CrippledLeftLeg, 6905, 5024), new(4, KnockedDown | CrippledLeftLeg | Bypass, -1, 0, N, 6905, 5000), new(4, KnockedDown | CrippledLeftLeg | Bypass, 2, 0, KnockedOut, 6905, 5026), new(4, KnockedOut | CrippledLeftLeg | Bypass, -1, 0, N, 6905, 5000) }, // Left leg
            new CriticalEffect[] { new(4, N, 6, 4, Blind, 6900, 5028), new(4, Bypass, 6, 3, Blind, 6906, 5028), new(6, Bypass, 6, 2, Blind, 6901, 5028), new(6, Blind | Bypass | LoseTurn, -1, 0, N, 6901, 5000), new(8, KnockedOut | Blind | Bypass, -1, 0, N, 6901, 5000), new(8, Dead, -1, 0, N, 6901, 5000) }, // Eyes
            new CriticalEffect[] { new(3, N, -1, 0, N, 6900, 5000), new(3, Bypass, 2, -3, KnockedDown, 6900, 5035), new(3, KnockedDown, 2, -3, KnockedOut, 6900, 5036), new(3, KnockedOut, -1, 0, N, 6903, 5000), new(4, KnockedDown | Bypass, 2, 0, KnockedOut, 6903, 5036), new(4, KnockedOut | Bypass, -1, 0, N, 6903, 5000) }, // Groin
            new CriticalEffect[] { new(3, N, -1, 0, N, 6900, 5000), new(3, Bypass, -1, 0, N, 6900, 5000), new(4, N, -1, 0, N, 6904, 5000), new(4, KnockedDown | Bypass, -1, 0, N, 6904, 5000), new(6, KnockedOut | Bypass, -1, 0, N, 6904, 5000), new(6, Dead, -1, 0, N, 6904, 5000) }, // Uncalled
        },
        // 18 Big Bad Boss
        new[]
        {
            new CriticalEffect[] { new(3, N, -1, 0, N, 7101, 7100), new(3, N, -1, 0, N, 7102, 7103), new(4, N, -1, 0, N, 7102, 7103), new(4, LoseTurn, -1, 0, N, 7104, 7103), new(5, KnockedDown, 6, 0, Blind, 7105, 7106), new(6, KnockedDown, -1, 0, N, 7105, 7100) }, // Head
            new CriticalEffect[] { new(3, N, -1, 0, N, 7106, 7100), new(3, N, -1, 0, N, 7106, 7100), new(3, LoseTurn, -1, 0, N, 7106, 7011), new(4, LoseTurn, -1, 0, N, 7106, 7100), new(4, CrippledLeftArm, -1, 0, N, 7106, 7100), new(4, CrippledLeftArm, -1, 0, N, 7106, 7100) }, // Left arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 7106, 7100), new(3, N, -1, 0, N, 7106, 7100), new(3, LoseTurn, -1, 0, N, 7106, 7100), new(4, LoseTurn, -1, 0, N, 7106, 7100), new(4, CrippledRightArm, -1, 0, N, 7106, 7100), new(4, CrippledRightArm, -1, 0, N, 7106, 7100) }, // Right arm
            new CriticalEffect[] { new(3, N, -1, 0, N, 7106, 7100), new(3, N, -1, 0, N, 7106, 7100), new(3, N, -1, 0, N, 7106, 7100), new(4, N, -1, 0, N, 7106, 7100), new(4, KnockedDown, -1, 0, N, 7106, 7100), new(5, KnockedDown, -1, 0, N, 7106, 7100) }, // Torso
            new CriticalEffect[] { new(3, KnockedDown, -1, 0, N, 7106, 7100), new(3, KnockedDown, 2, 0, CrippledRightLeg, 7106, 7106), new(3, KnockedDown, 2, -3, CrippledRightLeg, 7060, 7106), new(4, KnockedDown | CrippledRightLeg, -1, 0, N, 7106, 7100), new(4, KnockedDown | CrippledRightLeg, -1, 0, N, 7106, 7106), new(4, CrippledRightLeg, -1, 0, N, 7106, 7100) }, // Right leg
            new CriticalEffect[] { new(3, KnockedDown, -1, 0, N, 7106, 7100), new(3, KnockedDown, 2, 0, CrippledLeftLeg, 7106, 7024), new(3, KnockedDown, 2, -3, CrippledLeftLeg, 7106, 7024), new(4, KnockedDown | CrippledLeftLeg, -1, 0, N, 7106, 7100), new(4, KnockedDown | CrippledLeftLeg, -1, 0, N, 7106, 7106), new(4, CrippledLeftLeg, -1, 0, N, 7106, 7100) }, // Left leg
            new CriticalEffect[] { new(3, N, -1, 0, N, 7106, 7106), new(3, N, -1, 0, N, 7106, 7106), new(4, N, 6, 2, Blind, 7106, 7106), new(4, LoseTurn, -1, 0, N, 7106, 7100), new(5, Blind | LoseTurn, -1, 0, N, 7106, 7100), new(5, Blind | LoseTurn, -1, 0, N, 7106, 7100) }, // Eyes
            new CriticalEffect[] { new(3, N, -1, 0, N, 7106, 7100), new(3, N, 2, -3, KnockedDown, 7106, 7106), new(3, KnockedDown, -1, 0, N, 7106, 7106), new(3, KnockedDown, -1, 0, N, 7106, 7100), new(4, N, -1, 0, N, 7106, 7106), new(4, KnockedDown, -1, 0, N, 7106, 7100) }, // Groin
            new CriticalEffect[] { new(3, N, -1, 0, N, 7106, 7100), new(3, N, -1, 0, N, 7106, 7100), new(4, N, -1, 0, N, 7106, 7100), new(4, N, -1, 0, N, 7106, 7100), new(5, KnockedDown, -1, 0, N, 7106, 7100), new(5, KnockedDown, -1, 0, N, 7106, 7100) }, // Uncalled
        },
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
