using FalloutRemake.Geometry;

internal static class TempleArchitectureTests
{
    static int Tile(int column, int row) => row * 200 + 199 - column;
    static readonly GroundPoint Origin = FalloutGrid.HexCenter(28906);
    static GroundPoint At(int tile) => FalloutGrid.HexCenter(tile) - Origin;
    static bool Drawn(WallPlan plan, GroundPoint point, double margin) => plan.Pieces.Any(p => p.Contains(point, margin));

    public static void Run(Action<string, Action> check)
    {
        check("temple kit plan lays one-column source walls as one Z run with exact length and every hex drawn", () =>
        {
            int[] wall = Enumerable.Range(120, 6).Select(r => Tile(90, r)).ToArray();
            WallPlan plan = TempleArchitecture.PlanWalls(wall, Origin);
            WallRun run = plan.Runs.Single();
            if (run.Axis != WallAxis.Z || Math.Abs(run.Length - 6) > 1e-9 || !run.Tiles.SequenceEqual(wall)) throw new Exception("Z run mismatch.");
            double walls = plan.Pieces.Where(p => p.Module != ArchitectureModule.Pillar).Sum(p => p.Length);
            if (Math.Abs(walls - 6) > 1e-9) throw new Exception("Wall modules do not span the run.");
            foreach (int tile in wall) if (!plan.Covers(tile) || !Drawn(plan, At(tile), 0)) throw new Exception("Source wall hex not drawn.");
            if (plan.Pieces.Count(p => p.Module == ArchitectureModule.Pillar) != 2) throw new Exception("Free wall ends need pillars.");
        });
        check("temple kit plan follows zigzag X walls on their mean line and keeps the adjacent walkable rows clear", () =>
        {
            int[] wall = Enumerable.Range(80, 7).Select(c => Tile(c, 130)).ToArray();
            WallPlan plan = TempleArchitecture.PlanWalls(wall, Origin);
            WallRun run = plan.Runs.Single();
            double z = (At(wall[0]).Z + At(wall[1]).Z) / 2;
            if (run.Axis != WallAxis.X || Math.Abs(run.Start.Z - z) > 1e-9 || Math.Abs(run.Length - 7 * FalloutGrid.ColumnStep) > 1e-9)
                throw new Exception("X run mismatch.");
            foreach (int tile in wall)
            {
                if (!Drawn(plan, At(tile), 0)) throw new Exception("Zigzag hex left outside the wall.");
                foreach (int side in new[] { 0, 3 })
                {
                    // Rotations 0/3 leave the line sideways; neighbors that are not walls stay walkable.
                    int? next = FalloutGrid.Neighbor(tile, side == 0 ? 1 : 4);
                    if (next is int open && !wall.Contains(open) && Drawn(plan, At(open), .25)) throw new Exception("Wall intrudes on a walkable neighbor.");
                }
            }
        });
        check("temple kit plan joins corners without a pillar, frames door gaps and turns lone hexes into columns", () =>
        {
            int[] side = Enumerable.Range(120, 5).Select(r => Tile(90, r)).ToArray();
            int[] top = Enumerable.Range(91, 5).Select(c => Tile(c, 120)).ToArray();
            int[] split = Enumerable.Range(140, 3).Concat(Enumerable.Range(144, 3)).Select(r => Tile(100, r)).ToArray();
            int lone = Tile(110, 150);
            WallPlan plan = TempleArchitecture.PlanWalls(side.Concat(top).Concat(split).Append(lone), Origin);
            if (plan.Runs.Count(r => r.Axis == WallAxis.Z) != 3 || plan.Runs.Count(r => r.Axis == WallAxis.X) != 1) throw new Exception("Run split mismatch.");
            if (plan.Pieces.Any(p => p.Module == ArchitectureModule.Pillar && p.Tiles.Contains(Tile(90, 120)))) throw new Exception("Corner received a pillar.");
            int gap = Tile(100, 143);
            if (Drawn(plan, At(gap), .2)) throw new Exception("Door gap was walled over.");
            foreach (int jamb in new[] { Tile(100, 142), Tile(100, 144) })
                if (!plan.Pieces.Any(p => p.Module == ArchitectureModule.Pillar && p.Tiles.Contains(jamb))) throw new Exception("Door jamb lacks a pillar.");
            ArchitecturePiece column = plan.Pieces.Single(p => p.Tiles.SequenceEqual(new[] { lone }));
            if (column.Module != ArchitectureModule.Pillar || !column.Contains(At(lone), 0)) throw new Exception("Lone wall hex is not a column.");
            foreach (int tile in side.Concat(top).Concat(split)) if (!plan.Covers(tile)) throw new Exception("Wall hex not represented.");
        });
        check("temple kit plan closes blocker gaps and diagonal junctions but keeps open passages and side blockers empty", () =>
        {
            int[] wall = new[] { 120, 121, 122, 124, 125, 126 }.Select(r => Tile(90, r)).ToArray();
            int filler = Tile(90, 123), beside = Tile(89, 121);
            // A zigzag X line leaving the column diagonally from its last hex.
            int[] branch = Enumerable.Range(91, 5).Select(c => Tile(c, 126)).ToArray();
            WallPlan plan = TempleArchitecture.PlanWalls(wall.Concat(branch), Origin, new[] { filler, beside });
            if (!plan.Filled.SequenceEqual(new[] { filler })) throw new Exception("Only the blocker between two walls becomes stone.");
            if (!Drawn(plan, At(filler), 0) || Drawn(plan, At(beside), -.05)) throw new Exception("Filler drawing mismatch.");
            var links = TempleArchitecture.UncoveredLinks(plan, wall.Concat(branch).Append(filler), Origin).ToArray();
            if (links.Length != 0) throw new Exception($"Open link {links[0].A}-{links[0].B}.");
            WallPlan passage = TempleArchitecture.PlanWalls(wall, Origin);
            if (passage.Filled.Length != 0 || Drawn(passage, At(filler), .2)) throw new Exception("Walkable gap without a blocker was closed.");
            // A pot in a wall niche stands on a blocker between two walls: its hex stays clear of stone.
            WallPlan niche = TempleArchitecture.PlanWalls(wall.Concat(branch), Origin, new[] { filler, beside }, new[] { filler });
            if (niche.Filled.Length != 0 || Drawn(niche, At(filler), .25)) throw new Exception("Stone drawn over a blocker that holds a visible object.");
        });
        check("temple kit plan is deterministic, ignores invalid hexes and closes the slit between parallel walls", () =>
        {
            int[] inner = Enumerable.Range(120, 8).Select(r => Tile(90, r)).ToArray();
            int[] outer = Enumerable.Range(121, 6).Select(r => Tile(91, r)).ToArray();
            WallPlan a = TempleArchitecture.PlanWalls(inner.Concat(outer).Append(-1).Append(40000), Origin);
            WallPlan b = TempleArchitecture.PlanWalls(outer.Reverse().Concat(inner.Reverse()), Origin);
            string Key(WallPlan p) => string.Join(";", p.Pieces.Select(x => $"{x.Module}:{x.Origin.X:F6},{x.Origin.Z:F6}:{x.Direction.X},{x.Direction.Z}:{x.Length:F6}:{x.Vines}")
                .OrderBy(s => s, StringComparer.Ordinal));
            if (Key(a) != Key(b)) throw new Exception("Plan depends on input order.");
            if (!a.Runs.All(r => r.Thickened)) throw new Exception("Parallel walls were not thickened.");
            var between = new GroundPoint((At(inner[3]).X + At(outer[2]).X) / 2, At(inner[3]).Z);
            if (!Drawn(a, between, 0)) throw new Exception("Parallel walls leave a slit.");
            if (TempleArchitecture.PlanWalls(Array.Empty<int>(), Origin).Pieces.Length != 0) throw new Exception("Empty input produced stone.");
        });
    }
}
