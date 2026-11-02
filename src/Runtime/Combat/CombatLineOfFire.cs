using System;
using System.Collections.Generic;
using System.Linq;
using FalloutRemake.Geometry;

namespace FalloutRemake.Runtime.Combat;

/// <summary>
/// What stands between a shooter (or thrower) and the hexes its shot crosses, after CE _obj_shoot_blocking_at walked with
/// _make_straight_path_func (a node every 32 steps): the living combatants where they stand now, and the map's own shot
/// blockers (walls, scenery, critters outside the fight). Corpses never block (the sfall fix CE carries). The combatants'
/// source records are skipped, since they move.
/// </summary>
public sealed class CombatLineOfFire
{
    /// <summary>A combatant, or a source object of the map, on the hex that stops the line.</summary>
    public sealed record Obstacle(int Hex, CombatantState? Combatant, HexBlocker? Source)
    {
        /// <summary>CE FID_TYPE == OBJ_TYPE_CRITTER.</summary>
        public bool Critter => Combatant != null || Source?.FidType == 1;
        internal object Identity => (object?)Combatant ?? Source!;
    }

    private readonly HexNavigation _navigation;
    private readonly IReadOnlyList<CombatantState> _combatants;
    private readonly HashSet<string> _keys;

    public CombatLineOfFire(HexNavigation navigation, IReadOnlyList<CombatantState> combatants)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
        _keys = combatants.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>The first shot obstacle on <paramref name="hex"/> other than <paramref name="exclude"/> and the previous obstacle.</summary>
    public Obstacle? At(int hex, object? exclude, object? previous)
    {
        CombatantState? critter = _combatants.FirstOrDefault(c => c.Alive && c.Hex == hex && !ReferenceEquals(c, exclude) && !ReferenceEquals(c, previous));
        if (critter != null) return new Obstacle(hex, critter, null);
        HexBlocker? source = _navigation.ShotBlockersAt(hex).FirstOrDefault(b => !_keys.Contains(b.Key) && !ReferenceEquals(b, previous));
        return source == null ? null : new Obstacle(hex, null, source);
    }

    /// <summary>_make_straight_path_func with _obj_shoot_blocking_at: the first obstacle from one hex toward another.</summary>
    public Obstacle? First(int from, int to, object? exclude, object? previous)
    {
        Obstacle? found = null;
        OriginalStraightPath.FirstBlocked(from, to, tile => (found = At(tile, exclude, previous)) != null, nodeStep: 32);
        return found;
    }

    /// <summary>
    /// CE _combat_is_shot_blocked: walking from the attacker to the target, anything but a critter (or the target) blocks the
    /// shot; each standing critter in the way other than the target counts toward the line-of-fire penalty (knocked down,
    /// out or dead ones do not, as sfall fixed).
    /// </summary>
    public bool Blocked(CombatantState attacker, CombatantState target, out int crittersOnLine)
    {
        ArgumentNullException.ThrowIfNull(attacker); ArgumentNullException.ThrowIfNull(target);
        crittersOnLine = 0;
        int current = attacker.Hex; object? previous = attacker;
        for (int guard = 0; guard < 400 && current != target.Hex; guard++)
        {
            Obstacle? obstacle = First(current, target.Hex, attacker, previous);
            if (obstacle == null) return false;
            if (!obstacle.Critter) return true;
            if (obstacle.Combatant != target && obstacle.Combatant is not { Prone: true })
                crittersOnLine += obstacle.Source?.Multihex == true ? 2 : 1;
            current = obstacle.Hex; previous = obstacle.Identity;
        }
        return false;
    }

    /// <summary>
    /// attackCompute after a plain ranged miss: the shot flies on to <paramref name="landing"/> (_tile_num_beyond); the first
    /// obstacle past the defender takes its place, else whatever blocks walkers on the landing hex (_obj_blocking_at).
    /// </summary>
    public Obstacle? Beyond(CombatantState defender, int landing)
    {
        ArgumentNullException.ThrowIfNull(defender);
        Obstacle? obstacle = First(defender.Hex, landing, defender, defender);
        if (obstacle != null) return obstacle;
        CombatantState? critter = _combatants.FirstOrDefault(c => c.Alive && c.Hex == landing && c != defender);
        if (critter != null) return new Obstacle(landing, critter, null);
        HexBlocker? source = _navigation.BlockersAt(landing).FirstOrDefault(b => !_keys.Contains(b.Key));
        return source == null ? null : new Obstacle(landing, null, source);
    }
}
