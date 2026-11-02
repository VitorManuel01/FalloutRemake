using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;

internal static class CombatLeaveTests
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static int Tile(int column, int row) => row * 200 + 199 - column;
    static int Walk(int from, int direction, int steps) { for (int i = 0; i < steps; i++) from = FalloutGrid.Neighbor(from, direction)!.Value; return from; }

    // The player at the centre, one fixture ant (PE 2) on a straight line in direction 1 (east), facing `rotation`.
    static CombatSession Duel(int distance, int rotation, int[]? rolls = null, string initiator = "player")
    {
        int centre = Tile(100, 100);
        var source = CombatTests.Critter(Walk(centre, 1, distance), 0x0100017E, 6, 1, "ant");
        source = source with { Header = source.Header with { Rotation = rotation } };
        var ant = CombatantState.FromMapCritter(source, CritterPrototypeData.Read(CombatTests.AntPrototype()), "ant");
        var player = new CombatantState("player", "p", null, 0, true, centre, 0, DerivedCombatStats.ForCharacter(Special.Neutral), CombatWeapon.Unarmed(), 30);
        return new CombatSession(new[] { player, ant }, new HexNavigation(Array.Empty<MapObject>(), 0), new CombatTests.Scripted(rolls ?? Array.Empty<int>()), initiator);
    }

    public static void Run(Action<string, Action> check)
    {
        check("Enter leaves a fight only when no enemy perceives the player: PE x 5 in its front cone, PE x 2 around, no line of sight needed", () =>
        {
            // The ant east of the player looks back west (4) at it, or away east (1).
            Assert(Duel(10, 4).Preview(CombatActionProposal.LeaveCombat("player")).Status == CombatActionStatus.EnemiesAware, "Facing, 10 hexes.");
            Assert(Duel(11, 4).Preview(CombatActionProposal.LeaveCombat("player")).Status == CombatActionStatus.Ready, "Facing, 11 hexes.");
            Assert(Duel(4, 1).Preview(CombatActionProposal.LeaveCombat("player")).Status == CombatActionStatus.EnemiesAware, "Away, 4 hexes (PE x 2 in combat).");
            var fight = Duel(5, 1);
            Assert(fight.Preview(CombatActionProposal.LeaveCombat("player")).Status == CombatActionStatus.Ready, "Away, 5 hexes.");
            CombatActionResult left = fight.Execute(CombatActionProposal.LeaveCombat("player"));
            Assert(left.Status == CombatActionStatus.Ready && fight.Left && fight.Turn is { Over: true, WinningTeam: null }
                && left.Events.OfType<CombatLeft>().Single().Survivors.SequenceEqual(new[] { "ant" }) && left.Events.OfType<CombatEnded>().Any(), "Left with the ant standing.");
            Assert(fight.Execute(CombatActionProposal.Attack("player", "ant")).Status == CombatActionStatus.CombatOver, "Nothing happens after leaving.");
            var creatureTurn = Duel(20, 4, initiator: "ant");
            Assert(creatureTurn.Preview(CombatActionProposal.LeaveCombat("ant")).Status == CombatActionStatus.InvalidTarget, "Only the player leaves.");
        });
        check("combatants face along their last step and toward the target they strike, as CE animations leave them", () =>
        {
            // The ant (5 AP) walks 5 of the 7 hexes west toward the player, then strikes on its next turn.
            var fight = Duel(7, 1, new[] { 1, 1 }, initiator: "ant");
            ProvisionalMeleeBehavior.TakeTurn(fight);
            var ant = fight.Get("ant");
            int[] walked = fight.Log.OfType<CombatantMoved>().Single().Path;
            Assert(walked.Length == 5 && ant.Rotation == OriginalStraightPath.RotationTo(walked[^2], walked[^1]) && ant.Rotation != 1
                && CritterSight.Facing(ant.Rotation, ant.Hex, Tile(100, 100)), "Walking toward the player turns it from east to face along the last step.");
            fight.Execute(CombatActionProposal.Move("player", Walk(Tile(100, 100), 0, 1)));
            Assert(fight.Get("player").Rotation == 0, "The player faces along its step too.");
            // Round 2 starts with the player (Sequence 10 against 9).
            fight.Execute(CombatActionProposal.EndTurn("player")); fight.Execute(CombatActionProposal.EndTurn("player"));
            ProvisionalMeleeBehavior.TakeTurn(fight);
            Assert(fight.Log.OfType<AttackResolved>().Any() && ant.Rotation == OriginalStraightPath.RotationTo(ant.Hex, fight.Get("player").Hex), "Striking faces the target.");
        });
        check("a left fight keeps its survivors where they stand, with HP and facing, through encounters, new fights and checkpoints", () =>
        {
            var world = new WorldSession(_ => WorldCombatTests.Map(), new[] { 3, 126 });
            int sourceHex = Tile(99, 150);
            var fight = world.BeginCombat(WorldCombatTests.NearAnt(world, "map3:far-ant"), WorldCombatTests.Spear(), new CombatTests.Scripted());
            ProvisionalMeleeBehavior.TakeTurn(fight);
            var ant = fight.Get("map3:far-ant");
            Assert(ant.Hex != sourceHex && fight.Active!.IsPlayer, "The far ant closed in by 5 hexes.");
            Assert(fight.Execute(CombatActionProposal.LeaveCombat("player")).Status == CombatActionStatus.Ready && world.CommitCombat(fight) == CombatCommitStatus.Ready
                && !world.InCombat && world.CombatExperience == 0, "Left and committed without XP.");
            Assert(world.Survivors.Single() is { Map: 3, Key: "map3:far-ant", HitPoints: 6 } s && s.Hex == ant.Hex && s.Rotation == ant.Rotation && world.HasCombatOutcome,
                "Survivor recorded.");
            var source = world.Current.Map.ObjectLayers[0].Objects.Single(o => o.Key == "map3:far-ant");
            var projected = world.EffectiveObject(3, source);
            var navigation = world.Current.Doors.Navigation(0);
            Assert(projected.Header.Tile == ant.Hex && projected.Header.Rotation == ant.Rotation && source.Header.Tile == sourceHex
                && navigation.Walkable(sourceHex) && !navigation.Walkable(ant.Hex) && world.FindEncounter(world.Location) == null, "Projected where it stands, still too far to see.");
            // Checkpoint round trip, then forged survivors.
            WorldCheckpoint saved = CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog")));
            SurvivingCritter survivor = saved.Combat!.Survivors!.Single();
            WorldSession Load(WorldCheckpoint checkpoint) => WorldSession.Restore(_ => WorldCombatTests.Map(), new[] { 3, 126 }, checkpoint, "catalog");
            var restored = Load(saved);
            Assert(restored.Survivors.Single() == survivor && saved.Version == 13, "Restored survivor.");
            var hurt = Load(saved with { Combat = saved.Combat with { Survivors = new[] { survivor with { HitPoints = 3 } } } });
            var second = hurt.BeginCombat(WorldCombatTests.NearAnt(hurt, "map3:far-ant"), WorldCombatTests.Spear(), new CombatTests.Scripted());
            Assert(second.Get("map3:far-ant") is { HitPoints: 3 } again && again.Hex == survivor.Hex && again.Rotation == survivor.Rotation, "A new fight starts from the survivor.");
            int wall = FalloutGrid.Neighbor(Tile(95, 100), 0)!.Value;
            var forged = new[]
            {
                survivor with { HitPoints = 0 }, survivor with { HitPoints = 7 }, survivor with { Rotation = 6 }, survivor with { Hex = wall },
                survivor with { Hex = 20100 }, survivor with { Pid = 0x01000005 }, survivor with { Key = "map3:ghost" }, survivor with { Elevation = 1 }
            };
            foreach (SurvivingCritter bad in forged)
                try { Load(saved with { Combat = saved.Combat with { Survivors = new[] { bad } } }); throw new Exception($"Forged survivor accepted: {bad}."); }
                catch (InvalidDataException) { }
            try { Load(saved with { Combat = saved.Combat with { Survivors = new[] { survivor, survivor } } }); throw new Exception("Duplicate survivor accepted."); }
            catch (InvalidDataException) { }
            Assert(Load(saved with { Combat = saved.Combat with { Survivors = null } }).Survivors.Count == 0, "Older v13 combat blocks carry none.");
            world.Reset();
            Assert(world.Survivors.Count == 0 && !world.HasCombatOutcome && world.EffectiveObject(3, source).Header.Tile == sourceHex, "Reset returns the ant.");
        });
        check("a survivor killed in a later fight becomes a corpse and leaves the survivors", () =>
        {
            var first = new WorldSession(_ => WorldCombatTests.Map(), new[] { 3, 126 });
            var fight = first.BeginCombat(WorldCombatTests.NearAnt(first, "map3:far-ant"), WorldCombatTests.Spear(), new CombatTests.Scripted());
            ProvisionalMeleeBehavior.TakeTurn(fight); fight.Execute(CombatActionProposal.LeaveCombat("player")); first.CommitCombat(fight);
            // The wounded survivor stands beside the player (hex 20099) when the next fight opens; the player strikes first.
            WorldCheckpoint saved = first.Capture("catalog");
            int hex = 20099;
            var world = WorldSession.Restore(_ => WorldCombatTests.Map(), new[] { 3, 126 },
                saved with { Combat = saved.Combat! with { Survivors = new[] { saved.Combat.Survivors!.Single() with { Hex = hex, HitPoints = 3 } } } }, "catalog");
            var again = world.BeginCombat(new CombatEncounter(world.Location, new[] { "map3:far-ant" }, "player"), WorldCombatTests.Spear(), new CombatTests.Scripted(1, 11));
            Assert(again.Get("map3:far-ant") is { HitPoints: 3 } ant && ant.Hex == hex, "Second fight from the survivor.");
            again.Execute(CombatActionProposal.Attack("player", "map3:far-ant"));
            Assert(again.Turn.Over && world.CommitCombat(again) == CombatCommitStatus.Ready && world.Survivors.Count == 0
                && world.Defeated.Single() is { Key: "map3:far-ant" } dead && dead.Hex == hex && world.CombatExperience == 25, "Corpse where the survivor stood.");
        });
    }
}
