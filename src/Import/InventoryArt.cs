using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace FalloutRemake.Import;

/// <summary>Single-frame inventory FRM profile. Frame displacements are retained; UI artRender uses the frame pixels centered.</summary>
public sealed record InventoryArt(int Fid, ResourceIdentity Source, int Width, int Height, int OffsetX, int OffsetY,
    int DirectionOffsetX, int DirectionOffsetY, ReadOnlyMemory<byte> Rgba)
{
    public static InventoryArt Decode(int fid, GameResource source, GameResource palette)
    {
        if ((fid & ~0xFFF) != 0x07000000 || source.Bytes.Length < 74 || source.Bytes.Length > 74 + 512 * 512)
            throw new InvalidDataException("Unsupported inventory FID or FRM size.");
        int Word(int offset) => BinaryPrimitives.ReadInt32BigEndian(source.Bytes.AsSpan(offset, 4));
        short Half(int offset) => BinaryPrimitives.ReadInt16BigEndian(source.Bytes.AsSpan(offset, 2));
        int width = Half(62), height = Half(64);
        if (width is < 1 or > 512 || height is < 1 or > 512 || Word(0) != 4 || Half(6) != 0 || Half(8) != 1
            || Word(66) != width * height || Word(58) != 12 + width * height || source.Bytes.Length != 74 + width * height)
            throw new InvalidDataException("Inventory FRM requires version 4 and one bounded, complete frame.");
        for (int direction = 0; direction < 6; direction++)
            if (Word(34 + direction * 4) != 0) throw new InvalidDataException("Directional inventory FRM data is outside the static profile.");
        byte[] rgba = IndexedPalette.Decode(source.Bytes.AsSpan(74), palette.Bytes);
        return new(fid, ResourceIdentity.From(source), width, height, Half(70), Half(72), Half(10), Half(22), rgba);
    }
}

internal static class IndexedPalette
{
    public static byte[] Decode(ReadOnlySpan<byte> indices, ReadOnlySpan<byte> colors)
    {
        if (colors.Length < 768 + 32768) throw new InvalidDataException("Truncated color.pal.");
        var rgba = new byte[checked(indices.Length * 4)];
        for (int i = 0; i < indices.Length; i++)
        {
            int index = indices[i], p = index * 3, target = i * 4;
            // CE colorPaletteLoad leaves unmapped colors black; svga.cc expands six bits by << 2.
            bool mapped = colors[p] <= 63 && colors[p + 1] <= 63 && colors[p + 2] <= 63;
            if (mapped) for (int channel = 0; channel < 3; channel++) rgba[target + channel] = (byte)(colors[p + channel] << 2);
            rgba[target + 3] = index == 0 ? (byte)0 : (byte)255;
        }
        return rgba;
    }
}

public sealed class InventoryArtCatalog
{
    private readonly ResourceCatalog _resources;
    private readonly Dictionary<int, InventoryArt> _icons = new();
    public InventoryArtCatalog(Func<string, GameResource> read) => _resources = new ResourceCatalog(read);
    public ResourceIdentity[] Dependencies => _resources.Identities;
    public InventoryArt? Get(int fid)
    {
        if (fid == -1) return null;
        if ((fid & ~0xFFF) != 0x07000000) throw new InvalidDataException("Expected canonical inventory-art FID.");
        if (!_icons.TryGetValue(fid, out InventoryArt? icon))
        {
            if (_icons.Count >= 512) throw new InvalidDataException("Inventory artwork cache limit reached.");
            string name = _resources.ListEntry("art/inven/inven.lst", fid & 0xFFF, ".frm");
            icon = InventoryArt.Decode(fid, _resources.Read("art/inven/" + name), _resources.Read("color.pal"));
            _icons.Add(fid, icon);
        }
        return icon;
    }
}
