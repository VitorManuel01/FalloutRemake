using FalloutRemake.Import;

internal static class NatureSourceReport
{
    public static object Create(GameData data)
    {
        var text = new PrototypeTextCatalog(data.Read,data.FindPaths("pro_"));
        var catalog = new MapCatalog(data.Read("data/maps.txt"));
        return new[] {3,4,126}.Select(id =>
        {
            var map = catalog.Load(data,id); MapLayoutWriter.Verify(map);
            var roots = map.ObjectLayers.SelectMany(l => l.Objects).ToArray();
            return new { Map = id, map.Source, Nature = map.Prototypes.Select(p => new {p.Pid,p.Fid,p.Type,p.Subtype,p.ScriptId,Text = text.Resolve(p),
                Count = roots.Count(o => o.Header.Pid == p.Pid),Objects = roots.Where(o => o.Header.Pid == p.Pid).Select(o => new {o.Key,o.Header.Id,o.Header.Tile,o.Header.Elevation,o.Header.Flags,o.Header.Sid,o.Header.ScriptIndex})})
                .Where(p => p.Text?.Name is string name && new[] {"rock","stone","tree","stump","cliff","plant","bush","shrub"}.Any(word => name.Contains(word,StringComparison.OrdinalIgnoreCase))).ToArray() };
        }).ToArray();
    }
}
