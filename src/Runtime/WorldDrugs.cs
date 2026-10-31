using System;
using System.Linq;
using FalloutRemake.Import;
using FalloutRemake.Runtime.Combat;

namespace FalloutRemake.Runtime;

public enum DrugUseStatus { Ready, NoItem, Unsupported, Busy, InCombat }
/// <summary>Result of taking a drug: the rolled amount, the HP actually restored (capped at the maximum) and the HP after.</summary>
public sealed record DrugUseResult(DrugUseStatus Status, int Rolled = 0, int Healed = 0, int HitPoints = 0);

/// <summary>
/// Healing Powder (PID 273) as read from its original PRO: immediate random 8–18 current HP (stat −2 → stat 35) and
/// PE −1, restored after 360 game minutes. Only the HP part is applied: the project has no game clock to expire the
/// PE penalty yet. The prototype is pinned to its source fingerprint, like the explosive profile.
/// </summary>
public static class HealingDrugProfile
{
    public const int Pid = 273, MinimumHealing = 8, MaximumHealing = 18;
    public const string SourcePrototypeHash = "144249197661F7B116EB3B6A4958326EA7D7920438D818FA9979F569D344FA51";

    /// <summary>Trusted identity: the fingerprint pins the PRO bytes whose effect <see cref="Matches"/> confirms.</summary>
    public static bool Supported(Prototype? prototype) => prototype is { Pid: Pid, Type: 0, Subtype: DrugPrototypeData.Subtype, ScriptId: -1 }
        && prototype.Resource.Size == 125 && prototype.Resource.Sha256 == SourcePrototypeHash;

    /// <summary>The original effect this profile applies: stat −2 → random current HP between the first two amounts.</summary>
    public static bool Matches(DrugPrototypeData drug)
    {
        ArgumentNullException.ThrowIfNull(drug);
        int[] stats = drug.Stats, amounts = drug.Immediate;
        return drug.Pid == Pid && stats[0] == DrugPrototypeData.RandomStat && stats[1] == DrugPrototypeData.CurrentHitPoints
            && amounts[0] == MinimumHealing && amounts[1] == MaximumHealing;
    }

    /// <summary>CE _perform_drug_effect with stat −2: randomBetween(amount[0], amount[1]).</summary>
    public static int RollHealing(ICombatRandom random) => (random ?? throw new ArgumentNullException(nameof(random))).Between(MinimumHealing, MaximumHealing);
}

public sealed partial class WorldSession
{
    /// <summary>
    /// Takes one carried unit of a supported healing drug outside combat (CE uses no AP then). Healing above the maximum is
    /// lost, as critterAdjustHitPoints clamps it; taking it unhurt is allowed and wastes it, as in the original.
    /// </summary>
    public DrugUseResult UseDrug(CarriedItem item, ICombatRandom random, bool moving)
    {
        ArgumentNullException.ThrowIfNull(random);
        DrugUseStatus status = CanUseDrug(item, moving);
        if (status != DrugUseStatus.Ready) return new(status);
        if (_areas[item.Map].Items.ConsumeCarried(item.OwnerKey, item.Source.Key, 1) != ItemUseStatus.Ready) return new(DrugUseStatus.NoItem);
        int rolled = HealingDrugProfile.RollHealing(random), healed = Math.Min(rolled, _playerDamage);
        _playerDamage -= healed;
        return new(DrugUseStatus.Ready, rolled, healed, HitPoints);
    }
    /// <summary>Whether <see cref="UseDrug"/> would take the item now; changes nothing.</summary>
    public DrugUseStatus CanUseDrug(CarriedItem item, bool moving)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (InCombat) return DrugUseStatus.InCombat;
        if (moving) return DrugUseStatus.Busy;
        if (!_areas.TryGetValue(item.Map, out WorldArea? area)
            || area.Items.Carried.SingleOrDefault(c => c.OwnerKey == item.OwnerKey && c.Source.Key == item.Source.Key) is not { Quantity: > 0 } carried)
            return DrugUseStatus.NoItem;
        return HealingDrugProfile.Supported(area.Items.PrototypeOf(carried.Source)) ? DrugUseStatus.Ready : DrugUseStatus.Unsupported;
    }
}
