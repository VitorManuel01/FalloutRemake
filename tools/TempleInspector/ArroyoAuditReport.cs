using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FalloutRemake.Geometry;
using FalloutRemake.Import;

namespace FalloutRemake.TempleInspector;

public static class ArroyoAuditReport
{
    public static object Create(GameData data)
    {
        var text = new PrototypeTextCatalog(data.Read, data.FindPaths("pro_"));
        var catalog = new MapCatalog(data.Read("data/maps.txt"));

        int[] arroyoMapIds = new[] { 3, 4, 5, 35, 126, 127 };
        var mapsResult = new List<object>();

        var allPids = new HashSet<int>();
        var allCritters = new Dictionary<int, object>();
        var allItems = new Dictionary<int, object>();
        var allScenery = new Dictionary<int, object>();
        var allWalls = new Dictionary<int, object>();

        foreach (int mapId in arroyoMapIds)
        {
            var map = catalog.Load(data, mapId);
            MapLayoutWriter.Verify(map);

            var allObjects = map.AllObjects.ToArray();
            var mapProtos = map.Prototypes.ToDictionary(p => p.Pid);

            var objectSummaries = allObjects.GroupBy(o => o.Header.Pid).Select(g =>
            {
                int pid = g.Key;
                mapProtos.TryGetValue(pid, out var proto);
                var resolvedText = proto != null ? text.Resolve(proto) : null;
                int type = pid >> 24;

                var instances = g.Select(o => new
                {
                    Key = o.Key,
                    Elevation = o.Header.Elevation,
                    Tile = o.Header.Tile,
                    Rotation = o.Header.Rotation,
                    Flags = o.Header.Flags,
                    Sid = o.Header.Sid,
                    ScriptIndex = o.Header.ScriptIndex,
                    InventoryCount = o.Inventory.Length,
                    InventoryPids = o.Inventory.Select(i => new { Pid = i.Object.Header.Pid, Quantity = i.Quantity }).ToArray()
                }).ToArray();

                var itemInfo = new
                {
                    Pid = pid,
                    Type = type,
                    TypeName = TypeName(type),
                    Subtype = proto?.Subtype,
                    Fid = proto?.Fid ?? g.First().Header.Fid,
                    Name = resolvedText?.Name ?? $"Unknown_0x{pid:X8}",
                    Description = resolvedText?.Description,
                    Count = g.Count(),
                    Instances = instances
                };

                // Add to global set
                if (type == 1 && !allCritters.ContainsKey(pid)) allCritters[pid] = itemInfo;
                else if (type == 0 && !allItems.ContainsKey(pid)) allItems[pid] = itemInfo;
                else if (type == 2 && !allScenery.ContainsKey(pid)) allScenery[pid] = itemInfo;
                else if (type == 3 && !allWalls.ContainsKey(pid)) allWalls[pid] = itemInfo;

                return itemInfo;
            }).OrderBy(p => p.Type).ThenByDescending(p => p.Count).ToArray();

            mapsResult.Add(new
            {
                MapId = mapId,
                Source = map.Source.Path,
                TotalObjects = allObjects.Length,
                UniquePrototypes = objectSummaries.Length,
                Elevations = allObjects.Select(o => o.Header.Elevation).Distinct().OrderBy(e => e).ToArray(),
                Prototypes = objectSummaries
            });
        }

        return new
        {
            Status = "ARROYO_DEEP_AUDIT_OK",
            TotalArroyoMaps = arroyoMapIds.Length,
            Summary = new
            {
                TotalUniqueCritters = allCritters.Count,
                TotalUniqueItems = allItems.Count,
                TotalUniqueScenery = allScenery.Count,
                TotalUniqueWalls = allWalls.Count,
                UniqueCritters = allCritters.Values.ToArray(),
                UniqueItems = allItems.Values.ToArray(),
                UniqueScenery = allScenery.Values.ToArray(),
                UniqueWalls = allWalls.Values.ToArray()
            },
            Maps = mapsResult
        };
    }

    private static string TypeName(int type) => type switch
    {
        0 => "Item",
        1 => "Critter",
        2 => "Scenery",
        3 => "Wall",
        4 => "Tile",
        5 => "Misc",
        _ => "Unknown"
    };
}
