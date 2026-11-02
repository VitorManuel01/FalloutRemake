using System;
using System.Collections.Generic;
using System.Linq;
using FalloutRemake.Geometry;

namespace FalloutRemake.Runtime.Combat;

/// <summary>
/// Combat steps over the existing hex navigation: one AP per hex, six neighbors, no diagonal shortcuts.
/// Source blockers keep deciding passability, except the combatants' own source records: those move, so their
/// living positions block instead and a corpse's hex is free. Exit grids cannot be entered during combat.
/// </summary>
public sealed class CombatMovement
{
    public const int ActionPointsPerHex = 1;
    private readonly HexNavigation _navigation;
    private readonly IReadOnlyList<CombatantState> _combatants;
    private readonly HashSet<string> _combatantKeys;

    public CombatMovement(HexNavigation navigation, IReadOnlyList<CombatantState> combatants)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
        _combatantKeys = combatants.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
    }

    public bool Passable(int hex, CombatantState mover) =>
        HexNavigation.Valid(hex) && !_navigation.IsExit(hex)
        && _navigation.BlockersAt(hex).All(b => _combatantKeys.Contains(b.Key))
        && !_combatants.Any(c => c != mover && c.Alive && c.Elevation == mover.Elevation && c.Hex == hex);

    /// <summary>A critter (combatant or a map critter outside the fight) standing on the hex, which CE _obj_ai_blocking_at may step past.</summary>
    public bool CritterAt(int hex, CombatantState mover) =>
        _combatants.Any(c => c != mover && c.Alive && c.Elevation == mover.Elevation && c.Hex == hex)
        || _navigation.BlockersAt(hex).Any(b => b.FidType == 1 && !_combatantKeys.Contains(b.Key));

    /// <summary>Passable once every critter on the hex is ignored: only walls, scenery, closed doors and exit grids count.</summary>
    public bool PassableIgnoringCritters(int hex, CombatantState mover) =>
        HexNavigation.Valid(hex) && !_navigation.IsExit(hex) && _navigation.BlockersAt(hex).All(b => b.FidType == 1 || _combatantKeys.Contains(b.Key));

    /// <summary>Whether the hexes form a walk from the mover's hex, one neighbor at a time, over passable hexes.</summary>
    public bool Follows(CombatantState mover, int[] path)
    {
        ArgumentNullException.ThrowIfNull(mover); ArgumentNullException.ThrowIfNull(path);
        if (path.Length == 0 || _navigation.Elevation != mover.Elevation) return false;
        int previous = mover.Hex;
        foreach (int hex in path)
        {
            if (FalloutGrid.Distance(previous, hex) != 1 || !Passable(hex, mover)) return false;
            previous = hex;
        }
        return true;
    }

    /// <summary>Shortest path from the mover to the target, excluding the start hex; null when no path exists.</summary>
    public int[]? Path(CombatantState mover, int target) => Path(mover, t => t == target);

    /// <summary>Shortest path to the first hex accepted by <paramref name="goal"/> (e.g. any hex within weapon reach).</summary>
    public int[]? Path(CombatantState mover, Func<int, bool> goal)
    {
        ArgumentNullException.ThrowIfNull(mover); ArgumentNullException.ThrowIfNull(goal);
        if (_navigation.Elevation != mover.Elevation) return null;
        if (goal(mover.Hex)) return Array.Empty<int>();
        var parent = new Dictionary<int, int> { [mover.Hex] = mover.Hex };
        var queue = new Queue<int>(); queue.Enqueue(mover.Hex);
        while (queue.TryDequeue(out int current))
            for (int direction = 0; direction < 6; direction++)
            {
                if (FalloutGrid.Neighbor(current, direction) is not int next || parent.ContainsKey(next) || !Passable(next, mover)) continue;
                parent[next] = current;
                if (goal(next))
                {
                    var path = new List<int> { next };
                    for (int step = current; step != mover.Hex; step = parent[step]) path.Add(step);
                    path.Reverse(); return path.ToArray();
                }
                queue.Enqueue(next);
            }
        return null;
    }
}
