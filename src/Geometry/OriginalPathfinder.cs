using System;
using System.Collections.Generic;

namespace FalloutRemake.Geometry;

/// <summary>
/// CE pathfinderFindPath (animation.cc) for a critter that cannot open doors: A* over at most 2000 open and 2000 closed
/// nodes, 50 per step plus 10 per turn outside combat, the estimate is _idist between screen points, and the first
/// cheapest open node in slot order is expanded next. With a5 = 1 a blocked destination fails at once. Radioactive goo
/// costs are left out (no goo in the supported maps).
/// </summary>
public static class OriginalPathfinder
{
    private struct Node { public int Tile, From, Rotation, Estimate, Cost; }
    private const int MaxNodes = 2000, MaxSteps = 800;

    /// <summary>tileIsEdge: the outer ring of the 200x200 grid, where tileGetTileInDirection stops.</summary>
    public static bool IsEdge(int tile) => HexNavigation.Valid(tile) && (tile < 200 || tile >= 40000 - 200 || tile % 200 == 0 || tile % 200 == 199);

    /// <summary>tileGetTileInDirection: up to <paramref name="distance"/> steps, stopping on the grid edge.</summary>
    public static int TileInDirection(int tile, int rotation, int distance)
    {
        for (int step = 0; step < distance; step++)
        {
            if (IsEdge(tile)) break;
            tile = FalloutGrid.Neighbor(tile, rotation) ?? tile;
        }
        return tile;
    }

    private static int Distance(int x1, int y1, int x2, int y2)
    {
        int dx = Math.Abs(x2 - x1), dy = Math.Abs(y2 - y1);
        return dx + dy - Math.Min(dx, dy) / 2;
    }

    /// <summary>The rotations of the steps from <paramref name="from"/> to <paramref name="to"/>, or null when CE finds no path.</summary>
    public static int[]? FindPath(int from, int to, Func<int, bool> blocked, bool inCombat = false)
    {
        ArgumentNullException.ThrowIfNull(blocked);
        if (!HexNavigation.Valid(from) || !HexNavigation.Valid(to) || blocked(to)) return null;
        var processed = new bool[40000]; processed[from] = true;
        var open = new Node[MaxNodes]; for (int i = 0; i < MaxNodes; i++) open[i].Tile = -1;
        var closed = new Node[MaxNodes];
        (int toX, int toY) = OriginalStraightPath.Screen(to); (int fromX, int fromY) = OriginalStraightPath.Screen(from);
        open[0] = new Node { Tile = from, From = -1, Rotation = 0, Estimate = Distance(fromX, fromY, toX, toY), Cost = 0 };
        int openCount = 1, closedCount = 0;
        Node current;
        while (true)
        {
            int best = -1;
            for (int index = 0, seen = 0; seen < openCount; index++)
            {
                if (open[index].Tile == -1) continue;
                seen++;
                if (best == -1 || open[index].Estimate + open[index].Cost < open[best].Estimate + open[best].Cost) best = index;
            }
            current = open[best]; openCount--; open[best].Tile = -1;
            if (current.Tile == to) { if (openCount == 0) openCount = 1; break; }
            closed[closedCount++] = current;
            if (closedCount == MaxNodes) return null;
            for (int rotation = 0; rotation < 6; rotation++)
            {
                int tile = TileInDirection(current.Tile, rotation, 1);
                if (processed[tile]) continue;
                if (tile != to && blocked(tile)) continue;
                int slot = 0; while (slot < MaxNodes && open[slot].Tile != -1) slot++;
                if (++openCount == MaxNodes) return null;
                processed[tile] = true;
                (int x, int y) = OriginalStraightPath.Screen(tile);
                open[slot] = new Node { Tile = tile, From = current.Tile, Rotation = rotation, Estimate = Distance(x, y, toX, toY),
                    Cost = current.Cost + 50 + (!inCombat && current.Rotation != rotation ? 10 : 0) };
            }
            if (openCount == 0) break;
        }
        if (openCount == 0) return null;
        var rotations = new List<int>();
        for (int step = 0; step < MaxSteps && current.Tile != from; step++)
        {
            rotations.Add(current.Rotation);
            int parent = current.From, index = 0;
            while (closed[index].Tile != parent) index++;
            current = closed[index];
        }
        rotations.Reverse();
        return rotations.Count == 0 ? null : rotations.ToArray();
    }
}
