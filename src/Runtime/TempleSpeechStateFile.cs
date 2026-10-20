using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FalloutRemake.Runtime;

/// <summary>Standalone rehearsal state. Never reads or writes a world checkpoint.</summary>
public static class TempleSpeechStateFile
{
    public const int MaxBytes = 65536;
    private sealed record Envelope([property: JsonRequired] int Version, [property: JsonRequired] string Profile,
        [property: JsonRequired] ScriptStateSnapshot State);
    private static readonly JsonSerializerOptions Options = new()
    { MaxDepth = 12, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, WriteIndented = true };

    public static byte[] Encode(TempleSpeechProfile profile, BoundedScriptState state)
    {
        ScriptStateSnapshot snapshot = state.Capture();
        if (profile.Classify(snapshot) == TempleSpeechState.Unsupported)
            throw new InvalidDataException("Unsupported Temple Speech rehearsal state.");
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new Envelope(1, profile.Fingerprint, snapshot), Options);
        if (bytes.Length > MaxBytes) throw new InvalidDataException("Temple Speech rehearsal exceeds the file size limit.");
        return bytes;
    }
    public static BoundedScriptState Decode(TempleSpeechProfile profile, byte[] bytes)
    {
        if (bytes == null || bytes.Length is < 1 or > MaxBytes) throw new InvalidDataException("Invalid Temple Speech file size.");
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 12 });
            RejectDuplicateNames(document.RootElement);
            var envelope = document.Deserialize<Envelope>(Options);
            if (envelope == null || envelope.Version != 1 || envelope.Profile != profile.Fingerprint
                || profile.Classify(envelope.State) == TempleSpeechState.Unsupported)
                throw new InvalidDataException("Unsupported Temple Speech rehearsal profile or state.");
            var state = profile.CreateInitial(); state.Restore(envelope.State); return state;
        }
        catch (JsonException error) { throw new InvalidDataException("Malformed Temple Speech rehearsal JSON.", error); }
    }
    private static void RejectDuplicateNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate Temple Speech JSON property.");
                RejectDuplicateNames(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RejectDuplicateNames(item);
    }
    public static BoundedScriptState Read(string path, TempleSpeechProfile profile)
    {
        using var file = File.OpenRead(path);
        if (file.Length is < 1 or > MaxBytes) throw new InvalidDataException("Invalid Temple Speech file size.");
        var bytes = new byte[(int)file.Length]; file.ReadExactly(bytes);
        if (file.ReadByte() != -1) throw new InvalidDataException("Temple Speech file changed during read.");
        return Decode(profile, bytes);
    }
    public static void RestoreInto(string path, TempleSpeechProfile profile, BoundedScriptState destination)
    {
        var candidate = Read(path, profile);
        destination.Restore(candidate.Capture());
    }
    public static void Write(string path, TempleSpeechProfile profile, BoundedScriptState state)
    {
        byte[] bytes = Encode(profile, state);
        string full = Path.GetFullPath(path), directory = Path.GetDirectoryName(full)!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $"temple-speech-{Guid.NewGuid():N}.tmp");
        try { File.WriteAllBytes(temporary, bytes); AtomicFileReplacement.Replace(temporary, full); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
