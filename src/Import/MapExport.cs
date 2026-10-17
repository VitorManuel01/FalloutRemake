using System.Linq;
using System.Text.Json;

namespace FalloutRemake.Import;

public static class MapExport
{
    public static string ToJson(MapDocument map) => JsonSerializer.Serialize(new
    {
        SchemaVersion = 2,
        Status = "structural-import; no script execution or Godot geometry",
        map.Source,
        OriginalMap = map.Prefix.OriginalBytes.ToArray(),
        Header = map.Prefix.Header,
        map.Prefix.GlobalVariables, map.Prefix.LocalVariables,
        Layers = map.Prefix.Layers.Select(l => new { l.Elevation, RawTiles = l.Tiles.Select(t => t.Raw).ToArray() }),
        map.ScriptGroups, map.ObjectsOffset, map.DeclaredRootObjects, map.ObjectLayers,
        map.Prototypes, map.MapProgram, map.ScriptBindings, map.Dependencies,
        map.Diagnostics, map.ConsumedBytes
    }, new JsonSerializerOptions { WriteIndented = true, MaxDepth = 256 });
}
