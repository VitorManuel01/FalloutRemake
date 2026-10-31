using FalloutRemake.Geometry;
using FalloutRemake.Import;

/// <summary>
/// Read-only: containers (and other visible non-wall objects) of the Temple whose hex center lies under a kit piece,
/// using the same wall/filler selection as TempleArchitecturePreview.
/// </summary>
internal static class ArchitectureClearanceReport
{
    static readonly int[] SceneryWallPids = { 0x02000640, 0x02000641 };

    public static object Create(GameData data)
    {
        var catalog = new MapCatalog(data.Read("data/maps.txt")); var art = new ResourceCatalog(data.Read);
        MapDocument map = catalog.Load(data, 3);
        string Art(string list, int index) { try { return art.ListEntry(list, index, ".frm"); } catch (InvalidDataException) { return ""; } }
        var hits = new List<object>();
        foreach (int elevation in new[] { 0, 1, 2 })
        {
            PreviewLayout layout = PreviewLayout.Create(map, elevation);
            bool Wall(PreviewObject o) => !o.Hidden && (o.Kind == PreviewKind.Scenery ? SceneryWallPids.Contains(o.Source.Header.Pid)
                : o.Kind == PreviewKind.Wall && !Art("art/walls/walls.lst", o.Source.Header.Fid & 0xFFF).StartsWith("block", StringComparison.OrdinalIgnoreCase));
            bool Filler(PreviewObject o) => (o.Source.Header.Flags & (HexNavigation.Hidden | HexNavigation.NoBlock)) == 0
                && (o.Kind == PreviewKind.Wall ? Art("art/walls/walls.lst", o.Source.Header.Fid & 0xFFF)
                    : o.Kind == PreviewKind.Scenery ? Art("art/scenery/scenery.lst", o.Source.Header.Fid & 0xFFF) : "").StartsWith("block", StringComparison.OrdinalIgnoreCase);
            WallPlan plan = TempleArchitecture.PlanWalls(layout.Objects.Where(Wall).Select(o => o.Source.Header.Tile), layout.Origin,
                layout.Objects.Where(Filler).Select(o => o.Source.Header.Tile),
                layout.Objects.Where(o => !o.Hidden && o.Kind == PreviewKind.Item).Select(o => o.Source.Header.Tile));
            foreach (PreviewObject obj in layout.Objects.Where(o => !o.Hidden && o.Kind != PreviewKind.Wall && !Wall(o) && !Filler(o)))
            {
                GroundPoint center = FalloutGrid.HexCenter(obj.Source.Header.Tile) - layout.Origin;
                foreach (ArchitecturePiece piece in plan.Pieces.Where(p => p.Contains(center, .25)))
                    hits.Add(new { Elevation = elevation, obj.Source.Header.Tile, obj.Source.Offset, Kind = obj.Kind.ToString(), Pid = $"0x{obj.Source.Header.Pid:X8}", Flags = $"0x{obj.Source.Header.Flags:X}",
                        Art = obj.Kind == PreviewKind.Scenery ? Art("art/scenery/scenery.lst", obj.Source.Header.Fid & 0xFFF) : "",
                        Container = obj.Source.Subtype == 1 && obj.Source.Header.Pid >> 24 == 0, Piece = piece.Module.ToString(), PieceTiles = piece.Tiles,
                        Joint = piece.Module == ArchitectureModule.Pillar && piece.Tiles.Length == 2 });
            }
        }
        var containers = new List<object>();
        foreach (int elevation in new[] { 0, 1, 2 })
        {
            PreviewLayout layout = PreviewLayout.Create(map, elevation);
            foreach (PreviewObject pot in layout.Objects.Where(o => !o.Hidden && o.Source.Subtype == 1 && o.Source.Header.Pid >> 24 == 0))
                containers.Add(new { Elevation = elevation, pot.Source.Header.Tile, pot.Source.Offset,
                    SameHex = layout.Objects.Where(o => o != pot && o.Source.Header.Tile == pot.Source.Header.Tile)
                        .Select(o => $"{o.Kind} PID 0x{o.Source.Header.Pid:X8} flags 0x{o.Source.Header.Flags:X} art {(o.Kind == PreviewKind.Wall ? Art("art/walls/walls.lst", o.Source.Header.Fid & 0xFFF) : o.Kind == PreviewKind.Scenery ? Art("art/scenery/scenery.lst", o.Source.Header.Fid & 0xFFF) : "")}").ToArray(),
                    Neighbors = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(pot.Source.Header.Tile, d) ?? -1)
                        .SelectMany(t => layout.Objects.Where(o => o.Source.Header.Tile == t && o.Kind is PreviewKind.Wall or PreviewKind.Scenery)
                            .Select(o => $"{t}:{o.Kind}:{(o.Kind == PreviewKind.Wall ? Art("art/walls/walls.lst", o.Source.Header.Fid & 0xFFF) : Art("art/scenery/scenery.lst", o.Source.Header.Fid & 0xFFF))}")).ToArray() });
        }
        return new { Status = "ARCHITECTURE_CLEARANCE", Hits = hits, Containers = containers };
    }
}
