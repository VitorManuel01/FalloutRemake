using System;
using System.Linq;
using FalloutRemake.Geometry;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public sealed record ProjectedScenery(Guid Id, Guid CauseUseId, string ReplacesKey, ActorLocation Location,
    int Pid, int Fid, string PrototypeHash);
public enum BarrierProjectionStatus { Ready, InvalidIdentity, StalePlan }
public sealed record BarrierProjectionResult(BarrierProjectionStatus Status, TempleBarrierProjection? Projection = null);

/// <summary>Detached candidate scene/navigation, not an applied or checkpointed world mutation.</summary>
public sealed class TempleBarrierProjection
{
    public ProjectedScenery Replacement { get; }
    public HexNavigation Navigation { get; }
    private TempleBarrierProjection(ProjectedScenery replacement, HexNavigation navigation)
    { Replacement = replacement; Navigation = navigation; }

    public static BarrierProjectionResult Create(WorldSession world, TempleBarrierEffectPlan? plan, Prototype? prototype, Guid replacementId)
    {
        if (replacementId == Guid.Empty || world.ItemUses.Any(u => u.Allocation.Id == replacementId)) return new(BarrierProjectionStatus.InvalidIdentity);
        if (plan == null) return new(BarrierProjectionStatus.StalePlan);
        var current = TempleBarrierEffectProposal.Create(world, plan.UseId, prototype);
        if (current.Status != BarrierEffectStatus.Ready || current.Plan != plan) return new(BarrierProjectionStatus.StalePlan);
        WorldArea area = world.Areas.Single(a => a.Map.Prefix.Header.MapIndex == plan.Center.Map);
        MapObject barrier = TempleBarrierEffectProposal.Match(area.Map, prototype)!;
        var replacement = new ProjectedScenery(replacementId, plan.UseId, barrier.Key,
            new(plan.Center.Map, barrier.Header.Elevation, barrier.Header.Tile, barrier.Header.Rotation), plan.ReplacementPid, plan.ReplacementFid, plan.ReplacementHash);
        // Hide only the original blocker in this detached graph. The verified replacement has NoBlock,
        // no script and no exit behavior, so it adds no blockers or exits. Do not fabricate a MAP record.
        var navigation = new HexNavigation(area.Map.ObjectLayers.SelectMany(l => l.Objects), barrier.Header.Elevation,
            obj => obj.Key == barrier.Key ? new(obj.Header.Flags | HexNavigation.Hidden, null)
                : area.Doors.Get(obj.Key) is DoorState door ? new(door.Flags, door.OpenFlags)
                : new(obj.Header.Flags, obj.State.TryGetValue("openFlags", out int open) ? open : null));
        return new(BarrierProjectionStatus.Ready, new(replacement, navigation));
    }
}
