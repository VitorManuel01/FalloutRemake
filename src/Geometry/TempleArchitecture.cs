using System;
using System.Collections.Generic;
using System.Linq;

namespace FalloutRemake.Geometry;

public enum WallAxis { X, Z }
public enum ArchitectureModule { Wall1, Wall2, Wall4, Pillar }

/// <summary>Straight line of source wall hexes. Start/End are the outer ends of the visual wall along the axis.</summary>
public sealed record WallRun(WallAxis Axis, GroundPoint Start, GroundPoint End, int[] Tiles, bool Thickened)
{
    public double Length => Axis == WallAxis.X ? End.X - Start.X : End.Z - Start.Z;
}

/// <summary>
/// One kit module. Walls start at Origin and extend Length along Direction; pillars are centered on Origin.
/// Tiles are the source hexes the module stands for, so presentation can follow their filters and cutaway.
/// </summary>
public sealed record ArchitecturePiece(ArchitectureModule Module, GroundPoint Origin, GroundPoint Direction,
    double Length, double ThicknessScale, bool Vines, int[] Tiles)
{
    public bool Contains(GroundPoint point, double margin)
    {
        double dx = point.X - Origin.X, dz = point.Z - Origin.Z;
        if (Module == ArchitectureModule.Pillar)
            return Math.Abs(dx) <= TempleArchitecture.PillarHalfSize + margin && Math.Abs(dz) <= TempleArchitecture.PillarHalfSize + margin;
        double along = dx * Direction.X + dz * Direction.Z, across = -dx * Direction.Z + dz * Direction.X;
        return along >= -margin && along <= Length + margin && Math.Abs(across) <= TempleArchitecture.WallHalfThickness * ThicknessScale + margin;
    }
}

public sealed record WallPlan(WallRun[] Runs, ArchitecturePiece[] Pieces, int[] Filled)
{
    public bool Covers(int tile) => Pieces.Any(p => p.Tiles.Contains(tile));
    public bool Draws(GroundPoint point, double margin = 0) => Pieces.Any(p => p.Contains(point, margin));
}

/// <summary>
/// Lays the Temple kit (metres) over the source hex walls (one unit between hex centers), independent of Godot.
/// Original walls are hex lines along world X (alternate columns zigzag half a row) or world Z (one column).
/// The source hexes keep deciding traversal; this plan only decides where stone is drawn.
/// </summary>
public static class TempleArchitecture
{
    // Kit metres per hex unit: the 2.97 m kit wall stands 2.2 units, twice the provisional 1.1-unit actor,
    // close to the original wall/critter sprite ratio. Stone proportions are kept by scaling uniformly.
    public const double KitScale = .75;
    public const double WallHalfThickness = .95 * KitScale / 2;
    public const double PillarHalfSize = 1.04 * KitScale / 2;
    public const double WallHeight = 2.97 * KitScale;
    // Parallel wall lines one column apart overlap instead of leaving a slit between them.
    public const double ParallelThickness = 1.25;
    static readonly double HalfColumn = FalloutGrid.ColumnStep / 2;

    static int Column(int tile) => 199 - tile % 200;
    static int Row(int tile) => tile / 200;
    static int TileAt(int column, int row) => column is >= 0 and < 200 && row is >= 0 and < 200 ? row * 200 + 199 - column : -1;
    // Twice the hex Z, shifted to an integer: even columns are even, odd columns odd.
    static int HalfRow(int tile) => 2 * Row(tile) + (Column(tile) & 1);

    /// <summary>
    /// Original wall sprites are wider than their hex, so the map leaves single hexes between wall objects and
    /// fills them with invisible blockers (block.frm). Those fillers are drawn as stone when they sit between two
    /// walls; walkable hexes are never fillers, so doorways stay open.
    /// </summary>
    public static WallPlan PlanWalls(IEnumerable<int> wallTiles, GroundPoint origin, IEnumerable<int>? fillers = null)
    {
        var tiles = wallTiles.Where(HexNavigation.Valid).ToHashSet();
        var candidates = (fillers ?? Array.Empty<int>()).Where(t => HexNavigation.Valid(t) && !tiles.Contains(t)).ToHashSet();
        var filled = new List<int>();
        // Two passes close gaps two blockers wide; a blocker beside a wall but not between two stays empty.
        for (int pass = 0; pass < 2; pass++)
        {
            int[] bridged = candidates.Where(t => Bridges(t, tiles)).OrderBy(t => t).ToArray();
            tiles.UnionWith(bridged); candidates.ExceptWith(bridged); filled.AddRange(bridged);
        }
        var zRuns = new List<int[]>();
        foreach (var column in tiles.GroupBy(Column))
        {
            int[] rows = column.Select(Row).OrderBy(r => r).ToArray();
            for (int i = 0; i < rows.Length;)
            {
                int j = i;
                while (j + 1 < rows.Length && rows[j + 1] == rows[j] + 1) j++;
                if (j > i) zRuns.Add(Enumerable.Range(rows[i], j - i + 1).Select(r => TileAt(column.Key, r)).ToArray());
                i = j + 1;
            }
        }

        // Candidate X lines: (half-row on even columns, half-row on odd columns), one half-row apart.
        var lines = new HashSet<(int Even, int Odd)>();
        foreach (int tile in tiles)
        {
            int h = HalfRow(tile);
            if ((Column(tile) & 1) == 0) { lines.Add((h, h + 1)); lines.Add((h, h - 1)); }
            else { lines.Add((h + 1, h)); lines.Add((h - 1, h)); }
        }
        var xCandidates = new List<int[]>();
        int minColumn = tiles.Count == 0 ? 0 : tiles.Min(Column), maxColumn = tiles.Count == 0 ? -1 : tiles.Max(Column);
        foreach (var (even, odd) in lines.OrderBy(l => l.Even).ThenBy(l => l.Odd))
        {
            var run = new List<int>();
            for (int column = minColumn; column <= maxColumn + 1; column++)
            {
                int h = (column & 1) == 0 ? even : odd, tile = column <= maxColumn ? TileAt(column, (h - (column & 1)) / 2) : -1;
                if (tile >= 0 && tiles.Contains(tile)) { run.Add(tile); continue; }
                if (run.Count >= 3) xCandidates.Add(run.ToArray());
                run.Clear();
            }
        }
        // Longest first; a line mostly drawn by an earlier parallel line is the other zigzag of the same wall.
        var xRuns = new List<int[]>();
        var xCovered = new HashSet<int>();
        foreach (int[] run in xCandidates.OrderByDescending(r => r.Length).ThenBy(r => r[0]))
        {
            if (run.Count(xCovered.Contains) * 2 > run.Length) continue;
            xRuns.Add(run); xCovered.UnionWith(run);
        }

        var runs = new List<WallRun>();
        foreach (int[] run in zRuns)
        {
            GroundPoint first = FalloutGrid.HexCenter(run[0]) - origin, last = FalloutGrid.HexCenter(run[^1]) - origin;
            bool parallel = zRuns.Any(o => o != run && Math.Abs(Column(o[0]) - Column(run[0])) == 1
                && Row(o[0]) <= Row(run[^1]) && Row(o[^1]) >= Row(run[0]));
            runs.Add(new WallRun(WallAxis.Z, new(first.X, first.Z - .5), new(last.X, last.Z + .5), run, parallel));
        }
        foreach (int[] run in xRuns)
        {
            GroundPoint first = FalloutGrid.HexCenter(run[0]) - origin, last = FalloutGrid.HexCenter(run[^1]) - origin;
            double z = run.Take(2).Average(t => FalloutGrid.HexCenter(t).Z) - origin.Z;
            bool parallel = xRuns.Any(o => o != run && Math.Abs(o.Take(2).Average(t => FalloutGrid.HexCenter(t).Z) - origin.Z - z) <= 1.01
                && Column(o[0]) <= Column(run[^1]) && Column(o[^1]) >= Column(run[0]));
            runs.Add(new WallRun(WallAxis.X, new(first.X - HalfColumn, z), new(last.X + HalfColumn, z), run, parallel));
        }

        var pieces = new List<ArchitecturePiece>();
        foreach (WallRun run in runs) pieces.AddRange(Modules(run));
        var drawn = runs.SelectMany(r => r.Tiles).ToHashSet();
        var pillars = new List<ArchitecturePiece>();
        void Pillar(int tile, int[] stands)
        {
            GroundPoint center = FalloutGrid.HexCenter(tile) - origin;
            if (pillars.Any(p => Math.Abs(p.Origin.X - center.X) < .9 && Math.Abs(p.Origin.Z - center.Z) < .9)) return;
            pillars.Add(new ArchitecturePiece(ArchitectureModule.Pillar, center, new(1, 0), 0, 1, false, stands));
        }
        // Lone source walls are free-standing columns.
        foreach (int tile in tiles.Where(t => !drawn.Contains(t)).OrderBy(t => t)) Pillar(tile, new[] { tile });
        // Ends without another wall beyond them frame doorways and terminate walls.
        foreach (WallRun run in runs)
            foreach (int end in new[] { run.Tiles[0], run.Tiles[^1] })
            {
                bool free = Enumerable.Range(0, 6).All(d => FalloutGrid.Neighbor(end, d) is not int n || !tiles.Contains(n) || run.Tiles.Contains(n));
                if (free) Pillar(end, new[] { end });
            }
        pieces.AddRange(pillars);
        // Corners and diagonal junctions between lines can leave the stretch between two touching wall hexes
        // open; a pillar on that junction closes it, as a corner post.
        var plan = new WallPlan(runs.ToArray(), pieces.ToArray(), filled.ToArray());
        // Each round places a post on the first open point of every still-open link; the post covers that
        // point, so every round makes progress and a few rounds close any junction.
        for (int round = 0; round < 4; round++)
        {
            var joints = new List<ArchitecturePiece>();
            foreach (var (a, b) in UncoveredLinks(plan, tiles, origin))
            {
                GroundPoint pa = FalloutGrid.HexCenter(a) - origin, pb = FalloutGrid.HexCenter(b) - origin;
                GroundPoint open = Enumerable.Range(0, 11).Select(i => new GroundPoint(pa.X + (pb.X - pa.X) * i / 10, pa.Z + (pb.Z - pa.Z) * i / 10))
                    .First(p => !plan.Draws(p));
                if (joints.Any(j => j.Contains(open, 0))) continue;
                joints.Add(new ArchitecturePiece(ArchitectureModule.Pillar, open, new(1, 0), 0, 1, false, new[] { a, b }));
            }
            if (joints.Count == 0) break;
            plan = plan with { Pieces = plan.Pieces.Concat(joints).ToArray() };
        }
        return plan;
    }

    /// <summary>Pairs of neighboring wall hexes whose connecting segment is not entirely under stone.</summary>
    public static IEnumerable<(int A, int B)> UncoveredLinks(WallPlan plan, IEnumerable<int> wallTiles, GroundPoint origin)
    {
        var tiles = wallTiles.ToHashSet();
        foreach (int a in tiles.OrderBy(t => t))
            for (int d = 0; d < 6; d++)
            {
                if (FalloutGrid.Neighbor(a, d) is not int b || b < a || !tiles.Contains(b)) continue;
                GroundPoint pa = FalloutGrid.HexCenter(a) - origin, pb = FalloutGrid.HexCenter(b) - origin;
                for (int i = 0; i <= 10; i++)
                    if (!plan.Draws(new GroundPoint(pa.X + (pb.X - pa.X) * i / 10, pa.Z + (pb.Z - pa.Z) * i / 10)))
                    { yield return (a, b); break; }
            }
    }

    // A filler continues a wall when two non-touching wall hexes beside it line up through it
    // (one column: exact midpoint; zigzag X line or diagonal: within half a row).
    static bool Bridges(int tile, HashSet<int> walls)
    {
        GroundPoint c = FalloutGrid.HexCenter(tile);
        var sides = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(tile, d)).OfType<int>().Where(walls.Contains)
            .Select(FalloutGrid.HexCenter).ToArray();
        for (int i = 0; i < sides.Length; i++)
            for (int j = i + 1; j < sides.Length; j++)
            {
                double span = Math.Sqrt(Math.Pow(sides[i].X - sides[j].X, 2) + Math.Pow(sides[i].Z - sides[j].Z, 2));
                double mx = (sides[i].X + sides[j].X) / 2 - c.X, mz = (sides[i].Z + sides[j].Z) / 2 - c.Z;
                if (span > 1.7 && Math.Sqrt(mx * mx + mz * mz) <= .51) return true;
            }
        return false;
    }

    static IEnumerable<ArchitecturePiece> Modules(WallRun run)
    {
        GroundPoint direction = run.Axis == WallAxis.X ? new(1, 0) : new(0, 1);
        int metres = Math.Max(1, (int)Math.Round(run.Length / KitScale));
        // Deterministic variety per wall: module order and which face shows depend only on the source hexes.
        uint seed = (uint)run.Tiles[0] * 2654435761u ^ (uint)run.Tiles.Length * 40503u;
        var sizes = new List<int>();
        for (int left = metres; left > 0;)
        {
            int size = left >= 4 && (seed >> sizes.Count) % 3 != 0 ? 4 : left >= 2 ? 2 : 1;
            sizes.Add(size); left -= size;
        }
        if (sizes.Count > 1 && (seed & 8) != 0) sizes.Reverse();
        double scale = run.Length / metres, along = 0;
        var tiles = run.Tiles.Select(t => (Tile: t, Along: run.Axis == WallAxis.X
            ? FalloutGrid.HexCenter(t).X : FalloutGrid.HexCenter(t).Z)).ToArray();
        double first = run.Axis == WallAxis.X ? run.Start.X : run.Start.Z;
        double offset = tiles[0].Along - (first + (run.Axis == WallAxis.X ? HalfColumn : .5));
        for (int i = 0; i < sizes.Count; i++)
        {
            double length = sizes[i] * scale;
            int[] stands = tiles.Where(t => t.Along - offset >= first + along - 1e-6 && t.Along - offset < first + along + length - 1e-6)
                .Select(t => t.Tile).ToArray();
            if (stands.Length == 0) stands = new[] { tiles.MinBy(t => Math.Abs(t.Along - offset - (first + along + length / 2))).Tile };
            var origin = new GroundPoint(run.Start.X + direction.X * along, run.Start.Z + direction.Z * along);
            GroundPoint facing = direction;
            // Turning a module end for end shows its other face; both faces are finished masonry.
            if (((seed >> (i + 1)) & 1) != 0)
            {
                origin = new GroundPoint(origin.X + direction.X * length, origin.Z + direction.Z * length);
                facing = new GroundPoint(-direction.X, -direction.Z);
            }
            bool vines = ((seed >> (i + 3)) * 2246822519u >> 28) % 7 == 0;
            yield return new ArchitecturePiece(sizes[i] switch { 4 => ArchitectureModule.Wall4, 2 => ArchitectureModule.Wall2, _ => ArchitectureModule.Wall1 },
                origin, facing, length, run.Thickened ? ParallelThickness : 1, vines, stands);
            along += length;
        }
    }
}
