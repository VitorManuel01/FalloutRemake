using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FalloutRemake.Runtime;

public sealed record SavedArea(int Map, string SourceHash, string DependenciesHash, ActorLocation Location, SavedDoorSession Session, TakenItem[]? Taken = null, ItemUseAllocation[]? Uses = null, SceneryReplacement[]? Replacements = null);
public sealed record WorldCheckpoint(int Version, string Profile, string CatalogHash, int InitialMap, int CurrentMap, SavedArea[] Areas, SavedTempleProgress? Temple = null, SavedWorldEntries? Entries = null);

/// <summary>Small project-local checkpoint, not an original Fallout SAV and never an asset archive.</summary>
public static class CheckpointFile
{
    public const int MaxBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new()
    { WriteIndented = true, MaxDepth = 16, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static byte[] Encode(WorldCheckpoint checkpoint)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(checkpoint, Options);
        if (bytes.Length > MaxBytes) throw new InvalidDataException("Checkpoint exceeds its size limit.");
        return bytes;
    }
    public static WorldCheckpoint Decode(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes.Length > MaxBytes) throw new InvalidDataException("Invalid checkpoint size.");
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
            RejectDuplicateNames(document.RootElement);
            var checkpoint = document.Deserialize<WorldCheckpoint>(Options) ?? throw new InvalidDataException("Empty checkpoint.");
            if (checkpoint.Version >= 6 && !document.RootElement.TryGetProperty("Temple", out _))
                throw new InvalidDataException("Checkpoint v6 requires an explicit Temple field.");
            if (checkpoint.Version >= 8 && !document.RootElement.TryGetProperty("Entries",out _))
                throw new InvalidDataException("Checkpoint v8 requires an explicit Entries field.");
            if (checkpoint.Version >= 9 && (!document.RootElement.TryGetProperty("Entries",out var entries)
                || entries.ValueKind != JsonValueKind.Object || !entries.TryGetProperty("ArroyoGlobals",out _)))
                throw new InvalidDataException("Checkpoint v9 requires explicit ArroyoGlobals entry state.");
            return checkpoint;
        }
        catch (JsonException error) { throw new InvalidDataException("Malformed checkpoint JSON.", error); }
    }
    private static void RejectDuplicateNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate checkpoint JSON property.");
                RejectDuplicateNames(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RejectDuplicateNames(item);
    }
    public static void Write(string path, WorldCheckpoint checkpoint)
    {
        byte[] bytes = Encode(checkpoint);
        string full = Path.GetFullPath(path), directory = Path.GetDirectoryName(full)!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $"checkpoint-{Guid.NewGuid():N}.tmp");
        try { File.WriteAllBytes(temporary, bytes); AtomicFileReplacement.Replace(temporary, full); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static WorldCheckpoint Read(string path)
    {
        using var file = File.OpenRead(path);
        if (file.Length == 0 || file.Length > MaxBytes) throw new InvalidDataException("Invalid checkpoint size.");
        var bytes = new byte[(int)file.Length]; file.ReadExactly(bytes);
        if (file.ReadByte() != -1) throw new InvalidDataException("Checkpoint changed during read.");
        return Decode(bytes);
    }
}
