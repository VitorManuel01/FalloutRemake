using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace FalloutRemake.Import;

public sealed record ResourceIdentity(string Path, string Source, int Size, string Sha256)
{
    public static ResourceIdentity From(GameResource resource) => new(resource.Path, resource.Source,
        resource.Bytes.Length, Convert.ToHexString(SHA256.HashData(resource.Bytes)));
}

/// <summary>A per-import snapshot of every resolved dependency.</summary>
public sealed class ResourceCatalog
{
    private readonly Func<string, GameResource> _read;
    private readonly Dictionary<string, GameResource> _cache = new(StringComparer.OrdinalIgnoreCase);
    public ResourceCatalog(Func<string, GameResource> read) => _read = read;
    public GameResource Read(string path)
    {
        path = DatArchive.NormalizePath(path);
        if (!_cache.TryGetValue(path, out GameResource? resource))
        {
            GameResource input = _read(path);
            resource = new GameResource(path, input.Source, (byte[])input.Bytes.Clone());
            _cache.Add(path, resource);
        }
        return resource;
    }
    public ResourceIdentity[] Identities => _cache.Values.Select(ResourceIdentity.From).OrderBy(r => r.Path, StringComparer.Ordinal).ToArray();

    public string ListEntry(string listPath, int zeroBasedIndex, string extension)
    {
        // ReadLine keeps internal blank/comment lines: skipping them would change IDs.
        using var reader = new StringReader(Encoding.Latin1.GetString(Read(listPath).Bytes));
        if (zeroBasedIndex < 0) throw new InvalidDataException($"Negative index into {listPath}.");
        string? line = null;
        for (int i = 0; i <= zeroBasedIndex; i++)
            line = reader.ReadLine() ?? throw new InvalidDataException($"Index {zeroBasedIndex} outside {listPath}.");
        string token = line!.TrimStart().Split(new[] { ' ', '\t', ';', ',' }, StringSplitOptions.None)[0];
        if (!token.EndsWith(extension, StringComparison.OrdinalIgnoreCase) || token.IndexOfAny(new[] { '/', '\\', ':' }) >= 0)
            throw new InvalidDataException($"Invalid resource name at index {zeroBasedIndex} in {listPath}.");
        return DatArchive.NormalizePath(token);
    }

    public ResourceIdentity Script(int index)
    {
        string name = ListEntry("scripts/scripts.lst", index, ".int");
        return ResourceIdentity.From(Read("scripts/" + name));
    }
}
