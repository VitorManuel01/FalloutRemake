using System.Linq;
using System;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public enum ExplosiveProposalStatus { Ready, Canceled, NoItem, Unsupported, Busy, Unavailable, AtExit, InvalidTimer, InvalidRoll }
public sealed record ExplosiveUsePlan(CarriedItem Source, int Quantity, ActorLocation Placement, int ActivePid, ExplosiveSchedule Schedule, int MinimumDamage, int MaximumDamage, int SceneryRadius,
    Guid Id, Guid SessionToken, long SourceRevision);
public sealed record ExplosiveProposal(ExplosiveProposalStatus Status, ExplosiveUsePlan? Plan = null);

/// <summary>Read-only source-bound proposal; the world revalidates it before reserving ownership.</summary>
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
        WorldArea area = world.Areas.Single(a => a.Map.Prefix.Header.MapIndex == item.Map);
        Prototype? prototype = area.Map.Prototypes.SingleOrDefault(p => p.Pid == item.Source.Header.Pid);
        if (item.Source.Header.Pid != InactivePid || !ItemUseProfile.Supported(prototype))
            return new(ExplosiveProposalStatus.Unsupported);
        if (!world.Current.Travel.CanArrive(world.Location)) return new(ExplosiveProposalStatus.Unavailable);
        if (world.Current.Doors.Navigation(world.Location.Elevation).IsExit(world.Location.Hex)) return new(ExplosiveProposalStatus.AtExit);
        if (seconds < ExplosiveTimer.MinimumSeconds || seconds > ExplosiveTimer.MaximumSeconds || seconds % ExplosiveTimer.StepSeconds != 0)
            return new(ExplosiveProposalStatus.InvalidTimer);
        if (!System.Enum.IsDefined(outcome)) return new(ExplosiveProposalStatus.InvalidRoll);
        // Normalize the verified inactive item to its active identity explicitly; do not forward the CE helper's unchecked active-PID lookup.
        return new(ExplosiveProposalStatus.Ready, new(item, 1, world.Location, ActivePid, ExplosiveTimer.Plan(seconds, outcome, demolitionExpert), MinimumDamage, MaximumDamage, SceneryRadius,
            Guid.NewGuid(), world.SessionToken, area.Items.Revision));
    }
}
