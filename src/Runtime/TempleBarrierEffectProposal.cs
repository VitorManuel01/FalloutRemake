using System;
using System.Buffers.Binary;
using System.Linq;
using FalloutRemake.Geometry;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public enum BarrierEffectStatus { Ready, NoUse, NotDue, Consumed, UnsupportedArea, UnsupportedProfile, NoTarget, AlreadyReplaced }
public sealed record TempleBarrierEffectPlan(Guid UseId, Guid SessionToken, int SourceMap, long SourceRevision,
    ActorLocation Center, string TargetKey, int ReplacementPid, int ReplacementFid, string ReplacementHash, int Distance);
public sealed record BarrierEffectProposal(BarrierEffectStatus Status, TempleBarrierEffectPlan? Plan = null);

/// <summary>Read-only, source-bound proposal for the verified aibkdor explosion branch. No generic damage dispatch.</summary>
public static class TempleBarrierEffectProposal
{
    public const string MapHash = "BA35474B8EA67AEC50B6A962A1BC767A87366449D4B631FD1B4CE241847B5BFE";
    public const string ProgramHash = "11749956A873B8B7EAC7CCC4D5B3C286F2C4B2E2F5E24EB17C9CDEBEC34426EA";
    public const string OriginalPrototypeHash = "093064F93642C699B08BB19D0CFFCD7A64D0ACA903FC9981588F07995B4603D4";
    public const string ReplacementPrototypeHash = "F374D40BB5EEE3CBC06D56DB422FCF40D7EEFA18115613BFBCB1370E1C6A730B";
    public const int ReplacementPid = 33555365, ReplacementFid = 33555376;

    public static BarrierEffectProposal Create(WorldSession world, Guid useId, Prototype? replacement)
    {
        WorldItemUse? pending = world.ItemUses.SingleOrDefault(u => u.Allocation.Id == useId);
        if (pending == null) return new(BarrierEffectStatus.NoUse);
        ItemUseAllocation use = pending.Allocation;
        if (use.Phase == ItemUsePhase.Consumed) return new(BarrierEffectStatus.Consumed);
        if (use.RemainingTicks != 0) return new(BarrierEffectStatus.NotDue);
        // Inspect cached areas only. The source inventory may belong to a different area from the placement.
        WorldArea? area = world.Areas.SingleOrDefault(a => a.Map.Prefix.Header.MapIndex == use.Placement.Map);
        if (area == null || use.Placement.Map != 3) return new(BarrierEffectStatus.UnsupportedArea);
        MapObject? barrier = Match(area.Map, replacement);
        if (barrier == null) return new(BarrierEffectStatus.UnsupportedProfile);
        if (area.Scenery.Replaces(barrier.Key)) return new(BarrierEffectStatus.AlreadyReplaced);
        int distance = FalloutGrid.Distance(use.Placement.Hex, barrier.Header.Tile);
        if (use.Placement.Elevation != barrier.Header.Elevation || distance > ExplosiveUseProposal.SceneryRadius)
            return new(BarrierEffectStatus.NoTarget);
        long revision = world.Areas.Single(a => a.Map.Prefix.Header.MapIndex == pending.SourceMap).Items.Revision;
        return new(BarrierEffectStatus.Ready, new(use.Id, world.SessionToken, pending.SourceMap, revision, use.Placement,
            barrier.Key, ReplacementPid, ReplacementFid, ReplacementPrototypeHash, distance));
    }

    // Parsed resource identities are trusted importer snapshots, as in the restricted door adapter.
    internal static MapObject? Match(MapDocument map, Prototype? replacement)
    {
        if (map.Source is not { Path: "maps/arcaves.map", Size: 526972, Sha256: MapHash } || map.Prefix.Header.MapIndex != 3) return null;
        MapObject[] matches = map.ObjectLayers.SelectMany(l => l.Objects).Where(o => o.Offset == 303324).ToArray();
        if (matches.Length != 1) return null;
        MapObject barrier = matches[0]; ObjectHeader h = barrier.Header;
        if (barrier.Key != MapHash + ":303324" || barrier.Subtype != 5 || barrier.ContainerElevation != 1 || barrier.Inventory.Length != 0
            || h is not { Id: 44, Tile: 14322, Elevation: 1, Pid: 33555364, Fid: 33555375, Sid: 50331649, ScriptIndex: 34, Flags: 0, Frame: 0 }) return null;
        ScriptBinding[] bindings = map.ScriptBindings.Where(b => b.Sid == h.Sid).ToArray();
        if (bindings.Length != 1 || bindings[0].Index != 34 || bindings[0].Program is not { Path: "scripts/aibkdor.int", Size: 2470, Sha256: ProgramHash }) return null;
        MapScript[] scripts = map.ActiveScripts.Where(s => s.Sid == h.Sid).ToArray();
        if (scripts.Length != 1 || scripts[0].ScriptIndex != 34 || scripts[0].OwnerId != 44 || scripts[0].LocalVariablesOffset != -1 || scripts[0].LocalVariablesCount != 0) return null;
        Prototype[] prototypes = map.Prototypes.Where(p => p.Pid == h.Pid).ToArray();
        if (prototypes.Length != 1 || !KnownPrototype(prototypes[0], 33555364, 33555375, "proto/scenery/00000933.pro", OriginalPrototypeHash, 0)
            || !KnownPrototype(replacement, ReplacementPid, ReplacementFid, "proto/scenery/00000934.pro", ReplacementPrototypeHash, DoorSession.OpenDoorMask)) return null;
        return barrier;
    }
    private static bool KnownPrototype(Prototype? prototype, int pid, int fid, string path, string hash, int flags) => prototype != null
        && prototype.Pid == pid && prototype.Type == 2 && prototype.Subtype == 5 && prototype.ScriptId == -1 && prototype.Fid == fid
        && prototype.Resource is { Size: 45 } resource && resource.Path == path && resource.Sha256 == hash && prototype.RawBytes is { Length: 45 }
        && BinaryPrimitives.ReadInt32BigEndian(prototype.RawBytes.AsSpan(20, 4)) == flags;
}
