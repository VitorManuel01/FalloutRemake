using System;
using System.Linq;
using FalloutRemake.Geometry;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

/// <summary>
/// CE obj_can_see_obj(critter, dude) outside combat, the test zclrat (Giant Ant) and zclscorp (Radscorpion) make in
/// critter_p_proc before attack_complex(dude_obj). isWithinPerception: PE x 5 hexes inside the 3-direction front cone
/// (_can_see), PE hexes in any direction; the dude is not sneaking. Then the straight path from the critter must meet the
/// dude before any other unhidden, blocking critter, scenery or wall (_obj_blocking_at): closed doors block, open ones do not.
/// </summary>
public static class CritterSight
{
    public const int FacingRangePerPerception = 5;

    /// <summary>critterGetStat(STAT_PERCEPTION) for a map critter: PRO base + bonus, clamped to 1..10 (no blindness yet).</summary>
    public static int Perception(CritterPrototypeData prototype) => Math.Clamp(prototype.BaseStat(1) + prototype.BonusStat(1), 1, 10);

    /// <summary>_can_see: the target lies in the facing direction or one of its two neighbours.</summary>
    public static bool Facing(int rotation, int from, int to)
    {
        int difference = Math.Abs(rotation - OriginalStraightPath.RotationTo(from, to));
        return difference is 0 or 1 or 5;
    }

    /// <summary>
    /// Whether the critter, standing at its header tile with its header rotation, sees the dude at <paramref name="playerHex"/>.
    /// <paramref name="navigation"/> supplies the blockers of the critter's elevation as the session projects them.
    /// </summary>
    public static bool Sees(MapObject critter, int perception, int playerHex, HexNavigation navigation)
    {
        ArgumentNullException.ThrowIfNull(critter); ArgumentNullException.ThrowIfNull(navigation);
        int tile = critter.Header.Tile;
        if (!HexNavigation.Valid(tile) || !HexNavigation.Valid(playerHex) || tile == playerHex) return false;
        // objectGetDistanceBetween: a multihex critter stands one hex closer.
        int distance = Math.Max(0, FalloutGrid.Distance(tile, playerHex) - ((critter.Header.Flags & HexNavigation.Multihex) != 0 ? 1 : 0));
        bool perceived = Facing(critter.Header.Rotation, tile, playerHex) && distance <= perception * FacingRangePerPerception || distance <= perception;
        return perceived && OriginalStraightPath.FirstBlocked(tile, playerHex,
            t => t == playerHex || navigation.BlockersAt(t).Any(b => b.Key != critter.Key)) == playerHex;
    }
}
