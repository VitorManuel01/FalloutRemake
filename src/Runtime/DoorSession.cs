using System;
using System.Collections.Generic;
using System.Linq;
using FalloutRemake.Geometry;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public enum DoorUseStatus { Ready, NoDoor, Unavailable, ScriptRequired, Locked, Busy, TooFar, Occupied, Inconsistent, AtExit }
public sealed record DoorState(MapObject Source, int Flags, int OpenFlags, bool Scripted)
{
    public bool IsOpen => (OpenFlags & 1) != 0;
    public bool IsLocked => (OpenFlags & 0x02000000) != 0;
}

/// <summary>Ephemeral door state; source records and MAP/PRO bytes are never changed.</summary>
public sealed class DoorSession
{
    public const int OpenDoorMask = unchecked((int)0xA0000010); // SHOOT_THRU | LIGHT_THRU | NO_BLOCK.
    private readonly MapObject[] _roots;
    private readonly HashSet<int> _scriptedPrototypes;
    private readonly Dictionary<string, DoorState> _doors = new();
    public static DoorSession FromMap(MapDocument map) => new(map.ObjectLayers.SelectMany(l => l.Objects),
        map.Prototypes.Where(p => p.ScriptId is >= 0).Select(p => p.Pid));

    public DoorSession(IEnumerable<MapObject> roots, IEnumerable<int>? scriptedPrototypes = null)
    {
        _roots = roots.ToArray(); _scriptedPrototypes = new(scriptedPrototypes ?? Array.Empty<int>()); Reset();
    }
    public DoorState? Get(string key) => _doors.GetValueOrDefault(key);
    public void Reset()
    {
        _doors.Clear();
        foreach (MapObject obj in _roots.Where(o => (o.Header.Pid >> 24) == 2 && o.Subtype == 0))
            if (obj.State.TryGetValue("openFlags", out int open))
                _doors.Add(obj.Key, new DoorState(obj, obj.Header.Flags, open,
                    obj.Header.Sid != -1 || obj.Header.ScriptIndex != -1 || _scriptedPrototypes.Contains(obj.Header.Pid)));
    }
    public HexNavigation Navigation(int elevation) => new(_roots, elevation, obj =>
        _doors.TryGetValue(obj.Key, out DoorState? state) ? new(state.Flags, state.OpenFlags)
            : new(obj.Header.Flags, obj.State.TryGetValue("openFlags", out int open) ? open : null));

    public DoorUseStatus CanToggle(string key, int actorHex, int elevation, bool moving)
    {
        if (!_doors.TryGetValue(key, out DoorState? door)) return DoorUseStatus.NoDoor;
        ObjectHeader h = door.Source.Header;
        if (!HexNavigation.Valid(h.Tile) || !HexNavigation.Valid(actorHex) || h.Elevation != elevation
            || ((h.Fid >> 24) & 15) != 2 || (door.Flags & (HexNavigation.Hidden | HexNavigation.Multihex)) != 0)
            return DoorUseStatus.Unavailable;
        if (door.Scripted) return DoorUseStatus.ScriptRequired;
        if (door.IsOpen != ((door.Flags & HexNavigation.NoBlock) != 0)) return DoorUseStatus.Inconsistent;
        // Locked-door behavior may be script-defined. This milestone never bypasses a lock.
        if (door.IsLocked) return DoorUseStatus.Locked;
        if (moving) return DoorUseStatus.Busy;
        HexNavigation navigation = Navigation(elevation);
        if (navigation.IsExit(actorHex)) return DoorUseStatus.AtExit;
        if (door.IsOpen && (actorHex == h.Tile || navigation.BlockersAt(h.Tile).Count != 0)) return DoorUseStatus.Occupied;
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
        return DoorUseStatus.Ready;
    }
}
