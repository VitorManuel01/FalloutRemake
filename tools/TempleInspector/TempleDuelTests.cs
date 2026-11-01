using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;

internal static class TempleDuelTests
{
    const int CameronPid = 16777490, ChestTile = 20132;
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Reject(Action action, string message) { try { action(); } catch (InvalidDataException) { return; } throw new Exception(message); }

    // Dumar/Cameron as the Temple PRO gives him: SPECIAL 8 5 5 5 5 8 5, 40 HP, 9 AP, AC 8, MD 3, Unarmed 70, 100 XP, team 1.
    static Prototype Cameron()
    {
        var bytes = new byte[416];
        void Put(int offset, int value) => BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(offset, 4), value);
        Put(0, CameronPid); Put(28, -1); Put(40, 1);
        int[] special = { 8, 5, 5, 5, 5, 8, 5 };
        for (int i = 0; i < 7; i++) Put(48 + 4 * i, special[i]);
        Put(48 + 4 * 7, 40); Put(48 + 4 * 8, 9); Put(48 + 4 * 9, 8); Put(48 + 4 * 11, 3); Put(48 + 4 * 13, 15); Put(48 + 4 * 15, 5);
        Put(328 + 4 * 3, 8); Put(404, 100);
        return PrototypeCatalog.Read(new GameResource("proto/critters/00000274.pro", "synthetic", bytes), CameronPid);
    }

    // The Speech fixture's elevation 2 (gate, Cameron, exit) plus the challenge chest, and a pot whose item the actor carries.
    static (WorldSession World, TempleNpcDepartureProfile Profile, MapDocument Map) Fixture(int cameronHp = 22, PlayerCharacter? character = null)
    {
        var (speechMap, _, _) = WorldTempleSpeechTests.Fixture(false);
        MapObject owner = ItemTransferTests.Map().ObjectLayers[0].Objects[0];
        var level2 = speechMap.ObjectLayers.Single(l => l.Elevation == 2);
        MapObject actor = level2.Objects.Single(o => o.Offset == 425776);
        actor = actor with { State = new Dictionary<string, int> { ["hp"] = cameronHp } };
        MapObject chest = owner with { Key = speechMap.Source.Sha256 + ":465604", Offset = 465604, ContainerElevation = 2,
            Header = owner.Header with { Id = 100, Pid = WorldSession.TempleChestPid, Tile = ChestTile, Elevation = 2 },
            Inventory = Array.Empty<InventoryEntry>(), InventoryCount = 0 };
        var objects2 = level2.Objects.Select(o => o.Offset == 425776 ? actor : o).Append(chest).ToArray();
        var layers = speechMap.ObjectLayers.Select(l => l.Elevation == 2 ? l with { Objects = objects2, DeclaredCount = objects2.Length }
            : l with { Objects = l.Objects.Append(owner).ToArray(), DeclaredCount = l.Objects.Length + 1 }).ToArray();
        Prototype Proto(int pid, int subtype) => new(pid, 0, subtype, 0, -1, new("test.pro", "synthetic", 125, "hash"), new byte[125]);
        var map = speechMap with { ObjectLayers = layers, Prototypes = speechMap.Prototypes.Concat(new[] { Proto(243, 1), Proto(49, 2), Proto(WorldSession.TempleChestPid, 1), Cameron() }).ToArray() };
        var gate = level2.Objects.Single(o => o.Offset == 462744);
        var speech = new TempleSpeechProfile(actor.Key, gate.Key, map.Source.Sha256, "data/vault13.gam", new int[21]);
        var profile = new TempleNpcDepartureProfile(speech, map.ObjectLayers.SelectMany(l => l.Objects).ToArray(), actor, gate);
        var world = new WorldSession(id => id == 3 ? map : WorldTests.Map(id, 3), new[] { 3, 126 }, templeProfile: _ => profile);
        if (character != null) world.StartNewGame(character);
        world.Current.Items.Restore(new[] { new TakenItem("owner", "item", 2) });
        int[] route = new[] { NpcDeparturePhase.ToGate, NpcDeparturePhase.ToMiddle, NpcDeparturePhase.ToExit }.SelectMany(profile.Route).ToArray();
        int adjacent = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(13728, d)!.Value).First(t => !route.Contains(t));
        world.Current.Travel.Arrive(new(3, 2, adjacent, 0));
        return (world, profile, map);
    }
    static int Read(WorldSession world, TempleNpcDepartureProfile profile, ScriptVariableScope scope, string key, int index)
    {
        var state = profile.Speech.CreateInitial(); state.Restore(world.TempleProgress!.Scripts);
        return state.TryRead(scope, key, index, out int value) ? value : -1;
    }
    // Cameron misses three times; the player lands two 3-point punches (22 -> 16); Cameron yields on his next turn.
    static CombatSession YieldFight(WorldSession world, TempleNpcDepartureProfile profile)
    {
        var fight = world.BeginTempleDuel(profile, new CombatTests.Scripted(99, 99, 99, 1, 3, 1, 3));
        ProvisionalMeleeBehavior.TakeTurn(fight);
        fight.Execute(CombatActionProposal.Attack("player", profile.ActorKey)); fight.Execute(CombatActionProposal.Attack("player", profile.ActorKey));
        fight.Execute(CombatActionProposal.EndTurn("player"));
        return fight;
    }

    public static void Run(Action<string, Action> check)
    {
        check("Cameron's challenge stores the whole inventory in the chest, locks the gate and lets Cameron strike first", () =>
        {
            var (world, profile, _) = Fixture();
            Assert(world.PrepareTempleDuel(profile, true) == TempleWorldStatus.Busy && world.PrepareTempleDuel(null, false) == TempleWorldStatus.Unsupported, "Guards.");
            Assert(world.PrepareTempleDuel(profile, false) == TempleWorldStatus.Ready && world.CarriedCount == 2, "Ready and read-only.");
            var fight = world.BeginTempleDuel(profile, new CombatTests.Scripted());
            var hero = fight.Get("player");
            Assert(world.CarriedCount == 0 && world.TempleChestCustody.Single() is { Map: 3, ItemKey: "item", Quantity: 2 } && world.InCombat
                && world.TempleDuel == TempleDuelOutcome.InProgress, "Custody.");
            Assert(fight.Active!.Id == profile.ActorKey && hero.Weapon.AttackType == WeaponAttackType.Unarmed && hero.HealingDoses == 0
                && fight.Get(profile.ActorKey) is { HitPoints: 22, YieldsBelowHalf: true }, "Fighters.");
            Assert(world.Current.Doors.Get(profile.GateKey) is { IsLocked: true, IsOpen: false }, "Node009 closes and locks the gate.");
            Assert(world.PrepareTempleSpeech(profile, WorldTempleSpeechTests.Accepted(profile), false, 5, true).Status == TempleWorldStatus.AlreadyAccepted, "No Speech mid-challenge.");
            world.AbandonCombat();
            Assert(world.CarriedCount == 2 && world.TempleChestCustody.Length == 0 && world.TempleDuel == TempleDuelOutcome.None && !world.InCombat
                && world.Current.Doors.Get(profile.GateKey)?.IsLocked != true, "Abandoning undoes the challenge.");
        });
        check("Cameron yields on his turn below half HP: no XP, duel writes (G10=1), the same departure, and the chest returns the equipment", () =>
        {
            var (world, profile, _) = Fixture();
            var fight = YieldFight(world, profile);
            Assert(fight.Turn.Over && fight.Turn.WinningTeam == 0 && fight.Log.OfType<CombatantYielded>().Single().HitPoints == 16 && fight.Get(profile.ActorKey).Alive, "Yield.");
            Assert(world.CommitCombat(fight) == CombatCommitStatus.Ready && world.TempleDuel == TempleDuelOutcome.Yielded && world.TempleTrial == TempleTrialPath.Duel
                && world.Experience == 0 && !world.IsDefeated(3, profile.ActorKey), "Commit.");
            Assert(Read(world, profile, ScriptVariableScope.GameGlobal, profile.Speech.GameKey, 10) == 1 && Read(world, profile, ScriptVariableScope.ScriptLocal, profile.ActorKey, 13) == 2
                && Read(world, profile, ScriptVariableScope.Map, profile.Speech.MapKey, 20) == 2 && Read(world, profile, ScriptVariableScope.ScriptLocal, profile.ActorKey, 12) == 1
                && Read(world, profile, ScriptVariableScope.ScriptLocal, profile.ActorKey, 7) == 0, "combat_p_proc writes.");
            Assert(world.TempleProgress!.Departure.Phase == NpcDeparturePhase.ToGate && !world.TempleProgress.InitialGateOpen, "Departure.");
            Assert(world.CarriedCount == 0 && world.CanRetrieveTempleChest(false) == TempleChestStatus.TooFar && world.CanRetrieveTempleChest(true) == TempleChestStatus.Busy, "Still in the chest.");
            world.Current.Travel.Arrive(new(3, 2, FalloutGrid.Neighbor(ChestTile, 0)!.Value, 0));
            Assert(world.RetrieveTempleChest(false) == TempleChestStatus.Ready && world.CarriedCount == 2 && world.RetrieveTempleChest(false) == TempleChestStatus.Empty, "Retrieved.");
            Assert(world.PrepareTempleDuel(profile, false) == TempleWorldStatus.AlreadyAccepted, "One challenge.");
        });
        check("checkpoint v12 keeps the duel outcome and the chest custody, and refuses outcomes that disagree with Cameron or the items", () =>
        {
            var (world, profile, map) = Fixture();
            var fight = YieldFight(world, profile); world.CommitCombat(fight);
            WorldSession Load(WorldCheckpoint saved) => WorldSession.Restore(id => id == 3 ? map : WorldTests.Map(id, 3), new[] { 3, 126 }, saved, "catalog", templeProfile: _ => profile);
            WorldCheckpoint saved = CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog")));
            Assert(saved.Duel is { Outcome: TempleDuelOutcome.Yielded } duel && duel.Custody.Single() == new CustodyItem(3, "owner", "item", 2), "Captured.");
            var restored = Load(saved);
            Assert(restored.TempleDuel == TempleDuelOutcome.Yielded && restored.TempleTrial == TempleTrialPath.Duel && restored.CarriedCount == 0
                && restored.TempleChestCustody.Single().Quantity == 2, "Restored.");
            var forged = new[]
            {
                saved with { Duel = saved.Duel! with { Outcome = TempleDuelOutcome.None } },
                saved with { Duel = saved.Duel! with { Outcome = TempleDuelOutcome.Killed } },
                saved with { Duel = saved.Duel! with { Outcome = TempleDuelOutcome.InProgress } },
                saved with { Duel = saved.Duel! with { Custody = new[] { new CustodyItem(3, "owner", "item", 3) } } },
                saved with { Duel = saved.Duel! with { Custody = new[] { new CustodyItem(126, "owner", "item", 1) } } },
                saved with { Temple = null },
                saved with { Duel = null }
            };
            for (int i = 0; i < forged.Length; i++) Reject(() => Load(forged[i]), $"Forged challenge {i} accepted.");
            var node = JsonNode.Parse(CheckpointFile.Encode(saved))!; node.AsObject().Remove("Duel");
            Reject(() => CheckpointFile.Decode(Encoding.UTF8.GetBytes(node.ToJsonString())), "v12 without Duel decoded.");
        });
        check("killing Cameron passes no exit: corpse, his 100 XP and the locked gate are kept, and the checkpoint keeps them", () =>
        {
            var narg = PlayerCharacter.FromPremade(CharacterFile.Read(PlayerCharacterTests.Gcd()));
            var (world, profile, map) = Fixture(21, narg);
            // Cameron misses three times; Narg (9 AP, punch 1-10) lands three 10-point punches: 21 -> 0.
            var fight = world.BeginTempleDuel(profile, new CombatTests.Scripted(99, 99, 99, 1, 10, 1, 10, 1, 10));
            ProvisionalMeleeBehavior.TakeTurn(fight);
            for (int i = 0; i < 3; i++) fight.Execute(CombatActionProposal.Attack("player", profile.ActorKey));
            Assert(fight.Turn.Over && !fight.Get(profile.ActorKey).Alive && fight.Turn.ExperienceAwarded == 100, "Killed.");
            Assert(world.CommitCombat(fight) == CombatCommitStatus.Ready && world.TempleDuel == TempleDuelOutcome.Killed && world.IsDefeated(3, profile.ActorKey)
                && world.Experience == 100 && world.TempleProgress == null && world.Current.Doors.Get(profile.GateKey) is { IsLocked: true }, "Outcome.");
            Assert(world.InspectTempleVillageRoute(false).Status == TempleWorldStatus.Unavailable, "No exit route without the departure.");
            WorldCheckpoint saved = world.Capture("catalog");
            var restored = WorldSession.Restore(id => id == 3 ? map : WorldTests.Map(id, 3), new[] { 3, 126 }, saved, "catalog", templeProfile: _ => profile,
                premade: _ => CharacterFile.Read(PlayerCharacterTests.Gcd()));
            Assert(restored.TempleDuel == TempleDuelOutcome.Killed && restored.IsDefeated(3, profile.ActorKey) && restored.Experience == 100
                && restored.TempleChestCustody.Single().Quantity == 2, "Restored kill.");
            Reject(() => WorldSession.Restore(id => id == 3 ? map : WorldTests.Map(id, 3), new[] { 3, 126 }, saved with { Duel = saved.Duel! with { Outcome = TempleDuelOutcome.None } },
                "catalog", templeProfile: _ => profile, premade: _ => CharacterFile.Read(PlayerCharacterTests.Gcd())), "Cameron's corpse without the challenge accepted.");
        });
    }
}
