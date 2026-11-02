using System.Text;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;

/// <summary>
/// The weapons of the supported maps (3, 4 and 126, ground and nested inventories) with their minimum Strength and
/// two-handed flag, against the Strength of the neutral profile and the three premades: the to-hit penalty each one
/// would take (CE attackDetermineToHit, 20 per missing point).
/// </summary>
internal static class StrengthReport
{
    public static object Create(GameData data)
    {
        var catalog = new MapCatalog(data.Read("data/maps.txt"));
        var resources = new ResourceCatalog(data.Read);
        string text = Encoding.Latin1.GetString(resources.Read("text/english/game/pro_item.msg").Bytes);
        string Name(int messageId) => text.Split('\n').FirstOrDefault(l => l.StartsWith("{" + messageId + "}")) is { } line ? line.Split('{', '}')[^2].Trim() : "?";
        var weapons = new Dictionary<int, (string Name, WeaponPrototypeData Weapon, List<int> Maps)>();
        foreach (int id in new[] { 3, 4, 126 })
        {
            MapDocument map = catalog.Load(data, id);
            IEnumerable<MapObject> Walk(MapObject owner) => owner.Inventory.SelectMany(e => new[] { e.Object }.Concat(Walk(e.Object)));
            foreach (MapObject item in map.ObjectLayers.SelectMany(l => l.Objects).SelectMany(o => new[] { o }.Concat(Walk(o))))
                if (map.Prototypes.FirstOrDefault(p => p.Pid == item.Header.Pid) is { Type: 0, Subtype: WeaponPrototypeData.Subtype } proto)
                {
                    if (!weapons.TryGetValue(proto.Pid, out var entry)) weapons[proto.Pid] = entry = (Name(proto.MessageId), WeaponPrototypeData.Read(proto), new List<int>());
                    if (!entry.Maps.Contains(id)) entry.Maps.Add(id);
                }
        }
        if (!weapons.Values.Any(w => w.Name == "Spear")) throw new InvalidDataException("The scan missed the Temple's spear.");
        var characters = new[] { PlayerCharacter.Neutral }.Concat(new[] { "combat", "stealth", "diplomat" }
            .Select(name => PlayerCharacter.FromPremade(CharacterFile.Read(data.Read($"premade/{name}.gcd"))))).ToArray();
        return new
        {
            Status = "STRENGTH_SCANNED",
            Weapons = weapons.OrderBy(w => w.Key).Select(w => new { Pid = w.Key, w.Value.Name, w.Value.Weapon.MinStrength, w.Value.Weapon.TwoHanded, Maps = w.Value.Maps }),
            Characters = characters.Select(c => new
            {
                c.Name, Strength = c.Effective.Strength, OneHander = c.Has(CharacterTrait.OneHander),
                Penalties = weapons.OrderBy(w => w.Key).Select(w => new { w.Value.Name, Penalty = 20 * Math.Max(0, w.Value.Weapon.MinStrength - c.Effective.Strength) })
                    .Where(p => p.Penalty > 0)
            }),
            OriginalReconstruction = "verified"
        };
    }
}
