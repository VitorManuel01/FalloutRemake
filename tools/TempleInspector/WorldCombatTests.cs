using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;

internal static class WorldCombatTests
{
    const int AntPid = 0x0100017E;
    static int Tile(int column, int row) => row * 200 + 199 - column;
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }

    // WorldTests map 3 (entry 20100 = column 99, row 100) with a near ant, a far ant and an ant walled in.
    static MapDocument Map()
    {
        var map = WorldTests.Map(3, 126);
        int walled = Tile(95, 100);
        var objects = map.ObjectLayers[0].Objects.Concat(new[]
        {
            CombatTests.Critter(Tile(99, 103), AntPid, 6, 1, "map3:near-ant"),
            CombatTests.Critter(Tile(99, 150), AntPid, 6, 1, "map3:far-ant"),
            CombatTests.Critter(walled, AntPid, 6, 1, "map3:walled-ant")
        }).Concat(Enumerable.Range(0, 6).Select(d => CombatTests.Blocker(FalloutGrid.Neighbor(walled, d)!.Value, 3, 0x03000001, $"map3:wall{d}"))).ToArray();
        return map with { ObjectLayers = new[] { map.ObjectLayers[0] with { Objects = objects, DeclaredCount = objects.Length } },
            Prototypes = new[] { CombatTests.AntPrototype() } };
    }
    static CombatWeapon Spear() => CombatWeapon.Primary("Lança", WeaponPrototypeData.Read(CombatTests.Spear()));

    public static void Run(Action<string, Action> check)
    {
        check("world combat notices only reachable ants within five hexes and opens the fight with the noticing ant first", () =>
        {
            var world = new WorldSession(_ => Map(), new[] { 3, 126 });
            var encounter = world.FindEncounter(world.Location);
            Assert(encounter != null && encounter.FoeKeys.SequenceEqual(new[] { "map3:near-ant" }) && encounter.InitiatorKey == "map3:near-ant", "Only the near ant notices.");
            Assert(world.FindEncounter(world.Location with { Elevation = 1 }) == null && world.FindEncounter(world.Location with { Map = 126 }) == null, "Other elevation or map.");
            var fight = world.BeginCombat(encounter!, Spear(), new CombatTests.Scripted());
            Assert(fight.Active!.Id == "map3:near-ant" && fight.Get("player").HitPoints == 30 && fight.Get("player").Hex == world.Location.Hex
                && fight.Get("map3:near-ant").HitPoints == 6 && fight.Combatants.Count == 2, "Fight setup.");
            try { world.BeginCombat(encounter! with { Player = world.Location with { Hex = 20101 } }, Spear(), new CombatTests.Scripted()); throw new Exception("Stale location accepted."); }
            catch (InvalidOperationException) { }
        });
        check("world combat commits a won fight once: corpse projected where it fell without blocking, 25 XP, unsaved outcome flagged", () =>
        {
            var world = new WorldSession(_ => Map(), new[] { 3, 126 });
            int before = world.Experience;
            var fight = world.BeginCombat(world.FindEncounter(world.Location)!, Spear(), new CombatTests.Scripted(1, 11));
            Assert(world.CommitCombat(fight) == CombatCommitStatus.NotOver, "Open fight committed.");
            ProvisionalMeleeBehavior.TakeTurn(fight);
            var ant = fight.Get("map3:near-ant");
            Assert(FalloutGrid.Distance(ant.Hex, world.Location.Hex) == 1 && ant.Hex != Tile(99, 103) && fight.Active!.IsPlayer, "Ant walked around the closed door (3 AP) and cannot bite.");
            fight.Execute(CombatActionProposal.Attack("player", ant.Id));
            Assert(fight.Turn.Over && !ant.Alive, "Ant killed.");
            Assert(world.CommitCombat(fight) == CombatCommitStatus.Ready && world.IsDefeated(3, ant.Id) && world.CombatExperience == 25
                && world.Experience == before + 25 && world.PlayerHitPoints == null && world.CombatOutcomeUnsaved, "Commit.");
            var source = world.Current.Map.ObjectLayers[0].Objects.Single(o => o.Key == ant.Id);
            var corpse = world.EffectiveObject(3, source);
            Assert(corpse.Header.Tile == ant.Hex && (corpse.Header.Flags & HexNavigation.NoBlock) != 0 && source.Header.Tile == Tile(99, 103)
                && source.Header.Flags == 0, "Corpse projection keeps the source record.");
            var navigation = world.Current.Doors.Navigation(0);
            Assert(navigation.Walkable(ant.Hex) && navigation.Walkable(Tile(99, 103)), "Corpse and the ant's old hex are free.");
            Assert(world.FindEncounter(world.Location) == null, "Dead ant does not notice.");
            Assert(world.CommitCombat(fight) == CombatCommitStatus.AlreadyDefeated && world.CombatExperience == 25, "Single commit.");
            world.Reset();
            Assert(!world.CombatOutcomeUnsaved && world.Experience == before && !world.IsDefeated(3, ant.Id) && world.FindEncounter(world.Location) != null, "Reset.");
        });
        check("world combat keeps the wounded player's HP for the next fight and refuses lost, foreign or forged fights atomically", () =>
        {
            var world = new WorldSession(_ => Map(), new[] { 3, 126 });
            var fight = world.BeginCombat(world.FindEncounter(world.Location)!, Spear(), new CombatTests.Scripted(1, 4, 1, 11));
            ProvisionalMeleeBehavior.TakeTurn(fight);
            // Round 1 ends with the player; round 2 re-sorts by Sequence (player 10 > ant 9), so the player waits twice.
            fight.Execute(CombatActionProposal.EndTurn("player")); fight.Execute(CombatActionProposal.EndTurn("player"));
            ProvisionalMeleeBehavior.TakeTurn(fight);
            Assert(fight.Get("player").HitPoints == 26 && fight.Active!.IsPlayer, "Bite for 4.");
            fight.Execute(CombatActionProposal.Attack("player", "map3:near-ant"));
            Assert(world.CommitCombat(fight) == CombatCommitStatus.Ready && world.PlayerHitPoints == 26, "Wounded HP kept.");
            world.Reset();
            var source = world.Current.Map.ObjectLayers[0].Objects.Single(o => o.Key == "map3:near-ant");
            var stats = DerivedCombatStats.ForCharacter(Special.Neutral);
            CombatantState Hero(int hp) => new("player", "p", null, 0, true, world.Location.Hex, 0, stats, Spear(), hp);
            int beside = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(world.Location.Hex, d)!.Value).First(h => world.Current.Doors.Navigation(0).Walkable(h));
            var adjacent = source with { Header = source.Header with { Tile = beside } };
            var lost = new CombatSession(new[] { Hero(1), CombatantState.FromMapCritter(adjacent, CritterPrototypeData.Read(CombatTests.AntPrototype()), "ant") },
                world.Current.Doors.Navigation(0), new CombatTests.Scripted(1, 1), source.Key);
            ProvisionalMeleeBehavior.TakeTurn(lost);
            Assert(lost.Turn.Over && lost.Turn.WinningTeam == 1 && world.CommitCombat(lost) == CombatCommitStatus.PlayerDefeated
                && !world.CombatOutcomeUnsaved, "Lost fight changes nothing.");
            var foreign = new CombatantState("map9:ghost", "ghost", AntPid, 1, false, Tile(99, 101), 0, DerivedCombatStats.FromCritter(CritterPrototypeData.Read(CombatTests.AntPrototype())),
                CombatWeapon.Unarmed(), 1);
            var stale = new CombatSession(new[] { Hero(30), foreign }, world.Current.Doors.Navigation(0), new CombatTests.Scripted(1, 3), "player");
            stale.Execute(CombatActionProposal.Attack("player", foreign.Id));
            Assert(stale.Turn.Over && world.CommitCombat(stale) == CombatCommitStatus.StaleSource && !world.CombatOutcomeUnsaved, "Foreign critter refused.");
        });
    }
}
