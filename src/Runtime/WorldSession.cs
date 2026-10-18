using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public sealed record WorldArea(MapDocument Map, DoorSession Doors, TempleTravel Travel);

/// <summary>Cached area sessions with transactional destination validation; no general map scripts.</summary>
public sealed class WorldSession
{
    private readonly Func<int, MapDocument> _load;
    private readonly HashSet<int> _allowed;
    private readonly Dictionary<int, WorldArea> _areas = new();
    public int InitialMap { get; }
    public int CurrentMap { get; private set; }
    public WorldArea Current => _areas[CurrentMap];
    public ActorLocation Location => Current.Travel.Location;
    public int Experience => _areas.Values.Sum(a => a.Doors.TempleDoor?.Experience ?? 0);
    public IReadOnlyList<WorldArea> Areas => _areas.OrderBy(p => p.Key).Select(p => p.Value).ToArray();
    public WorldSession(Func<int, MapDocument> load, IEnumerable<int> allowedMaps, int initialMap = 3)
    {
        _load = load; _allowed = allowedMaps.ToHashSet(); InitialMap = initialMap;
        if (!_allowed.Contains(initialMap)) throw new ArgumentException("Initial map must belong to the supported area profile.");
        EnsureArea(initialMap); CurrentMap = initialMap;
    }
    private WorldArea EnsureArea(int id)
    {
        if (!_allowed.Contains(id)) throw new InvalidDataException("Destination lies outside the supported area profile.");
        if (_areas.TryGetValue(id, out WorldArea? area)) return area;
        MapDocument map = _load(id);
        if (map.Prefix.Header.MapIndex != id) throw new InvalidDataException("Loaded MAP ID does not match the requested destination.");
        DoorSession doors = DoorSession.FromMap(map, DoorSession.SupportsTempleDoorAdapter(map));
        area = new WorldArea(map, doors, new TempleTravel(map, doors));
        _areas.Add(id, area); return area;
    }
    public ExitOption InspectExit(bool moving)
    {
        ExitOption option = Current.Travel.InspectExit(moving);
        if (option.Status != ExitStatus.ExternalMap) return option;
        try
        {
            ActorLocation destination = option.Destination!;
            WorldArea area = EnsureArea(destination.Map);
            return area.Travel.CanArrive(destination)
                ? option with { Status = ExitStatus.Ready }
                : option with { Status = ExitStatus.BlockedDestination };
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or KeyNotFoundException or ArgumentException)
        { return option with { Status = ExitStatus.UnavailableMap }; }
    }
    public ExitOption UseExit(bool moving)
    {
        ExitOption option = InspectExit(moving);
        if (option.Status != ExitStatus.Ready) return option;
        ActorLocation destination = option.Destination!;
        if (destination.Map == CurrentMap) return Current.Travel.UseExit(moving);
        WorldArea target = _areas[destination.Map];
        target.Travel.Arrive(destination); CurrentMap = destination.Map;
        return option;
    }
    public void Reset()
    {
        foreach (WorldArea area in _areas.Values) { area.Doors.Reset(); area.Travel.Reset(); }
        CurrentMap = InitialMap;
    }
}
