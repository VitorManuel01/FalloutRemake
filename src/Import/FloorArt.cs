using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace FalloutRemake.Import;

/// <summary>Static floor profile only, not a general FRM animation reader.</summary>
public sealed record FloorArt(int Id, ResourceIdentity Source, ReadOnlyMemory<byte> Rgba)
{
    public const int Width = 80, Height = 36, HeaderBytes = 62, FrameBytes = 12;
    public static FloorArt Decode(int id, GameResource source, GameResource palette)
    {
        ReadOnlySpan<byte> bytes = source.Bytes, colors = palette.Bytes;
        const int pixels = Width * Height;
        if (id is < 0 or > 4095 || bytes.Length != HeaderBytes + FrameBytes + pixels
            || colors.Length < 768 + 32768)
            throw new InvalidDataException("Unsupported static floor FRM or truncated color.pal.");
        int Word(int offset) => BinaryPrimitives.ReadInt32BigEndian(source.Bytes.AsSpan(offset, 4));
        short Half(int offset) => BinaryPrimitives.ReadInt16BigEndian(source.Bytes.AsSpan(offset, 2));
        if (Word(0) != 4 || Half(6) != 0 || Half(8) != 1 || Word(58) != FrameBytes + pixels
            || Half(62) != Width || Half(64) != Height || Word(66) != pixels || Half(70) != 0 || Half(72) != 0)
            throw new InvalidDataException("Floor FRM requires version 4, one 80x36 frame and zero displacement.");
        for (int direction = 0; direction < 6; direction++)
            if (Half(10 + direction * 2) != 0 || Half(22 + direction * 2) != 0 || Word(34 + direction * 4) != 0)
                throw new InvalidDataException("Directional floor FRM offsets are outside the static profile.");
        var rgba = new byte[pixels * 4];
        for (int i = 0; i < pixels; i++)
        {
            int index = bytes[HeaderBytes + FrameBytes + i], p = index * 3, target = i * 4;
            // CE colorPaletteLoad: unmapped entries become black. svga.cc expands six bits by << 2.
            bool mapped = colors[p] <= 63 && colors[p + 1] <= 63 && colors[p + 2] <= 63;
            if (mapped)
                for (int channel = 0; channel < 3; channel++) rgba[target + channel] = (byte)(colors[p + channel] << 2);
            rgba[target + 3] = index == 0 ? (byte)0 : (byte)255;
        }
        return new FloorArt(id, ResourceIdentity.From(source), rgba);
    }
}

public sealed class FloorArtCatalog
{
    private readonly ResourceCatalog _resources;
    private readonly Dictionary<int, FloorArt> _tiles = new();
    public FloorArtCatalog(Func<string, GameResource> read) => _resources = new ResourceCatalog(read);
    public ResourceIdentity[] Dependencies => _resources.Identities;
    public FloorArt Get(int id)
    {
        if (id is < 0 or > 4095) throw new InvalidDataException("Invalid floor tile ID.");
        if (!_tiles.TryGetValue(id, out FloorArt? tile))
        {
            string name = _resources.ListEntry("art/tiles/tiles.lst", id, ".frm");
            tile = FloorArt.Decode(id, _resources.Read("art/tiles/" + name), _resources.Read("color.pal"));
            _tiles.Add(id, tile);
        }
        return tile;
    }
}
