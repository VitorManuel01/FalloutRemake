using System.Text.Json;
using FalloutRemake.Import;

try
{
    if (args.Length == 1 && args[0] == "--self-test")
    {
        try { return SelfTests.Run(); }
        catch (Exception error) { Console.Error.WriteLine($"Synthetic tests failed: {error}"); return 1; }
    }
    if (args.Length < 2 || args[0] != "--data-dir")
    {
        Console.Error.WriteLine("Usage: TempleInspector --self-test | --data-dir <language-folder> [--map maps/arcaves.map] [--export-local] [--navigation] | --data-dir <language-folder> --script scripts/<name>.int [--instructions] | --data-dir <language-folder> <report>");
        Console.Error.WriteLine("Reports: --door-adapter-test, --item-transfer-report, --temple-chest-report, --temple-dialogue-report, --temple-state-report, --arroyo-entry-report, --arroyo-elder-report, --temple-aftermath-report, --temple-npc-departure-report, --item-origin-route-report, --item-art-report, --temple-collection-report, --temple-barrier-report, --explosive-profile-report, --item-use-checkpoint-report, --barrier-effect-report, --barrier-commit-report, --combat-report, --critical-report, --combat-ai-report, --critter-sight-report, --temple-run-report, --drug-report");
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
    if (args.Length == 3 && args[2] == "--temple-state-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(TempleStateReport.Create(new GameData(args[1])), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--arroyo-entry-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(ArroyoEntryAudit.Create(new GameData(args[1])), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--arroyo-elder-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(ArroyoElderAudit.Create(new GameData(args[1])), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--arroyo-audit-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(FalloutRemake.TempleInspector.ArroyoAuditReport.Create(new GameData(args[1])), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--nature-source-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(NatureSourceReport.Create(new GameData(args[1])), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--temple-aftermath-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(TempleSpeechAftermathReport.Create(new GameData(args[1])), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--temple-npc-departure-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(TempleNpcDepartureReport.Create(new GameData(args[1])), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--temple-dialogue-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(TempleDialogueReport.Create(new GameData(args[1])), new JsonSerializerOptions { WriteIndented = true }));
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
    if (args.Length == 3 && args[2] == "--temple-run-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(TempleRunAudit.Create(new GameData(args[1])), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--architecture-clearance-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(ArchitectureClearanceReport.Create(new GameData(args[1])), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 4 && args[2] == "--msg")
    {
        var messages = MessageTextFile.Read(new GameData(args[1]).Read(args[3]));
        foreach (var row in messages.Entries.Values.OrderBy(r => r.Id)) Console.WriteLine($"{row.Id}: {row.Text}");
        return 0;
    }
    if (args.Length == 5 && args[2] == "--find-pid")
    {
        // Every object of the MAP with this PID (hex), or the object at "@offset", on the ground or inside an inventory,
        // with its owner chain and its own inventory.
        var pidData = new GameData(args[1]); var pidMap = MapDocument.Read(pidData.Read(args[3]), pidData.Read);
        bool byOffset = args[4].StartsWith('@'); int wanted = byOffset ? int.Parse(args[4][1..]) : Convert.ToInt32(args[4], 16);
        void Walk(MapObject o, string path)
        {
            if (byOffset ? o.Offset == wanted : o.Header.Pid == wanted)
            {
                Console.WriteLine($"{path} -> offset {o.Offset} hex {o.Header.Tile} elev {o.Header.Elevation} pid 0x{o.Header.Pid:X8} sid {o.Header.Sid} flags 0x{o.Header.Flags:X8}");
                foreach (InventoryEntry entry in o.Inventory)
                    Console.WriteLine($"    x{entry.Quantity} offset {entry.Object.Offset} pid 0x{entry.Object.Header.Pid:X8} sid {entry.Object.Header.Sid} flags 0x{entry.Object.Header.Flags:X8}");
            }
            foreach (InventoryEntry entry in o.Inventory) Walk(entry.Object, $"{path} / offset {o.Offset} pid 0x{o.Header.Pid:X8}");
        }
        foreach (MapObject o in pidMap.ObjectLayers.SelectMany(l => l.Objects)) Walk(o, "map");
        return 0;
    }
    if (args.Length == 4 && args[2] == "--text")
    {
        Console.Write(System.Text.Encoding.Latin1.GetString(new GameData(args[1]).Read(args[3]).Bytes));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--premade-report")
    {
        var game = new GameData(args[1]);
        var paths = game.FindPaths("premade/");
        MessageTextFile traits = MessageTextFile.Read(game.Read("text/english/game/trait.msg")), skills = MessageTextFile.Read(game.Read("text/english/game/skill.msg"));
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Paths = paths.Select(p => new { Path = p, Size = game.Read(p).Bytes.Length, Head = Convert.ToHexString(game.Read(p).Bytes.Take(48).ToArray()) }),
            Characters = paths.Where(p => p.EndsWith(".gcd", StringComparison.OrdinalIgnoreCase)).Select(p => { var c = CharacterFile.Read(game.Read(p)); return new
                { Path = p, c.Name, Base = c.BaseStats, Bonus = c.BonusStats, c.SkillPoints, c.TaggedSkills, c.Traits, c.CharacterPoints, c.Flags, c.BodyType, c.Experience, c.KillType, c.Source }; }),
            Sheets = paths.Where(p => p.EndsWith(".gcd", StringComparison.OrdinalIgnoreCase)).Select(p => CharacterFile.Read(game.Read(p))).Where(c => c.CharacterPoints == 0).Select(c =>
            {
                var sheet = FalloutRemake.Runtime.PlayerCharacter.FromPremade(c); var e = sheet.Effective; int[] b = c.BaseStats;
                // Values the character editor stored (base, before trait stat modifiers) against the same CE formulas.
                var derived = new (string Name, int Index, int Mine)[] { ("HP", 7, sheet.BaseMaxHitPoints), ("AP", 8, 5 + e.Agility / 2), ("AC", 9, e.Agility),
                    ("MD", 11, Math.Max(e.Strength - 5, 1)), ("Carry", 12, 25 + 25 * e.Strength), ("Sequence", 13, 2 * e.Perception),
                    ("Healing", 14, Math.Max(e.Endurance / 3, 1)), ("Critical", 15, e.Luck), ("RadRes", 31, 2 * e.Endurance), ("PoisonRes", 32, 5 * e.Endurance) };
                return new { c.Name, Premade = sheet.Premade, Effective = e.ToArray(), Traits = sheet.Traits.Select(t => traits.Entries.GetValueOrDefault(100 + (int)t)?.Text),
                    Tags = sheet.TaggedSkills.Select(t => skills.Entries.GetValueOrDefault(100 + t)?.Text), sheet.Age, sheet.Gender,
                    Mismatches = derived.Where(d => b[d.Index] != d.Mine).Select(d => $"{d.Name}: GCD {b[d.Index]} vs {d.Mine}").ToArray(),
                    Final = new { HP = sheet.BaseMaxHitPoints, sheet.MaxActionPoints, sheet.ArmorClass, sheet.MeleeDamage, sheet.CarryWeight, sheet.Sequence, sheet.CriticalChance, sheet.HealingRate },
                    Skills = Enumerable.Range(0, 18).Select(i => $"{skills.Entries.GetValueOrDefault(100 + i)?.Text}{(sheet.IsTagged(i) ? "*" : "")} {sheet.Skill(i)}%").ToArray() };
            }),
            TraitNames = Enumerable.Range(0, 16).Select(i => $"{i}: {traits.Entries.GetValueOrDefault(100 + i)?.Text}"),
            Bios = paths.Where(p => p.EndsWith(".bio", StringComparison.OrdinalIgnoreCase)).Select(p => new { Path = p, Text = System.Text.Encoding.Latin1.GetString(game.Read(p).Bytes) })
        }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--rest-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(RestReport.Create(new GameData(args[1])), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--perk-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(PerkReport.Create(new GameData(args[1]), args[1]), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--strength-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(StrengthReport.Create(new GameData(args[1])), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--armor-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(ArmorReport.Create(new GameData(args[1]), args[1]), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--drug-report")
    {
        var drugs = new PrototypeCatalog(new ResourceCatalog(new GameData(args[1]).Read));
        Console.WriteLine(JsonSerializer.Serialize(new[] { 273, 49 }.Select(pid => DrugPrototypeData.Read(drugs.Get(pid))).Select(d => new
        { d.Pid, d.Stats, d.Immediate, d.FirstDelayMinutes, d.First, d.SecondDelayMinutes, d.Second, d.AddictionChance, d.WithdrawalEffect, d.WithdrawalOnset, d.Source }),
            new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--walk-timing-report")
    {
        var walkData = new GameData(args[1]);
        Console.WriteLine(JsonSerializer.Serialize(new[] { WalkTiming.ForGender(walkData, 0), WalkTiming.ForGender(walkData, 1) }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--duel-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(DuelReport.Create(new GameData(args[1])), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--combat-ai-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(CombatAiReport.Create(new GameData(args[1])), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--critical-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(CriticalReport.Create(new GameData(args[1]), args[1]), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--critter-sight-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(CritterSightReport.Create(new GameData(args[1])), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--combat-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(CombatReport.Create(new GameData(args[1])), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--explosive-profile-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(ExplosiveProfileReport.Create(new GameData(args[1])), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--item-use-checkpoint-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(ItemUseCheckpointReport.Create(new GameData(args[1])), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--barrier-effect-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(BarrierEffectReport.Create(new GameData(args[1])), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args.Length == 3 && args[2] == "--barrier-commit-report")
    {
        Console.WriteLine(JsonSerializer.Serialize(BarrierEffectReport.Create(new GameData(args[1]), true), new JsonSerializerOptions { WriteIndented = true }));
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
