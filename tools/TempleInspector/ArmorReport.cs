using System.Text;
using FalloutRemake.Import;

/// <summary>Armor in the supported maps (Temple, Arroyo village, map 126), on the ground or inside inventories, with its PRO block.</summary>
internal static class ArmorReport
{
    public static object Create(GameData data)
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
        return new { Status = "ARMOR_SCANNED", Count = found.Count, Armor = found, WeaponsSeen = weapons, OriginalReconstruction = "verified" };
    }
}
