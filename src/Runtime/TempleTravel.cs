using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FalloutRemake.Geometry;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public sealed record ActorLocation(int Map, int Elevation, int Hex, int Rotation);
public enum ExitStatus { Ready, NoExit, Busy, Invalid, Ambiguous, ExternalMap, ReloadRequired, BlockedDestination, UnavailableMap }
public sealed record ExitOption(ExitStatus Status, ActorLocation? Destination = null, IReadOnlyList<string>? SourceKeys = null);

/// <summary>Explicit same-MAP elevation travel, separate from the inspection camera and immutable source.</summary>
public sealed class TempleTravel
{
    private readonly MapDocument _map;
    private readonly DoorSession _doors;
    private readonly MapObject[] _roots;
    public ActorLocation Location { get; private set; }
    public TempleTravel(MapDocument map, DoorSession doors)
    {
        _map = map; _doors = doors; _roots = map.ObjectLayers.SelectMany(l => l.Objects).ToArray();
        Location = Entry();
    }
    private ActorLocation Entry() => new(_map.Prefix.Header.MapIndex, _map.Prefix.Header.EnteringElevation,
        _map.Prefix.Header.EnteringTile, _map.Prefix.Header.EnteringRotation);
    public void Reset() => Location = Entry();
    public bool CanArrive(ActorLocation destination) => destination.Map == _map.Prefix.Header.MapIndex
        && destination.Elevation is >= 0 and <= 2 && destination.Rotation is >= 0 and <= 5
        && HexNavigation.Valid(destination.Hex) && destination.Hex != 0
        && _map.Prefix.Layers.Any(l => l.Elevation == destination.Elevation)
        && _doors.Navigation(destination.Elevation).Walkable(destination.Hex);
    public void Arrive(ActorLocation destination)
    {
        if (!CanArrive(destination)) throw new InvalidDataException("Arrival must target a present, walkable position on this map.");
        Location = destination;
    }
    public void MoveTo(int hex)
    {
        if (hex == Location.Hex) return;
        HexRoute route = _doors.Navigation(Location.Elevation).FindRoute(Location.Hex, hex);
        if (route.Status != RouteStatus.Ready) throw new InvalidOperationException("Actor movement must follow a valid route on its current elevation.");
        int previous = route.Tiles[^2];
        int rotation = Enumerable.Range(0, 6).Single(d => FalloutGrid.Neighbor(previous, d) == hex);
        Location = Location with { Hex = hex, Rotation = rotation };
    }
    public ExitOption InspectExit(bool moving)
    {
        if (moving) return new(ExitStatus.Busy);
        MapObject[] exits = _roots.Where(o => o.Header.Elevation == Location.Elevation && o.Header.Tile == Location.Hex
            && o.Header.Pid is >= 0x05000010 and <= 0x05000017 && (o.Header.Flags & HexNavigation.Hidden) == 0).ToArray();
        if (exits.Length == 0) return new(ExitStatus.NoExit);
        if (!_doors.Navigation(Location.Elevation).Walkable(Location.Hex)) return new(ExitStatus.Invalid);
        var destinations = new List<ActorLocation>();
        foreach (MapObject exit in exits)
        {
            if (((exit.Header.Fid >> 24) & 15) != 5
                || !exit.State.TryGetValue("destinationMap", out int map)
                || !exit.State.TryGetValue("destinationElevation", out int elevation)
                || !exit.State.TryGetValue("destinationTile", out int hex)
                || !exit.State.TryGetValue("destinationRotation", out int rotation)
                || elevation is < 0 or > 2 || !HexNavigation.Valid(hex) || hex == 0 || rotation is < 0 or > 5)
                return new(ExitStatus.Invalid);
            destinations.Add(new ActorLocation(map, elevation, hex, rotation));
        }
        if (destinations.Distinct().Count() != 1) return new(ExitStatus.Ambiguous);
        ActorLocation destination = destinations[0];
        var keys = Array.AsReadOnly(exits.Select(o => o.Key).ToArray());
        if (destination.Map != Location.Map) return new(ExitStatus.ExternalMap, destination, keys);
        // CE reloads the MAP for a transition to the same elevation. That lifecycle is outside this subset.
        if (destination.Elevation == Location.Elevation) return new(ExitStatus.ReloadRequired, destination, keys);
        if (!_map.Prefix.Layers.Any(l => l.Elevation == destination.Elevation)
            || !_doors.Navigation(destination.Elevation).Walkable(destination.Hex))
            return new(ExitStatus.BlockedDestination, destination, keys);
        return new(ExitStatus.Ready, destination, keys);
    }
    public ExitOption UseExit(bool moving)
    {
        ExitOption option = InspectExit(moving);
        if (option.Status == ExitStatus.Ready) Location = option.Destination!;
        return option;
    }
}
