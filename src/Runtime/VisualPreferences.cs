using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FalloutRemake.Runtime;

public sealed record VisualPreferences(bool ShowNavigationGrid, bool UseOriginalFloor, bool RevealActor)
{
    public static VisualPreferences Defaults { get; } = new(true, false, true);
}

/// <summary>Project-local visual choices, independent of source data paths and world checkpoints.</summary>
public static class VisualPreferencesFile
{
    public const int MaxBytes = 4096;
    public static VisualPreferences Read(string path)
    {
        try
        {
            using var file = File.OpenRead(path);
            if (file.Length is < 1 or > MaxBytes) throw new InvalidDataException("Invalid visual preferences size.");
            var bytes = new byte[(int)file.Length]; file.ReadExactly(bytes);
            if (file.ReadByte() != -1) throw new InvalidDataException("Visual preferences changed during read.");
            return Decode(bytes);
        }
        catch (FileNotFoundException) { return VisualPreferences.Defaults; }
        catch (DirectoryNotFoundException) { return VisualPreferences.Defaults; }
    }
    public static VisualPreferences Decode(byte[] bytes)
    {
        if (bytes.Length is < 1 or > MaxBytes) throw new InvalidDataException("Invalid visual preferences size.");
        try
        {
            using var doc = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 4 });
            JsonElement root = doc.RootElement;
            string[] names = { "Version", "ShowNavigationGrid", "UseOriginalFloor", "RevealActor" };
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != names.Length
                || names.Any(n => !root.TryGetProperty(n, out _))
                || !root.GetProperty("Version").TryGetInt32(out int version) || version != 1
                || names.Skip(1).Any(n => root.GetProperty(n).ValueKind is not (JsonValueKind.True or JsonValueKind.False)))
                throw new InvalidDataException("Unsupported visual preferences schema.");
            return new(root.GetProperty(names[1]).GetBoolean(), root.GetProperty(names[2]).GetBoolean(), root.GetProperty(names[3]).GetBoolean());
        }
        catch (JsonException error) { throw new InvalidDataException("Malformed visual preferences JSON.", error); }
        catch (InvalidOperationException error) { throw new InvalidDataException("Invalid visual preferences types.", error); }
    }
    public static void Write(string path, VisualPreferences preferences)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new { Version = 1, preferences.ShowNavigationGrid,
            preferences.UseOriginalFloor, preferences.RevealActor }, new JsonSerializerOptions { WriteIndented = true });
        string full = Path.GetFullPath(path), directory = Path.GetDirectoryName(full)!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $"visual-preferences-{Guid.NewGuid():N}.tmp");
        try { File.WriteAllBytes(temporary, bytes); File.Move(temporary, full, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
