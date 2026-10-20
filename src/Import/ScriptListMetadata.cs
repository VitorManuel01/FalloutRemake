using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace FalloutRemake.Import;

public sealed record ScriptListEntry(int Index, string ProgramPath, int DeclaredLocals, ResourceIdentity Source);

/// <summary>Bounded scripts.lst metadata profile. Keeps physical line indices; malformed declarations fail rather than emulate atoi.</summary>
public static class ScriptListMetadata
{
    public static ScriptListEntry Read(GameResource resource, int index)
    {
        if (DatArchive.NormalizePath(resource.Path) != "scripts/scripts.lst" || resource.Bytes.Length > 4 * 1024 * 1024
            || index < 0 || index > 100000) throw new InvalidDataException("Unsupported scripts.lst source or index.");
        GameResource snapshot = resource with { Bytes = (byte[])resource.Bytes.Clone() };
        var catalog = new ResourceCatalog(_ => snapshot);
        string name = catalog.ListEntry("scripts/scripts.lst", index, ".int");
        using var reader = new StringReader(Encoding.Latin1.GetString(snapshot.Bytes)); string? line = null;
        for (int i = 0; i <= index; i++) line = reader.ReadLine() ?? throw new InvalidDataException("Script metadata line missing.");
        int count = 0, marker = line!.IndexOf("local_vars=", StringComparison.Ordinal);
        // CE reads this declaration only when the line also contains '#'.
        if (line.Contains('#') && marker >= 0)
        {
            if (line.IndexOf("local_vars=", marker + 11, StringComparison.Ordinal) >= 0)
                throw new InvalidDataException("Ambiguous script local-variable declaration.");
            string token = line[(marker + 11)..].TrimStart().Split(new[] { ' ', '\t', ';', '#' }, StringSplitOptions.None)[0];
            if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out count) || count > 4096)
                throw new InvalidDataException("Unsupported script local-variable count.");
        }
        return new(index, "scripts/" + name, count, ResourceIdentity.From(snapshot));
    }
}
