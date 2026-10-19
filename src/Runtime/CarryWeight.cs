using System;
using System.Collections.Generic;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

/// <summary>Explicit neutral test profile. Perks, stat changes and loaded-weapon ammunition are outside this profile.</summary>
public static class CarryWeight
{
    public const int TestStrength = 5;
    public static int TestCapacity => CapacityForStrength(TestStrength);
    public static int CapacityForStrength(int strength) => strength is >= 1 and <= 10 ? 25 * strength + 25
        : throw new ArgumentOutOfRangeException(nameof(strength));
    public static long? UnitWeight(Prototype prototype, MapObject item)
    {
        if (prototype.Type != 0 || prototype.Pid != item.Header.Pid || prototype.Subtype != item.Subtype
            || prototype.BaseWeight is not >= 0 || item.Inventory.Length != 0 || prototype.Subtype is null or 1) return null;
        if ((prototype.ExtendedFlags & 0x08000000) != 0) return 0;
        if (prototype.Subtype == 3 && (!item.State.TryGetValue("ammoQuantity", out int ammo) || ammo != 0)) return null;
        long weight = prototype.BaseWeight.Value;
        if (prototype.Subtype == 0 && prototype.Pid is 3 or 232 or 348 or 349) weight /= 2;
        return weight;
    }
    public static long? Total(IEnumerable<(long? UnitWeight, int Quantity)> lines)
    {
        long total = 0;
        try
        {
            foreach (var line in lines)
            {
                if (line.UnitWeight is not >= 0 || line.Quantity <= 0) return null;
                total = checked(total + checked(line.UnitWeight.Value * line.Quantity));
            }
            return total;
        }
        catch (OverflowException) { return null; }
    }
}
