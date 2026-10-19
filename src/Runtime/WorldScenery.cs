using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FalloutRemake.Geometry;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public enum BarrierCommitStatus { Ready, InvalidIdentity, StalePlan, Busy, AlreadyReplaced, UnsupportedProfile, LimitReached }
public sealed record BarrierCommitResult(BarrierCommitStatus Status, SceneryReplacement? Replacement = null);

public sealed partial class WorldSession
{
    private readonly Func<int, Prototype>? _runtimePrototype;
    public SceneryReplacement[] SceneryReplacements => Areas.SelectMany(a => a.Scenery.Capture()).ToArray();
    private Prototype ReplacementPrototype()
    {
        try { return _runtimePrototype?.Invoke(TempleBarrierEffectProposal.ReplacementPid) ?? throw new InvalidDataException("Runtime prototype resolver is required for scenery replacements."); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or KeyNotFoundException or ArgumentException)
        { throw new InvalidDataException("Runtime replacement prototype is unavailable.", error); }
    }
    private void ValidateScenery(IEnumerable<(WorldArea Area, SceneryReplacement[] Records)> snapshots)
    {
        var uses = new Dictionary<Guid, WorldItemUse>(); var identities = new HashSet<Guid>(); var causes = new HashSet<Guid>();
        foreach (WorldItemUse use in ItemUses)
            if (!uses.TryAdd(use.Allocation.Id, use) || !identities.Add(use.Allocation.Id)) throw new InvalidDataException("Duplicate item-use identity.");
        foreach (var (area, records) in snapshots)
        {
            if (records.Length > 1) throw new InvalidDataException("The supported scenery profile permits only one replacement per area.");
            if (records.Length == 0) continue;
            MapObject? barrier = TempleBarrierEffectProposal.Match(area.Map, ReplacementPrototype());
            if (barrier == null) throw new InvalidDataException("Scenery replacement source/prototype profile is unsupported.");
            foreach (SceneryReplacement record in records)
            {
                if (record == null || record.Id == Guid.Empty || !identities.Add(record.Id) || !causes.Add(record.CauseUseId)
                    || record.ReplacesKey != barrier.Key || record.Location != new ActorLocation(area.Map.Prefix.Header.MapIndex, barrier.Header.Elevation, barrier.Header.Tile, barrier.Header.Rotation)
                    || record.Pid != TempleBarrierEffectProposal.ReplacementPid || record.Fid != TempleBarrierEffectProposal.ReplacementFid
                    || record.PrototypeHash != TempleBarrierEffectProposal.ReplacementPrototypeHash
                    || !uses.TryGetValue(record.CauseUseId, out WorldItemUse? cause) || cause.Allocation.Phase != ItemUsePhase.Consumed
                    || cause.Allocation.RemainingTicks != 0 || cause.Allocation.Placement.Map != record.Location.Map
                    || cause.Allocation.Placement.Elevation != record.Location.Elevation || cause.Allocation.Placement.Hex == record.Location.Hex
                    || FalloutGrid.Distance(cause.Allocation.Placement.Hex, record.Location.Hex) > ExplosiveUseProposal.SceneryRadius)
                    throw new InvalidDataException("Scenery replacement identity, location, profile or consumed cause is invalid.");
            }
        }
    }
    public BarrierCommitResult CommitBarrierEffect(TempleBarrierEffectPlan? plan, Guid replacementId, bool moving)
    {
        if (moving) return new(BarrierCommitStatus.Busy);
        if (replacementId == Guid.Empty || ItemUses.Any(u => u.Allocation.Id == replacementId) || SceneryReplacements.Any(r => r.Id == replacementId)) return new(BarrierCommitStatus.InvalidIdentity);
        if (plan == null || plan.TargetKey == null || !_areas.TryGetValue(plan.Center?.Map ?? -1, out WorldArea? target)) return new(BarrierCommitStatus.StalePlan);
        if (target.Scenery.Replaces(plan.TargetKey)) return new(BarrierCommitStatus.AlreadyReplaced);
        Prototype prototype;
        try { prototype = ReplacementPrototype(); }
        catch (InvalidDataException) { return new(BarrierCommitStatus.UnsupportedProfile); }
        if (TempleBarrierEffectProposal.Match(target.Map, prototype) == null) return new(BarrierCommitStatus.UnsupportedProfile);
        var projected = TempleBarrierProjection.Create(this, plan, prototype, replacementId);
        if (projected.Status != BarrierProjectionStatus.Ready) return new(BarrierCommitStatus.StalePlan);
        ProjectedScenery p = projected.Projection!.Replacement;
        var replacement = new SceneryReplacement(p.Id, p.CauseUseId, p.ReplacesKey, p.Location, p.Pid, p.Fid, p.PrototypeHash);
        var records = target.Scenery.Capture().Append(replacement).ToArray();
        if (records.Length > 1) return new(BarrierCommitStatus.LimitReached);
        // Prepare allocations and serialization capacity before either side of the transaction changes.
        var prepared = ScenerySession.Prepare(records);
        WorldCheckpoint snapshot = Capture(new string('0', 64));
        var candidate = snapshot with { Areas = snapshot.Areas.Select(a => a with {
            Uses = a.Map == plan.SourceMap ? a.Uses!.Select(u => u.Id == plan.UseId ? u with { Phase = ItemUsePhase.Consumed, RemainingTicks = 0 } : u).ToArray() : a.Uses,
            Replacements = a.Map == p.Location.Map ? records : a.Replacements }).ToArray() };
        try { CheckpointFile.Encode(candidate); }
        catch (InvalidDataException) { return new(BarrierCommitStatus.LimitReached); }
        // Main-thread operation: no callbacks, I/O or additional validation after consumption.
        if (CompleteDueUse(plan.UseId) != ItemUseStatus.Ready) return new(BarrierCommitStatus.StalePlan);
        target.Scenery.ApplyPrepared(prepared);
        return new(BarrierCommitStatus.Ready, replacement);
    }
}
