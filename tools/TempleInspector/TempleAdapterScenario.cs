using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class TempleAdapterScenario
{
    public static object Run(MapDocument map)
    {
        MapLayoutWriter.Verify(map);
        byte[] source = MapLayoutWriter.Rebuild(map);
        DoorSession session = DoorSession.FromMap(map, enableTempleDoorAdapter: true);
        string key = session.TempleDoor!.SourceKey;
        MapObject door = session.Get(key)!.Source;
        MapObject firstDoor = map.ObjectLayers[0].Objects.Single(o => o.Offset == 244040);
        int entry = map.Prefix.Header.EnteringTile;
        HexNavigation navigation = session.Navigation(0);
        int initialReachable = navigation.ReachableFrom(entry).Count;
        Expect(initialReachable == 540 && session.Get(key)!.IsLocked, "Initial state mismatch.");
        int firstApproach = Approach(navigation, entry, firstDoor.Header.Tile);
        var walker = new HexWalker(navigation, entry);
        HexRoute firstRoute = walker.WalkTo(firstApproach);
        Expect(firstRoute.Status == RouteStatus.Ready, "First door cannot be approached.");
        walker.Advance(10000);
        Expect(session.Toggle(firstDoor.Key, walker.CurrentHex, 0, false) == DoorUseStatus.Ready, "First door failed to open.");
        navigation = session.Navigation(0);
        int firstOpenedReachable = navigation.ReachableFrom(entry).Count;
        Expect(firstOpenedReachable == 2184, "First-door reachability regression.");
        int secondApproach = Approach(navigation, walker.CurrentHex, door.Header.Tile);
        walker = new HexWalker(navigation, walker.CurrentHex);
        HexRoute secondRoute = walker.WalkTo(secondApproach);
        Expect(secondRoute.Status == RouteStatus.Ready, "Second door cannot be approached.");
        walker.Advance(10000);
        Expect(session.Toggle(key, walker.CurrentHex, 0, false) == DoorUseStatus.Locked, "Locked door was bypassed.");

        LockpickResult failure = session.UseLockpick(key, secondApproach, 0, false, _ => SkillRollOutcome.Failure);
        Expect(failure.Outcome == SkillRollOutcome.Failure && session.Get(key)!.IsLocked && failure.ExperienceAwarded == 0, "Ordinary failure changed the lock.");
        LockpickResult criticalFailure = session.UseLockpick(key, secondApproach, 0, false, _ => SkillRollOutcome.CriticalFailure);
        Expect(criticalFailure.Outcome == SkillRollOutcome.CriticalFailure && session.Get(key)!.IsJammed, "Critical failure did not jam.");
        int resolverCalls = 0;
        LockpickResult jammed = session.UseLockpick(key, secondApproach, 0, false, _ => { resolverCalls++; return SkillRollOutcome.Success; });
        Expect(jammed.Status == DoorUseStatus.Jammed && resolverCalls == 0, "Jammed door invoked another roll.");

        session.Reset();
        Expect(!session.Get(key)!.IsJammed && session.Get(key)!.IsLocked && session.TempleDoor!.Experience == 0, "Reset retained attempt state.");
        Expect(session.Toggle(firstDoor.Key, firstApproach, 0, false) == DoorUseStatus.Ready, "First door failed after reset.");
        LockpickRequest? request = null;
        LockpickResult success = session.UseLockpick(key, secondApproach, 0, false, r => { request = r; return SkillRollOutcome.Success; });
        Expect(request is { Skill: 9, Modifier: 100, Action: LockpickAction.Unlock }
            && success.ExperienceAwarded == 25 && !session.Get(key)!.IsLocked && !session.Get(key)!.IsOpen, "Unlock result mismatch.");
        Expect(!session.Navigation(0).Walkable(door.Header.Tile), "Unlock incorrectly opened the door.");
        HexReachability before = session.Navigation(0).ReachableFrom(entry);
        Expect(session.Toggle(key, secondApproach, 0, false) == DoorUseStatus.Ready, "Unlocked door did not open.");
        navigation = session.Navigation(0);
        int bothOpenedReachable = navigation.ReachableFrom(entry).Count;
        Expect(bothOpenedReachable > firstOpenedReachable, "Second door did not expand reachability.");
        walker = new HexWalker(navigation, secondApproach);
        Expect(walker.WalkTo(door.Header.Tile).Status == RouteStatus.Ready, "Doorway cannot be entered.");
        walker.Advance(1);
        Expect(session.Toggle(key, walker.CurrentHex, 0, false) == DoorUseStatus.Occupied, "Door closed onto the marker.");
        int beyond = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(door.Header.Tile, d))
            .Where(t => t.HasValue && navigation.Walkable(t.Value) && !navigation.IsExit(t.Value))
            .Select(t => t!.Value).First(t => before.Distance(t) < 0);
        Expect(walker.WalkTo(beyond).Status == RouteStatus.Ready, "Doorway cannot be crossed.");
        walker.Advance(1);
        Expect(session.Toggle(key, walker.CurrentHex, 0, false) == DoorUseStatus.Ready, "Door failed to close after crossing.");
        Expect(session.Navigation(0).ReachableFrom(entry).Count == firstOpenedReachable, "Closing did not restore the second boundary.");

        LockpickResult relock = session.UseLockpick(key, beyond, 0, false, r =>
        { Expect(r.Action == LockpickAction.Lock, "Relock action mismatch."); return SkillRollOutcome.CriticalSuccess; });
        Expect(relock.ExperienceAwarded == 0 && session.Get(key)!.IsLocked, "Relock farmed XP.");
        LockpickResult again = session.UseLockpick(key, beyond, 0, false, _ => SkillRollOutcome.Success);
        Expect(again.ExperienceAwarded == 0 && session.TempleDoor!.Experience == 25, "Repeated unlock farmed XP.");
        session.EnterMap();
        Expect(!session.Get(key)!.IsLocked && session.TempleDoor!.Locals[5] == 1, "Re-entry lost solved locals.");
        session.Reset();
        Expect(session.Get(key)!.IsLocked && !session.Get(key)!.IsJammed && session.TempleDoor!.Experience == 0
            && session.Navigation(0).ReachableFrom(entry).Count == initialReachable, "Final reset mismatch.");
        MapLayoutWriter.Verify(map);
        Expect(source.SequenceEqual(MapLayoutWriter.Rebuild(map)) && door.State["openFlags"] == 0, "Source bytes changed.");
        return new
        {
            Status = "TEMPLE_ADAPTER_TEST_OK",
            Mode = "Explicit deterministic validation; no character profile, randomness or INT execution",
            map.Source, SourceKey = key, DoorId = door.Header.Id, DoorHex = door.Header.Tile,
            InitialLocals = session.TempleDoor!.Locals,
            FirstApproachSteps = firstRoute.Tiles.Length - 1,
            SecondApproachSteps = secondRoute.Tiles.Length - 1,
            InitialReachable = initialReachable, FirstOpenedReachable = firstOpenedReachable, BothOpenedReachable = bothOpenedReachable,
            RollSkill = request!.Skill, RollModifier = request.Modifier,
            FirstReward = success.ExperienceAwarded, RepeatedReward = again.ExperienceAwarded,
            Verified = "failure, jam refusal, success, opening, crossing, occupied-close rejection, relock, XP guard, re-entry, reset and byte reconstruction"
        };
    }
    private static int Approach(HexNavigation navigation, int from, int tile)
    {
        HexReachability reach = navigation.ReachableFrom(from);
        return Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(tile, d))
            .Where(t => t.HasValue && reach.Distance(t.Value) >= 0 && !navigation.IsExit(t.Value))
            .Select(t => t!.Value).OrderBy(reach.Distance).ThenBy(t => t).First();
    }
    private static void Expect(bool condition, string message)
    { if (!condition) throw new InvalidDataException("Temple adapter scenario: " + message); }
}
