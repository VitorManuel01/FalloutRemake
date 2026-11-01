using System;
using System.IO;
using System.Linq;

namespace FalloutRemake.Runtime;

/// <summary>What a worn armor's perk adds: bonus Strength and radiation resistance (CE STAT_RADIATION_RESISTANCE).</summary>
public sealed record ArmorPerk(int Id, int Strength, int RadiationResistance)
{
    public static ArmorPerk None { get; } = new(-1, 0, 0);
    public bool HasEffect => Strength != 0 || RadiationResistance != 0;
}

/// <summary>
/// CE _adjust_ac gives a party member its armor's perk (armorGetPerk) through perkAddEffect: the perk's stat modifier
/// (gPerkDescriptions stat/statModifier) and, for perks without ranks (maxRank -1), its SPECIAL row as bonus stats. These
/// are the gPerkDescriptions rows (fallout2.exe 0x519DCC) of the only perks the original armor PROs name (power armor,
/// combat armor, advanced power armor I and II). Any other perk id is refused rather than guessed.
/// </summary>
public static class ArmorPerks
{
    public const int PoweredArmor = 62, CombatArmor = 63, AdvancedArmorI = 68, AdvancedArmorII = 69;
    public const int RadiationResistanceStat = 31;

    public static readonly ArmorPerk[] Rows =
    {
        new(PoweredArmor, 3, 30), new(CombatArmor, 0, 20), new(AdvancedArmorI, 4, 60), new(AdvancedArmorII, 4, 75)
    };

    public static ArmorPerk Effect(int perk) => perk == -1 ? ArmorPerk.None
        : Rows.FirstOrDefault(r => r.Id == perk) ?? throw new InvalidDataException($"Armor perk {perk} is not one the original armor uses.");
}
