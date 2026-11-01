using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;

internal static class PoisonTests
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    const int ScorpionPid = 0x01000005;

    // The player (neutral: LK 5, so a sting is shrugged off on 1..4; PR 25%) with a radscorpion beside it that acts first.
    static CombatSession Stung(int[] rolls, PoisonState poison = default, int hitPoints = 30, bool stings = true)
    {
        int player = 20100, beside = FalloutGrid.Neighbor(player, 1)!.Value;
        var scorpion = CombatantState.FromMapCritter(CombatTests.Critter(beside, ScorpionPid, 6, 1, "scorpion"),
            CritterPrototypeData.Read(CombatTests.AntPrototype(pid: ScorpionPid)), "scorpion", poisonsOnHit: stings);
        var hero = new CombatantState("player", "p", null, 0, true, player, 0, DerivedCombatStats.ForCharacter(Special.Neutral), CombatWeapon.Unarmed(), hitPoints)
            { PoisonResistance = 25, Poison = poison };
        return new CombatSession(new[] { hero, scorpion }, new HexNavigation(Array.Empty<MapObject>(), 0), new CombatTests.Scripted(rolls), "scorpion");
    }

    public static void Run(Action<string, Action> check)
    {
        check("poison follows CE critterAdjustPoison and poisonEventProcess: resistance share, 10 x (505 - 5 x level) ticks, 1 HP and 2 levels per event", () =>
        {
            Assert(PoisonRules.Absorb(6, 25) == 5 && PoisonRules.Absorb(5, 25) == 4 && PoisonRules.Absorb(3, 25) == 3 && PoisonRules.Absorb(6, 0) == 6, "Resistance share.");
            Assert(PoisonRules.DelayTicks(5) == 4800 && PoisonRules.DelayTicks(1) == 5000 && PoisonRules.DelayTicks(101) == 1, "Event delay.");
            Assert(PoisonState.None.Add(5) == new PoisonState(5, 4800) && new PoisonState(5, 10).Add(0) == new PoisonState(5, 4800) && PoisonState.None.Add(0) == PoisonState.None,
                "Each dose re-queues the event at the new level.");
            Assert(new PoisonState(4, 100).Advance(99, out int none) == new PoisonState(4, 1) && none == 0, "Not yet due.");
            Assert(new PoisonState(4, 100).Advance(100, out int one) == new PoisonState(2, 4950) && one == 1, "Due: 1 HP, level 2, re-queued.");
            Assert(new PoisonState(3, 10).Advance(10 + PoisonRules.DelayTicks(1), out int two) == PoisonState.None && two == 2, "Level 3 costs 2 HP, then it is cured.");
            Assert(new PoisonState(4, 60).AdvanceRound(out int round) == new PoisonState(4, 10) && round == 0
                && new PoisonState(4, 50).AdvanceRound(out int due) == new PoisonState(2, 4950) && due == 1, "A combat round is 50 ticks.");
            Assert(PoisonState.None.Valid && !new PoisonState(0, 5).Valid && !new PoisonState(2, 0).Valid && !new PoisonState(2, 4951).Valid, "Valid states.");
        });
        check("a radscorpion hit rolls Luck - 1 on 1..10: a failure poisons for 1..6 less resistance; misses and ants never poison", () =>
        {
            var fight = Stung(new[] { 1, 1, 5, 6 });
            ProvisionalMeleeBehavior.TakeTurn(fight);
            var check = fight.Log.OfType<PoisonChecked>().Single();
            Assert(check is { LuckRoll: 5, Needed: 4, Poisoned: true, Rolled: 6, Absorbed: 5, Level: 5 } && fight.Get("player").Poison == new PoisonState(5, 4800), "Poisoned.");
            var shrugged = Stung(new[] { 1, 1, 4 });
            ProvisionalMeleeBehavior.TakeTurn(shrugged);
            Assert(shrugged.Log.OfType<PoisonChecked>().Single() is { Poisoned: false, LuckRoll: 4 } && !shrugged.Get("player").Poison.Active, "Luck held.");
            var missed = Stung(new[] { 99 });
            ProvisionalMeleeBehavior.TakeTurn(missed);
            Assert(!missed.Log.OfType<PoisonChecked>().Any(), "A miss does not sting.");
            var ant = Stung(new[] { 1, 1 }, stings: false);
            ProvisionalMeleeBehavior.TakeTurn(ant);
            Assert(!ant.Log.OfType<PoisonChecked>().Any() && ant.Log.OfType<AttackResolved>().Single().Hit, "Ants do not sting.");
        });
        check("poison acts at each round's end (5 s of game time) and can kill the player in a fight", () =>
        {
            var fight = Stung(new[] { 99 }, new PoisonState(4, 50));
            ProvisionalMeleeBehavior.TakeTurn(fight); fight.Execute(CombatActionProposal.EndTurn("player"));
            Assert(fight.Log.OfType<PoisonDamage>().Single() is { CombatantId: "player", Lost: 1, HitPoints: 29, Level: 2 } && fight.Get("player").Poison == new PoisonState(2, 4950),
                "Round 1 ends: 1 HP.");
            var lethal = Stung(new[] { 99 }, new PoisonState(2, 50), hitPoints: 1);
            ProvisionalMeleeBehavior.TakeTurn(lethal); lethal.Execute(CombatActionProposal.EndTurn("player"));
            Assert(lethal.Turn is { Over: true, WinningTeam: 1 } && lethal.Log.OfType<CombatantDied>().Single() is { CombatantId: "player", KillerId: CombatSession.PoisonKiller },
                "Killed by poison.");
        });
        check("the world carries poison out of the fight, runs it on exploration time, saves it and refuses forged levels and timers", () =>
        {
            var baseMap = WorldCombatTests.Map();
            var scorpion = CombatTests.Critter(20099, ScorpionPid, 1, 1, "map3:scorpion");
            var map = baseMap with { ObjectLayers = new[] { baseMap.ObjectLayers[0] with { Objects = baseMap.ObjectLayers[0].Objects.Append(scorpion).ToArray(),
                    DeclaredCount = baseMap.ObjectLayers[0].Objects.Length + 1 } }, Prototypes = baseMap.Prototypes.Append(CombatTests.AntPrototype(pid: ScorpionPid)).ToArray() };
            var world = new WorldSession(_ => map, new[] { 3, 126 });
            var fight = world.BeginCombat(WorldCombatTests.NearAnt(world, "map3:scorpion"), WorldCombatTests.Spear(), new CombatTests.Scripted(1, 1, 5, 6, 1, 11));
            ProvisionalMeleeBehavior.TakeTurn(fight);
            fight.Execute(CombatActionProposal.Attack("player", "map3:scorpion"));
            Assert(fight.Turn.Over && world.CommitCombat(fight) == CombatCommitStatus.Ready && world.Poison == new PoisonState(5, 4800) && world.HasCombatOutcome, "Poisoned out of the fight.");
            int hp = world.HitPoints;
            Assert(world.AdvanceTime(4799) is { HitPointsLost: 0, Died: false } && world.HitPoints == hp, "Not yet.");
            Assert(world.AdvanceTime(1) is { HitPointsLost: 1, Died: false } && world.HitPoints == hp - 1 && world.Poison == new PoisonState(3, 4900), "First event.");
            WorldCheckpoint saved = CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog")));
            WorldSession Load(WorldCheckpoint checkpoint) => WorldSession.Restore(_ => map, new[] { 3, 126 }, checkpoint, "catalog");
            Assert(saved.Combat!.Poison == new SavedPoison(3, 4900) && Load(saved).Poison == world.Poison, "Checkpoint round trip.");
            foreach (SavedPoison bad in new[] { new SavedPoison(0, 0), new SavedPoison(3, 0), new SavedPoison(3, 4901), new SavedPoison(-1, 10) })
                try { Load(saved with { Combat = saved.Combat with { Poison = bad } }); throw new Exception($"Forged poison accepted: {bad}."); }
                catch (InvalidDataException) { }
            var dying = Load(saved with { Combat = saved.Combat with { PlayerHitPoints = 1, Poison = new SavedPoison(2, 1) } });
            Assert(dying.AdvanceTime(1) is { Died: true, HitPointsLost: 1 } && dying.HitPoints == 1 && dying.Poison == new PoisonState(2, 1), "A lethal event is reported, not applied.");
            world.Reset();
            Assert(!world.Poison.Active && world.AdvanceTime(10000).HitPointsLost == 0, "Reset cures.");
        });
    }
}
