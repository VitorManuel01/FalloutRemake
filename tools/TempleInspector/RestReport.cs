using FalloutRemake.Import;

/// <summary>
/// maps.txt can_rest_here of the supported maps (3, 4, 126) and, on every elevation that forbids rest, the critters of
/// another team that would refuse it (CE _critter_can_obj_dude_rest, before any of them dies).
/// </summary>
internal static class RestReport
{
    public static object Create(GameData data)
    {
        var catalog = new MapCatalog(data.Read("data/maps.txt"));
        return new
        {
            Status = "REST_SCANNED",
            Maps = new[] { 3, 4, 126 }.Select(id =>
            {
                MapCatalogEntry entry = catalog.Get(id);
                MapDocument map = catalog.Load(data, id);
                return new
                {
                    Map = id, entry.Name, CanRestHere = Enumerable.Range(0, 3).Select(entry.CanRest).ToArray(),
                    Blocking = Enumerable.Range(0, 3).Where(e => !entry.CanRest(e)).Select(e => new
                    {
                        Elevation = e,
                        Critters = map.ObjectLayers.SelectMany(l => l.Objects).Where(o => o.Header.Pid >> 24 == 1 && o.Header.Elevation == e && (o.Header.Flags & 1) == 0)
                            .Select(o => (o, Team: o.State.TryGetValue("team", out int t) ? t
                                : map.Prototypes.FirstOrDefault(p => p.Pid == o.Header.Pid) is { } proto ? CritterPrototypeData.Read(proto).Team : -1))
                            .Where(c => c.Team != 0).Select(c => $"{c.o.Key} PID {c.o.Header.Pid:X8} team {c.Team}").ToArray()
                    }).ToArray()
                };
            }).ToArray(),
            OriginalReconstruction = "verified"
        };
    }
}
