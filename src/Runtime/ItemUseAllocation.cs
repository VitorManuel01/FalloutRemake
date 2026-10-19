using System;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public enum ItemUsePhase { Placed, Consumed }
public enum ItemUseStatus { Ready, NoItem, Empty, Unsupported, InvalidState, Duplicate, LimitReached }
public sealed record ItemUseAllocation(Guid Id, string OwnerKey, string ItemKey, int Quantity, ItemUsePhase Phase,
    ActorLocation Placement, ExplosiveSchedule Schedule, int RemainingTicks);
public sealed record ItemBalance(int Original, int Remaining, int Carried, int Placed, int Consumed);

internal static class ItemUseProfile
{
    internal static bool Supported(Prototype? prototype) => prototype is { Pid: ExplosiveUseProposal.InactivePid, Type: 0, Subtype: 5, ScriptId: -1, BaseWeight: 4 }
        && prototype.Resource.Size == 69 && prototype.Resource.Sha256 == ExplosiveUseProposal.SourcePrototypeHash;
    internal static bool ValidSchedule(ExplosiveSchedule? schedule) => schedule != null
        && schedule.SelectedSeconds is >= ExplosiveTimer.MinimumSeconds and <= ExplosiveTimer.MaximumSeconds
        && schedule.SelectedSeconds % ExplosiveTimer.StepSeconds == 0
        && (schedule.Expiry == ExplosiveExpiry.Normal && schedule.DelayTicks == schedule.SelectedSeconds * ExplosiveTimer.TicksPerSecond
            || schedule.Expiry == ExplosiveExpiry.Premature && (schedule.DelayTicks == 0 || schedule.DelayTicks == schedule.SelectedSeconds * ExplosiveTimer.TicksPerSecond / 2));
    internal static bool Valid(ItemUseAllocation? use) => use != null && use.Id != Guid.Empty && use.OwnerKey != null && use.ItemKey != null && use.Quantity == 1
        && use.Placement is { Map: >= 0 and <= 1023, Elevation: >= 0 and <= 2, Hex: > 0 and < 40000, Rotation: >= 0 and <= 5 }
        && ValidSchedule(use.Schedule) && use.RemainingTicks >= 0 && use.RemainingTicks <= use.Schedule.DelayTicks
        && (use.Phase == ItemUsePhase.Placed || use.Phase == ItemUsePhase.Consumed && use.RemainingTicks == 0);
}
