using FalloutRemake.Geometry;
using FalloutRemake.Import;

internal static class NavigationReport
{
    public static object Create(MapDocument map)
    {
        var nav = HexNavigation.FromMap(map, 0);
        int start = map.Prefix.Header.EnteringTile;
        HexReachability reached = nav.ReachableFrom(start);
        return new
        {
            Policy = "serialized flags/FID; single-hex mover; closed doors block; exits are terminal; no scripts or floor mask",
            Elevation = 0, Start = start, StartWalkable = nav.Walkable(start),
            nav.BlockedHexCount, nav.ExitHexCount, nav.DoorFlagDisagreements,
            ReachableHexes = reached.Count,
            Doors = map.ObjectLayers[0].Objects.Where(o => (o.Header.Pid >> 24) == 2 && o.Subtype == 0)
                .Select(o => new { o.Key, o.Header.Id, Hex = o.Header.Tile, Blocked = !nav.Walkable(o.Header.Tile),
                    OpenFlags = o.State.GetValueOrDefault("openFlags"), o.Header.Flags,
                    ReachableApproaches = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(o.Header.Tile, d))
                        .Where(t => t.HasValue && reached.Distance(t.Value) >= 0).Select(t => new { Hex = t!.Value, Steps = reached.Distance(t.Value) }) }),
            ReachableExits = Enumerable.Range(0, 40000).Where(t => nav.IsExit(t) && reached.Distance(t) >= 0)
                .Select(t => new { Hex = t, Steps = reached.Distance(t) })
        };
    }
}
