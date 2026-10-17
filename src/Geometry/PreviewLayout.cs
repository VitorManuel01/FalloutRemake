using System;
using System.Collections.Generic;
using System.Linq;
using FalloutRemake.Import;

namespace FalloutRemake.Geometry;

public enum PreviewKind { Wall, Door, Scenery, Critter, Item, Exit, Marker }
public sealed record PreviewObject(MapObject Source, PreviewKind Kind, GroundPoint Position, bool Hidden);
public sealed record PreviewFloor(int Square, uint Raw, GroundPoint Position);

public sealed record PreviewLayout(int Elevation, GroundPoint Origin, PreviewFloor[] Floors,
    PreviewObject[] Objects, int UnplacedObjects, int InventoryRecords)
{
    public static PreviewLayout Create(MapDocument map, int elevation)
    {
        if (elevation is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(elevation));
        GroundPoint origin = FalloutGrid.HexCenter(map.Prefix.Header.EnteringTile);
        MapLayer? layer = map.Prefix.Layers.FirstOrDefault(l => l.Elevation == elevation);
        var floors = new List<PreviewFloor>();
        if (layer != null)
            for (int i = 0; i < layer.Tiles.Length; i++)
            {
                SquareTile tile = layer.Tiles[i];
                // Tile 1 is the default blank; flag bit 0 disables floor rendering in CE.
                if (tile.FloorId == 1 || (tile.FloorFlags & 1) != 0) continue;
                floors.Add(new PreviewFloor(i, tile.Raw, FalloutGrid.SquareCenter(i) - origin));
            }
        var objects = new List<PreviewObject>();
        ObjectLayer objectLayer = map.ObjectLayers.Single(l => l.Elevation == elevation);
        int unplaced = 0, inventory = 0;
        foreach (MapObject obj in objectLayer.Objects)
        {
            inventory += MapDocument.Flatten(obj).Count() - 1;
            if (obj.Header.Tile == -1) { unplaced++; continue; }
            objects.Add(new PreviewObject(obj, Kind(obj), FalloutGrid.HexCenter(obj.Header.Tile) - origin, (obj.Header.Flags & 1) != 0));
        }
        return new PreviewLayout(elevation, origin, floors.ToArray(), objects.ToArray(), unplaced, inventory);
    }

    public static PreviewKind Kind(MapObject obj)
    {
        if (obj.Header.Pid is >= 0x05000010 and <= 0x05000017) return PreviewKind.Exit;
        return (obj.Header.Pid >> 24) switch
        {
            0 => PreviewKind.Item,
            1 => PreviewKind.Critter,
            2 when obj.Subtype == 0 => PreviewKind.Door,
            2 => PreviewKind.Scenery,
            3 => PreviewKind.Wall,
            _ => PreviewKind.Marker
        };
    }
}
