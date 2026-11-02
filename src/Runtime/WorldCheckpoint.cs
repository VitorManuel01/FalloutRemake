using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FalloutRemake.Runtime;

/// <summary>Ground (optional, inside v13): thrown units of the area's collected items lying on a hex; absent when there are none.</summary>
public sealed record SavedArea(int Map, string SourceHash, string DependenciesHash, ActorLocation Location, SavedDoorSession Session, TakenItem[]? Taken = null, ItemUseAllocation[]? Uses = null, SceneryReplacement[]? Replacements = null,
    ConsumedItem[]? Consumed = null, GroundItem[]? Ground = null);
public sealed record WorldCheckpoint(int Version, string Profile, string CatalogHash, int InitialMap, int CurrentMap, SavedArea[] Areas, SavedTempleProgress? Temple = null, SavedWorldEntries? Entries = null,
    SavedCombat? Combat = null, SavedCharacter? Character = null, SavedTempleDuel? Duel = null, SavedSkills? Skills = null, SavedFirearms? Firearms = null);
/// <summary>v11 adds <see cref="SavedArea.Consumed"/> (drugs taken from collected items). v10: outcome of won fights. Experience must equal the PRO experience of the defeated critters.</summary>
/// <summary>Survivors (optional, inside v13): critters left standing when the player ended a fight, where they stand.</summary>
/// <summary>
/// GameTime (optional, inside v13): the game clock in CE ticks, absent at the start time; criticals wait for day 1.
/// PlayerCrippled (optional, inside v13): the dude's crippled limbs (CE damage flags), which outlast fights.
/// Hands (optional, inside v13): the items the player holds and the active hand (<see cref="SavedHands"/>); absent at the defaults.
/// </summary>
public sealed record SavedCombat([property: JsonRequired] DefeatedCritter[] Defeated, [property: JsonRequired] int Experience, int? PlayerHitPoints,
    SurvivingCritter[]? Survivors = null, SavedPoison? Poison = null, long? GameTime = null, int? PlayerCrippled = null, SavedHands? Hands = null);
/// <summary>The dude's poison (optional, inside v13): level and game ticks until the next poison event.</summary>
public sealed record SavedPoison([property: JsonRequired] int Level, [property: JsonRequired] int TicksLeft);

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
            if (checkpoint.Version >= 10 && (!document.RootElement.TryGetProperty("Combat", out var combat) || combat.ValueKind != JsonValueKind.Object))
                throw new InvalidDataException("Checkpoint v10 requires an explicit Combat object.");
            if (checkpoint.Version >= 12 && (!document.RootElement.TryGetProperty("Character", out var character) || character.ValueKind != JsonValueKind.Object))
                throw new InvalidDataException("Checkpoint v12 requires an explicit Character object.");
            if (checkpoint.Version >= 12 && (!document.RootElement.TryGetProperty("Duel", out var duel) || duel.ValueKind != JsonValueKind.Object))
                throw new InvalidDataException("Checkpoint v12 requires an explicit Duel object.");
            if (checkpoint.Version >= 13 && (!document.RootElement.TryGetProperty("Skills", out var skills) || skills.ValueKind != JsonValueKind.Object))
                throw new InvalidDataException("Checkpoint v13 requires an explicit Skills object.");
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
