using FalloutRemake.Geometry;
using FalloutRemake.Import;

internal static class GeometryTests
{
    public static void Run(Action<string, Action> check)
    {
        check("hex anchors agree with CE screen formulas at both parities and map boundaries", () =>
        {
            foreach (int tile in new[] { 0, 1, 198, 199, 200, 201, 28906, 11108, 24308, 39998, 39999 })
            {
                int x = 199 - tile % 200, y = tile / 200;
                int screenX = 48 * (x / 2) + ((x & 1) != 0 ? 32 : 0) + 16 * y + 16;
                int screenY = -12 * (x / 2) + 12 * y + 8;
                var projected = FalloutGrid.OriginalScreen(FalloutGrid.HexCenter(tile));
                Near(projected.X, screenX);
                Near(projected.Y, screenY);
            }
        });
        check("six hex neighbors have unit distance and preserve original rotations", () =>
        {
            foreach (int tile in new[] { 20100, 20101, 28906 })
                for (int rotation = 0; rotation < 6; rotation++)
                {
                    int next = FalloutGrid.Neighbor(tile, rotation)!.Value;
                    GroundPoint delta = FalloutGrid.HexCenter(next) - FalloutGrid.HexCenter(tile);
                    GroundPoint facing = FalloutGrid.Facing(rotation);
                    Near(delta.X * delta.X + delta.Z * delta.Z, 1);
                    Near(delta.X, facing.X);
                    Near(delta.Z, facing.Z);
                    if (FalloutGrid.Neighbor(next, (rotation + 3) % 6) != tile) throw new Exception("Neighbor reciprocity failed.");
                }
            if (FalloutGrid.Neighbor(0, 0) != null || FalloutGrid.Neighbor(199, 3) != null) throw new Exception("Grid edge wrapped.");
        });
        check("square centers and original floor hit-test share one coordinate frame", () =>
        {
            foreach (int square in new[] { 0, 99, 100, 7500, 9999 })
            {
                int x = 99 - square % 100, y = square / 100;
                GroundPoint point = FalloutGrid.SquareCenter(square);
                var screen = FalloutGrid.OriginalScreen(point);
                Near(screen.X, -16 + 48 * x + 32 * y + 40);
                Near(screen.Y, -2 - 12 * x + 24 * y + 18);
                if (FalloutGrid.SquareAt(point) != square) throw new Exception("Square inverse failed.");
            }
            foreach (int tile in new[] { 28906, 11108, 24308, 20100, 20101 })
            {
                var point = FalloutGrid.HexCenter(tile);
                var (sx, sy) = FalloutGrid.OriginalScreen(point);
                double vx = sx + 16, vy = sy - 10;
                int col = (int)Math.Floor((3 * vx - 4 * vy) / 192);
                int row = (int)Math.Floor((vx + 4 * vy) / 128);
                if (FalloutGrid.SquareAt(point) != row * 100 + 99 - col) throw new Exception("Floor/hex alignment failed.");
            }
        });
        check("invalid coordinate indices fail and floor lookup never wraps", () =>
        {
            Reject(() => FalloutGrid.HexCenter(-1));
            Reject(() => FalloutGrid.HexCenter(40000));
            Reject(() => FalloutGrid.SquareCenter(10000));
            Reject(() => FalloutGrid.Facing(6));
            if (FalloutGrid.SquareAt(new GroundPoint(-10, -10)) != null) throw new Exception("Floor bounds failed.");
        });
    }
    private static void Near(double a, double b) { if (Math.Abs(a - b) > 1e-8) throw new Exception($"Geometry mismatch {a} != {b}."); }
    private static void Reject(Action action)
    {
        try { action(); } catch (ArgumentOutOfRangeException) { return; }
        throw new Exception("Invalid geometry input accepted.");
    }
}
