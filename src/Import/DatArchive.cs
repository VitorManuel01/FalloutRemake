using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace FalloutRemake.Import;

public sealed record DatEntry(string Path, bool Compressed, int Size, int StoredSize, long Offset);

/// <summary>Read-only Fallout 2 DAT reader. No files are extracted to disk.</summary>
public sealed class DatArchive
{
    // Deliberate milestone limits, well above the observed Temple resources.
    public const int MaxResourceBytes = 64 * 1024 * 1024;
    private readonly Dictionary<string, DatEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    public string FilePath { get; }
    public IReadOnlyDictionary<string, DatEntry> Entries => _entries;

    public DatArchive(string path)
    {
        FilePath = System.IO.Path.GetFullPath(path);
        using var file = File.OpenRead(FilePath);
        using var reader = new BinaryReader(file, Encoding.ASCII);
        if (file.Length < 12) throw new InvalidDataException("DAT footer is truncated.");
        file.Position = file.Length - 8;
        int indexBytes = reader.ReadInt32(); // DAT integers are little-endian.
        int archiveBytes = reader.ReadInt32();
        if (indexBytes < 4 || indexBytes > MaxResourceBytes || archiveBytes < (long)indexBytes + 8 || archiveBytes > file.Length)
            throw new InvalidDataException("Invalid DAT footer bounds.");
        long dataStart = file.Length - archiveBytes;
        long indexStart = file.Length - 8 - indexBytes;
        file.Position = indexStart;
        int count = reader.ReadInt32();
        if (count < 0 || count > (indexBytes - 4) / 18)
            throw new InvalidDataException("Invalid DAT entry count.");
        for (int i = 0; i < count; i++)
        {
            Require(file, 4, file.Length - 8);
            int nameBytes = reader.ReadInt32();
            if (nameBytes < 1 || nameBytes > 1024) throw new InvalidDataException("Invalid DAT path length.");
            Require(file, nameBytes + 13, file.Length - 8);
            string name = NormalizePath(Encoding.ASCII.GetString(reader.ReadBytes(nameBytes)));
            byte compression = reader.ReadByte();
            int size = reader.ReadInt32();
            int storedSize = reader.ReadInt32();
            int relativeOffset = reader.ReadInt32();
            long offset = dataStart + relativeOffset;
            if (compression > 1 || size < 0 || storedSize < 0 || relativeOffset < 0 ||
                offset > indexStart || storedSize > indexStart - offset ||
                (compression == 0 && size != storedSize))
                throw new InvalidDataException($"Invalid DAT entry bounds: {name}");
            if (!_entries.TryAdd(name, new DatEntry(name, compression == 1, size, storedSize, offset)))
                throw new InvalidDataException($"Duplicate DAT path: {name}");
        }
        if (file.Position != file.Length - 8) throw new InvalidDataException("DAT index size mismatch.");
    }

    public byte[] Read(string path)
    {
        DatEntry entry = _entries[NormalizePath(path)];
        if (entry.Size > MaxResourceBytes || entry.StoredSize > MaxResourceBytes)
            throw new InvalidDataException("Resource exceeds this inspector's 64 MiB limit.");
        using var file = File.OpenRead(FilePath);
        file.Position = entry.Offset;
        var stored = new byte[entry.StoredSize];
        file.ReadExactly(stored);
        if (!entry.Compressed) return stored;
        using var input = new MemoryStream(stored, false);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        var result = new byte[entry.Size];
        zlib.ReadExactly(result);
        if (zlib.ReadByte() != -1) throw new InvalidDataException("DAT decompressed size mismatch.");
        return result;
    }

    public static string NormalizePath(string path)
    {
        string normalized = path.Replace('\\', '/');
        foreach (string part in normalized.Split('/'))
            if (part.Length == 0 || part == "." || part == ".." || part.Contains(':') || part.Contains('\0'))
                throw new InvalidDataException("Expected a relative game resource path.");
        return normalized.ToLowerInvariant();
    }

    private static void Require(Stream file, long count, long end)
    {
        if (count > end - file.Position) throw new InvalidDataException("Truncated DAT index.");
    }
}
