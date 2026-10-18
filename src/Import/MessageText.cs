using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FalloutRemake.Import;

public sealed record MessageText(int Id, string Audio, string Text);

public sealed record MessageTextFile(ResourceIdentity Source, IReadOnlyDictionary<int, MessageText> Entries, int Replacements)
{
    public const int MaxBytes = 1024 * 1024, MaxField = 1023, MaxRecords = 20000;
    public static MessageTextFile Read(GameResource resource)
    {
        if (resource.Bytes.Length > MaxBytes) throw new InvalidDataException("MSG exceeds the prototype text profile limit.");
        string text = Encoding.Latin1.GetString(resource.Bytes);
        int position = 0, records = 0, replacements = 0;
        string? Field()
        {
            int open = text.IndexOf('{', position), stray = text.IndexOf('}', position);
            if (stray >= 0 && (open < 0 || stray < open)) throw new InvalidDataException("MSG has a stray closing delimiter.");
            if (open < 0) return null;
            int close = text.IndexOf('}', open + 1);
            if (close < 0) throw new InvalidDataException("MSG has an unterminated field.");
            string field = text.Substring(open + 1, close - open - 1).Replace("\r", "").Replace("\n", "");
            if (field.Length > MaxField || field.Contains('\0')) throw new InvalidDataException("MSG field is too long or contains NUL.");
            position = close + 1; return field;
        }
        var entries = new Dictionary<int, MessageText>();
        while (Field() is { } number)
        {
            if (++records > MaxRecords || !int.TryParse(number, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int id))
                throw new InvalidDataException("MSG has an invalid message ID or too many records.");
            string audio = Field() ?? throw new InvalidDataException("MSG audio field missing.");
            string value = Field() ?? throw new InvalidDataException("MSG text field missing.");
            if (entries.ContainsKey(id)) replacements++; // CE replaces the previous entry with this ID.
            entries[id] = new MessageText(id, audio, value);
        }
        return new MessageTextFile(ResourceIdentity.From(resource), new ReadOnlyDictionary<int, MessageText>(entries), replacements);
    }
}

public sealed record PrototypeText(int Pid, int MessageId, string? Name, string? Description, ResourceIdentity Source);

/// <summary>One unambiguous, complete language set; never combines messages from different languages.</summary>
public sealed class PrototypeTextCatalog
{
    private static readonly string[] Types = { "item", "crit", "scen", "wall", "tile", "misc" };
    private readonly ResourceCatalog _resources;
    private readonly Dictionary<int, MessageTextFile> _files = new();
    public string Language { get; }
    public ResourceIdentity[] Dependencies => _resources.Identities;
    public PrototypeTextCatalog(Func<string, GameResource> read, IEnumerable<string> availablePaths)
    {
        _resources = new ResourceCatalog(read);
        string[] paths = availablePaths.Select(DatArchive.NormalizePath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        string[] languages = paths.Select(p => Regex.Match(p, @"^text/([a-z0-9_-]+)/game/pro_(item|crit|scen|wall|tile|misc)\.msg$"))
            .Where(m => m.Success).Select(m => m.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(language => Types.All(type => paths.Contains($"text/{language}/game/pro_{type}.msg", StringComparer.OrdinalIgnoreCase))).ToArray();
        if (languages.Length != 1) throw new InvalidDataException("Prototype messages require one complete, unambiguous language set.");
        Language = languages[0];
    }
    public MessageTextFile FileForType(int type)
    {
        if (type is < 0 or > 5) throw new InvalidDataException("Invalid prototype text type.");
        if (!_files.TryGetValue(type, out MessageTextFile? file))
        {
            file = MessageTextFile.Read(_resources.Read($"text/{Language}/game/pro_{Types[type]}.msg"));
            _files.Add(type, file);
        }
        return file;
    }
    public PrototypeText? Resolve(Prototype prototype)
    {
        if (prototype.MessageId == -1) return null;
        MessageTextFile file = FileForType(prototype.Type);
        string? Entry(int id) => file.Entries.TryGetValue(id, out MessageText? row) && !string.IsNullOrWhiteSpace(row.Text) ? row.Text : null;
        return new PrototypeText(prototype.Pid, prototype.MessageId, Entry(prototype.MessageId),
            prototype.MessageId < int.MaxValue ? Entry(prototype.MessageId + 1) : null, file.Source);
    }
}
