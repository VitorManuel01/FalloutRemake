using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;

internal static class WanderTests
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static int Tile(int column, int row) => row * 200 + 199 - column;

    public static void Run(Action<string, Action> check)
    {
        check("CE pathfinder: straight when free, around walls, refused on a blocked destination, enclosed start or no move; tile steps stop on the grid edge", () =>
        {
            int start = Tile(100, 100);
            Assert(OriginalPathfinder.FindPath(start, OriginalPathfinder.TileInDirection(start, 3, 3), _ => false) is [3, 3, 3], "Straight walk.");
            int wall = FalloutGrid.Neighbor(start, 1)!.Value, beyond = FalloutGrid.Neighbor(wall, 1)!.Value;
            int[] around = OriginalPathfinder.FindPath(start, beyond, t => t == wall)!;
            int end = around.Aggregate(start, (tile, rotation) => FalloutGrid.Neighbor(tile, rotation)!.Value), walked = start;
            foreach (int rotation in around) { walked = FalloutGrid.Neighbor(walked, rotation)!.Value; Assert(walked != wall, "The path crosses the wall."); }
            Assert(around.Length == 3 && end == beyond, "Around the wall in 3 steps.");
            Assert(OriginalPathfinder.FindPath(start, beyond, t => t == beyond) == null, "Blocked destination.");
            Assert(OriginalPathfinder.FindPath(start, beyond, t => t != start && FalloutGrid.Distance(t, start) == 1) == null, "Enclosed start.");
            Assert(OriginalPathfinder.FindPath(start, start, _ => false) == null, "No move.");
            Assert(OriginalPathfinder.TileInDirection(Tile(0, 50), 1, 3) == Tile(0, 50) && OriginalPathfinder.TileInDirection(Tile(2, 50), 4, 3) == FalloutGrid.Neighbor(FalloutGrid.Neighbor(Tile(2, 50), 4)!.Value, 4),
                "Steps stop on the edge.");
            Assert(OriginalPathfinder.IsEdge(Tile(0, 50)) && OriginalPathfinder.IsEdge(Tile(199, 50)) && OriginalPathfinder.IsEdge(Tile(50, 0)) && !OriginalPathfinder.IsEdge(start), "Edge ring.");
        });
        check("creatures wander on game time as zclrat timed_event_p_proc: first event in 1..5 s, then a 1..3 hex walk every 3..5 s, facing along the last step", () =>
        {
            var world = new WorldSession(_ => WorldCombatTests.Map(), new[] { 3, 126 });
            int near = Tile(99, 103), target = OriginalPathfinder.TileInDirection(near, 3, 2);
            // map_enter draws for near, far and walled ants (MAP order); at tick 10 the near ant walks 2 hexes south-west.
            var random = new CombatTests.Scripted(1, 5, 5, 3, 2, 4);
            WorldTimeAdvance first = world.AdvanceTime(10, random);
            Assert(random.Left == 0 && first.Wandered.SequenceEqual(new[] { "map3:near-ant" }) && world.Survivors.Single() is { Key: "map3:near-ant", Rotation: 3, HitPoints: 6 } s
                && s.Hex == target && world.WanderTimers["map3:near-ant"] == 40 && world.WanderTimers["map3:far-ant"] == 40, "First walk.");
            var source = world.Current.Map.ObjectLayers[0].Objects.Single(o => o.Key == "map3:near-ant");
            Assert(world.EffectiveObject(3, source).Header.Tile == target && world.Current.Doors.Navigation(0).Walkable(near) && !world.Current.Doors.Navigation(0).Walkable(target),
                "The ant stands where it walked.");
            // At tick 40 all three act: the near ant's goal is reserved by the walking player, the walled ant faces a wall.
            int blockedGoal = OriginalPathfinder.TileInDirection(target, 4, 1), far = Tile(99, 150), farGoal = OriginalPathfinder.TileInDirection(far, 5, 3);
            random = new CombatTests.Scripted(4, 1, 3, 5, 3, 3, 0, 1, 3);
            WorldTimeAdvance second = world.AdvanceTime(40, random, new[] { blockedGoal });
            Assert(random.Left == 0 && second.Wandered.SequenceEqual(new[] { "map3:far-ant" }) && world.Survivors.Single(v => v.Key == "map3:near-ant").Hex == target
                && world.Survivors.Single(v => v.Key == "map3:far-ant") is { Rotation: 5 } f && f.Hex == farGoal && !world.Survivors.Any(v => v.Key == "map3:walled-ant")
                && world.WanderTimers.Values.All(t => t == 30), "Reserved and walled goals refuse the walk; the timer still restarts.");
            // The wanderers are part of the checkpoint; a reset or a load restarts the timers as map_enter does.
            WorldCheckpoint saved = CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog")));
            var restored = WorldSession.Restore(_ => WorldCombatTests.Map(), new[] { 3, 126 }, saved, "catalog");
            Assert(restored.Survivors.Count == 2 && restored.WanderTimers.Count == 0, "Restored positions, fresh timers.");
            world.Reset();
            Assert(world.Survivors.Count == 0 && world.WanderTimers.Count == 0, "Reset.");
        });
        check("wandering waits for fights and, for zclscorp on map 3, for the player's elevation", () =>
        {
            var baseMap = WorldCombatTests.Map();
            var scorpion = CombatTests.Critter(Tile(80, 100), 0x01000005, 10, 1, "map3:upstairs-scorpion");
            scorpion = scorpion with { Header = scorpion.Header with { Elevation = 1 } };
            var map = baseMap with { ObjectLayers = new[] { baseMap.ObjectLayers[0] with { Objects = baseMap.ObjectLayers[0].Objects.Append(scorpion).ToArray(),
                    DeclaredCount = baseMap.ObjectLayers[0].Objects.Length + 1 } }, Prototypes = baseMap.Prototypes.Append(CombatTests.AntPrototype(pid: 0x01000005)).ToArray() };
            var world = new WorldSession(_ => map, new[] { 3, 126 });
            // Only the scorpion fires at tick 10; on another elevation it draws just the next delay.
            var random = new CombatTests.Scripted(5, 5, 5, 1, 3);
            Assert(world.AdvanceTime(10, random).Wandered.Length == 0 && random.Left == 0 && world.WanderTimers["map3:upstairs-scorpion"] == 30, "Scorpion waits.");
            // The near ant aims 3 hexes north, at the player's own hex: refused, as the dude blocks pathfinderFindPath.
            var plain = new WorldSession(_ => WorldCombatTests.Map(), new[] { 3, 126 });
            Assert(OriginalPathfinder.TileInDirection(Tile(99, 103), 5, 3) == plain.Location.Hex, "Fixture: north leads to the player.");
            random = new CombatTests.Scripted(1, 5, 5, 5, 3, 3);
            Assert(plain.AdvanceTime(10, random).Wandered.Length == 0 && random.Left == 0 && plain.Survivors.Count == 0, "The player's hex is never a goal.");
            var fight = world.BeginCombat(WorldCombatTests.NearAnt(world), WorldCombatTests.Spear(), new CombatTests.Scripted());
            Assert(world.AdvanceTime(100, new CombatTests.Scripted()).Wandered.Length == 0, "No time passes in a fight.");
            world.AbandonCombat();
        });
    }
}
