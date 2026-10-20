using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace FalloutRemake.Import;

public sealed record GameGlobalEntry(int Index, int Line, string? Name, int Value);
public sealed record GameGlobalFile(ResourceIdentity Source, IReadOnlyList<GameGlobalEntry> Entries)
{
    /// <summary>Bounded classic GAM profile. Counts native implicit-zero rows; does not compact whitespace or indented comments.</summary>
    public static GameGlobalFile Read(GameResource resource)
    {
        if (resource.Bytes.Length > 1024 * 1024) throw new InvalidDataException("GAM source exceeds supported size.");
        GameResource snapshot = resource with { Bytes = (byte[])resource.Bytes.Clone() };
        using var reader = new StringReader(Encoding.Latin1.GetString(snapshot.Bytes));
        var entries = new List<GameGlobalEntry>(); bool section = false; int number = 0; string? line;
        while ((line = reader.ReadLine()) != null)
        {
            number++; if (line.Length > 257) throw new InvalidDataException("GAM line exceeds native line profile.");
            if (!section) { if (line == "GAME_GLOBAL_VARS:") section = true; continue; }
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal)) continue;
            int semicolon = line.IndexOf(';'); string valueLine = semicolon < 0 ? line : line[..semicolon];
            int equals = valueLine.IndexOf('='); int value = 0; string? name = null;
            if (equals >= 0)
            {
                if (!int.TryParse(valueLine[(equals + 1)..].Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
                    throw new InvalidDataException("Unsupported GAM scalar assignment.");
                name = valueLine[..equals].Trim().TrimEnd(':').Trim();
                if (name.Length == 0) throw new InvalidDataException("Missing GAM variable name.");
            }
            if (entries.Count >= 4096) throw new InvalidDataException("Too many GAM variables.");
            entries.Add(new(entries.Count, number, name, value));
        }
        if (!section || entries.Count == 0) throw new InvalidDataException("Missing or empty game global section.");
        return new(ResourceIdentity.From(snapshot), entries.AsReadOnly());
    }
}
