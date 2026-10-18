using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FalloutRemake.Import;

public sealed record MapCatalogEntry(int Id, string Name, string ResourcePath);

/// <summary>Bounded data/maps.txt subset. Section numbers are map IDs; never renumbered.</summary>
public sealed class MapCatalog
{
    private readonly Dictionary<int, MapCatalogEntry> _entries = new();
    public ResourceIdentity Source { get; }
    public IReadOnlyList<MapCatalogEntry> Entries => _entries.Values.OrderBy(e => e.Id).ToArray();
    public MapCatalogEntry Get(int id) => _entries.TryGetValue(id, out MapCatalogEntry? entry)
        ? entry : throw new InvalidDataException($"Map {id} is absent from the map catalog.");
    public MapCatalog(GameResource resource)
    {
        if (resource.Bytes.Length > 2 * 1024 * 1024) throw new InvalidDataException("Map catalog exceeds 2 MiB.");
        Source = ResourceIdentity.From(resource);
        using var reader = new StringReader(Encoding.Latin1.GetString(resource.Bytes));
        int? id = null; string? file = null, name = null;
        void Finish()
        {
            if (!id.HasValue) return;
            if (file == null || !Regex.IsMatch(file, @"^[A-Za-z0-9_]{1,32}$"))
                throw new InvalidDataException("Map catalog contains an invalid or missing map_name.");
            if (!_entries.TryAdd(id.Value, new MapCatalogEntry(id.Value, name ?? file, $"maps/{file.ToLowerInvariant()}.map")))
                throw new InvalidDataException("Duplicate map catalog ID.");
        }
        while (reader.ReadLine() is { } raw)
        {
            if (raw.Length > 4096) throw new InvalidDataException("Map catalog line exceeds supported bounds.");
            string line = raw.Split(';')[0].Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith('['))
            {
                Finish();
                Match section = Regex.Match(line, @"^\[Map ([0-9]{3,4})\]$", RegexOptions.IgnoreCase);
                if (!section.Success || !int.TryParse(section.Groups[1].Value, out int number) || number > 1023)
                    throw new InvalidDataException("Unsupported map catalog section.");
                id = number; file = null; name = null;
            }
            else if (id.HasValue && line.IndexOf('=') is int split && split > 0)
            {
                string key = line[..split].Trim(), value = line[(split + 1)..].Trim();
                if (key.Equals("map_name", StringComparison.OrdinalIgnoreCase))
                { if (file != null) throw new InvalidDataException("Repeated map_name."); file = value; }
                else if (key.Equals("lookup_name", StringComparison.OrdinalIgnoreCase)) name = value;
            }
        }
        Finish();
        if (_entries.Count == 0 || !_entries.Keys.OrderBy(i => i).SequenceEqual(Enumerable.Range(0, _entries.Count)))
            throw new InvalidDataException("Map catalog IDs must be contiguous from zero.");
    }
    public MapDocument Load(GameData data, int id)
    {
        MapDocument map = MapDocument.Read(data.Read(Get(id).ResourcePath), data.Read);
        if (map.Prefix.Header.MapIndex != id) throw new InvalidDataException("MAP header ID differs from its catalog entry.");
        MapLayoutWriter.Verify(map); return map;
    }
}
