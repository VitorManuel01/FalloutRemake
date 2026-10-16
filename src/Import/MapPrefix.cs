using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FalloutRemake.Import;

public sealed record MapHeader(int Version, byte[] RawName, int EnteringTile, int EnteringElevation,
    int EnteringRotation, int LocalVariablesCount, int ScriptIndex, int Flags, int Darkness,
    int GlobalVariablesCount, int MapIndex, uint LastVisitTime, int[] Reserved)
{
    public string Name => Encoding.ASCII.GetString(RawName).TrimEnd('\0');
}

public readonly record struct SquareTile(uint Raw)
{
    public int FloorId => (int)(Raw & 0xFFF);
    public int FloorFlags => (int)((Raw >> 12) & 0xF);
    public int RoofId => (int)((Raw >> 16) & 0xFFF);
    public int RoofFlags => (int)(Raw >> 28);
}

public sealed record MapLayer(int Elevation, SquareTile[] Tiles);

/// <summary>Lossless prefix interpretation, NOT a complete map or object importer.</summary>
public sealed record MapPrefix(MapHeader Header, int[] GlobalVariables, int[] LocalVariables,
    IReadOnlyList<MapLayer> Layers, int UnparsedOffset, ReadOnlyMemory<byte> OriginalBytes)
{
    public ReadOnlyMemory<byte> UnparsedScriptsAndObjects => OriginalBytes[UnparsedOffset..];
    public const int HeaderBytes = 236;

    public static MapPrefix Read(byte[] bytes)
    {
        // Own a copy so the caller cannot change the source bytes after parsing.
        byte[] original = (byte[])bytes.Clone();
        using var file = new MemoryStream(original, false);
        int I32()
        {
            Span<byte> word = stackalloc byte[4];
            file.ReadExactly(word);
            return BinaryPrimitives.ReadInt32BigEndian(word);
        }
        int[] Words(int count)
        {
            if (count < 0 || count > (file.Length - file.Position) / 4)
                throw new InvalidDataException("MAP count exceeds remaining bytes.");
            var words = new int[count];
            for (int i = 0; i < count; i++) words[i] = I32();
            return words;
        }
        int version = I32();
        if (version != 20) throw new InvalidDataException($"Only Fallout 2 MAP version 20 is supported; got {version}.");
        var name = new byte[16];
        file.ReadExactly(name);
        var h = new MapHeader(version, name, I32(), I32(), I32(), I32(), I32(), I32(), I32(), I32(), I32(), unchecked((uint)I32()), Words(44));
        if (h.EnteringTile < 0 || h.EnteringTile >= 40000 || h.EnteringElevation < 0 || h.EnteringElevation > 2 || h.EnteringRotation < 0 || h.EnteringRotation > 5)
            throw new InvalidDataException("Invalid MAP entrance coordinates.");
        if (h.LocalVariablesCount < 0 || h.GlobalVariablesCount < 0)
            throw new InvalidDataException("Negative MAP variable count.");
        int[] globals = Words(h.GlobalVariablesCount);
        int[] locals = Words(h.LocalVariablesCount);
        var layers = new List<MapLayer>();
        for (int elevation = 0; elevation < 3; elevation++)
        {
            if ((h.Flags & (2 << elevation)) != 0) continue;
            int[] words = Words(10000);
            var tiles = new SquareTile[words.Length];
            for (int i = 0; i < words.Length; i++) tiles[i] = new SquareTile(unchecked((uint)words[i]));
            layers.Add(new MapLayer(elevation, tiles));
        }
        // Tail is retained verbatim. Its presence does NOT validate scripts or objects.
        return new MapPrefix(h, globals, locals, layers, (int)file.Position, original);
    }
}
