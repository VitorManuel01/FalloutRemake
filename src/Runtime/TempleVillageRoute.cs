using System;
using System.Linq;

namespace FalloutRemake.Runtime;

public sealed record TempleVillageRoute(TempleWorldStatus Status, int? ExitHex = null, int[]? Tiles = null,
    ActorLocation? Destination = null, string[]? SourceKeys = null, string? DoorKey = null, int? ApproachHex = null)
{ public int? TargetHex => ApproachHex ?? ExitHex; }

public sealed partial class WorldSession
{
    // Read-only guidance only. This does not gate existing exits or execute Arroyo map scripts.
    public TempleVillageRoute InspectTempleVillageRoute(bool moving)
    {
        if (_templeProgress == null || CurrentMap != 3 || Location.Elevation != 2 || !_allowed.Contains(4)) return new(TempleWorldStatus.Unavailable);
        if (moving) return new(TempleWorldStatus.Busy);
        if (_templeProgress.Departure.Phase != NpcDeparturePhase.Hidden) return new(TempleWorldStatus.NotWalking);
        var navigation = Current.Doors.Navigation(2); var reach = navigation.ReachableFrom(Location.Hex);
        var candidates = Current.Map.ObjectLayers.SelectMany(l => l.Objects).Where(o => o.Header.Elevation == 2
            && o.Header.Pid is >= 0x05000010 and <= 0x05000017 && o.State.TryGetValue("destinationMap",out int map) && map == 4)
            .Select(o => o.Header.Tile).Distinct().OrderBy(t => reach.Distance(t) < 0 ? int.MaxValue : reach.Distance(t)).ThenBy(t => t).ToArray();
        foreach (int hex in candidates)
        {
            if (reach.Distance(hex) < 0) continue;
            ExitOption option = PreviewExitAt(2,hex);
            if (option.Status is not (ExitStatus.Ready or ExitStatus.ExternalMap) || option.Destination?.Map != 4) continue;
            return new(TempleWorldStatus.Ready,hex,reach.PathTo(hex),option.Destination,option.SourceKeys!.ToArray());
        }
        var exitDoor = _templeProgress.Profile.ExitDoor;
        if (exitDoor != null && CanUseTempleExit(exitDoor.Key) && Current.Doors.Get(exitDoor.Key) is { IsOpen: false } door)
        {
            int? approach = Enumerable.Range(0,6).Select(d => Geometry.FalloutGrid.Neighbor(door.Source.Header.Tile,d))
                .Where(h => h.HasValue && reach.Distance(h.Value) >= 0 && !navigation.IsExit(h.Value))
                .OrderBy(h => reach.Distance(h!.Value)).ThenBy(h => h).FirstOrDefault();
            if (approach.HasValue && candidates.Any(h => PreviewExitAt(2,h) is { Status: ExitStatus.Ready or ExitStatus.ExternalMap, Destination.Map: 4 }))
                return new(TempleWorldStatus.DoorRequired,Tiles:reach.PathTo(approach.Value),DoorKey:exitDoor.Key,ApproachHex:approach);
        }
        return new(TempleWorldStatus.Blocked);
    }
}
