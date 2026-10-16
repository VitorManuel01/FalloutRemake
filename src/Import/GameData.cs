using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace FalloutRemake.Import;

public sealed record GameResource(string Path, string Source, byte[] Bytes);

/// <summary>Explicit standard-install profile. Custom CFG/Sfall/mod paths are not supported.</summary>
public sealed class GameData
{
    private sealed record Source(string Path, DatArchive? Archive);
    private readonly List<Source> _sources = new();
    public string Root { get; }
    public IReadOnlyList<string> SourcePriority => _sources.Select(s => s.Path).ToArray();

    public GameData(string directory)
    {
        Root = Path.GetFullPath(directory);
        if (!Directory.Exists(Root)) throw new DirectoryNotFoundException(Root);
        var files = Directory.GetFiles(Root).ToDictionary(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase);
        if (!files.ContainsKey("master.dat") || !files.ContainsKey("critter.dat"))
            throw new InvalidDataException("Select the language folder containing master.dat and critter.dat.");

        // CE game.cc/db.cc/xfile.cc: last registered source wins. Standard profile only.
        if (files.TryGetValue("f2_res.dat", out string? hiRes)) AddArchive(hiRes);
        foreach (string patch in files.Values.Where(p => Regex.IsMatch(Path.GetFileName(p), @"^patch[0-9]{3}\.dat$", RegexOptions.IgnoreCase))
                     .OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            AddArchive(patch);
        string? loose = Directory.GetDirectories(Root).FirstOrDefault(p => Path.GetFileName(p).Equals("data", StringComparison.OrdinalIgnoreCase));
        if (loose != null) _sources.Add(new Source(loose, null));
        AddArchive(files["critter.dat"]);
        AddArchive(files["master.dat"]);
    }

    public GameResource Read(string path)
    {
        string normalized = DatArchive.NormalizePath(path);
        foreach (Source source in _sources)
        {
            if (source.Archive != null)
            {
                if (source.Archive.Entries.ContainsKey(normalized))
                    return new GameResource(normalized, source.Path, source.Archive.Read(normalized));
            }
            else
            {
                string? loose = FindLoose(source.Path, normalized);
                if (loose == null) continue;
                using var stream = File.OpenRead(loose);
                if (stream.Length > DatArchive.MaxResourceBytes) throw new InvalidDataException("Loose resource exceeds 64 MiB.");
                byte[] bytes = new byte[(int)stream.Length];
                stream.ReadExactly(bytes);
                return new GameResource(normalized, loose, bytes);
            }
        }
        throw new FileNotFoundException($"Game resource not found: {normalized}");
    }

    public IReadOnlyList<string> FindPaths(string fragment) => _sources
        .SelectMany(s => s.Archive != null ? s.Archive.Entries.Keys.AsEnumerable() :
            Directory.EnumerateFiles(s.Path, "*", SearchOption.AllDirectories).Select(p => DatArchive.NormalizePath(Path.GetRelativePath(s.Path, p))))
        .Where(p => p.Contains(fragment, StringComparison.OrdinalIgnoreCase))
        .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.Ordinal).ToArray();

    private void AddArchive(string path) => _sources.Add(new Source(path, new DatArchive(path)));

    private static string? FindLoose(string root, string path)
    {
        string current = root;
        foreach (string part in path.Split('/'))
        {
            if (!Directory.Exists(current)) return null;
            string? match = Directory.EnumerateFileSystemEntries(current)
                .FirstOrDefault(p => Path.GetFileName(p).Equals(part, StringComparison.OrdinalIgnoreCase));
            if (match == null) return null;
            current = match;
        }
        return File.Exists(current) ? current : null;
    }
}
