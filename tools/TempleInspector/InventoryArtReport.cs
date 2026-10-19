using FalloutRemake.Import;

internal static class InventoryArtReport
{
    public static object Create(GameData data)
    {
        MapDocument map = MapDocument.Read(data.Read("maps/arcaves.map"), data.Read); MapLayoutWriter.Verify(map);
        var catalog = new InventoryArtCatalog(data.Read); var text = new PrototypeTextCatalog(data.Read, data.FindPaths("pro_"));
        var icons = map.Prototypes.Where(p => p.Type == 0 && p.InventoryFid is >= 0).Select(p =>
        {
            InventoryArt art = catalog.Get(p.InventoryFid!.Value)!;
            return new { p.Pid, Name = text.Resolve(p)?.Name, WorldFid = p.Fid, InventoryFid = p.InventoryFid,
                art.Source, art.Width, art.Height, art.OffsetX, art.OffsetY, art.DirectionOffsetX, art.DirectionOffsetY,
                TransparentPixels = Enumerable.Range(0, art.Width * art.Height).Count(i => art.Rgba.Span[i * 4 + 3] == 0) };
        }).ToArray();
        return new { Status = "INVENTORY_ART_OK", Map = map.Source, Icons = icons, catalog.Dependencies, OriginalReconstruction = "verified", ExportedAssets = false };
    }
}
