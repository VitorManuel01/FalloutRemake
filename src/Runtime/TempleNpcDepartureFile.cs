using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FalloutRemake.Runtime;

/// <summary>Only departure metadata; separate from both the accepted Speech file and the world checkpoint.</summary>
public static class TempleNpcDepartureFile
{
    public const int MaxBytes = 8192;
    private static readonly JsonSerializerOptions Options = new()
    { MaxDepth = 4, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, WriteIndented = true };
    public static byte[] Encode(TempleNpcDeparture session)
    {
        var snapshot = session.Capture(); session.Profile.Validate(snapshot);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, Options);
        if (bytes.Length > MaxBytes) throw new InvalidDataException("NPC departure file exceeds its size limit.");
        return bytes;
    }
    public static TempleNpcDeparture Decode(TempleNpcDepartureProfile profile, byte[] bytes)
    {
        if (bytes == null || bytes.Length is < 1 or > MaxBytes) throw new InvalidDataException("Invalid NPC departure file size.");
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 4 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("NPC departure must be a JSON object.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
                if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate NPC departure property.");
            var snapshot = document.Deserialize<NpcDepartureSnapshot>(Options);
            if (snapshot == null) throw new InvalidDataException("Missing NPC departure state.");
            return profile.Restore(snapshot);
        }
        catch (JsonException error) { throw new InvalidDataException("Malformed NPC departure JSON.", error); }
    }
    public static TempleNpcDeparture Read(string path, TempleNpcDepartureProfile profile)
    {
        using var file = File.OpenRead(path);
        if (file.Length is < 1 or > MaxBytes) throw new InvalidDataException("Invalid NPC departure file size.");
        var bytes = new byte[(int)file.Length]; file.ReadExactly(bytes);
        if (file.ReadByte() != -1) throw new InvalidDataException("NPC departure file changed during read.");
        return Decode(profile, bytes);
    }
    public static void Write(string path, TempleNpcDeparture session)
    {
        byte[] bytes = Encode(session);
        string full = Path.GetFullPath(path), directory = Path.GetDirectoryName(full)!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $"temple-departure-{Guid.NewGuid():N}.tmp");
        try { File.WriteAllBytes(temporary, bytes); AtomicFileReplacement.Replace(temporary, full); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
