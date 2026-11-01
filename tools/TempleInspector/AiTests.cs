using System.Text;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;

internal static class AiTests
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static int Tile(int column, int row) => row * 200 + 199 - column;
    static int Walk(int from, int direction, int steps) { for (int i = 0; i < steps; i++) from = FalloutGrid.Neighbor(from, direction)!.Value; return from; }
    static readonly int Centre = Tile(100, 100);

    // The Rat packet's numbers (ai.txt [Rat]: max_dist 15, min_to_hit 0, min_hp 0, hurt_too_much blind, called_freq 1000000).
    internal static AiPacket Rat(int minHp = 0, int minToHit = 0, int calledFrequency = 1000000, int maxDistance = 15) =>
        new("Rat", 7, maxDistance, minToHit, minHp, 45, 0x40, 40, calledFrequency, 0, -1, -1, -1, -1, -1, -1, -1);

    static CombatantState Ant(int hex, string key, AiPacket ai, Func<DerivedCombatStats, DerivedCombatStats>? stats = null, int rotation = 4)
    {
        var source = CombatTests.Critter(hex, 0x0100017E, 6, 1, key);
        source = source with { Header = source.Header with { Rotation = rotation } };
        var ant = CombatantState.FromMapCritter(source, CritterPrototypeData.Read(CombatTests.AntPrototype()), key, ai: ai);
        return stats == null ? ant : new CombatantState(ant.Id, ant.Name, ant.Pid, ant.Team, false, ant.Hex, 0, stats(ant.Stats), ant.Weapon, ant.HitPoints, ant.Experience)
            { Ai = ai, KillType = ant.KillType, Rotation = ant.Rotation };
    }
    static CombatantState Hero(int hex = 0, CombatWeapon? weapon = null) =>
        new("player", "p", null, 0, true, hex == 0 ? Centre : hex, 0, DerivedCombatStats.ForCharacter(Special.Neutral), weapon ?? CombatWeapon.Unarmed(), 30);
    // Ends the player's turns until the given creature acts.
    static void Until(CombatSession fight, string id)
    {
        for (int guard = 0; guard < 10 && !fight.Turn.Over && fight.Active!.Id != id; guard++) fight.Execute(CombatActionProposal.EndTurn("player"));
    }
    static CombatSession Fight(IEnumerable<CombatantState> combatants, string initiator, params int[] rolls) =>
        new(combatants, new HexNavigation(Array.Empty<MapObject>(), 0), new CombatTests.Scripted(rolls), initiator);

    const string Packets = """
        ; comment line
        [Rat]
        aggression=45
        attack_end=50140
        attack_start=50140
        called_freq=1000000
        chance=0
        color=58
        font=101
        hurt_too_much=blind
        max_dist=15
        min_hp=0
        min_to_hit=0
        miss_end=50160
        miss_start=50160
        move_end=50120
        move_start=50120
        outline_color=55
        packet_num=7
        run_end=50100
        run_start=50100
        secondary_freq=40
        [Dumar]
        aggression=10
        attack_end=1
        attack_start=1
        called_freq=10
        chance=15
        color=1
        disposition=none
        distance=Charge
        font=1
        hurt_too_much=crippled_legs, blind
        max_dist=10
        min_hp=10
        min_to_hit=10
        miss_end=1
        miss_start=1
        move_end=1
        move_start=1
        outline_color=1
        packet_num=143
        run_away_mode=coward
        run_end=1
        run_start=1
        secondary_freq=5000
        [Copy]
        aggression=0
        attack_end=1
        attack_start=1
        called_freq=1
        chance=0
        color=1
        font=1
        max_dist=1
        min_hp=0
        min_to_hit=0
        miss_end=1
        miss_start=1
        move_end=1
        move_start=1
        outline_color=1
        packet_num=7
        run_end=1
        run_start=1
        secondary_freq=1
        """;

    public static void Run(Action<string, Action> check)
    {
        check("ai.txt packets parse as CE aiInit: required integers, hurt_too_much flags, enumerated keys with CE's off-by-one, first packet number wins", () =>
        {
            var packets = AiPacketCatalog.Read(Encoding.Latin1.GetBytes(Packets));
            Assert(packets[7] == Rat() && packets.Count == 2, "Rat, and the later packet 7 ignored.");
            Assert(packets[143] is { MaxDistance: 10, MinToHit: 10, MinHitPoints: 10, CalledFrequency: 10, Chance: 15, Distance: AiPacket.Charge, HurtTooMuch: 0x4C,
                RunAwayMode: 0, Disposition: -1, AttackWho: -1 }, "Dumar-like packet.");
            try { AiPacketCatalog.Read(Encoding.Latin1.GetBytes(Packets.Replace("max_dist=15", ""))); throw new Exception("Missing max_dist accepted."); }
            catch (InvalidDataException) { }
        });
        check("a creature closes in along the CE pathfinder's route to the player's hex, drops the last step and strikes; the plain blow names the torso", () =>
        {
            int start = Walk(Centre, 1, 3);
            var fight = Fight(new[] { Hero(), Ant(start, "ant", Rat()) }, "ant", 1, 1);
            CombatAi.TakeTurn(fight);
            int[] route = OriginalPathfinder.FindPath(start, Centre, _ => false, inCombat: true, checkDestination: false)!;
            int[] expected = route.Take(2).Aggregate(new List<int>(), (hexes, r) => { hexes.Add(OriginalPathfinder.TileInDirection(hexes.Count == 0 ? start : hexes[^1], r, 1)); return hexes; }).ToArray();
            Assert(fight.Log.OfType<CombatantMoved>().Single().Path.SequenceEqual(expected), "The CE route, minus the occupied last hex.");
            Assert(fight.Log.OfType<AttackResolved>().Single() is { AttackerId: "ant", Location: HitLocation.Torso, ActionPointsSpent: 3, Chance: 60 }
                && fight.Active!.IsPlayer && fight.Get("ant").WhoHitMe == "player", "One blow with the 3 AP left, then the turn ends.");
            // Out of AP after closing in: no blow, no stall.
            var far = Fight(new[] { Hero(), Ant(Walk(Centre, 1, 4), "ant", Rat()) }, "ant");
            CombatAi.TakeTurn(far);
            Assert(far.Log.OfType<CombatantMoved>().Single().Path.Length == 3 && !far.Log.OfType<AttackResolved>().Any() && far.Active!.IsPlayer, "Closed in, then ended the turn.");
        });
        check("called shots follow _ai_called_shot: one draw in called_freq while an aimed blow is affordable, IN 5 picks a location, min_to_hit read with CE's swapped arguments", () =>
        {
            int beside = Walk(Centre, 1, 1);
            Func<DerivedCombatStats, DerivedCombatStats> smart = s => s with { Special = s.Special with { Intelligence = 5 } };
            var aimed = Fight(new[] { Hero(), Ant(beside, "ant", Rat(calledFrequency: 1), smart) }, "ant", 1, 0, 1, 1);
            CombatAi.TakeTurn(aimed);
            Assert(aimed.Log.OfType<AttackResolved>().Single() is { Location: HitLocation.Head, ActionPointsSpent: 4, Chance: 40 }, "Head: +1 AP, -20 to hit for an unarmed blow.");
            // The check reads the right leg's chance (65 - 5 - 10 = 50) whatever the location: above min_to_hit 50 it stays, at 51 it is the torso.
            var careful = Fight(new[] { Hero(), Ant(beside, "ant", Rat(minToHit: 51, calledFrequency: 1), smart) }, "ant", 1, 6, 1, 1);
            CombatAi.TakeTurn(careful);
            Assert(careful.Log.OfType<AttackResolved>().Single() is { Location: HitLocation.Torso, ActionPointsSpent: 3 }, "Below min_to_hit: torso, after both draws.");
            var plain = Fight(new[] { Hero(), Ant(beside, "ant", Rat(calledFrequency: 1)) }, "ant", 1, 1, 1);
            CombatAi.TakeTurn(plain);
            Assert(plain.Log.OfType<AttackResolved>().Single().Location == HitLocation.Torso, "IN 1: no location draw.");
        });
        check("too hurt (min_hp, hurt_too_much) or unable to hit, a creature flees each turn, disengages past max_dist and leaves the fight at the round's end", () =>
        {
            var fight = Fight(new[] { Hero(), Ant(Walk(Centre, 1, 3), "ant", Rat(minHp: 7)) }, "ant");
            var ant = fight.Get("ant");
            CombatAi.TakeTurn(fight);
            Assert(fight.Log.OfType<CombatantFleeing>().Single() is { CombatantId: "ant", Reason: "below min_hp" } && ant.Fleeing
                && FalloutGrid.Distance(ant.Hex, Centre) == 8 && fight.Active!.IsPlayer, "Ran 5 hexes straight away.");
            Assert(fight.Preview(CombatActionProposal.LeaveCombat("player")).Status == CombatActionStatus.Ready, "A fleeing creature wants to stop.");
            Until(fight, "ant"); CombatAi.TakeTurn(fight);
            Until(fight, "ant"); CombatAi.TakeTurn(fight);
            Assert(FalloutGrid.Distance(ant.Hex, Centre) == 18 && !ant.Disengaging && fight.Log.OfType<CombatantFleeing>().Count() == 1, "Still fleeing, below max_dist.");
            Until(fight, "ant"); CombatAi.TakeTurn(fight);
            Assert(FalloutGrid.Distance(ant.Hex, Centre) == 18, "Past max_dist: disengaging, no step.");
            Assert(ant.Disengaged && !ant.InFight && fight.Turn is { Over: true, WinningTeam: 0 } && fight.Log.OfType<CombatantDisengaged>().Single().HitPoints == 6,
                "Gone at the round's end; the fight is over.");
            var blind = Fight(new[] { Hero(), Ant(Walk(Centre, 1, 3), "ant", Rat()) }, "ant");
            blind.Get("ant").SetConditions(DamageFlags.Blind);
            CombatAi.TakeTurn(blind);
            Assert(blind.Log.OfType<CombatantFleeing>().Single().Reason == "hurt_too_much", "Blind is what the Rat packet fears.");
            var hopeless = Fight(new[] { Hero(), Ant(Walk(Centre, 1, 1), "ant", Rat(minToHit: 95)) }, "ant");
            CombatAi.TakeTurn(hopeless);
            Assert(hopeless.Log.OfType<CombatantFleeing>().Single().Reason == "cannot possibly hit" && !hopeless.Log.OfType<AttackResolved>().Any(), "Below min_to_hit even point blank.");
        });
        check("whoHitMe bookkeeping: creatures hold the player, the player its target; a friend's death unseen by its killer sends an onlooker 10 hexes from the corpse", () =>
        {
            // The near ant (east, 2 hexes) dies to the spear; the far ant (11 hexes east, facing west) sees it fall but not the player.
            int near = Walk(Centre, 1, 2), far = Walk(Centre, 1, 11);
            var fight = Fight(new[] { Hero(weapon: WorldCombatTests.Spear()), Ant(near, "near", Rat()), Ant(far, "far", Rat()) }, "player", 1, 11);
            var watcher = fight.Get("far");
            Assert(watcher.WhoHitMe == "player" && watcher.Engaging && fight.Get("player").WhoHitMe == null, "Every creature that noticed the player holds it.");
            watcher.Engaging = false;
            fight.Execute(CombatActionProposal.Attack("player", "near"));
            Assert(!fight.Get("near").Alive && fight.Get("player").WhoHitMe == "near" && fight.Get("near").WhoHitMe == "player"
                && watcher.Engaging && watcher.FriendlyDead == "near", "The watcher remembers the corpse.");
            fight.Execute(CombatActionProposal.EndTurn("player"));
            Assert(fight.Active == watcher, "The watcher's turn.");
            CombatAi.TakeTurn(fight);
            // It closes in on the player with all its AP; none are left to keep away from the corpse, so it still remembers it.
            Assert(fight.Log.OfType<CombatantMoved>().Single(m => m.CombatantId == "far").Path.Length == 5 && watcher.FriendlyDead == "near", "Moved toward its danger.");
            Assert(CombatAi.Rating(fight.Get("player")) == 10 + fight.Get("player").EffectiveArmorClass && CombatAi.Rating(fight.Get("near")) == 0, "_combatai_rating.");
        });
        check("the world gives temple creatures their script packets (ants Rat 7, radscorpions Scorpion 8, Cameron Dumar 143) and keeps a disengaged creature as a survivor", () =>
        {
            var packets = new Dictionary<int, AiPacket> { [7] = Rat(minHp: 7), [8] = Rat() with { Name = "Scorpion", Number = 8 } };
            var world = new WorldSession(_ => WorldCombatTests.Map(), new[] { 3, 126 }, aiPackets: packets);
            var fight = world.BeginCombat(WorldCombatTests.NearAnt(world, "map3:far-ant"), WorldCombatTests.Spear(), new CombatTests.Scripted());
            var ant = fight.Get("map3:far-ant");
            Assert(ant.Ai == packets[7] && fight.Get("player").Ai == null, "Rat packet from zclrat.");
            for (int guard = 0; guard < 20 && !fight.Turn.Over; guard++)
                if (fight.Active!.IsPlayer) fight.Execute(CombatActionProposal.EndTurn("player")); else CombatAi.TakeTurn(fight);
            Assert(fight.Turn.Over && ant.Disengaged && world.CommitCombat(fight) == CombatCommitStatus.Ready
                && world.Survivors.Single() is { Key: "map3:far-ant", HitPoints: 6 } s && s.Hex == ant.Hex && world.Defeated.Count == 0, "Survivor where it stopped.");
            Assert(new WorldSession(_ => WorldCombatTests.Map(), new[] { 3, 126 }).BeginCombat(WorldCombatTests.NearAnt(world, "map3:far-ant"), WorldCombatTests.Spear(),
                new CombatTests.Scripted()).Get("map3:far-ant").Ai == null, "Without ai.txt, the provisional behavior stays.");
            try { new WorldSession(_ => WorldCombatTests.Map(), new[] { 3, 126 }, aiPackets: new Dictionary<int, AiPacket>()).BeginCombat(WorldCombatTests.NearAnt(world, "map3:far-ant"),
                    WorldCombatTests.Spear(), new CombatTests.Scripted()); throw new Exception("A missing packet was accepted."); }
            catch (InvalidDataException) { }
            Assert(WorldSession.ScriptPackets[0x0100017E] == 7 && WorldSession.ScriptPackets[0x01000005] == 8 && WorldSession.CameronPacket == 143, "Script packets.");
        });
    }
}
