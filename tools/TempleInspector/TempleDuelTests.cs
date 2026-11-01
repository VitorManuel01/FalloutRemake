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
    internal const string KeyItem = "cameron-temple-key";
    // keyAndExit: Cameron carries the Temple Key (as in arcaves, offset 425908), exit door 100 stands at 13528 and the exit
    // grid at 13326 leads to the verified Arroyo village.
    internal static (WorldSession World, TempleNpcDepartureProfile Profile, MapDocument Map) Fixture(int cameronHp = 22, PlayerCharacter? character = null,
        bool keyAndExit = false)
    {
        var (speechMap, _, _) = WorldTempleSpeechTests.Fixture(false);
        MapObject owner = ItemTransferTests.Map().ObjectLayers[0].Objects[0];
        var level2 = speechMap.ObjectLayers.Single(l => l.Elevation == 2);
        MapObject actor = level2.Objects.Single(o => o.Offset == 425776);
        actor = actor with { State = new Dictionary<string, int> { ["hp"] = cameronHp } };
        if (keyAndExit)
        {
            MapObject item = owner.Inventory[0].Object;
            MapObject key = item with { Key = KeyItem, Offset = 425908, Subtype = 6, ContainerElevation = 2,
                Header = item.Header with { Pid = TempleExitDoorProfile.TempleKeyPid, Tile = -1, Elevation = 2 } };
            actor = actor with { Inventory = new[] { new InventoryEntry(0, 1, key) }, InventoryCount = 1 };
        }
        MapObject chest = owner with { Key = speechMap.Source.Sha256 + ":465604", Offset = 465604, ContainerElevation = 2,
            Header = owner.Header with { Id = 100, Pid = WorldSession.TempleChestPid, Tile = ChestTile, Elevation = 2 },
            Inventory = Array.Empty<InventoryEntry>(), InventoryCount = 0 };
        var gate = level2.Objects.Single(o => o.Offset == 462744);
        MapObject? exitDoor = keyAndExit ? gate with { Key = speechMap.Source.Sha256 + ":423748", Offset = 423748,
            Header = gate.Header with { Id = 100, Tile = 13528, Flags = 32768, Sid = 50331651 } } : null;
        var objects2 = level2.Objects.Select(o => o.Offset == 425776 ? actor : o).Append(chest).Concat(exitDoor == null ? Array.Empty<MapObject>() : new[] { exitDoor }).ToArray();
        var layers = speechMap.ObjectLayers.Select(l => l.Elevation == 2 ? l with { Objects = objects2, DeclaredCount = objects2.Length }
            : l with { Objects = l.Objects.Append(owner).ToArray(), DeclaredCount = l.Objects.Length + 1 }).ToArray();
        Prototype Proto(int pid, int subtype) => new(pid, 0, subtype, 0, -1, new("test.pro", "synthetic", 125, "hash"), new byte[125]);
        var map = speechMap with { ObjectLayers = layers, Prototypes = speechMap.Prototypes.Concat(new[] { Proto(243, 1), Proto(49, 2), Proto(WorldSession.TempleChestPid, 1), Cameron(),
            Proto(TempleExitDoorProfile.TempleKeyPid, 6) }).ToArray() };
        TempleExitDoorProfile? exitProfile = null;
        if (exitDoor != null)
        {
            var fields = new int[14]; fields[1] = 749; fields[3] = 100; fields[4] = -1;
            var script = new MapScript(0, 64, 50331651, -1, Array.Empty<int>(), fields);
            var group = map.ScriptGroups.Single(); var block = group.Blocks.Single();
            map = WorldEntryTests.WithArroyoExit(map with { ScriptBindings = map.ScriptBindings.Append(new ScriptBinding(50331651, 749, map.ScriptBindings.Single(b => b.Index == 749).Program)).ToArray(),
                ScriptGroups = new[] { group with { DeclaredCount = group.DeclaredCount + 1, Blocks = new[] { block with { Used = block.Used + 1, Slots = block.Slots.Append(script).ToArray() } } } } });
            exitProfile = new(exitDoor.Key, new(369, 378, "GVAR_DUMAR_STATUS", 0));
        }
        var speech = new TempleSpeechProfile(actor.Key, gate.Key, map.Source.Sha256, "data/vault13.gam", new int[21]);
        var profile = new TempleNpcDepartureProfile(speech, map.ObjectLayers.SelectMany(l => l.Objects).ToArray(), actor, gate, exitProfile);
        MapDocument village = WorldEntryTests.Village(), temple = map;
        var world = new WorldSession(id => id == 3 ? temple : id == 4 ? village : WorldTests.Map(id, 3), new[] { 3, 4, 126 }, templeProfile: _ => profile,
            arroyoProfile: _ => new ArroyoRewardProfile());
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

    // Narg against a 21-HP Cameron: Cameron misses three times, three 10-point punches kill him.
    static void Kill(WorldSession world, TempleNpcDepartureProfile profile)
    {
        var fight = world.BeginTempleDuel(profile, new CombatTests.Scripted(99, 99, 99, 1, 10, 1, 10, 1, 10));
        ProvisionalMeleeBehavior.TakeTurn(fight);
        for (int i = 0; i < 3; i++) fight.Execute(CombatActionProposal.Attack("player", profile.ActorKey));
        Assert(world.CommitCombat(fight) == CombatCommitStatus.Ready && world.TempleDuel == TempleDuelOutcome.Killed, "Cameron killed.");
    }

    public static void Run(Action<string, Action> check)
    {
        check("a yielded challenge earns 300 XP on the first Arroyo entry (G10=1, not the Speech result) with the neutral entry globals", () =>
        {
            var (world, profile, _) = Fixture(keyAndExit: true);
            Assert(world.CommitCombat(YieldFight(world, profile)) == CombatCommitStatus.Ready && world.TempleDuel == TempleDuelOutcome.Yielded, "Yielded.");
            world.Current.Travel.Arrive(new(3, 2, 13326, 0));
            Assert(world.UseExit(false).Status == ExitStatus.Ready && world.CurrentMap == 4 && world.Experience == ArroyoRewardProfile.OtherTrialExperience
                && world.ArroyoReward!.Experience == 300 && world.ArroyoGlobals == new ArroyoRewardProfile().EntryGlobals(), "300 XP, karma 15, reputation 65.");
        });
        check("Cameron's body yields the Temple Key, which unlocks the gate and the exit door; Arroyo then gives 300 XP after karma -10 and reputation -5", () =>
        {
            CharacterFile gcd = CharacterFile.Read(PlayerCharacterTests.Gcd());
            var (world, profile, map) = Fixture(21, PlayerCharacter.FromPremade(gcd), keyAndExit: true);
            string cameron = profile.ActorKey, exit = profile.ExitDoor!.Key, gate = profile.GateKey;
            Assert(world.Current.Items.IsBody(cameron) && world.Current.Items.ContainerStatus(cameron) == ItemTakeStatus.Unavailable
                && world.Current.Items.Containers.All(c => c.Key != cameron), "A living Cameron keeps his key.");
            Kill(world, profile);
            int corpse = world.Defeated.Single(d => d.Key == cameron).Hex;
            Assert(world.Current.Items.ContainerStatus(cameron) == ItemTakeStatus.Ready && world.Current.Items.Owner(cameron)!.Header.Tile == corpse, "His body can be searched.");
            Assert(world.CanUseTempleKey(gate, false) == TempleWorldStatus.DoorRequired && world.Current.Doors.Get(gate) is { IsLocked: true }, "Locked in without the key.");
            Assert(world.TakeItem(cameron, KeyItem, 1, false) == ItemTakeStatus.Ready && world.TempleKeyCarried, "Temple Key taken.");
            int nearExit = FalloutGrid.Neighbor(13528, 2)!.Value;
            world.Current.Travel.Arrive(new(3, 2, nearExit, 0));
            Assert(world.Current.Doors.CanToggle(exit, nearExit, 2, false) == DoorUseStatus.ScriptRequired && world.CanUseTempleKey(gate, false) == TempleWorldStatus.TooFar
                && world.CanUseTempleKey(exit, true) == TempleWorldStatus.Busy, "G369 = 1 keeps the exit locked.");
            Assert(world.UseTempleKey(exit, false) == TempleWorldStatus.Ready && world.UseTempleKey(exit, false) == TempleWorldStatus.AlreadyAccepted
                && world.Current.Doors.CanToggle(exit, nearExit, 2, false) == DoorUseStatus.Ready, "The key unlocks the exit (L7 = 1).");
            int nearGate = FalloutGrid.Neighbor(19928, 2)!.Value;
            world.Current.Travel.Arrive(new(3, 2, nearGate, 0));
            Assert(world.UseTempleKey(gate, false) == TempleWorldStatus.Ready && world.Current.Doors.Get(gate) is { IsLocked: false, IsOpen: false }
                && world.Current.Doors.Toggle(gate, nearGate, 2, false) == DoorUseStatus.Ready && world.Current.Doors.Get(gate)!.IsOpen, "The gate opens with the key.");
            WorldSession Load(WorldCheckpoint saved) => WorldSession.Restore(id => id == 3 ? map : id == 4 ? WorldEntryTests.Village() : WorldTests.Map(id, 3), new[] { 3, 4, 126 },
                saved, "catalog", templeProfile: _ => profile, arroyoProfile: _ => new ArroyoRewardProfile(), premade: _ => gcd);
            WorldCheckpoint before = CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog")));
            Assert(before.Duel!.KeyUnlocked!.OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(new[] { exit, gate }.OrderBy(k => k, StringComparer.Ordinal))
                && before.Areas.Single(a => a.Map == 3).Taken!.Any(t => t.OwnerKey == cameron && t.ItemKey == KeyItem), "Captured.");
            var restored = Load(before);
            Assert(restored.TempleKeyCarried && restored.KeyUnlocked(exit) && restored.KeyUnlocked(gate) && restored.Current.Doors.Get(gate)!.IsOpen
                && restored.Current.Doors.CanToggle(exit, nearExit, 2, false) == DoorUseStatus.Ready, "Restored.");
            var forged = new[]
            {
                before with { Duel = before.Duel with { KeyUnlocked = new[] { "elsewhere" } } },
                before with { Duel = before.Duel with { KeyUnlocked = new[] { exit, exit } } },
                before with { Combat = before.Combat! with { Defeated = Array.Empty<DefeatedCritter>(), Experience = 0 } }
            };
            for (int i = 0; i < forged.Length; i++) Reject(() => Load(forged[i]), $"Forged key state {i} accepted.");
            world.Current.Travel.Arrive(new(3, 2, 13326, 0));
            Assert(world.UseExit(false).Status == ExitStatus.Ready && world.CurrentMap == 4 && world.Experience == 100 + 300
                && world.ArroyoGlobals == new ArroyoRewardProfile().EntryGlobalsAfterCameronKilled() && world.ArroyoGlobals is { Karma: 5, ArroyoReputation: 60, KarmaTierGlobal: 41 },
                "300 XP, karma 0 - 10 + 15, reputation 50 - 5 + 15.");
            WorldCheckpoint entered = world.Capture("catalog");
            Assert(Load(entered).ArroyoReward!.Experience == 300, "Arroyo entry restored.");
            Reject(() => Load(entered with { Entries = entered.Entries! with { ArroyoReward = entered.Entries.ArroyoReward! with { Experience = 600 } } }), "Speech XP after a kill accepted.");
            Reject(() => Load(entered with { Entries = entered.Entries! with { ArroyoGlobals = new ArroyoRewardProfile().EntryGlobals() } }), "Globals without the kill accepted.");
        });
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
                saved with { Duel = null, Skills = null }
            };
            for (int i = 0; i < forged.Length; i++) Reject(() => Load(forged[i]), $"Forged challenge {i} accepted.");
            var node = JsonNode.Parse(CheckpointFile.Encode(saved))!; node.AsObject().Remove("Duel");
            Reject(() => CheckpointFile.Decode(Encoding.UTF8.GetBytes(node.ToJsonString())), "v12 without Duel decoded.");
        });
        check("killing Cameron keeps his corpse, his 100 XP and the locked gate (G369 = 1), and the checkpoint keeps them", () =>
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
