using System;
using System.Collections.Generic;
using System.Linq;
using FalloutRemake.Import;

namespace FalloutRemake.Geometry;

public sealed record HexBlocker(string Key, int Id, int Hex, int FidType, bool IsDoor, bool Multihex);
public enum RouteStatus { Ready, AlreadyThere, OutsideGrid, Blocked, Unreachable, Busy }
public enum ExitTraversalPolicy { StopAtExits, ContinueAcrossExits }
public sealed record HexRoute(RouteStatus Status, int[] Tiles);
public readonly record struct ObjectTraversalState(int Flags, int? DoorOpenFlags);

/// <summary>Single-hex movement using source geometry and optional session flags; no scripts or automatic door use.</summary>
public sealed class HexNavigation
{
    public const int Hidden = 0x1, NoBlock = 0x10, Multihex = 0x800;
    private readonly Dictionary<int, List<HexBlocker>> _blockers = new();
    private readonly HashSet<int> _exits = new();
    public int Elevation { get; }
    public int BlockedHexCount => _blockers.Count;
    public int ExitHexCount => _exits.Count;
    public int DoorFlagDisagreements { get; private set; }
    public ExitTraversalPolicy ExitPolicy { get; }

    public static HexNavigation FromMap(MapDocument map, int elevation) =>
        new(map.ObjectLayers.SelectMany(layer => layer.Objects), elevation);

    // Accept roots only. Inventory records are never placed in the world.
    public HexNavigation(IEnumerable<MapObject> roots, int elevation, Func<MapObject, ObjectTraversalState>? stateFor = null,
        ExitTraversalPolicy exitPolicy = ExitTraversalPolicy.StopAtExits)
    {
        if (elevation is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(elevation));
        if (!Enum.IsDefined(exitPolicy)) throw new ArgumentOutOfRangeException(nameof(exitPolicy));
        ExitPolicy = exitPolicy;
        Elevation = elevation;
        foreach (MapObject obj in roots)
        {
            ObjectHeader h = obj.Header;
            if (h.Elevation != elevation || !Valid(h.Tile)) continue;
            ObjectTraversalState state = stateFor?.Invoke(obj) ?? new ObjectTraversalState(h.Flags,
                obj.State.TryGetValue("openFlags", out int sourceOpen) ? sourceOpen : null);
            int flags = state.Flags;
            bool door = (h.Pid >> 24) == 2 && obj.Subtype == 0;
            if (door && state.DoorOpenFlags is int open
                && ((open & 1) != 0) != ((flags & NoBlock) != 0)) DoorFlagDisagreements++;
            if ((flags & Hidden) == 0 && h.Pid is >= 0x05000010 and <= 0x05000017) _exits.Add(h.Tile);
            // CE _obj_blocking_at uses the FID nibble, not the PID or the preview shape.
            int type = (h.Fid >> 24) & 0xF;
            if ((flags & (Hidden | NoBlock)) != 0 || type is < 1 or > 3) continue;
            var blocker = new HexBlocker(obj.Key, h.Id, h.Tile, type, door, (flags & Multihex) != 0);
            Add(h.Tile, blocker);
            if (blocker.Multihex)
                for (int direction = 0; direction < 6; direction++)
                    if (FalloutGrid.Neighbor(h.Tile, direction) is int neighbor) Add(neighbor, blocker);
        }
    }

    private void Add(int tile, HexBlocker blocker)
    {
        if (!_blockers.TryGetValue(tile, out List<HexBlocker>? list)) _blockers[tile] = list = new();
        list.Add(blocker);
    }
    public static bool Valid(int tile) => tile is >= 0 and < 40000;
    public bool Walkable(int tile) => Valid(tile) && !_blockers.ContainsKey(tile);
    public bool IsExit(int tile) => _exits.Contains(tile);
    public IReadOnlyList<HexBlocker> BlockersAt(int tile) =>
        _blockers.TryGetValue(tile, out List<HexBlocker>? list) ? list.AsReadOnly() : Array.Empty<HexBlocker>();

    public HexReachability ReachableFrom(int start)
    {
        var parent = new int[40000]; Array.Fill(parent, -1);
        var distance = new int[40000]; Array.Fill(distance, -1);
        if (!Walkable(start)) return new HexReachability(start, parent, distance);
        var queue = new Queue<int>(); queue.Enqueue(start); parent[start] = start; distance[start] = 0;
        while (queue.TryDequeue(out int current))
        {
            // Player routes stop for explicit travel. NPC-only graphs may cross the same markers without travelling.
            if (ExitPolicy == ExitTraversalPolicy.StopAtExits && IsExit(current)) continue;
            for (int direction = 0; direction < 6; direction++)
            {
                if (FalloutGrid.Neighbor(current, direction) is not int next || !Walkable(next) || parent[next] != -1) continue;
                parent[next] = current; distance[next] = distance[current] + 1; queue.Enqueue(next);
            }
        }
        return new HexReachability(start, parent, distance);
    }

    public HexRoute FindRoute(int from, int to)
    {
        if (!Valid(from) || !Valid(to)) return new(RouteStatus.OutsideGrid, Array.Empty<int>());
        if (!Walkable(from) || !Walkable(to)) return new(RouteStatus.Blocked, Array.Empty<int>());
        if (from == to) return new(RouteStatus.AlreadyThere, new[] { from });
        int[] route = ReachableFrom(from).PathTo(to);
        return new(route.Length == 0 ? RouteStatus.Unreachable : RouteStatus.Ready, route);
    }
}

public sealed class HexReachability
{
    private readonly int[] _parent, _distance;
    public int Start { get; }
    public int Count => _distance.Count(d => d >= 0);
    internal HexReachability(int start, int[] parent, int[] distance) { Start = start; _parent = parent; _distance = distance; }
    public int Distance(int tile) => HexNavigation.Valid(tile) ? _distance[tile] : -1;
    public int[] PathTo(int tile)
    {
        int steps = Distance(tile);
        if (steps < 0) return Array.Empty<int>();
        var route = new int[steps + 1];
        for (int i = steps; i >= 0; i--) { route[i] = tile; tile = _parent[tile]; }
        return route;
    }
}

/// <summary>Visual marker with continuous interpolation and discrete, validated hex steps.</summary>
public sealed class HexWalker
{
    private readonly HexNavigation _navigation;
    private int[] _route = Array.Empty<int>();
    private int _index;
    public int CurrentHex { get; private set; }
    public bool Moving => _index + 1 < _route.Length;
    public bool StopRequested { get; private set; }
    public double Progress { get; private set; }
    public int NextHex => Moving ? _route[_index + 1] : CurrentHex;
    public HexWalker(HexNavigation navigation, int start)
    {
        if (!navigation.Walkable(start)) throw new ArgumentException("Marker start is blocked or outside the grid.", nameof(start));
        _navigation = navigation; CurrentHex = start;
    }
    public HexRoute WalkTo(int target)
    {
        if (Moving) return new(RouteStatus.Busy, Array.Empty<int>());
        HexRoute result = _navigation.FindRoute(CurrentHex, target);
        if (result.Status == RouteStatus.Ready) { _route = (int[])result.Tiles.Clone(); _index = 0; Progress = 0; StopRequested = false; }
        return result;
    }
    public bool RequestStop()
    {
        if (!Moving) return false;
        if (StopRequested) return true;
        // Complete a step already in progress; never jump backwards or save a fractional hex.
        if (Progress > 0)
        { _route = new[] { CurrentHex, NextHex }; StopRequested = true; }
        else _route = Array.Empty<int>();
        _index = 0;
        return true;
    }
    public void Advance(double seconds)
    {
        if (seconds < 0 || !double.IsFinite(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (!Moving) return;
        Progress += seconds * 5;
        while (Moving && Progress >= 1)
        {
            if (!_navigation.Walkable(NextHex)) throw new InvalidOperationException("Route entered a blocked hex.");
            CurrentHex = NextHex; _index++; Progress -= 1;
        }
        if (!Moving) { Progress = 0; StopRequested = false; }
    }
    public GroundPoint Position
    {
        get
        {
            GroundPoint a = FalloutGrid.HexCenter(CurrentHex), b = FalloutGrid.HexCenter(NextHex);
            return new GroundPoint(a.X + (b.X - a.X) * Progress, a.Z + (b.Z - a.Z) * Progress);
        }
    }
}
