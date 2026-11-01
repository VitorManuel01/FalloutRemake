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
    internal static MapDocument Map()
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
    internal static CombatWeapon Spear() => CombatWeapon.Primary("Lança", WeaponPrototypeData.Read(CombatTests.Spear()));

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
        check("world combat commits a won fight once: corpse projected where it fell without blocking, 25 XP, outcome flagged for the checkpoint", () =>
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
                && world.Experience == before + 25 && world.PlayerHitPoints == null && world.HasCombatOutcome, "Commit.");
            var source = world.Current.Map.ObjectLayers[0].Objects.Single(o => o.Key == ant.Id);
            var corpse = world.EffectiveObject(3, source);
            Assert(corpse.Header.Tile == ant.Hex && (corpse.Header.Flags & HexNavigation.NoBlock) != 0 && source.Header.Tile == Tile(99, 103)
                && source.Header.Flags == 0, "Corpse projection keeps the source record.");
            var navigation = world.Current.Doors.Navigation(0);
            Assert(navigation.Walkable(ant.Hex) && navigation.Walkable(Tile(99, 103)), "Corpse and the ant's old hex are free.");
            Assert(world.FindEncounter(world.Location) == null, "Dead ant does not notice.");
            Assert(world.CommitCombat(fight) == CombatCommitStatus.AlreadyDefeated && world.CombatExperience == 25, "Single commit.");
            world.Reset();
            Assert(!world.HasCombatOutcome && world.Experience == before && !world.IsDefeated(3, ant.Id) && world.FindEncounter(world.Location) != null, "Reset.");
        });
        check("an open fight projects its creatures where they stand, so exploration routes reach their source hexes; one fight at a time", () =>
        {
            var world = new WorldSession(_ => Map(), new[] { 3, 126 });
            int source = Tile(99, 103);
            var fight = world.BeginCombat(world.FindEncounter(world.Location)!, Spear(), new CombatTests.Scripted(99));
            Assert(world.InCombat, "Fight registered.");
            try { world.BeginCombat(world.FindEncounter(world.Location) ?? new CombatEncounter(world.Location, new[] { "map3:near-ant" }, "map3:near-ant"), Spear(), new CombatTests.Scripted()); throw new Exception("Second fight opened."); }
            catch (InvalidOperationException) { }
            ProvisionalMeleeBehavior.TakeTurn(fight);
            var ant = fight.Get("map3:near-ant");
            var navigation = world.Current.Doors.Navigation(0);
            Assert(ant.Hex != source && navigation.Walkable(source) && !navigation.Walkable(ant.Hex), "Live position blocks, source hex is free.");
            int start = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(source, d)!.Value).First(h => navigation.Walkable(h) && !navigation.IsExit(h) && h != ant.Hex);
            Assert(navigation.FindRoute(start, source).Status == RouteStatus.Ready, "Exploration route into the creature's source hex.");
            world.AbandonCombat();
            Assert(!world.InCombat && !world.Current.Doors.Navigation(0).Walkable(source) && world.CommitCombat(fight) == CombatCommitStatus.NotOver,
                "Abandoned fight: the creature is back on its source hex and nothing is applied.");
        });
        check("checkpoint v10+ round-trips won fights, migrates v9 without combat and refuses forged corpses, XP, HP and schema", () =>
        {
            WorldSession Load(WorldCheckpoint checkpoint) => WorldSession.Restore(_ => Map(), new[] { 3, 126 }, checkpoint, "catalog");
            var world = new WorldSession(_ => Map(), new[] { 3, 126 });
            var fight = world.BeginCombat(world.FindEncounter(world.Location)!, Spear(), new CombatTests.Scripted(1, 4, 1, 11));
            ProvisionalMeleeBehavior.TakeTurn(fight);
            fight.Execute(CombatActionProposal.EndTurn("player")); fight.Execute(CombatActionProposal.EndTurn("player"));
            ProvisionalMeleeBehavior.TakeTurn(fight); fight.Execute(CombatActionProposal.Attack("player", "map3:near-ant"));
            Assert(world.CommitCombat(fight) == CombatCommitStatus.Ready, "Setup fight.");
            WorldCheckpoint saved = CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog")));
            DefeatedCritter dead = saved.Combat!.Defeated.Single();
            Assert(saved.Version == 13 && saved.Profile == "neutral-v13" && saved.Combat.Experience == 25 && saved.Combat.PlayerHitPoints == 26
                && dead.Key == "map3:near-ant" && dead.Hex == fight.Get("map3:near-ant").Hex && dead.Pid == AntPid, "Captured outcome.");
            var restored = Load(saved);
            Assert(restored.IsDefeated(3, dead.Key) && restored.CombatExperience == 25 && restored.Experience == world.Experience && restored.PlayerHitPoints == 26
                && restored.FindEncounter(restored.Location) == null && restored.Current.Doors.Navigation(0).Walkable(dead.Hex)
                && restored.EffectiveObject(3, restored.Current.Map.ObjectLayers[0].Objects.Single(o => o.Key == dead.Key)).Header.Tile == dead.Hex, "Restored outcome.");
            var clean = new WorldSession(_ => Map(), new[] { 3, 126 }).Capture("catalog");
            Assert(clean.Combat is { Defeated.Length: 0, Experience: 0, PlayerHitPoints: null }, "Empty v10 combat block.");
            var legacy = Load(clean with { Version = 9, Profile = "neutral-v9", Combat = null, Character = null, Duel = null, Skills = null, Areas = clean.Areas.Select(a => a with { Consumed = null }).ToArray() });
            Assert(!legacy.HasCombatOutcome && legacy.Capture("catalog").Version == 13, "v9 migrates without combat.");
            int wall = FalloutGrid.Neighbor(Tile(95, 100), 0)!.Value;
            var forged = new[]
            {
                saved with { Combat = saved.Combat with { Experience = 50 } },
                saved with { Combat = saved.Combat with { PlayerHitPoints = 30 } },
                saved with { Combat = saved.Combat with { PlayerHitPoints = 0 } },
                saved with { Combat = saved.Combat with { Defeated = new[] { dead, dead }, Experience = 50 } },
                saved with { Combat = saved.Combat with { Defeated = new[] { dead with { Key = "map3:ghost" } } } },
                saved with { Combat = saved.Combat with { Defeated = new[] { dead with { Pid = 0x01000005 } } } },
                saved with { Combat = saved.Combat with { Defeated = new[] { dead with { Elevation = 1 } } } },
                saved with { Combat = saved.Combat with { Defeated = new[] { dead with { Hex = wall } } } },
                saved with { Combat = saved.Combat with { Defeated = new[] { dead with { Map = 126 } } } },
                saved with { Combat = null, Character = null, Duel = null, Skills = null },
                saved with { Version = 9, Profile = "neutral-v9" }
            };
            foreach (WorldCheckpoint bad in forged)
                try { Load(bad); throw new Exception("Forged combat checkpoint accepted."); } catch (InvalidDataException) { }
            string json = System.Text.Encoding.UTF8.GetString(CheckpointFile.Encode(saved));
            int start = json.IndexOf("\"Combat\"", StringComparison.Ordinal);
            try { CheckpointFile.Decode(System.Text.Encoding.UTF8.GetBytes(json[..start].TrimEnd().TrimEnd(',') + "\n}")); throw new Exception("v10 without Combat decoded."); }
            catch (InvalidDataException) { }
        });
        check("world combat wields the best carried hand weapon, ignores ranged weapons without ammunition and treats radscorpions as encounters", () =>
        {
            MapObject Item(string key, int pid) => CombatTests.Blocker(-1, 0, pid, key) with { Subtype = 3, Header = CombatTests.Blocker(-1, 0, pid, key).Header with { Tile = -1 } };
            var spear = Item("map3:spear", 7); var pistol = Item("map3:pistol", 8);
            var pot = CombatTests.Blocker(Tile(97, 100), 0, 0x000000FA, "map3:pot") with { Subtype = 1, InventoryCount = 2, State = new Dictionary<string, int> { ["flags"] = 0 },
                Inventory = new[] { new InventoryEntry(0, 1, spear), new InventoryEntry(0, 1, pistol) } };
            var scorpion = CombatTests.Critter(Tile(100, 102), 0x01000005, 10, 1, "map3:scorpion");
            var baseMap = Map();
            var map = baseMap with { ObjectLayers = new[] { baseMap.ObjectLayers[0] with { Objects = baseMap.ObjectLayers[0].Objects.Append(pot).Append(scorpion).ToArray(),
                    DeclaredCount = baseMap.ObjectLayers[0].Objects.Length + 2 } },
                Prototypes = baseMap.Prototypes.Append(CombatTests.Spear()).Append(CombatTests.Pistol()).Append(CombatTests.Item(0xFA, 1, 65, 0, _ => { })).ToArray() };
            var world = new WorldSession(_ => map, new[] { 3, 126 });
            Assert(world.PlayerWeapon() is { Pid: null, AttackType: FalloutRemake.Import.WeaponAttackType.Unarmed, ActionPoints: 3 }, "Empty bag fights bare-handed.");
            world.Current.Items.Restore(new[] { new TakenItem("map3:pot", "map3:pistol", 1) });
            Assert(world.PlayerWeapon().Pid == null, "A pistol without ammunition tracking is not wielded.");
            world.Current.Items.Restore(new[] { new TakenItem("map3:pot", "map3:pistol", 1), new TakenItem("map3:pot", "map3:spear", 1) });
            Assert(world.PlayerWeapon() is { Pid: 7, Range: 2, ActionPoints: 4, MinDamage: 3, MaxDamage: 10 }, "Carried spear is wielded.");
            var encounter = world.FindEncounter(world.Location);
            Assert(encounter != null && encounter.FoeKeys.Contains("map3:scorpion") && encounter.FoeKeys.Contains("map3:near-ant")
                && encounter.InitiatorKey == "map3:scorpion", "Radscorpion (2 hexes) notices before the ant (3 hexes).");
        });        check("world combat keeps the wounded player's HP for the next fight and refuses lost, foreign or forged fights atomically", () =>
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
                && !world.HasCombatOutcome, "Lost fight changes nothing.");
            var foreign = new CombatantState("map9:ghost", "ghost", AntPid, 1, false, Tile(99, 101), 0, DerivedCombatStats.FromCritter(CritterPrototypeData.Read(CombatTests.AntPrototype())),
                CombatWeapon.Unarmed(), 1);
            var stale = new CombatSession(new[] { Hero(30), foreign }, world.Current.Doors.Navigation(0), new CombatTests.Scripted(1, 3), "player");
            stale.Execute(CombatActionProposal.Attack("player", foreign.Id));
            Assert(stale.Turn.Over && world.CommitCombat(stale) == CombatCommitStatus.StaleSource && !world.HasCombatOutcome, "Foreign critter refused.");
        });
    }
}
