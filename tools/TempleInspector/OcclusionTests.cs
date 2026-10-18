using FalloutRemake.Geometry;

internal static class OcclusionTests
{
    public static void Run(Action<string, Action> check)
    {
        check("sight occlusion detects intervening boxes and ignores geometry outside the finite segment", () =>
        {
            var a = new SightPoint(0, 0, 0); var b = new SightPoint(0, 0, 10); var half = new SightPoint(.5, .5, .5);
            Assert(SightOcclusion.Intersects(a, b, new(0, 0, 5), half));
            Assert(SightOcclusion.Intersects(b, a, new(0, 0, 5), half));
            foreach (SightPoint center in new[] { new SightPoint(0, 0, -2), new(0, 0, 12), new(2, 0, 5), new(0, 2, 5) })
                Assert(!SightOcclusion.Intersects(a, b, center, half));
        });
        check("parallel rays, box boundaries and a target inside an occluder have stable results", () =>
        {
            var half = new SightPoint(1, 1, 1); var center = new SightPoint(0, 0, 0);
            Assert(SightOcclusion.Intersects(new(-5, 1, 0), new(5, 1, 0), center, half));
            Assert(!SightOcclusion.Intersects(new(-5, 1.0001, 0), new(5, 1.0001, 0), center, half));
            Assert(SightOcclusion.Intersects(new(0, 0, 0), new(0, 0, 0), center, half));
            Assert(!SightOcclusion.Intersects(new(2, 0, 0), new(2, 0, 0), center, half));
        });
        check("diagonal segments intersect only when all three slab intervals overlap", () =>
        {
            var a = new SightPoint(0, 0, 0); var b = new SightPoint(10, 10, 10); var half = new SightPoint(.25, .25, .25);
            Assert(SightOcclusion.Intersects(a, b, new(5, 5, 5), half));
            Assert(!SightOcclusion.Intersects(a, b, new(2, 8, 5), half));
        });
        check("nonfinite sight coordinates and negative box extents are rejected", () =>
        {
            var zero = new SightPoint(0, 0, 0); var half = new SightPoint(1, 1, 1);
            Reject(() => SightOcclusion.Intersects(new(double.NaN, 0, 0), zero, zero, half));
            Reject(() => SightOcclusion.Intersects(zero, new(0, double.PositiveInfinity, 0), zero, half));
            Reject(() => SightOcclusion.Intersects(zero, zero, zero, new(-1, 1, 1)));
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Sight occlusion assertion failed."); }
    private static void Reject(Action action) { try { action(); } catch (ArgumentOutOfRangeException) { return; } throw new Exception("Invalid sight input accepted."); }
}
