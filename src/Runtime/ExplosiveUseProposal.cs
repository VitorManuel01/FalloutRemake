using System.Linq;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public enum ExplosiveProposalStatus { Ready, Canceled, NoItem, Unsupported, Busy, AtExit, InvalidTimer, InvalidRoll }
public sealed record ExplosiveUsePlan(CarriedItem Source, ActorLocation Placement, int ActivePid, ExplosiveSchedule Schedule, int MinimumDamage, int MaximumDamage, int SceneryRadius);
public sealed record ExplosiveProposal(ExplosiveProposalStatus Status, ExplosiveUsePlan? Plan = null);

/// <summary>Confirmed, source-bound proposal only; committing ownership, time and world changes is a separate future operation.</summary>
public static class ExplosiveUseProposal
{
    public const int InactivePid = 85, ActivePid = 209, MinimumDamage = 40, MaximumDamage = 80, SceneryRadius = 3;
    public const string SourcePrototypeHash = "73A1D901A3F8E623220051B83E8311A00164F3654A3C8FFE024A0FE8B8664DA7";

    public static ExplosiveProposal Create(WorldSession world, CarriedItem requested, int seconds, SkillRollOutcome outcome, bool moving, bool demolitionExpert = false)
    {
        // Project compatibility decision: cancelling never removes or drops an item.
        if (seconds == -1) return new(ExplosiveProposalStatus.Canceled);
        if (moving) return new(ExplosiveProposalStatus.Busy);
        CarriedItem? item = world.Carried.SingleOrDefault(i => i.Map == requested.Map && i.OwnerKey == requested.OwnerKey && i.Source.Key == requested.Source.Key);
        if (item == null) return new(ExplosiveProposalStatus.NoItem);
        Prototype? prototype = world.Areas.Single(a => a.Map.Prefix.Header.MapIndex == item.Map).Map.Prototypes.SingleOrDefault(p => p.Pid == item.Source.Header.Pid);
        if (item.Source.Header.Pid != InactivePid || prototype is not { Type: 0, Subtype: 5, ScriptId: -1, BaseWeight: 4 }
            || prototype.Resource.Size != 69 || prototype.Resource.Sha256 != SourcePrototypeHash)
            return new(ExplosiveProposalStatus.Unsupported);
        if (world.Current.Doors.Navigation(world.Location.Elevation).IsExit(world.Location.Hex)) return new(ExplosiveProposalStatus.AtExit);
        if (seconds < ExplosiveTimer.MinimumSeconds || seconds > ExplosiveTimer.MaximumSeconds || seconds % ExplosiveTimer.StepSeconds != 0)
            return new(ExplosiveProposalStatus.InvalidTimer);
        if (!System.Enum.IsDefined(outcome)) return new(ExplosiveProposalStatus.InvalidRoll);
        // Normalize the verified inactive item to its active identity explicitly; do not forward the CE helper's unchecked active-PID lookup.
        return new(ExplosiveProposalStatus.Ready, new(item, world.Location, ActivePid, ExplosiveTimer.Plan(seconds, outcome, demolitionExpert), MinimumDamage, MaximumDamage, SceneryRadius));
    }
}
