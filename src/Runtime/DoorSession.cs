using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FalloutRemake.Geometry;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public enum DoorUseStatus { Ready, NoDoor, Unavailable, ScriptRequired, Locked, Busy, TooFar, Occupied, Inconsistent, AtExit, Jammed, StateChanged }
public sealed record DoorState(MapObject Source, int Flags, int OpenFlags, bool Scripted)
{
    public bool IsOpen => (OpenFlags & 1) != 0;
    public bool IsLocked => (OpenFlags & 0x02000000) != 0;
    public bool IsJammed => (OpenFlags & 0x04000000) != 0;
}

/// <summary>Ephemeral door state; source records and MAP/PRO bytes are never changed.</summary>
public sealed class DoorSession
{
    public const int OpenDoorMask = unchecked((int)0xA0000010); // SHOOT_THRU | LIGHT_THRU | NO_BLOCK.
    public const int Locked = 0x02000000, Jammed = 0x04000000;
    private readonly MapObject[] _roots;
    private readonly HashSet<int> _scriptedPrototypes;
    private readonly Dictionary<string, DoorState> _doors = new();
    private TempleDoorAdapter? _temple;
    private long _revision;
    private bool _resolving;
    public TempleDoorSnapshot? TempleDoor => _temple?.Snapshot;
    public static DoorSession FromMap(MapDocument map, bool enableTempleDoorAdapter = false)
    {
        var session = new DoorSession(map.ObjectLayers.SelectMany(l => l.Objects),
            map.Prototypes.Where(p => p.ScriptId is >= 0).Select(p => p.Pid));
        if (enableTempleDoorAdapter)
        {
            session._temple = TempleDoorAdapter.Match(map) ?? throw new InvalidDataException("Temple door adapter requires the verified original MAP, instance and script identities.");
            session.Reset();
        }
        return session;
    }

    public DoorSession(IEnumerable<MapObject> roots, IEnumerable<int>? scriptedPrototypes = null)
    {
        _roots = roots.ToArray(); _scriptedPrototypes = new(scriptedPrototypes ?? Array.Empty<int>()); Reset();
    }
    public DoorState? Get(string key) => _doors.GetValueOrDefault(key);
    public void Reset()
    {
        _revision++;
        _temple?.Reset();
        _doors.Clear();
        foreach (MapObject obj in _roots.Where(o => (o.Header.Pid >> 24) == 2 && o.Subtype == 0))
            if (obj.State.TryGetValue("openFlags", out int open))
                _doors.Add(obj.Key, new DoorState(obj, obj.Header.Flags, open,
                    obj.Header.Sid != -1 || obj.Header.ScriptIndex != -1 || _scriptedPrototypes.Contains(obj.Header.Pid)));
        EnterMap();
    }
    public void EnterMap()
    {
        if (_temple == null) return;
        _doors[_temple.SourceKey] = _temple.EnterMap(_doors[_temple.SourceKey]); _revision++;
    }
    public HexNavigation Navigation(int elevation) => new(_roots, elevation, obj =>
        _doors.TryGetValue(obj.Key, out DoorState? state) ? new(state.Flags, state.OpenFlags)
            : new(obj.Header.Flags, obj.State.TryGetValue("openFlags", out int open) ? open : null));

    public DoorUseStatus CanToggle(string key, int actorHex, int elevation, bool moving)
        => CanInteract(key, actorHex, elevation, moving, false);

    private DoorUseStatus CanInteract(string key, int actorHex, int elevation, bool moving, bool lockpick)
    {
        if (!_doors.TryGetValue(key, out DoorState? door)) return DoorUseStatus.NoDoor;
        ObjectHeader h = door.Source.Header;
        if (!HexNavigation.Valid(h.Tile) || !HexNavigation.Valid(actorHex) || h.Elevation != elevation
            || ((h.Fid >> 24) & 15) != 2 || (door.Flags & (HexNavigation.Hidden | HexNavigation.Multihex)) != 0)
            return DoorUseStatus.Unavailable;
        bool adapted = _temple?.SourceKey == key;
        if (lockpick && !adapted || door.Scripted && !adapted || adapted && !_temple!.TrapInactive)
            return DoorUseStatus.ScriptRequired;
        if (door.IsOpen != ((door.Flags & HexNavigation.NoBlock) != 0)) return DoorUseStatus.Inconsistent;
        if (lockpick && door.IsJammed) return DoorUseStatus.Jammed;
        if (!lockpick && door.IsLocked) return DoorUseStatus.Locked;
        if (moving || _resolving) return DoorUseStatus.Busy;
        HexNavigation navigation = Navigation(elevation);
        if (navigation.IsExit(actorHex)) return DoorUseStatus.AtExit;
        if (!lockpick && door.IsOpen && (actorHex == h.Tile || navigation.BlockersAt(h.Tile).Count != 0)) return DoorUseStatus.Occupied;
        if (actorHex == h.Tile || !Enumerable.Range(0, 6).Any(d => FalloutGrid.Neighbor(actorHex, d) == h.Tile)) return DoorUseStatus.TooFar;
        return DoorUseStatus.Ready;
    }
    public DoorUseStatus Toggle(string key, int actorHex, int elevation, bool moving)
    {
        DoorUseStatus status = CanToggle(key, actorHex, elevation, moving);
        if (status != DoorUseStatus.Ready) return status;
        DoorState door = _doors[key]; bool open = !door.IsOpen;
        _doors[key] = door with { Flags = open ? door.Flags | OpenDoorMask : door.Flags & ~OpenDoorMask,
            OpenFlags = open ? door.OpenFlags | 1 : door.OpenFlags & ~1 };
        _revision++;
        return DoorUseStatus.Ready;
    }
    public LockpickResult UseLockpick(string key, int actorHex, int elevation, bool moving,
        Func<LockpickRequest, SkillRollOutcome> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        DoorUseStatus status = CanInteract(key, actorHex, elevation, moving, true);
        if (status != DoorUseStatus.Ready) return new LockpickResult(status);
        long revision = _revision;
        SkillRollOutcome outcome;
        _resolving = true;
        try { outcome = resolve(_temple!.Request); }
        finally { _resolving = false; }
        if (_revision != revision) return new LockpickResult(DoorUseStatus.StateChanged);
        if (!Enum.IsDefined(outcome)) throw new ArgumentOutOfRangeException(nameof(resolve), "Unknown roll outcome.");
        (DoorState state, int award) = _temple!.Apply(_doors[key], outcome);
        _doors[key] = state; _revision++;
        return new LockpickResult(DoorUseStatus.Ready, outcome, award);
    }
}
