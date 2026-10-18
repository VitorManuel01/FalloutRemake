using System;
using System.Collections.Generic;
using System.Linq;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public enum SkillRollOutcome { CriticalFailure, Failure, Success, CriticalSuccess }
public enum LockpickAction { Unlock, Lock }
public sealed record LockpickRequest(string SourceKey, int Skill, int Modifier, LockpickAction Action);
public sealed record LockpickResult(DoorUseStatus Status, SkillRollOutcome? Outcome = null, int ExperienceAwarded = 0);
public sealed record TempleDoorSnapshot(string SourceKey, IReadOnlyList<int> Locals, int Experience);

/// <summary>Restricted, hand-mapped subset of the verified DoorL100 script, not an INT interpreter.</summary>
internal sealed class TempleDoorAdapter
{
    internal const string MapHash = "BA35474B8EA67AEC50B6A962A1BC767A87366449D4B631FD1B4CE241847B5BFE";
    internal const string ProgramHash = "A8791ECAA9C7CFA3528D537AA5F1E8F20067E4BB51995C22B14DFEFAD02741BA";
    private readonly int[] _locals = new int[8];
    private int _experience;
    internal string SourceKey { get; }
    internal bool TrapInactive => _locals[1] != 0;
    internal TempleDoorSnapshot Snapshot => new(SourceKey, Array.AsReadOnly((int[])_locals.Clone()), _experience);

    private TempleDoorAdapter(string key) => SourceKey = key;

    // Identity comes from the already parsed MAP/dependency snapshot. Source names/IDs alone are insufficient.
    internal static TempleDoorAdapter? Match(MapDocument map)
    {
        if (map.Source.Path != "maps/arcaves.map" || map.Source.Sha256 != MapHash || map.Source.Size != 526972
            || map.Prefix.Header.MapIndex != 3 || map.Prefix.LocalVariables.Length != 0) return null;
        MapObject[] matches = map.ObjectLayers.SelectMany(l => l.Objects).Where(o => o.Offset == 137396).ToArray();
        if (matches.Length != 1) return null;
        MapObject door = matches[0]; ObjectHeader h = door.Header;
        if (door.Key != $"{MapHash}:137396" || h.Id != 102 || h.Tile != 11108 || h.Elevation != 0
            || h.Pid != 33555366 || h.Fid != 33555377 || h.Sid != 50331648 || h.ScriptIndex != 25
            || h.Frame != 0 || h.Flags != 32768 || door.Subtype != 0
            || !door.State.TryGetValue("openFlags", out int open) || open != 0) return null;
        ScriptBinding[] bindings = map.ScriptBindings.Where(b => b.Sid == h.Sid).ToArray();
        if (bindings.Length != 1 || bindings[0].Index != 25 || bindings[0].Program.Path != "scripts/doorl100.int"
            || bindings[0].Program.Size != 12054 || bindings[0].Program.Sha256 != ProgramHash) return null;
        MapScript[] scripts = map.ActiveScripts.Where(s => s.Sid == h.Sid).ToArray();
        if (scripts.Length != 1 || scripts[0].ScriptIndex != 25 || scripts[0].OwnerId != 102
            || scripts[0].LocalVariablesOffset != -1 || scripts[0].LocalVariablesCount != 0) return null;
        Prototype[] prototypes = map.Prototypes.Where(p => p.Pid == h.Pid).ToArray();
        if (prototypes.Length != 1 || prototypes[0].ScriptId != -1) return null;
        return new TempleDoorAdapter(door.Key);
    }

    internal void Reset() { Array.Clear(_locals); _experience = 0; }
    internal DoorState EnterMap(DoorState door)
    {
        if (_locals[3] == 0) { _locals[3] = 1; _locals[0] = 0; _locals[1] = 1; }
        return door with { OpenFlags = _locals[0] == 0 ? door.OpenFlags | DoorSession.Locked : door.OpenFlags & ~DoorSession.Locked };
    }
    internal LockpickRequest Request => new(SourceKey, 9, 100, _locals[0] == 0 ? LockpickAction.Unlock : LockpickAction.Lock);
    internal (DoorState State, int Experience) Apply(DoorState door, SkillRollOutcome outcome)
    {
        bool unlocking = _locals[0] == 0;
        if (outcome is SkillRollOutcome.Success or SkillRollOutcome.CriticalSuccess)
        {
            _locals[0] = unlocking ? 1 : 0;
            int award = _locals[5] == 0 ? 25 : 0;
            _locals[5] = 1; _experience += award;
            return (door with { OpenFlags = unlocking ? door.OpenFlags & ~DoorSession.Locked : door.OpenFlags | DoorSession.Locked }, award);
        }
        // Lock_Door has no jam call on failed relocking. Only failed unlocking can jam.
        return (outcome == SkillRollOutcome.CriticalFailure && unlocking
            ? door with { OpenFlags = door.OpenFlags | DoorSession.Jammed } : door, 0);
    }
}
