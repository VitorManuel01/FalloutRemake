using System;

namespace FalloutRemake.Geometry;

public readonly record struct GroundPoint(double X, double Z)
{
    public static GroundPoint operator -(GroundPoint a, GroundPoint b) => new(a.X - b.X, a.Z - b.Z);
}

/// <summary>Logical floor-cell/hex anchors, independent of sprite size and Godot.</summary>
public static class FalloutGrid
{
    public static readonly double ColumnStep = Math.Sqrt(3) / 2;
    public static readonly double FloorWidth = Math.Sqrt(3);
    public const double FloorDepth = 2;

    public static GroundPoint HexCenter(int tile)
    {
        if (tile < 0 || tile >= 40000) throw new ArgumentOutOfRangeException(nameof(tile));
        int column = 199 - tile % 200;
        int row = tile / 200;
        // Shared with the floor-cell center via CE's original screen-space anchors.
        return new GroundPoint((column + 1.0 / 12) * ColumnStep, row + (column & 1) * 0.5 - 0.625);
    }

    public static GroundPoint SquareCenter(int square)
    {
        if (square < 0 || square >= 10000) throw new ArgumentOutOfRangeException(nameof(square));
        return new GroundPoint((99 - square % 100) * FloorWidth, square / 100 * FloorDepth);
    }

    // Nearest regular-hex center, using the same affine frame as HexCenter.
    public static int? HexAt(GroundPoint point)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Z)) return null;
        double columnValue = point.X / ColumnStep - 1.0 / 12;
        if (columnValue < -1 || columnValue > 200 || point.Z < -2 || point.Z > 201) return null;
        int approximate = (int)Math.Round(columnValue);
        int? result = null; double best = double.MaxValue;
        // Include virtual neighbors outside the board so picking never clamps to an edge.
        for (int column = approximate - 1; column <= approximate + 1; column++)
        {
            int row = (int)Math.Round(point.Z - (column & 1) * .5 + .625);
            double x = (column + 1.0 / 12) * ColumnStep, z = row + (column & 1) * .5 - .625;
            double distance = (x - point.X) * (x - point.X) + (z - point.Z) * (z - point.Z);
            if (distance >= best) continue;
            best = distance;
            result = column is >= 0 and < 200 && row is >= 0 and < 200 ? row * 200 + 199 - column : null;
        }
        return result;
    }

    public static int? SquareAt(GroundPoint point)
    {
        int column = (int)Math.Floor(point.X / FloorWidth + 0.5);
        int row = (int)Math.Floor(point.Z / FloorDepth + 0.5);
        return column is >= 0 and < 100 && row is >= 0 and < 100 ? row * 100 + 99 - column : null;
    }

    public static GroundPoint Facing(int rotation)
    {
        if (rotation < 0 || rotation > 5) throw new ArgumentOutOfRangeException(nameof(rotation));
        double angle = rotation * Math.PI / 3 - Math.PI / 6;
        return new GroundPoint(Math.Cos(angle), Math.Sin(angle));
    }

    public static int? Neighbor(int tile, int rotation)
    {
        _ = HexCenter(tile);
        _ = Facing(rotation);
        int x = tile % 200, y = tile / 200;
        int[] dx = { -1, -1, 0, 1, 1, 0 };
        int[] dy = (x & 1) == 0 ? new[] { 0, 1, 1, 1, 0, -1 } : new[] { -1, 0, 1, 0, -1, -1 };
        x += dx[rotation];
        y += dy[rotation];
        return x is >= 0 and < 200 && y is >= 0 and < 200 ? y * 200 + x : null;
    }

    // Useful only for reference checks: global CE screen coordinates at tile origin 199.
    public static (double X, double Y) OriginalScreen(GroundPoint point)
    {
        double column = point.X / FloorWidth;
        double row = point.Z / FloorDepth;
        return (24 + 48 * column + 32 * row, 16 - 12 * column + 24 * row);
    }
}
