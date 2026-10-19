using System.Text.Json;
using FalloutRemake.Import;

try
{
    if (args.Length == 1 && args[0] == "--self-test") return SelfTests.Run();
    if (args.Length < 2 || args[0] != "--data-dir")
    {
        Console.Error.WriteLine("Usage: TempleInspector --self-test | --data-dir <language-folder> [--map maps/arcaves.map] [--export-local] [--navigation] | --data-dir <language-folder> --script scripts/<name>.int [--instructions] | --data-dir <language-folder> <report>");
        Console.Error.WriteLine("Reports: --door-adapter-test, --item-transfer-report, --temple-chest-report, --item-origin-route-report, --item-art-report, --temple-collection-report, --temple-barrier-report");
        return 2;
    }
    if (args.Length == 3 && args[2] == "--door-adapter-test")
    {
        var adapterData = new GameData(args[1]);
        MapDocument adapterMap = MapDocument.Read(adapterData.Read("maps/arcaves.map"), adapterData.Read);
        Console.WriteLine(JsonSerializer.Serialize(TempleAdapterScenario.Run(adapterMap), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--item-transfer-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(ItemTransferReport.Create(new GameData(args[1])), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--temple-chest-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(TempleChestReport.Create(new GameData(args[1])), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--item-origin-route-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(ItemOriginRouteReport.Create(new GameData(args[1])), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--item-art-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(InventoryArtReport.Create(new GameData(args[1])), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--temple-collection-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(TempleCollectionReport.Create(new GameData(args[1])), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--temple-barrier-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(TempleBarrierReport.Create(new GameData(args[1])), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length >= 3 && args[2] == "--script")
    {
        if (args.Length is not (4 or 5) || args.Length == 5 && args[4] != "--instructions")
            throw new ArgumentException("Expected --script scripts/<name>.int [--instructions].");
        Console.WriteLine(JsonSerializer.Serialize(ScriptReport.Create(new GameData(args[1]), args[3], args.Length == 5),
            new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    bool export = false, navigation = false;
    string mapPath = "maps/arcaves.map";
    for (int i = 2; i < args.Length; i++)
    {
        if (args[i] == "--export-local" && !export) export = true;
        else if (args[i] == "--navigation") navigation = true;
        else if (args[i] == "--map" && i + 1 < args.Length) mapPath = args[++i];
        else throw new ArgumentException($"Unknown/incomplete argument: {args[i]}");
    }
    mapPath = DatArchive.NormalizePath(mapPath);
    if (!mapPath.StartsWith("maps/") || !mapPath.EndsWith(".map")) throw new ArgumentException("Expected maps/<name>.map.");
    if (export && !File.Exists("project.godot")) throw new ArgumentException("Run --export-local from the project root.");
    var data = new GameData(args[1]);
    MapDocument map = MapDocument.Read(data.Read(mapPath), data.Read);
    MapLayoutWriter.Verify(map);
    MapObject[] objects = map.AllObjects.ToArray();
    string? exportedFile = null;
    if (export)
    {
        Directory.CreateDirectory(".local");
        exportedFile = Path.GetFullPath(".local/" + Path.GetFileNameWithoutExtension(mapPath) + ".v2.json");
        File.WriteAllText(exportedFile + ".tmp", MapExport.ToJson(map));
        File.Move(exportedFile + ".tmp", exportedFile, true);
    }
    var report = new
    {
        SchemaVersion = 2,
        Status = "MAP structural parse complete; byte-for-byte reconstruction verified; no script execution",
        Profile = "standard CE source order; custom CFG and Sfall mods unsupported",
        map.Source, data.SourcePriority,
        Header = map.Prefix.Header,
        Layers = map.Prefix.Layers.Select(l => new { l.Elevation, Squares = l.Tiles.Length,
            UniqueFloorIds = l.Tiles.Select(t => t.FloorId).Distinct().Count() }),
        Scripts = map.ScriptGroups.Select(g => new { g.Type, g.DeclaredCount, Blocks = g.Blocks.Length }),
        map.ObjectsOffset, map.DeclaredRootObjects,
        TotalObjectRecords = objects.Length,
        InventoryObjectRecords = objects.Length - map.DeclaredRootObjects,
        ObjectsByType = objects.GroupBy(o => o.Header.Pid >> 24).OrderBy(g => g.Key).Select(g => new { Type = g.Key, Count = g.Count() }),
        Elevations = map.ObjectLayers.Select(l => new { l.Elevation, l.DeclaredCount }),
        UniquePids = map.Prototypes.Length,
        UniqueFids = objects.Select(o => o.Header.Fid).Distinct().Count(),
        DoorRecords = objects.Where(o => (o.Header.Pid >> 24) == 2 && o.Subtype == 0).Select(o => new { o.Offset, o.Header, o.State }),
        ExitGrids = objects.Where(o => o.Header.Pid >= 0x05000010 && o.Header.Pid <= 0x05000017).Select(o => new { o.Offset, o.Header, o.State }),
        map.MapProgram, map.ScriptBindings, map.Dependencies, map.Diagnostics, map.ConsumedBytes,
        RemainingMapBytes = map.Source.Size - map.ConsumedBytes,
        ExportedFile = exportedFile,
        Navigation = navigation ? NavigationReport.Create(map) : null
    };
    Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or KeyNotFoundException)
{
    Console.Error.WriteLine($"Inspection failed: {error.Message}");
    return 1;
}
