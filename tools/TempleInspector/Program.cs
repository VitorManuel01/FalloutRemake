using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FalloutRemake.Import;

try
{
    if (args.Length == 1 && args[0] == "--self-test") return SelfTests.Run();
    if (args.Length < 2 || args[0] != "--data-dir" || args.Length > 3)
    {
        Console.Error.WriteLine("Usage: TempleInspector --self-test | --data-dir <language-folder> [maps/artemple.map]");
        return 2;
    }
    var data = new GameData(args[1]);
    GameResource resource = data.Read(args.Length == 3 ? args[2] : "maps/artemple.map");
    MapPrefix map = MapPrefix.Read(resource.Bytes);
    var report = new
    {
        SchemaVersion = 1,
        Status = "prefix-only; scripts and objects NOT parsed or validated",
        Profile = "standard CE source order; custom CFG and Sfall mods unsupported",
        resource.Path, resource.Source,
        Size = resource.Bytes.Length,
        Sha256 = Convert.ToHexString(SHA256.HashData(resource.Bytes)),
        data.SourcePriority,
        Header = new { map.Header.Version, map.Header.Name, map.Header.MapIndex,
            map.Header.EnteringTile, map.Header.EnteringElevation, map.Header.EnteringRotation,
            map.Header.Flags, map.Header.ScriptIndex, map.Header.GlobalVariablesCount, map.Header.LocalVariablesCount },
        Layers = map.Layers.Select(layer => new { layer.Elevation, Squares = layer.Tiles.Length,
            UniqueFloorIds = layer.Tiles.Select(t => t.FloorId).Distinct().Count(),
            UniqueRoofIds = layer.Tiles.Select(t => t.RoofId).Distinct().Count() }),
        map.UnparsedOffset,
        UnparsedBytes = map.UnparsedScriptsAndObjects.Length,
        CandidateResourcesByName = data.FindPaths("temple"),
        Dependencies = new[] { "data/maps.txt", "art/tiles/tiles.lst", "scripts/scripts.lst", "scripts/artemple.int",
            "proto/items/items.lst", "proto/critters/critters.lst", "proto/scenery/scenery.lst",
            "proto/walls/walls.lst", "proto/misc/misc.lst", "art/scenery/scenery.lst", "art/walls/walls.lst",
            "art/critters/critters.lst", "art/items/items.lst", "art/misc/misc.lst" }.Select(path =>
            {
                GameResource dependency = data.Read(path);
                return new { dependency.Path, dependency.Source, Size = dependency.Bytes.Length };
            }),
        TempleMapRegistry = RelevantLines(data.Read("data/maps.txt"), "artemple", 3),
        TempleScriptListEntries = RelevantLines(data.Read("scripts/scripts.lst"), "temple", 0)
    };
    Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or KeyNotFoundException)
{
    Console.Error.WriteLine($"Inspection failed: {error.Message}");
    return 1;
}

static object[] RelevantLines(GameResource resource, string fragment, int context)
{
    string[] lines = Encoding.Latin1.GetString(resource.Bytes).Replace("\r", "").Split('\n');
    var indices = new SortedSet<int>();
    for (int i = 0; i < lines.Length; i++)
        if (lines[i].Contains(fragment, StringComparison.OrdinalIgnoreCase))
            for (int j = Math.Max(0, i - context); j <= Math.Min(lines.Length - 1, i + context); j++) indices.Add(j);
    return indices.Select(i => (object)new { ZeroBasedIndex = i, Text = lines[i] }).ToArray();
}
