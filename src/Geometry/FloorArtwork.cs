using System;

namespace FalloutRemake.Geometry;

public static class FloorArtwork
{
    // Inverse of CE's square screen axes (48,-12), (32,24), centered at (40,18).
    // Map the full image rectangle: cropping to the cell would trim rasterized edge pixels.
    public static GroundPoint Corner(double u, double v)
    {
        if (!double.IsFinite(u) || !double.IsFinite(v) || u < 0 || u > 1 || v < 0 || v > 1)
            throw new ArgumentOutOfRangeException(nameof(u));
        double sx = u * 80 - 40, sy = v * 36 - 18;
        return new GroundPoint((24 * sx - 32 * sy) / 1536 * FalloutGrid.FloorWidth,
            (12 * sx + 48 * sy) / 1536 * FalloutGrid.FloorDepth);
    }
}
