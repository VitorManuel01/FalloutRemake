using System;

namespace FalloutRemake.Geometry;

/// <summary>
/// CE screen-space hex geometry: tileToScreenXY, tileFromScreenXY (with its 32x16 hit mask), tileGetRotationTo and the walk of
/// _make_straight_path_func that obj_can_see_obj uses. The view is centred on column 0, row 0; CE always centres on an even
/// column (tileSetCenter), so tile offsets, and every line built from them, do not depend on where the view stands.
/// </summary>
public static class OriginalStraightPath
{
    private static readonly byte[] Mask = BuildMask();

    // tile.cc 0x4B0DEC (tile_init): rows 0-3 cut the upper corners, rows 12-15 the lower ones.
    private static byte[] BuildMask()
    {
        var mask = new byte[512]; int index = 0;
        for (int row = 0; row != 64; row += 16)
        {
            for (int x = 64; x != 0; x -= 4) mask[index++] = (byte)(x > row ? 1 : 0);
            for (int x = 0; x != 64; x += 4) mask[index++] = (byte)(x > row ? 2 : 0);
        }
        for (int row = 0; row < 8; row++) for (int x = 0; x < 32; x++) mask[index++] = 0;
        for (int row = 0; row != 64; row += 16)
        {
            for (int x = 0; x != 64; x += 4) mask[index++] = (byte)(x > row ? 0 : 3);
            for (int x = 64; x != 0; x -= 4) mask[index++] = (byte)(x > row ? 0 : 4);
        }
        return mask;
    }

    /// <summary>Top-left screen corner of a hex's 32x16 cell (tileToScreenXY).</summary>
    public static (int X, int Y) Screen(int tile)
    {
        if (!HexNavigation.Valid(tile)) throw new ArgumentOutOfRangeException(nameof(tile));
        int column = 199 - tile % 200, row = tile / 200;
        int x = 48 * (column / 2), y = 12 * (column / -2);
        if ((column & 1) != 0) x += 32;
        return (x + 16 * row, y + 12 * row);
    }

    /// <summary>The hex under a screen point (tileFromScreenXY); -1 outside the 200x200 grid.</summary>
    public static int TileAt(int x, int y)
    {
        int row = y >= 0 ? y / 12 : (y + 1) / 12 - 1;
        int across = x - 16 * row, inside = y - 12 * row;
        int pair = across >= 0 ? across / 64 : (across + 1) / 64 - 1;
        int tileRow = pair + row, offset = across - pair * 64, column = 2 * pair;
        if (offset >= 32) { offset -= 32; column++; }
        switch (Mask[32 * inside + offset])
        {
            case 2: column++; if ((column & 1) != 0) tileRow--; break;
            case 1: tileRow--; break;
            case 3: column--; if ((column & 1) == 0) tileRow++; break;
            case 4: tileRow++; break;
        }
        int tileColumn = 199 - column;
        return tileColumn is >= 0 and < 200 && tileRow is >= 0 and < 200 ? 200 * tileRow + tileColumn : -1;
    }

    /// <summary>Facing from one hex toward another (tileGetRotationTo): 0 NE, 1 E, 2 SE, 3 SW, 4 W, 5 NW.</summary>
    public static int RotationTo(int from, int to)
    {
        (int x1, int y1) = Screen(from); (int x2, int y2) = Screen(to);
        int dx = x2 - x1, dy = y2 - y1;
        if (dx == 0) return dy < 0 ? 0 : 2;
        int degrees = (int)Math.Truncate(Math.Atan2(-dy, dx) * 180.0 * 0.3183098862851122);
        int rotation = 360 - (degrees + 180) - 90;
        if (rotation < 0) rotation += 360;
        rotation /= 60;
        return rotation >= 6 ? 5 : rotation;
    }

    /// <summary>
    /// The first hex along the straight path from <paramref name="from"/> toward <paramref name="to"/> for which
    /// <paramref name="blocks"/> holds, as _make_straight_path_func with a node step of 16: the start hex is tested first, then
    /// each hex the line enters; the walk stops on reaching the target's screen row or column without testing that last
    /// point. Null when nothing blocks, or when the line outgrows CE's 200 path nodes.
    /// </summary>
    public static int? FirstBlocked(int from, int to, Func<int, bool> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        if (blocks(from)) return from;
        (int fromX, int fromY) = Screen(from); (int toX, int toY) = Screen(to);
        fromX += 16; fromY += 8; toX += 16; toY += 8;
        int stepX = Math.Sign(toX - fromX), stepY = Math.Sign(toY - fromY);
        int ddx = 2 * Math.Abs(toX - fromX), ddy = 2 * Math.Abs(toY - fromY);
        int x = fromX, y = fromY, previous = from, nodes = 0, sinceNode = 0;
        bool yMajor = ddx <= ddy;
        int middle = yMajor ? ddx - ddy / 2 : ddy - ddx / 2;
        while (true)
        {
            int tile = TileAt(x, y);
            if (++sinceNode == 16) { if (nodes >= 200) return null; sinceNode = 0; nodes++; }
            if (yMajor ? y == toY : x == toX) return null;
            if (yMajor)
            {
                if (middle >= 0) { x += stepX; middle -= ddy; }
                y += stepY; middle += ddx;
            }
            else
            {
                if (middle >= 0) { y += stepY; middle -= ddx; }
                x += stepX; middle += ddy;
            }
            if (tile != previous)
            {
                if (tile >= 0 && blocks(tile)) return tile;
                previous = tile;
            }
        }
    }
}
