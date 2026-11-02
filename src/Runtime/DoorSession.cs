using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FalloutRemake.Geometry;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public sealed record SavedDoor(string Key, int Flags, int OpenFlags);
public sealed record SavedDoorSession(SavedDoor[] Doors, int[]? TempleLocals, int Experience);

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
    private string? _initialGateKey;
    private string? _templeExitKey;
    public string? InitialTempleGateKey => _initialGateKey;
    private long _revision;
    private bool _resolving;
    internal Func<string, bool>? IsSceneryReplaced { get; set; }
    internal Func<MapObject, MapObject>? ProjectObject { get; set; }
    internal Func<string, DoorState?>? ProjectDoor { get; set; }
    internal Func<string, bool>? AllowTempleExitUse { get; set; }
    public TempleDoorSnapshot? TempleDoor => _temple?.Snapshot;
    /// <summary>Raised with the XP a lock gives (the Temple door's script, pcAddExperience): the world adds Swift Learner's share.</summary>
    public event Action<int>? ExperienceGained;
    public static bool SupportsTempleDoorAdapter(MapDocument map) => TempleDoorAdapter.Match(map) != null;
    public static DoorSession FromMap(MapDocument map, bool enableTempleDoorAdapter = false)
    {
        var session = new DoorSession(map.ObjectLayers.SelectMany(l => l.Objects),
            map.Prototypes.Where(p => p.ScriptId is >= 0).Select(p => p.Pid));
        if (enableTempleDoorAdapter)
        {
            session._temple = TempleDoorAdapter.Match(map) ?? throw new InvalidDataException("Temple door adapter requires the verified original MAP, instance and script identities.");
            session._initialGateKey = TempleInitialGateProfile.Match(map)?.Key;
            session._templeExitKey = TempleExitDoorProfile.Match(map)?.Key;
            session.Reset();
        }
        return session;
    }

    public DoorSession(IEnumerable<MapObject> roots, IEnumerable<int>? scriptedPrototypes = null)
    {
        _roots = roots.ToArray(); _scriptedPrototypes = new(scriptedPrototypes ?? Array.Empty<int>()); Reset();
    }
    public DoorState? Get(string key) => ProjectDoor?.Invoke(key) ?? _doors.GetValueOrDefault(key);
    /// <summary>A script sets this door's state (the Temple Key unlocking the challenge gate); only geometry and lock bits may differ.</summary>
    internal void SetScriptedState(string key, DoorState state)
    {
        if (!_doors.TryGetValue(key, out DoorState? door) || state.Source.Key != key) throw new InvalidOperationException("Unknown door.");
        _doors[key] = state; _revision++;
    }
    public SavedDoorSession Capture() => new(_doors.Keys.Select(key => Get(key)!).OrderBy(d => d.Source.Key, StringComparer.Ordinal)
        .Select(d => new SavedDoor(d.Source.Key, d.Flags, d.OpenFlags)).ToArray(), _temple?.Snapshot.Locals.ToArray(), _temple?.Snapshot.Experience ?? 0);
    /// <param name="allowInitialGateLock">The gate may also be locked: Cameron's challenge left it closed and locked (actemvil Node009).</param>
    public void Restore(SavedDoorSession saved, bool allowInitialGateChanges = true, bool allowTempleExitChanges = false, bool allowInitialGateLock = false)
    {
        if (saved?.Doors == null || saved.Doors.Length != _doors.Count)
            throw new InvalidDataException("Checkpoint door count differs from its source MAP.");
        var restored = new Dictionary<string, DoorState>();
        foreach (SavedDoor value in saved.Doors)
        {
            if (value == null || value.Key == null || !_doors.TryGetValue(value.Key, out DoorState? source) || restored.ContainsKey(value.Key))
                throw new InvalidDataException("Checkpoint contains an unknown or duplicate door key.");
            bool adapted = _temple?.SourceKey == value.Key;
            int originalFlags = source.Source.Header.Flags, originalOpen = source.Source.State["openFlags"];
            bool changed = value.Flags != originalFlags || value.OpenFlags != originalOpen;
            bool initialGate = _initialGateKey == value.Key && allowInitialGateChanges;
            bool templeExit = _templeExitKey == value.Key && allowTempleExitChanges;
            if (changed && source.Scripted && !adapted && !initialGate && !templeExit
                || ((value.Flags ^ originalFlags) & ~OpenDoorMask) != 0
                || ((value.OpenFlags ^ originalOpen) & ~(adapted ? 1 | Locked | Jammed : initialGate && allowInitialGateLock ? 1 | Locked : 1)) != 0)
                throw new InvalidDataException("Checkpoint attempts an unsupported door-state change.");
            if (changed && (!adapted && (originalOpen & Locked) != 0
                || (originalFlags & (HexNavigation.Hidden | HexNavigation.Multihex)) != 0
                || ((source.Source.Header.Fid >> 24) & 15) != 2))
                throw new InvalidDataException("Checkpoint modifies an unavailable door.");
            if (changed && value.Flags != ((value.OpenFlags & 1) != 0 ? originalFlags | OpenDoorMask : originalFlags & ~OpenDoorMask))
                throw new InvalidDataException("Checkpoint door geometry flags are inconsistent.");
            restored.Add(value.Key, source with { Flags = value.Flags, OpenFlags = value.OpenFlags });
        }
        if (_temple == null)
        {
            if (saved.TempleLocals != null || saved.Experience != 0) throw new InvalidDataException("Checkpoint grants XP to an unsupported script.");
        }
        else
        {
            DoorState door = restored[_temple.SourceKey];
            if (!TempleDoorAdapter.ValidCheckpoint(saved.TempleLocals, saved.Experience)
                || door.IsLocked != (saved.TempleLocals![0] == 0) || door.IsJammed && !door.IsLocked)
                throw new InvalidDataException("Checkpoint script locals, tranca or XP are inconsistent.");
            _temple.Restore(saved.TempleLocals!, saved.Experience);
        }
        _doors.Clear(); foreach (var pair in restored) _doors.Add(pair.Key, pair.Value);
        _revision++;
    }
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
    public HexNavigation Navigation(int elevation) => NavigationCore(elevation, null);
    internal HexNavigation NavigationForNpc(int elevation, string actorKey) => NavigationCore(elevation, actorKey);
    private HexNavigation NavigationCore(int elevation, string? excludeActor) => new(_roots.Select(o => ProjectObject?.Invoke(o) ?? o).Where(o => o.Key != excludeActor), elevation, obj =>
        IsSceneryReplaced?.Invoke(obj.Key) == true ? new(obj.Header.Flags | HexNavigation.Hidden, null)
            : Get(obj.Key) is DoorState state ? new(state.Flags, state.OpenFlags)
            : new(obj.Header.Flags, obj.State.TryGetValue("openFlags", out int open) ? open : null),
        excludeActor == null ? ExitTraversalPolicy.StopAtExits : ExitTraversalPolicy.ContinueAcrossExits);

    public DoorUseStatus CanToggle(string key, int actorHex, int elevation, bool moving)
        => CanInteract(key, actorHex, elevation, moving, false);
    public DoorUseStatus CanLockpick(string key, int actorHex, int elevation, bool moving)
        => CanInteract(key, actorHex, elevation, moving, true);

    private DoorUseStatus CanInteract(string key, int actorHex, int elevation, bool moving, bool lockpick)
    {
        if (ProjectDoor?.Invoke(key) != null) return DoorUseStatus.ScriptRequired;
        if (!_doors.TryGetValue(key, out DoorState? door)) return DoorUseStatus.NoDoor;
        ObjectHeader h = door.Source.Header;
        if (!HexNavigation.Valid(h.Tile) || !HexNavigation.Valid(actorHex) || h.Elevation != elevation
            || ((h.Fid >> 24) & 15) != 2 || (door.Flags & (HexNavigation.Hidden | HexNavigation.Multihex)) != 0)
            return DoorUseStatus.Unavailable;
        bool adapted = _temple?.SourceKey == key;
        if (lockpick && !adapted || door.Scripted && !adapted && key != _initialGateKey
            && !(key == _templeExitKey && AllowTempleExitUse?.Invoke(key) == true) || adapted && !_temple!.TrapInactive)
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
        if (award > 0) ExperienceGained?.Invoke(award);
        return new LockpickResult(DoorUseStatus.Ready, outcome, award);
    }
}
