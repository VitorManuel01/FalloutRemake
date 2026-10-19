using FalloutRemake.Geometry;

internal static class FramingTests
{
    public static void Run(Action<string, Action> check)
    {
        check("orthographic framing fits all route points inside an asymmetric unobscured viewport", () =>
        {
            FramePoint[] points = { new(-20, -12), new(25, 15), new(7, -9), new(-3, 16) };
            var safe = new FrameRect(32, 94, 1092, 518);
            OrthoFrame frame = OrthoFraming.Fit(points, 1440, 900, safe, 1.6, 1);
            foreach (FramePoint p in points)
            {
                double x = 720 + (p.X - frame.TargetX) * 1440 / (1.6 * frame.Size);
                double y = 450 - (p.Y - frame.TargetY) * 900 / frame.Size;
                Assert(x > safe.X && x < safe.X + safe.Width && y > safe.Y && y < safe.Y + safe.Height);
            }
        });
        check("framing handles degenerate paths, different aspect conventions and translated coordinates", () =>
        {
            var safe = new FrameRect(20, 40, 600, 380);
            foreach (var (sx, sy) in new[] { (1.6, 1d), (1d, .625), (1d, 1d) })
            {
                OrthoFrame f = OrthoFraming.Fit(new[] { new FramePoint(100, -80) }, 800, 600, safe, sx, sy);
                Assert(f.Size == 8);
                double x = 400 + (100 - f.TargetX) * 800 / (sx * f.Size);
                double y = 300 - (-80 - f.TargetY) * 600 / (sy * f.Size);
                Assert(Math.Abs(x - 320) < 1e-8 && Math.Abs(y - 230) < 1e-8);
            }
        });
        check("routes larger than the allowed camera range fail instead of claiming a clipped fit", () =>
        {
            Reject(() => OrthoFraming.Fit(new[] { new FramePoint(-1000, 0), new(1000, 0) }, 1000, 1000,
                new(0, 0, 1000, 1000), 1, 1));
        });
        check("framing rejects nonfinite points and invalid or outside safe rectangles", () =>
        {
            var points = new[] { new FramePoint(0, 0) };
            foreach (FrameRect safe in new[] { new FrameRect(-1, 0, 100, 100), new(0, 0, 0, 100), new(0, 0, 1001, 100), new(0, double.NaN, 100, 100) })
                Reject(() => OrthoFraming.Fit(points, 1000, 1000, safe, 1, 1));
            Reject(() => OrthoFraming.Fit(Array.Empty<FramePoint>(), 1000, 1000, new(0, 0, 100, 100), 1, 1));
            Reject(() => OrthoFraming.Fit(new[] { new FramePoint(double.NaN, 0) }, 1000, 1000, new(0, 0, 100, 100), 1, 1));
            Reject(() => OrthoFraming.Fit(points, 1000, 1000, new(0, 0, 100, 100), 0, 1));
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Camera framing assertion failed."); }
    private static void Reject(Action action) { try { action(); } catch (Exception e) when (e is ArgumentException or InvalidOperationException) { return; } throw new Exception("Invalid framing accepted."); }
}
