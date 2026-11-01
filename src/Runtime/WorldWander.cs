using System;
using System.Collections.Generic;
using System.Linq;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime.Combat;

namespace FalloutRemake.Runtime;

/// <summary>
/// The Temple creatures' wandering, from zclrat and zclscorp. map_enter_p_proc queues the first timed event
/// random(1, 5) seconds ahead; timed_event_p_proc, when no fight is open and the critter stands (critter_state & 1 == 0),
/// walks it to tile_num_in_direction(tile, random(0, 5), random(1, 3)) and always queues the next event random(3, 5)
/// seconds ahead. zclscorp skips the walk on map 3 while the dude stands on another elevation. The walk follows CE
/// pathfinderFindPath (OriginalPathfinder) around blockers and the dude; the critter ends facing along its last step.
/// </summary>
public sealed partial class WorldSession
{
    public const int WanderFirstMin = 1, WanderFirstMax = 5, WanderNextMin = 3, WanderNextMax = 5, WanderStepsMin = 1, WanderStepsMax = 3;
    private readonly Dictionary<string, int> _wanderTimers = new(StringComparer.Ordinal);
    private int _wanderMap = -1;

    /// <summary>Ticks until each wandering critter's next timed event on the current map (empty until time first runs there).</summary>
    public IReadOnlyDictionary<string, int> WanderTimers => _wanderTimers;
    private void ResetWandering() { _wanderTimers.Clear(); _wanderMap = -1; }

    private IEnumerable<MapObject> WanderingCritters() => Current.Map.ObjectLayers.SelectMany(l => l.Objects)
        .Where(o => EncounterPids.Contains(o.Header.Pid) && HexNavigation.Valid(o.Header.Tile) && (o.Header.Flags & HexNavigation.Hidden) == 0
            && !IsDefeated(CurrentMap, o.Key));

    // Ticks one at a time, firing due events in MAP order, as the CE queue runs them one tick apart.
    private string[] Wander(int ticks, ICombatRandom random, IReadOnlyCollection<int>? reserved)
    {
        if (_wanderMap != CurrentMap)
        {
            _wanderTimers.Clear(); _wanderMap = CurrentMap;
            foreach (MapObject critter in WanderingCritters()) _wanderTimers[critter.Key] = random.Between(WanderFirstMin, WanderFirstMax) * PoisonRules.TicksPerSecond;
        }
        var moved = new List<string>();
        var roots = Current.Map.ObjectLayers.SelectMany(l => l.Objects).ToDictionary(o => o.Key, StringComparer.Ordinal);
        for (int tick = 0; tick < ticks; tick++)
            foreach (string key in roots.Keys.Where(_wanderTimers.ContainsKey).ToArray())
            {
                if (IsDefeated(CurrentMap, key)) { _wanderTimers.Remove(key); continue; }
                if (--_wanderTimers[key] > 0) continue;
                if (WanderStep(EffectiveObject(CurrentMap, roots[key]), random, reserved)) moved.Add(key);
                _wanderTimers[key] = random.Between(WanderNextMin, WanderNextMax) * PoisonRules.TicksPerSecond;
            }
        return moved.Distinct().ToArray();
    }

    private bool WanderStep(MapObject critter, ICombatRandom random, IReadOnlyCollection<int>? reserved)
    {
        if (critter.Header.Pid == RadscorpionPid && CurrentMap == 3 && Location.Elevation != critter.Header.Elevation) return false;
        int rotation = random.Between(0, 5), distance = random.Between(WanderStepsMin, WanderStepsMax);
        int target = OriginalPathfinder.TileInDirection(critter.Header.Tile, rotation, distance);
        HexNavigation navigation = Current.Doors.NavigationForNpc(critter.Header.Elevation, critter.Key);
        bool sameLevel = Location.Elevation == critter.Header.Elevation;
        int[]? steps = OriginalPathfinder.FindPath(critter.Header.Tile, target, t => !navigation.Walkable(t)
            || sameLevel && (t == Location.Hex || reserved?.Contains(t) == true));
        if (steps == null) return false;
        Prototype? prototype = Current.Map.Prototypes.FirstOrDefault(p => p.Pid == critter.Header.Pid);
        if (prototype == null) return false;
        int hp = critter.State.TryGetValue("hp", out int stored) ? stored : DerivedCombatStats.FromCritter(CritterPrototypeData.Read(prototype)).MaxHitPoints;
        _survivors[(CurrentMap, critter.Key)] = new SurvivingCritter(CurrentMap, critter.Key, critter.Header.Pid, critter.Header.Elevation, target, hp, steps[^1]);
        return true;
    }
}
