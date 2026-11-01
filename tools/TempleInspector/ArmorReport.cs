using System.Text;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

/// <summary>
/// Armor in the supported maps (Temple, Arroyo village, map 126), on the ground or inside inventories, with its PRO block.
/// Then every armor PRO of proto/items and the gPerkDescriptions rows (fallout2.exe) of the perks they name, checked
/// against <see cref="ArmorPerks"/>.
/// </summary>
internal static class ArmorReport
{
    // CE perk.cc: gPerkDescriptions at 0x519DCC, 19 ints per perk (name, description, frmId, maxRank, minLevel, stat,
    // statModifier, param1, value1, paramMode, param2, value2, stats[7]).
    const uint PerkTable = 0x519DCC; const int PerkBytes = 19 * 4, PerkCount = 119;

    public static object Create(GameData data, string installDirectory)
    {
        var catalog = new MapCatalog(data.Read("data/maps.txt"));
        var resources = new ResourceCatalog(data.Read);
        string Name(int messageId)
        {
            string text = Encoding.Latin1.GetString(resources.Read("text/english/game/pro_item.msg").Bytes);
            string? line = text.Split('\n').FirstOrDefault(l => l.StartsWith("{" + messageId + "}"));
            return line == null ? "?" : line.Split('{', '}')[^2].Trim();
        }
        var found = new List<object>(); var weapons = new List<string>();
        foreach (int id in new[] { 3, 4, 126 })
        {
            MapDocument map = catalog.Load(data, id);
            IEnumerable<(MapObject Item, string Where)> Walk(MapObject owner, string where) =>
                owner.Inventory.SelectMany(e => new[] { (e.Object, where) }.Concat(Walk(e.Object, where + " > " + e.Object.Key)));
            foreach (var (item, where) in map.ObjectLayers.SelectMany(l => l.Objects).SelectMany(o => new[] { (o, "ground") }.Concat(Walk(o, o.Key))))
            {
                Prototype? proto = map.Prototypes.FirstOrDefault(p => p.Pid == item.Header.Pid);
                // Cross-check of the walk: weapons found the same way (the Temple's spear among them).
                if (proto is { Type: 0, Subtype: WeaponPrototypeData.Subtype }) weapons.Add($"map {id}: {Name(proto.MessageId)} ({where})");
                if (proto is not { Type: 0, Subtype: ArmorPrototypeData.Subtype }) continue;
                var armor = ArmorPrototypeData.Read(proto);
                found.Add(new { Map = id, item.Key, Pid = proto.Pid, Name = Name(proto.MessageId), Where = where, armor.ArmorClass, armor.DamageThreshold, armor.DamageResistance, armor.Perk });
            }
        }
        if (!weapons.Any(w => w.Contains("Spear"))) throw new InvalidDataException("The scan missed the Temple's spear.");

        // Every armor PRO the game lists, and the perk each one names.
        var prototypes = new PrototypeCatalog(resources);
        int listed = Encoding.Latin1.GetString(resources.Read("proto/items/items.lst").Bytes).Split('\n').Count(l => l.Trim().Length > 0);
        var armors = new List<(int Pid, string Name, ArmorPrototypeData Armor)>();
        for (int number = 1; number <= listed; number++)
        {
            Prototype proto;
            try { proto = prototypes.Get(number); } catch (InvalidDataException) { continue; }
            if (proto is { Type: 0, Subtype: ArmorPrototypeData.Subtype }) armors.Add((proto.Pid, Name(proto.MessageId), ArmorPrototypeData.Read(proto)));
        }
        if (armors.Count < 10) throw new InvalidDataException("The armor PROs were not read.");

        var exe = new CriticalReport.Executable(File.ReadAllBytes(Path.Combine(installDirectory, "fallout2.exe")));
        int[] Perk(int perk) => Enumerable.Range(0, 19).Select(i => exe.Int(PerkTable + (uint)(perk * PerkBytes + 4 * i))).ToArray();
        var perks = new List<object>();
        foreach (int perk in armors.Select(a => a.Armor.Perk).Where(p => p != -1).Distinct().OrderBy(p => p))
        {
            if (perk is < 0 or >= PerkCount) throw new InvalidDataException($"Armor perk {perk} lies outside gPerkDescriptions.");
            int[] row = Perk(perk);
            int maxRank = row[3], stat = row[5], modifier = row[6]; int[] special = row[12..19];
            if (stat is not (-1 or ArmorPerks.RadiationResistanceStat) || (maxRank != -1 && special.Any(v => v != 0))
                || special.Skip(1).Any(v => v != 0))
                throw new InvalidDataException($"Perk {perk} touches more than ST and radiation resistance.");
            var read = new ArmorPerk(perk, maxRank == -1 ? special[0] : 0, stat == -1 ? 0 : modifier);
            if (ArmorPerks.Effect(perk) != read) throw new InvalidDataException($"Perk {perk} differs from fallout2.exe: {read}.");
            perks.Add(new { read.Id, Row = row[2..], read.Strength, read.RadiationResistance });
        }
        if (ArmorPerks.Rows.Select(r => r.Id).Except(armors.Select(a => a.Armor.Perk)).ToArray() is { Length: > 0 } unused)
            throw new InvalidDataException($"The runtime lists perks no armor uses: {string.Join(", ", unused)}; armor perks: {string.Join(", ", armors.Select(a => $"{a.Pid}:{a.Armor.Perk}"))}.");
        return new
        {
            Status = "ARMOR_SCANNED", Count = found.Count, Armor = found, WeaponsSeen = weapons,
            ArmorPrototypes = armors.Select(a => new { a.Pid, a.Name, a.Armor.ArmorClass, a.Armor.Perk, Effect = ArmorPerks.Effect(a.Armor.Perk) }),
            PerkRows = perks, OriginalReconstruction = "verified"
        };
    }
}
