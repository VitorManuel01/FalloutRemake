using System.Buffers.Binary;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;
using static FalloutRemake.Runtime.Combat.DamageFlags;

internal static class CriticalTests
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    const long Day1 = CriticalRules.TicksPerDay, Day6 = 6 * CriticalRules.TicksPerDay;
    const int Centre = 20100;
    static readonly int Beside = FalloutGrid.Neighbor(Centre, 1)!.Value;

    // The fixture ant (ST 1, EN 1, AG 4, LK 1, 6 HP, 5 AP, AC 4, Critical Chance 1, Unarmed 65) with the PRO kill type set.
    static Prototype AntOfKind(int killType)
    {
        var bytes = (byte[])CombatTests.AntPrototype().RawBytes.Clone();
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(408, 4), killType);
        return PrototypeCatalog.Read(new GameResource("proto/critters/00000382.pro", "synthetic", bytes), 0x0100017E);
    }
    static CombatantState Ant(int killType = 17, Func<DerivedCombatStats, DerivedCombatStats>? stats = null)
    {
        var ant = CombatantState.FromMapCritter(CombatTests.Critter(Beside, 0x0100017E, 6, 1, "ant"), CritterPrototypeData.Read(AntOfKind(killType)), "ant");
        return stats == null ? ant : new CombatantState(ant.Id, ant.Name, ant.Pid, ant.Team, false, ant.Hex, 0, stats(ant.Stats), ant.Weapon, ant.HitPoints, ant.Experience)
            { KillType = ant.KillType };
    }
    // Neutral player (Unarmed 50, so 46% against the ant; Critical Chance 5, LK 5, 30 HP, 7 AP, AC 5) with bare hands (1..3).
    static CombatantState Hero(CombatWeapon? weapon = null, DamageFlags conditions = None, Func<DerivedCombatStats, DerivedCombatStats>? stats = null)
    {
        var sheet = DerivedCombatStats.ForCharacter(Special.Neutral);
        return new("player", "p", null, 0, true, Centre, 0, stats?.Invoke(sheet) ?? sheet, weapon ?? CombatWeapon.Unarmed(), 30) { Conditions = conditions };
    }
    static CombatSession Fight(CombatantState hero, CombatantState ant, long time, string initiator, CombatTests.Scripted random) =>
        new(new[] { hero, ant }, new HexNavigation(Array.Empty<MapObject>(), 0), random, initiator, time);

    public static void Run(Action<string, Action> check)
    {
        check("critical tables, effect and failure columns follow CE: torso rows by kill type, the player's own table, day 1 and day 6 gates", () =>
        {
            Assert(CriticalRules.Torso(false, 17, 4) == new CriticalEffect(6, KnockedOut | Bypass, -1, 0, None, 6904, 5000)
                && CriticalRules.Torso(false, 0, 5) == new CriticalEffect(6, Dead, -1, 0, None, 5021, 5000)
                && CriticalRules.Torso(false, 6, 4) == new CriticalEffect(4, Bypass, 5, 0, KnockedDown, 5605, 5606)
                && CriticalRules.Torso(true, 6, 5) == new CriticalEffect(4, KnockedDown | Bypass, 6, 2, Dead, 6503, 6513), "Torso rows.");
            Assert(new[] { 20, 21, 45, 46, 70, 71, 90, 91, 100, 101, -10, 160 }.Select(CriticalRules.Effect).SequenceEqual(new[] { 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 0, 5 }), "Effect bands.");
            Assert(new[] { 20, 21, 50, 51, 75, 76, 95, 96, 140 }.Select(CriticalRules.FailureEffect).SequenceEqual(new[] { 0, 1, 1, 2, 2, 3, 3, 4, 4 }), "Failure bands.");
            Assert(CriticalRules.Failure(-1, 4) == CrippleRandom && CriticalRules.Failure(0, 3) == (KnockedDown | HurtSelf) && CriticalRules.Failure(1, 4) == HitSelf
                && CriticalRules.Failure(-1, 0) == None, "Failure table.");
            Assert(!CriticalRules.CriticalsAllowed(CriticalRules.GameStartTicks) && !CriticalRules.CriticalsAllowed(Day1 - 1) && CriticalRules.CriticalsAllowed(Day1)
                && !CriticalRules.PlayerFailuresAllowed(Day6 - 1) && CriticalRules.PlayerFailuresAllowed(Day6), "Day gates.");
            Assert(CriticalRules.KnockoutTicks(1) == 320 && CriticalRules.KnockoutTicks(10) == 50, "Knockout delay.");
            Assert(CriticalRules.ActionPointsPerHex(None) == 1 && CriticalRules.ActionPointsPerHex(CrippledRightLeg) == 4 && CriticalRules.ActionPointsPerHex(CrippledLegs) == 8,
                "Crippled legs.");
            foreach (Action bad in new Action[] { () => CriticalRules.Torso(false, 19, 0), () => CriticalRules.Torso(false, 0, 6), () => CriticalRules.Failure(7, 0),
                () => CriticalRules.RandomCripple(4), () => _ = new CombatantState("x", "x", null, 1, false, Centre, 0, DerivedCombatStats.ForCharacter(Special.Neutral), CombatWeapon.Unarmed(), 1) { KillType = 19 },
                () => _ = Hero(conditions: Dead) })
                try { bad(); throw new Exception("Out-of-range critical input accepted."); } catch (ArgumentOutOfRangeException) { }
        });
        check("before game day 1 no critical roll is drawn; from day 1 a hit turns critical on margin / 10 + Critical Chance, with massive checks and Better Criticals", () =>
        {
            var plain = new CombatTests.Scripted(1, 1);
            var early = Fight(Hero(), Ant(), CriticalRules.GameStartTicks, "player", plain);
            early.Execute(CombatActionProposal.Attack("player", "ant"));
            Assert(plain.Left == 0 && !early.Log.OfType<CriticalHit>().Any() && early.Get("ant").HitPoints == 5, "Day 0: hit and damage rolls only.");

            // Hit roll 1 against 41% (AC 4 plus the 5 AP the ant holds): margin 40, so a second d100 of 9 or less (40 / 10 + 5) is critical.
            var rolls = new CombatTests.Scripted(1, 9, 91, 1);
            var fight = Fight(Hero(), Ant(), Day1, "player", rolls);
            fight.Execute(CombatActionProposal.Attack("player", "ant"));
            var ant = fight.Get("ant");
            Assert(rolls.Left == 0 && fight.Log.OfType<CriticalHit>().Single() is { Roll: 9, Needed: 9, EffectRoll: 91, Effect: 4, Multiplier: 6, StatRoll: 0, Massive: false, Message: 6904 } hit
                && hit.Flags == (KnockedOut | Bypass), "Giant Ant torso, effect 4.");
            Assert(fight.Log.OfType<AttackResolved>().Single() is { DamageRoll: 1, Damage: 3, TargetHitPoints: 3 } && ant.KnockedOut && ant.Prone && ant.WakeAt == Day1 + 320,
                "x6 then halved; knocked out for 10 x (35 - 3 x EN) ticks.");
            Assert(fight.Preview(CombatActionProposal.Attack("player", "ant")).ToHit is { ProneTarget: 40, Chance: 81 }, "+40 against a prone target (50 - AC 4 - its 5 unspent AP + 40).");
            var missed = Fight(Hero(), Ant(), Day1, "player", new CombatTests.Scripted(1, 10, 1));
            missed.Execute(CombatActionProposal.Attack("player", "ant"));
            Assert(!missed.Log.OfType<CriticalHit>().Any() && missed.Get("ant").HitPoints == 5, "One above the threshold stays a normal hit.");

            // Radscorpion row 4: AG + 0 on d10; the fixture's AG 4 fails on 5 and the knockdown is added.
            var massive = Fight(Hero(), Ant(6), Day1, "player", new CombatTests.Scripted(1, 9, 91, 5, 1));
            massive.Execute(CombatActionProposal.Attack("player", "ant"));
            Assert(massive.Log.OfType<CriticalHit>().Single() is { StatRoll: 5, Massive: true, Message: 5606, Multiplier: 4 } m && m.Flags == (Bypass | KnockedDown)
                && massive.Get("ant") is { HitPoints: 4, Prone: true, KnockedOut: false }, "Massive critical.");
            var held = Fight(Hero(), Ant(6), Day1, "player", new CombatTests.Scripted(1, 9, 91, 4, 1));
            held.Execute(CombatActionProposal.Attack("player", "ant"));
            Assert(held.Log.OfType<CriticalHit>().Single() is { Massive: false, Message: 5605 } h && h.Flags == Bypass && !held.Get("ant").Prone, "Stat held.");

            // Heavy Handed: -30 Better Criticals moves 91 to 61, row 2.
            var heavy = Fight(Hero(stats: s => s with { BetterCriticals = -30 }), Ant(), Day1, "player", new CombatTests.Scripted(1, 9, 91, 1));
            heavy.Execute(CombatActionProposal.Attack("player", "ant"));
            Assert(heavy.Log.OfType<CriticalHit>().Single() is { EffectRoll: 61, Effect: 2, Multiplier: 4 } && heavy.Get("ant") is { HitPoints: 4, Prone: true }, "Better Criticals.");

            // The player's own table: row 5 kills when d10 beats LK + 2.
            var lethal = Fight(Hero(), Ant(stats: s => s with { BetterCriticals = 20 }), Day1, "ant", new CombatTests.Scripted(1, 6, 100, 8, 1));
            ProvisionalMeleeBehavior.TakeTurn(lethal);
            Assert(lethal.Log.OfType<CriticalHit>().Single() is { Needed: 6, EffectRoll: 120, Effect: 5, Massive: true, Message: 6513 } d && (d.Flags & Dead) != 0
                && lethal.Get("player").HitPoints == 0 && lethal.Turn is { Over: true, WinningTeam: 1 }
                && lethal.Log.OfType<CombatantDied>().Single() is { CombatantId: "player", KillerId: "ant" }, "Instant death whatever the damage.");
        });
        check("knocked down and out follow CE _combat_turn: lost turns, the wake-up after the round's 5 s, rejoining at the round's end and standing up for 3 AP", () =>
        {
            // The player's table, row 5 without the massive death: knocked down, then up on its turn.
            var down = Fight(Hero(), Ant(stats: s => s with { BetterCriticals = 20 }), Day1, "ant", new CombatTests.Scripted(1, 6, 100, 7, 1));
            ProvisionalMeleeBehavior.TakeTurn(down);
            var player = down.Get("player");
            Assert(down.Active == player && player.HitPoints == 28 && !player.Prone && player.ActionPoints == 4
                && down.Log.OfType<StoodUp>().Single() is { CombatantId: "player", ActionPointsSpent: 3 }, "Standing up costs 3 AP.");

            // The ant (Sequence 20, so normally first) knocked out for 320 ticks: seven rounds, then it rejoins after the player.
            var fight = Fight(Hero(), Ant(stats: s => s with { Sequence = 20 }), Day1, "player", new CombatTests.Scripted(1, 9, 91, 1));
            fight.Execute(CombatActionProposal.Attack("player", "ant"));
            var ant = fight.Get("ant"); player = fight.Get("player");
            Assert(!fight.AwareEnemies(player).Any() && fight.Preview(CombatActionProposal.LeaveCombat("player")).Status == CombatActionStatus.Ready, "The knocked out perceive nothing.");
            for (int guard = 0; guard < 20 && !fight.Log.OfType<WokeUp>().Any(); guard++) fight.Execute(CombatActionProposal.EndTurn("player"));
            Assert(fight.Log.OfType<TurnLost>().Count(t => t.CombatantId == "ant" && t.Reason == KnockedOut && t.BonusArmorClass == 5) == 7
                && fight.GameTime == Day1 + 350 && fight.Turn.Round == 8 && fight.Turn.Order.SequenceEqual(new[] { "player", "ant" }) && fight.Active == player,
                "Seven lost turns; woke at the end of round 7 and waits for the round's end.");
            Assert(ant.Conditions == KnockedDown && ant.Rejoining && fight.AwareEnemies(player).Single() == ant
                && fight.Preview(CombatActionProposal.LeaveCombat("player")).Status == CombatActionStatus.EnemiesAware, "Woken: knocked down and wanting to rejoin.");
            fight.Execute(CombatActionProposal.EndTurn("player"));
            Assert(fight.Active == ant && ant.ActionPoints == 2 && ant.Conditions == None && fight.Log.OfType<StoodUp>().Last() is { CombatantId: "ant", ActionPointsSpent: 3 },
                "Its late turn: up for 3 of 5 AP.");
            fight.Execute(CombatActionProposal.EndTurn("ant"));
            Assert(fight.Turn.Round == 9 && fight.Turn.Order.SequenceEqual(new[] { "ant", "player" }) && !ant.Rejoining, "Back in its sorted place.");
            try { Fight(Hero(conditions: KnockedDown), Ant(), Day1, "player", new CombatTests.Scripted()); throw new Exception("A prone fighter was accepted."); }
            catch (InvalidDataException) { }
        });
        check("critical failures: creatures from day 1 and the player from day 6, by the weapon's column; lost turns, knockdown, random cripples and self hits", () =>
        {
            // The ant misses (99 against 53%: the player's AC 5 plus its 7 unspent AP): margin -46, so 1..4 fails critically; LK 1 adds 20 to the column roll.
            var lose = Fight(Hero(), Ant(), Day1, "ant", new CombatTests.Scripted(99, 3, 40));
            ProvisionalMeleeBehavior.TakeTurn(lose);
            Assert(lose.Log.OfType<CriticalFailure>().Single() is { AttackerId: "ant", Roll: 3, Needed: 4, EffectRoll: 60, Effect: 2, Flags: LoseTurn, SelfDamage: 0 }
                && lose.Active!.IsPlayer, "Lost the rest of its turn.");
            // Round 2 sorts the player (Sequence 10) before the ant (9): its lost turn follows the player's.
            lose.Execute(CombatActionProposal.EndTurn("player"));
            Assert(!lose.Log.OfType<TurnLost>().Any() && lose.Turn.Round == 2, "Round 2, the player first.");
            lose.Execute(CombatActionProposal.EndTurn("player"));
            Assert(lose.Log.OfType<TurnLost>().Single() is { CombatantId: "ant", Reason: LoseTurn } && lose.Active!.IsPlayer && lose.Turn.Round == 3
                && lose.Get("ant").Conditions == None, "And the next one.");
            lose.Execute(CombatActionProposal.EndTurn("player"));
            Assert(lose.Active!.Id == "ant" && lose.Log.OfType<TurnStarted>().Last().CombatantId == "ant", "The turn after is its own again.");
            var fall = Fight(Hero(), Ant(), Day1, "ant", new CombatTests.Scripted(99, 3, 56));
            ProvisionalMeleeBehavior.TakeTurn(fall);
            Assert(fall.Log.OfType<CriticalFailure>().Single().Flags == (KnockedDown | HurtSelf) && fall.Log.OfType<StoodUp>().Single() is { CombatantId: "ant", ActionPointsSpent: 2 }
                && fall.Get("ant") is { Conditions: None, HitPoints: 6 }, "Fell and got up at once with its last 2 AP; HurtSelf does nothing.");
            var limp = Fight(Hero(), Ant(), Day1, "ant", new CombatTests.Scripted(99, 3, 76, 0));
            ProvisionalMeleeBehavior.TakeTurn(limp);
            Assert(limp.Get("ant") is { Conditions: CrippledLeftLeg, ActionPointsPerHex: 4 } && limp.Log.OfType<CriticalFailure>().Single().Flags == CrippledLeftLeg, "Random cripple.");
            var plain = new CombatTests.Scripted(99, 5);
            ProvisionalMeleeBehavior.TakeTurn(Fight(Hero(), Ant(), Day1, "ant", plain));
            Assert(plain.Left == 0, "5 is above the failure threshold: a plain miss.");

            // The player (96 against 46%: margin -50, threshold 5) only suffers from day 6.
            var spared = new CombatTests.Scripted(96, 5);
            var early = Fight(Hero(), Ant(), Day1, "player", spared);
            early.Execute(CombatActionProposal.Attack("player", "ant"));
            Assert(spared.Left == 0 && !early.Log.OfType<CriticalFailure>().Any(), "Day 1: a plain miss for the player.");
            var arm = Fight(Hero(), Ant(), Day6, "player", new CombatTests.Scripted(96, 5, 96, 3));
            arm.Execute(CombatActionProposal.Attack("player", "ant"));
            Assert(arm.Get("player").Conditions == CrippledRightArm && arm.Preview(CombatActionProposal.Attack("player", "ant")).Status == CombatActionStatus.Ready,
                "Bare hands still strike with a crippled arm.");
            var self = Fight(Hero(CombatWeapon.Unarmed() with { CriticalFailureType = 1 }), Ant(), Day6, "player", new CombatTests.Scripted(96, 5, 96, 3));
            self.Execute(CombatActionProposal.Attack("player", "ant"));
            Assert(self.Log.OfType<CriticalFailure>().Single() is { Flags: HitSelf, SelfDamageRoll: 3, SelfDamage: 3, HitPoints: 27 } && self.Get("player").HitPoints == 27, "Hit itself x2.");
            var nothing = Fight(Hero(CombatWeapon.Unarmed() with { CriticalFailureType = 1 }), Ant(), Day6, "player", new CombatTests.Scripted(96, 5, 20));
            nothing.Execute(CombatActionProposal.Attack("player", "ant"));
            Assert(nothing.Log.OfType<CriticalFailure>().Single() is { Effect: 0, Flags: None } && nothing.Get("player").Conditions == None, "An empty column is a plain miss.");

            // Crippled arms refuse carried weapons (both arms, or one with a two-handed weapon); crippled legs make steps dearer.
            CombatWeapon spear = WorldCombatTests.Spear(), twoHanded = spear with { TwoHanded = true };
            CombatActionStatus Strike(CombatWeapon weapon, DamageFlags conditions) =>
                Fight(Hero(weapon, conditions), Ant(), Day1, "player", new CombatTests.Scripted()).Preview(CombatActionProposal.Attack("player", "ant")).Status;
            Assert(Strike(twoHanded, CrippledLeftArm) == CombatActionStatus.ArmCrippled && Strike(spear, CrippledLeftArm) == CombatActionStatus.Ready
                && Strike(spear, CrippledArms) == CombatActionStatus.ArmCrippled && Strike(CombatWeapon.Unarmed(), CrippledArms) == CombatActionStatus.Ready, "Arms.");
            int two = FalloutGrid.Neighbor(FalloutGrid.Neighbor(Centre, 4)!.Value, 4)!.Value;
            CombatActionPreview Walk(DamageFlags legs) => Fight(Hero(conditions: legs), Ant(), Day1, "player", new CombatTests.Scripted()).Preview(CombatActionProposal.Move("player", two));
            Assert(Walk(None).ActionPointCost == 2 && Walk(CrippledRightLeg) is { ActionPointCost: 8, Status: CombatActionStatus.NotEnoughActionPoints }
                && Walk(CrippledLegs).ActionPointCost == 16, "Legs.");
            var hobbled = Hero(conditions: CrippledLeftLeg);
            Assert(Fight(hobbled, Ant(), Day1, "player", new CombatTests.Scripted()) != null && hobbled.StepsAffordable(5) == 1, "Steps the AP pay for.");
        });
        check("the world runs the game clock through exploration and fights, carries crippled limbs, saves both and refuses forged values", () =>
        {
            WorldSession Load(WorldCheckpoint checkpoint) => WorldSession.Restore(_ => WorldCombatTests.Map(), new[] { 3, 126 }, checkpoint, "catalog");
            var world = new WorldSession(_ => WorldCombatTests.Map(), new[] { 3, 126 });
            Assert(world.GameTime == CriticalRules.GameStartTicks && world.Capture("catalog").Combat!.GameTime == null, "The clock starts at 302400 and is not stored there.");
            world.AdvanceTime(100);
            world.AdvanceTime((int)(Day1 - world.GameTime));
            Assert(world.GameTime == Day1, "Exploration ticks.");
            var fight = world.BeginCombat(new CombatEncounter(world.Location, new[] { "map3:far-ant" }, "player"), WorldCombatTests.Spear(), new CombatTests.Scripted());
            Assert(fight.GameTime == Day1, "The fight opens on the world's clock.");
            for (int round = 0; round < 2; round++) { fight.Execute(CombatActionProposal.EndTurn("player")); ProvisionalMeleeBehavior.TakeTurn(fight); }
            Assert(fight.GameTime == Day1 + 100 && fight.Execute(CombatActionProposal.LeaveCombat("player")).Status == CombatActionStatus.Ready
                && world.CommitCombat(fight) == CombatCommitStatus.Ready && world.GameTime == Day1 + 100, "Two rounds are 10 s of game time.");
            byte[] encoded = CheckpointFile.Encode(world.Capture("catalog"));
            WorldCheckpoint saved = CheckpointFile.Decode(encoded);
            Assert(saved.Combat!.GameTime == Day1 + 100 && Load(saved).GameTime == Day1 + 100 && !System.Text.Encoding.UTF8.GetString(encoded).Contains("\"Crippled\""),
                "Clock round trip; healthy survivors write no limbs.");
            // Crippled limbs: the player's and a survivor's, into the next fight and back out.
            SurvivingCritter survivor = saved.Combat.Survivors!.Single();
            var hurt = Load(CheckpointFile.Decode(CheckpointFile.Encode(saved with { Combat = saved.Combat with { PlayerCrippled = (int)CrippledLeftLeg,
                Survivors = new[] { survivor with { Crippled = CrippledRightArm } } } })));
            Assert(hurt.PlayerCrippled == CrippledLeftLeg && hurt.Survivors.Single().Crippled == CrippledRightArm && hurt.HasCombatOutcome, "Restored limbs.");
            var again = hurt.BeginCombat(new CombatEncounter(hurt.Location, new[] { "map3:far-ant" }, "player"), WorldCombatTests.Spear(), new CombatTests.Scripted());
            Assert(again.Get("player").Conditions == CrippledLeftLeg && again.Get("map3:far-ant").Conditions == CrippledRightArm, "Carried into the fight.");
            again.Execute(CombatActionProposal.LeaveCombat("player"));
            Assert(hurt.CommitCombat(again) == CombatCommitStatus.Ready && hurt.PlayerCrippled == CrippledLeftLeg && hurt.Survivors.Single().Crippled == CrippledRightArm, "And out of it.");
            var forged = new[]
            {
                saved.Combat with { GameTime = CriticalRules.GameStartTicks }, saved.Combat with { GameTime = 0 }, saved.Combat with { GameTime = CriticalRules.GameEndTicks },
                saved.Combat with { PlayerCrippled = 0 }, saved.Combat with { PlayerCrippled = (int)KnockedDown }, saved.Combat with { PlayerCrippled = (int)(CrippledLeftLeg | LoseTurn) },
                saved.Combat with { Survivors = new[] { survivor with { Crippled = KnockedOut } } }
            };
            foreach (SavedCombat bad in forged)
                try { Load(saved with { Combat = bad }); throw new Exception($"Forged clock or limbs accepted: {bad}."); }
                catch (InvalidDataException) { }
            hurt.Reset();
            Assert(hurt.GameTime == CriticalRules.GameStartTicks && hurt.PlayerCrippled == None && !hurt.HasCombatOutcome, "Reset.");
        });
    }
}
