using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FalloutRemake.Runtime;

/// <summary>One row of CE gPerkDescriptions: the per-rank stat bonus (perkAddEffect) and the requirements (perkCanAdd).</summary>
public sealed record PerkDefinition(int Id, int Frm, int MaxRank, int MinLevel, int Stat, int StatModifier, int Param1, int Value1, int ParamMode,
    int Param2, int Value2, int[] Stats)
{
    /// <summary>The character editor offers it (CE perkCanAdd refuses max rank -1: armor, drug and event perks).</summary>
    public bool Selectable => MaxRank != -1;
}

/// <summary>What a perk does in this game today.</summary>
public enum PerkEffect
{
    /// <summary>Its CE rule changes something the game uses now.</summary>
    Applied,
    /// <summary>Its CE rule is applied to the sheet, but no system of the game uses that number yet (healing rate, firearms...).</summary>
    SheetOnly,
    /// <summary>Its effect is not modeled: the perk is listed but cannot be chosen yet.</summary>
    NotModeled
}

/// <summary>CE perk ids (perk_defs.h order).</summary>
public static class Perk
{
    public const int Awareness = 0, BonusHtHAttacks = 1, BonusHtHDamage = 2, BonusMove = 3, BonusRangedDamage = 4, BonusRateOfFire = 5, EarlierSequence = 6, FasterHealing = 7, MoreCriticals = 8, NightVision = 9, Presence = 10, RadResistance = 11, Toughness = 12, StrongBack = 13, Sharpshooter = 14, SilentRunning = 15, Survivalist = 16, MasterTrader = 17, Educated = 18, Healer = 19, FortuneFinder = 20, BetterCriticals = 21, Empathy = 22, Slayer = 23, Sniper = 24, SilentDeath = 25, ActionBoy = 26, MentalBlock = 27, Lifegiver = 28, Dodger = 29, Snakeater = 30, MrFixit = 31, Medic = 32, MasterThief = 33, Speaker = 34, HeaveHo = 35, FriendlyFoe = 36, Pickpocket = 37, Ghost = 38, CultOfPersonality = 39, Scrounger = 40, Explorer = 41, FlowerChild = 42, Pathfinder = 43, AnimalFriend = 44, Scout = 45, MysteriousStranger = 46, Ranger = 47, QuickPockets = 48, SmoothTalker = 49, SwiftLearner = 50, Tag = 51, Mutate = 52, NukaColaAddiction = 53, BuffoutAddiction = 54, MentatsAddiction = 55, PsychoAddiction = 56, RadawayAddiction = 57, WeaponLongRange = 58, WeaponAccurate = 59, WeaponPenetrate = 60, WeaponKnockback = 61, PoweredArmor = 62, CombatArmor = 63, WeaponScopeRange = 64, WeaponFastReload = 65, WeaponNightSight = 66, WeaponFlameboy = 67, ArmorAdvancedI = 68, ArmorAdvancedIi = 69, JetAddiction = 70, TragicAddiction = 71, ArmorCharisma = 72, GeckoSkinning = 73, DermalImpactArmor = 74, DermalImpactAssaultEnhancement = 75, PhoenixArmorImplants = 76, PhoenixAssaultEnhancement = 77, VaultCityInoculations = 78, AdrenalineRush = 79, CautiousNature = 80, Comprehension = 81, DemolitionExpert = 82, Gambler = 83, GainStrength = 84, GainPerception = 85, GainEndurance = 86, GainCharisma = 87, GainIntelligence = 88, GainAgility = 89, GainLuck = 90, Harmless = 91, HereAndNow = 92, HtHEvade = 93, KamaSutraMaster = 94, KarmaBeacon = 95, LightStep = 96, LivingAnatomy = 97, MagneticPersonality = 98, Negotiator = 99, PackRat = 100, Pyromaniac = 101, QuickRecovery = 102, Salesman = 103, Stonewall = 104, Thief = 105, WeaponHandling = 106, VaultCityTraining = 107, AlcoholRaisedHitPoints = 108, AlcoholRaisedHitPointsIi = 109, AlcoholLoweredHitPoints = 110, AlcoholLoweredHitPointsIi = 111, AutodocRaisedHitPoints = 112, AutodocRaisedHitPointsIi = 113, AutodocLoweredHitPoints = 114, AutodocLoweredHitPointsIi = 115, ExpertExcrementExpeditor = 116, WeaponEnhancedKnockout = 117, Jinxed = 118;
}

/// <summary>The 119 CE perks, their requirements and what each does here.</summary>
public static class PerkCatalog
{
    public const int Count = 119;
    // PERK_PARAM_MODE_*: the first requirement only, either one, or both.
    public const int FirstOnly = 0, Either = 1, Both = 2, GlobalFlag = 0x4000000;

    // gPerkDescriptions (fallout2.exe 0x519DCC): frm, max rank, min level, stat, stat modifier, param1, value1, param mode,
    // param2, value2, SPECIAL requirements (generated from CE perk.cc; --perk-report compares every row with the executable).
    public static readonly PerkDefinition[] Rows =
    {
        new(0, 72, 1, 3, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 5, 0, 0, 0, 0, 0 }), // Awareness
        new(1, 73, 1, 15, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 6, 0 }), // BonusHtHAttacks
        new(2, 74, 3, 3, 11, 2, -1, 0, 0, -1, 0, new[] { 6, 0, 0, 0, 0, 6, 0 }), // BonusHtHDamage
        new(3, 75, 2, 6, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 5, 0 }), // BonusMove
        new(4, 76, 2, 6, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 6, 6 }), // BonusRangedDamage
        new(5, 77, 1, 15, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 6, 0, 0, 6, 7, 0 }), // BonusRateOfFire
        new(6, 78, 3, 3, 13, 2, -1, 0, 0, -1, 0, new[] { 0, 6, 0, 0, 0, 0, 0 }), // EarlierSequence
        new(7, 79, 3, 3, 14, 2, -1, 0, 0, -1, 0, new[] { 0, 0, 6, 0, 0, 0, 0 }), // FasterHealing
        new(8, 80, 3, 6, 15, 5, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 6 }), // MoreCriticals
        new(9, 81, 1, 3, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 6, 0, 0, 0, 0, 0 }), // NightVision
        new(10, 82, 3, 3, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 6, 0, 0, 0 }), // Presence
        new(11, 83, 2, 6, 31, 15, -1, 0, 0, -1, 0, new[] { 0, 0, 6, 0, 4, 0, 0 }), // RadResistance
        new(12, 84, 3, 3, 24, 10, -1, 0, 0, -1, 0, new[] { 0, 0, 6, 0, 0, 0, 6 }), // Toughness
        new(13, 85, 3, 3, 12, 50, -1, 0, 0, -1, 0, new[] { 6, 0, 6, 0, 0, 0, 0 }), // StrongBack
        new(14, 86, 1, 9, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 7, 0, 0, 6, 0, 0 }), // Sharpshooter
        new(15, 87, 1, 6, -1, 0, 8, 50, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 6, 0 }), // SilentRunning
        new(16, 88, 1, 3, -1, 0, 17, 40, 0, -1, 0, new[] { 0, 0, 6, 0, 6, 0, 0 }), // Survivalist
        new(17, 89, 1, 12, -1, 0, 15, 75, 0, -1, 0, new[] { 0, 0, 0, 7, 0, 0, 0 }), // MasterTrader
        new(18, 90, 3, 6, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 6, 0, 0 }), // Educated
        new(19, 91, 2, 3, -1, 0, 6, 40, 0, -1, 0, new[] { 0, 7, 0, 0, 5, 6, 0 }), // Healer
        new(20, 92, 1, 6, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 8 }), // FortuneFinder
        new(21, 93, 1, 9, 16, 20, -1, 0, 0, -1, 0, new[] { 0, 6, 0, 0, 0, 4, 6 }), // BetterCriticals
        new(22, 94, 1, 6, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 7, 0, 0, 5, 0, 0 }), // Empathy
        new(23, 95, 1, 24, -1, 0, 3, 80, 0, -1, 0, new[] { 8, 0, 0, 0, 0, 8, 0 }), // Slayer
        new(24, 96, 1, 24, -1, 0, 0, 80, 0, -1, 0, new[] { 0, 8, 0, 0, 0, 8, 0 }), // Sniper
        new(25, 97, 1, 18, -1, 0, 8, 80, 2, 3, 80, new[] { 0, 0, 0, 0, 0, 10, 0 }), // SilentDeath
        new(26, 98, 2, 12, 8, 1, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 5, 0 }), // ActionBoy
        new(27, 99, 1, 310, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // MentalBlock
        new(28, 100, 2, 12, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 4, 0, 0, 0, 0 }), // Lifegiver
        new(29, 101, 1, 9, 9, 5, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 6, 0 }), // Dodger
        new(30, 102, 2, 6, 32, 25, -1, 0, 0, -1, 0, new[] { 0, 0, 3, 0, 0, 0, 0 }), // Snakeater
        new(31, 103, 1, 12, -1, 0, 13, 40, 1, 12, 40, new[] { 0, 0, 0, 0, 0, 0, 0 }), // MrFixit
        new(32, 104, 1, 12, -1, 0, 6, 40, 1, 7, 40, new[] { 0, 0, 0, 0, 0, 0, 0 }), // Medic
        new(33, 105, 1, 12, -1, 0, 10, 50, 2, 9, 50, new[] { 0, 0, 0, 0, 0, 0, 0 }), // MasterThief
        new(34, 106, 1, 9, -1, 0, 14, 50, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // Speaker
        new(35, 107, 3, 6, -1, 0, -1, 0, 0, -1, 0, new[] { -9, 0, 0, 0, 0, 0, 0 }), // HeaveHo
        new(36, 108, 1, 310, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 4, 0, 0, 0, 0, 0 }), // FriendlyFoe
        new(37, 109, 1, 15, -1, 0, 10, 80, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 8, 0 }), // Pickpocket
        new(38, 110, 1, 6, -1, 0, 8, 60, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // Ghost
        new(39, 111, 1, 12, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 10, 0, 0, 0 }), // CultOfPersonality
        new(40, 112, 1, 310, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 8 }), // Scrounger
        new(41, 113, 1, 9, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // Explorer
        new(42, 114, 1, 310, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 5, 0, 0, 0, 0 }), // FlowerChild
        new(43, 115, 2, 6, -1, 0, 17, 40, 0, -1, 0, new[] { 0, 0, 6, 0, 0, 0, 0 }), // Pathfinder
        new(44, 116, 1, 310, -1, 0, 17, 25, 0, -1, 0, new[] { 0, 0, 0, 0, 5, 0, 0 }), // AnimalFriend
        new(45, 117, 1, 3, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 7, 0, 0, 0, 0, 0 }), // Scout
        new(46, 118, 1, 9, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 4 }), // MysteriousStranger
        new(47, 119, 1, 6, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 6, 0, 0, 0, 0, 0 }), // Ranger
        new(48, 120, 1, 3, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 5, 0 }), // QuickPockets
        new(49, 121, 3, 3, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 4, 0, 0 }), // SmoothTalker
        new(50, 122, 3, 3, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 4, 0, 0 }), // SwiftLearner
        new(51, 123, 1, 12, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // Tag
        new(52, 124, 1, 9, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // Mutate
        new(53, 125, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // NukaColaAddiction
        new(54, 126, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { -2, 0, -2, 0, 0, -3, 0 }), // BuffoutAddiction
        new(55, 127, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, -3, -2, 0 }), // MentatsAddiction
        new(56, 128, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, -2, 0, 0 }), // PsychoAddiction
        new(57, 129, -1, 1, 31, -20, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // RadawayAddiction
        new(58, 130, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // WeaponLongRange
        new(59, 131, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // WeaponAccurate
        new(60, 132, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // WeaponPenetrate
        new(61, 133, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // WeaponKnockback
        new(62, 134, -1, 1, 31, 30, -1, 0, 0, -1, 0, new[] { 3, 0, 0, 0, 0, 0, 0 }), // PoweredArmor
        new(63, 135, -1, 1, 31, 20, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // CombatArmor
        new(64, 136, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // WeaponScopeRange
        new(65, 137, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // WeaponFastReload
        new(66, 138, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // WeaponNightSight
        new(67, 139, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // WeaponFlameboy
        new(68, 140, -1, 1, 31, 60, -1, 0, 0, -1, 0, new[] { 4, 0, 0, 0, 0, 0, 0 }), // ArmorAdvancedI
        new(69, 141, -1, 1, 31, 75, -1, 0, 0, -1, 0, new[] { 4, 0, 0, 0, 0, 0, 0 }), // ArmorAdvancedIi
        new(70, 136, -1, 1, 8, -1, -1, 0, 0, -1, 0, new[] { -1, -1, 0, 0, 0, 0, 0 }), // JetAddiction
        new(71, 149, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, -2, 0, 0, -1, 0, -1 }), // TragicAddiction
        new(72, 154, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 2, 0, 0, 0 }), // ArmorCharisma
        new(73, 158, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // GeckoSkinning
        new(74, 157, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // DermalImpactArmor
        new(75, 157, -1, 1, 3, -1, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // DermalImpactAssaultEnhancement
        new(76, 168, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // PhoenixArmorImplants
        new(77, 168, -1, 1, 3, -1, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // PhoenixAssaultEnhancement
        new(78, 172, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // VaultCityInoculations
        new(79, 155, 1, 6, -1, 0, -1, 0, 0, -1, 0, new[] { -10, 0, 0, 0, 0, 0, 0 }), // AdrenalineRush
        new(80, 156, 1, 3, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 6, 0, 0, 0, 0, 0 }), // CautiousNature
        new(81, 122, 1, 3, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 6, 0, 0 }), // Comprehension
        new(82, 39, 1, 9, -1, 0, 11, 75, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 4, 0 }), // DemolitionExpert
        new(83, 44, 1, 6, -1, 0, 16, 50, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // Gambler
        new(84, 0, 1, 12, -1, 0, -1, 0, 0, -1, 0, new[] { -10, 0, 0, 0, 0, 0, 0 }), // GainStrength
        new(85, 1, 1, 12, -1, 0, -1, 0, 0, -1, 0, new[] { 0, -10, 0, 0, 0, 0, 0 }), // GainPerception
        new(86, 2, 1, 12, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, -10, 0, 0, 0, 0 }), // GainEndurance
        new(87, 3, 1, 12, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, -10, 0, 0, 0 }), // GainCharisma
        new(88, 4, 1, 12, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, -10, 0, 0 }), // GainIntelligence
        new(89, 5, 1, 12, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, -10, 0 }), // GainAgility
        new(90, 6, 1, 12, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, -10 }), // GainLuck
        new(91, 160, 1, 6, -1, 0, 10, 50, 2, 67108864, 50, new[] { 0, 0, 0, 0, 0, 0, 0 }), // Harmless
        new(92, 161, 1, 3, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // HereAndNow
        new(93, 159, 1, 12, -1, 0, 3, 75, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // HtHEvade
        new(94, 163, 1, 3, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 5, 0, 0, 5, 0 }), // KamaSutraMaster
        new(95, 162, 1, 9, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 6, 0, 0, 0 }), // KarmaBeacon
        new(96, 164, 1, 9, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 5, 5 }), // LightStep
        new(97, 165, 1, 12, -1, 0, 7, 60, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // LivingAnatomy
        new(98, 166, 1, 6, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, -10, 0, 0, 0 }), // MagneticPersonality
        new(99, 43, 1, 6, -1, 0, 15, 50, 2, 14, 50, new[] { 0, 0, 0, 0, 0, 0, 0 }), // Negotiator
        new(100, 167, 1, 6, 12, 50, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // PackRat
        new(101, 169, 1, 9, -1, 0, 1, 75, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // Pyromaniac
        new(102, 170, 1, 6, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 5, 0 }), // QuickRecovery
        new(103, 121, 1, 6, -1, 0, 15, 50, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // Salesman
        new(104, 171, 1, 3, -1, 0, -1, 0, 0, -1, 0, new[] { 6, 0, 0, 0, 0, 0, 0 }), // Stonewall
        new(105, 38, 1, 3, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // Thief
        new(106, 173, 1, 12, -1, 0, -1, 0, 0, -1, 0, new[] { -7, 0, 0, 0, 0, 5, 0 }), // WeaponHandling
        new(107, 104, -1, 1, -1, 0, 7, 75, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // VaultCityTraining
        new(108, 142, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // AlcoholRaisedHitPoints
        new(109, 142, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // AlcoholRaisedHitPointsIi
        new(110, 52, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // AlcoholLoweredHitPoints
        new(111, 52, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // AlcoholLoweredHitPointsIi
        new(112, 104, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // AutodocRaisedHitPoints
        new(113, 104, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // AutodocRaisedHitPointsIi
        new(114, 35, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // AutodocLoweredHitPoints
        new(115, 35, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // AutodocLoweredHitPointsIi
        new(116, 154, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // ExpertExcrementExpeditor
        new(117, 154, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // WeaponEnhancedKnockout
        new(118, 64, -1, 1, -1, 0, -1, 0, 0, -1, 0, new[] { 0, 0, 0, 0, 0, 0, 0 }), // Jinxed
    };

    public static PerkDefinition Get(int perk) => perk is >= 0 and < Count ? Rows[perk] : throw new ArgumentOutOfRangeException(nameof(perk));

    // Perks whose rule this game applies in full (generic stat bonuses and the CE code ported for them).
    private static readonly HashSet<int> AppliedPerks = new()
    {
        Perk.BonusHtHAttacks, Perk.BonusHtHDamage, Perk.BonusMove, Perk.EarlierSequence, Perk.MoreCriticals, Perk.Toughness, Perk.StrongBack,
        Perk.Sharpshooter, Perk.Educated, Perk.BetterCriticals, Perk.Slayer, Perk.ActionBoy, Perk.Lifegiver, Perk.Dodger, Perk.Snakeater,
        Perk.MasterThief, Perk.HeaveHo, Perk.QuickPockets, Perk.GainStrength, Perk.GainPerception, Perk.GainEndurance, Perk.GainCharisma,
        Perk.GainIntelligence, Perk.GainAgility, Perk.GainLuck, Perk.HtHEvade, Perk.LivingAnatomy, Perk.PackRat, Perk.QuickRecovery,
        Perk.Thief, Perk.WeaponHandling, Perk.Tag
    };
    // Perks whose rule is on the sheet with no system using it yet.
    private static readonly HashSet<int> SheetPerks = new()
    {
        Perk.BonusRangedDamage, Perk.BonusRateOfFire, Perk.FasterHealing, Perk.RadResistance, Perk.Sniper, Perk.Medic, Perk.MrFixit,
        Perk.Speaker, Perk.Ranger, Perk.Survivalist, Perk.Gambler, Perk.Negotiator, Perk.Salesman, Perk.Pyromaniac, Perk.VaultCityTraining
    };

    public static PerkEffect Effect(int perk) => AppliedPerks.Contains(perk) ? PerkEffect.Applied : SheetPerks.Contains(perk) ? PerkEffect.SheetOnly : PerkEffect.NotModeled;
}

/// <summary>
/// A perk the player chose, with the level it was chosen at (Lifegiver and Educated add again at every later level) and,
/// for Tag!, the fourth skill it tags (CE gTaggedSkills[3]).
/// </summary>
public sealed record ChosenPerk([property: System.Text.Json.Serialization.JsonRequired] int Perk, [property: System.Text.Json.Serialization.JsonRequired] int Level,
    int? Skill = null);

/// <summary>The player's perks in the order chosen: ranks, the CE stat and skill bonuses, and the per-level history.</summary>
public sealed class PerkRanks
{
    public static PerkRanks None { get; } = new(Array.Empty<ChosenPerk>());
    private readonly ChosenPerk[] _chosen;
    private readonly int[] _ranks = new int[PerkCatalog.Count];

    public PerkRanks(IEnumerable<ChosenPerk> chosen)
    {
        _chosen = (chosen ?? throw new ArgumentNullException(nameof(chosen))).ToArray();
        foreach (ChosenPerk row in _chosen)
        {
            if (row == null || row.Perk is < 0 or >= PerkCatalog.Count) throw new ArgumentOutOfRangeException(nameof(chosen));
            _ranks[row.Perk]++;
        }
    }

    public IReadOnlyList<ChosenPerk> Chosen => _chosen;
    public int Count => _chosen.Length;
    public int Rank(int perk) => perk is >= 0 and < PerkCatalog.Count ? _ranks[perk] : 0;
    public bool Has(int perk) => Rank(perk) != 0;
    /// <summary>Ranks chosen at a level below <paramref name="level"/> (the ones a level-up to it counts).</summary>
    public int RankBefore(int perk, int level) => _chosen.Count(c => c.Perk == perk && c.Level < level);
    public PerkRanks With(ChosenPerk chosen) => new(_chosen.Append(chosen));
    /// <summary>The skill Tag! tagged, if chosen.</summary>
    public int? TagSkill => _chosen.FirstOrDefault(c => c.Perk == Perk.Tag)?.Skill;

    /// <summary>perkAddEffect: the per-rank modifier of every chosen perk on one CE stat.</summary>
    public int StatBonus(int stat) => _chosen.Select(c => PerkCatalog.Get(c.Perk)).Where(d => d.Stat == stat).Sum(d => d.StatModifier);

    /// <summary>CE perkGetSkillModifier (Ghost's light-dependent Sneak bonus is not modeled).</summary>
    public int SkillModifier(int skill)
    {
        int m = 0;
        switch (skill)
        {
            case CharacterSkill.FirstAid:
                if (Has(Perk.Medic)) m += 10;
                if (Has(Perk.VaultCityTraining)) m += 5;
                break;
            case CharacterSkill.Doctor:
                if (Has(Perk.Medic)) m += 10;
                if (Has(Perk.LivingAnatomy)) m += 10;
                if (Has(Perk.VaultCityTraining)) m += 5;
                break;
            case CharacterSkill.Sneak: case CharacterSkill.Lockpick: case CharacterSkill.Steal: case CharacterSkill.Traps:
                if (Has(Perk.Thief)) m += 10;
                if (skill is CharacterSkill.Lockpick or CharacterSkill.Steal && Has(Perk.MasterThief)) m += 15;
                if (skill == CharacterSkill.Steal && Has(Perk.Harmless)) m += 20;
                break;
            case CharacterSkill.Science: case CharacterSkill.Repair:
                if (Has(Perk.MrFixit)) m += 10;
                break;
            case CharacterSkill.Speech: case CharacterSkill.Barter:
                if (skill == CharacterSkill.Speech && Has(Perk.Speaker)) m += 20;
                if (skill == CharacterSkill.Speech && Has(Perk.ExpertExcrementExpeditor)) m += 5;
                if (Has(Perk.Negotiator)) m += 10;
                if (skill == CharacterSkill.Barter && Has(Perk.Salesman)) m += 20;
                break;
            case CharacterSkill.Gambling:
                if (Has(Perk.Gambler)) m += 20;
                break;
            case CharacterSkill.Outdoorsman:
                if (Has(Perk.Ranger)) m += 15;
                if (Has(Perk.Survivalist)) m += 25;
                break;
        }
        return m;
    }
}

public enum PerkChoiceStatus { Ready, NoPerkAvailable, Unknown, NotSelectable, MaxRank, Level, Requirement, Special, NotModeled, InCombat, SkillChoice }

/// <summary>CE perkCanAdd for the player, against its current sheet (critterGetStat SPECIAL, skillGetValue) and level.</summary>
public static class PerkRules
{
    public static PerkChoiceStatus CanAdd(int perk, PlayerCharacter sheet, int level, PerkRanks ranks)
    {
        ArgumentNullException.ThrowIfNull(sheet); ArgumentNullException.ThrowIfNull(ranks);
        if (perk is < 0 or >= PerkCatalog.Count) return PerkChoiceStatus.Unknown;
        PerkDefinition d = PerkCatalog.Get(perk);
        if (!d.Selectable) return PerkChoiceStatus.NotSelectable;
        if (ranks.Rank(perk) >= d.MaxRank) return PerkChoiceStatus.MaxRank;
        if (level < d.MinLevel) return PerkChoiceStatus.Level;
        bool first = Requirement(d.Param1, d.Value1, sheet, out bool firstKnown);
        if (!firstKnown) return PerkChoiceStatus.Requirement;
        if (!first || d.ParamMode == PerkCatalog.Both)
        {
            if (d.ParamMode == PerkCatalog.FirstOnly) return PerkChoiceStatus.Requirement;
            if (!first && d.ParamMode == PerkCatalog.Both) return PerkChoiceStatus.Requirement;
            if (d.Param2 == -1) return PerkChoiceStatus.Requirement;
            if (!Requirement(d.Param2, d.Value2, sheet, out bool secondKnown) || !secondKnown) return PerkChoiceStatus.Requirement;
        }
        int[] special = sheet.Effective.ToArray();
        for (int stat = 0; stat < 7; stat++)
            if (d.Stats[stat] < 0 ? special[stat] >= -d.Stats[stat] : special[stat] < d.Stats[stat]) return PerkChoiceStatus.Special;
        return PerkCatalog.Effect(perk) == PerkEffect.NotModeled ? PerkChoiceStatus.NotModeled : PerkChoiceStatus.Ready;
    }

    // A skill threshold (a negative value means below it); a global variable (karma) is not tracked, so it stays unknown.
    private static bool Requirement(int param, int value, PlayerCharacter sheet, out bool known)
    {
        known = true;
        if (param == -1) return true;
        if ((param & PerkCatalog.GlobalFlag) != 0) { known = false; return false; }
        if (param is < 0 or >= CharacterSkill.Count) throw new InvalidDataException("Perk requirement names an unknown skill.");
        int skill = sheet.Skill(param);
        return value < 0 ? skill < -value : skill >= value;
    }
}
