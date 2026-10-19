using FalloutRemake.Geometry;
using FalloutRemake.Import;

internal static class HexDistanceTests
{
    public static void Run(Action<string, Action> check)
    {
        check("hex distance matches shortest unobstructed walks at both parities and board edges", () =>
        {
            var nav = new HexNavigation(Array.Empty<MapObject>(), 0);
            foreach (int start in new[] { 0, 199, 200, 39999, 14322, 14323 })
            {
                var reach = nav.ReachableFrom(start);
                foreach (int target in Enumerable.Range(0, 40000).Where(t => t % 127 == 0).Concat(new[] { 0, 199, 39800, 39999, start }))
                    Assert(FalloutGrid.Distance(start, target) == reach.Distance(target) && FalloutGrid.Distance(start, target) == FalloutGrid.Distance(target, start));
                foreach (int direction in Enumerable.Range(0, 6))
                    if (FalloutGrid.Neighbor(start, direction) is int neighbor) Assert(FalloutGrid.Distance(start, neighbor) == 1);
            }
        });
        check("hex distance agrees with CE angular stepping for sampled valid pairs and complete barrier blast neighborhood", () =>
        {
            var random = new Random(14322);
            for (int i = 0; i < 1000; i++)
            { int a = random.Next(50, 150) * 200 + random.Next(50, 150), b = random.Next(50, 150) * 200 + random.Next(50, 150);
                Assert(FalloutGrid.Distance(a, b) == Reference(a, b)); }
            for (int row = 60; row < 83; row++) for (int column = 111; column < 134; column++)
            { int tile = row * 200 + column; Assert(FalloutGrid.Distance(tile, 14322) == Reference(tile, 14322)); }
        });
        check("hex distance rejects inventory tiles and other invalid source locations", () =>
        {
            foreach (int value in new[] { -1, int.MinValue, 40000, int.MaxValue })
                foreach (bool first in new[] { true, false })
                {
                    bool rejected = false;
                    try { _ = first ? FalloutGrid.Distance(value, 14322) : FalloutGrid.Distance(14322, value); }
                    catch (ArgumentOutOfRangeException) { rejected = true; }
                    Assert(rejected);
                }
        });
    }
    // Independent reference follows tile.cc tileDistanceBetween, at an even screen origin.
    private static int Reference(int from, int to)
    {
        int original = from;
        static (int X, int Y) Screen(int tile)
        { int col = 199 - tile % 200, row = tile / 200; return (24 * col + 8 * (col & 1) + 16 * row, -6 * col + 6 * (col & 1) + 12 * row); }
        int[][] offsets = { new[] { -1, 199, 200, 201, 1, -200 }, new[] { -201, -1, 200, 1, -199, -200 } };
        var target = Screen(to); int count = 0;
        while (from != to)
        {
            if (from is < 0 or >= 40000 || count++ >= 1000) throw new Exception($"CE distance reference left its valid bounded profile: {original}->{to}, at {from}, count {count}.");
            var current = Screen(from); int dx = target.X - current.X, dy = target.Y - current.Y, rotation;
            if (dx == 0) rotation = dy < 0 ? 0 : 2;
            else
            {
                int angle = (int)Math.Truncate(Math.Atan2(-dy, dx) * 180.0 * 0.3183098862851122);
                rotation = 360 - (angle + 180) - 90; if (rotation < 0) rotation += 360;
                rotation = Math.Min(rotation / 60, 5);
            }
            from += offsets[from % 200 & 1][rotation];
        }
        return count;
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Hex distance assertion failed."); }
}
