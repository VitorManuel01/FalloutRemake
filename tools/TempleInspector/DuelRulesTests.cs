using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;

internal static class DuelRulesTests
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static readonly int Centre = 20100;
    static CombatantState Ant(string key = "ant") =>
        CombatantState.FromMapCritter(CombatTests.Critter(FalloutGrid.Neighbor(Centre, 1)!.Value, 0x0100017E, 6, 1, key), CritterPrototypeData.Read(CombatTests.AntPrototype()), key);
    static CombatantState Hero(CombatWeapon weapon, DerivedCombatStats? stats = null)
    {
        stats ??= DerivedCombatStats.ForCharacter(Special.Neutral);
        return new("player", "p", null, 0, true, Centre, 0, stats, weapon, stats.MaxHitPoints);
    }
    static CombatSession Fight(CombatantState hero, CombatantState ant, string initiator, params int[] rolls) =>
        new(new[] { hero, ant }, new HexNavigation(Array.Empty<MapObject>(), 0), new CombatTests.Scripted(rolls), initiator);

    // WorldCombatTests' map 3 with a pot holding the spear and an unloaded pistol, both carried.
    static MapDocument WorldWithSpearMap()
    {
        MapObject Item(string key, int pid) => CombatTests.Blocker(-1, 0, pid, key) with { Subtype = 3, Header = CombatTests.Blocker(-1, 0, pid, key).Header with { Tile = -1 } };
        var pot = CombatTests.Blocker(20000 + 199 - 97, 0, 0x000000FA, "map3:pot") with { Subtype = 1, InventoryCount = 2, State = new Dictionary<string, int> { ["flags"] = 0 },
            Inventory = new[] { new InventoryEntry(0, 1, Item("map3:spear", 7)), new InventoryEntry(0, 1, Item("map3:pistol", 8)) } };
        var baseMap = WorldCombatTests.Map();
        return baseMap with { ObjectLayers = new[] { baseMap.ObjectLayers[0] with { Objects = baseMap.ObjectLayers[0].Objects.Append(pot).ToArray(),
                DeclaredCount = baseMap.ObjectLayers[0].Objects.Length + 1 } },
            Prototypes = baseMap.Prototypes.Append(CombatTests.Spear()).Append(CombatTests.Pistol()).Append(CombatTests.Item(0xFA, 1, 65, 0, _ => { })).ToArray() };
    }
    static WorldSession WorldWithSpear()
    {
        var world = new WorldSession(_ => WorldWithSpearMap(), new[] { 3, 126 });
        world.Current.Items.Restore(new[] { new TakenItem("map3:pot", "map3:pistol", 1), new TakenItem("map3:pot", "map3:spear", 1) });
        return world;
    }

    public static void Run(Action<string, Action> check)
    {
        check("AC counts the AP a combatant holds outside its own turn, and every round starts by refilling everyone's AP (CE critterGetStat, _combat_set_move_all)", () =>
        {
            var hero = Hero(CombatWeapon.Unarmed()); var ant = Ant();
            var fight = Fight(hero, ant, "ant", 99);
            Assert(hero.ActionPoints == 7 && hero.EffectiveArmorClass == 12 && ant.Acting && ant.EffectiveArmorClass == 4, "Round 1: the player waits with all 7 AP.");
            Assert(fight.Preview(CombatActionProposal.Attack("ant", "player")).ToHit!.TargetArmorClass == 12, "The ant strikes AC 5 + 7.");
            fight.Execute(CombatActionProposal.Attack("ant", "player")); fight.Execute(CombatActionProposal.EndTurn("ant"));
            Assert(ant.ActionPoints == 2 && ant.EffectiveArmorClass == 6 && hero.Acting && hero.EffectiveArmorClass == 5, "The ant keeps its 2 unspent AP as AC.");
            fight.Execute(CombatActionProposal.EndTurn("player"));
            // Round 2: refilled; the player (Sequence 10) acts before the ant (9), which now defends with all 5 AP.
            Assert(fight.Turn.Round == 2 && fight.Active == hero && hero.ActionPoints == 7 && ant.ActionPoints == 5 && ant.EffectiveArmorClass == 9, "Refilled.");
        });
        check("unarmed blows follow CE's hit-mode table: the best punch or kick the skill, level, ST and AG allow, bonus damage on both ends, penetration and the bonus critical chance", () =>
        {
            Special Sheet(int st, int ag) => Special.Neutral with { Strength = st, Agility = ag };
            Assert(UnarmedBlows.Best(false, false, 60, 1, Sheet(5, 10)).Name == "Strong Punch" && UnarmedBlows.Best(true, false, 60, 1, Sheet(5, 10)).Name == "Strong Kick", "Mingan's sheet.");
            Assert(UnarmedBlows.Best(false, false, 54, 1, Sheet(9, 8)) == UnarmedBlows.Punch && UnarmedBlows.Best(true, false, 54, 1, Sheet(9, 8)).Name == "Strong Kick", "Narg: Unarmed 54 is below Strong Punch's 55.");
            Assert(UnarmedBlows.Best(false, false, 60, 1, Sheet(5, 5)) == UnarmedBlows.Punch, "AG 5 is below 6.");
            Assert(UnarmedBlows.Best(false, false, 80, 6, Sheet(5, 7)).Name == "Hammer Punch" && UnarmedBlows.Best(false, true, 80, 6, Sheet(5, 7)).Name == "Chop Punch"
                && UnarmedBlows.Best(false, false, 80, 5, Sheet(5, 7)).Name == "Strong Punch", "Level gates; secondary blows stay apart.");
            var mingan = DerivedCombatStats.ForCharacter(Sheet(5, 10));
            CombatWeapon strong = CombatWeapon.UnarmedFor(mingan, 1);
            var hero = Hero(strong, mingan);
            Assert(strong is { Name: "Strong Punch", ActionPoints: 3, BonusDamage: 3, Penetrates: false } && DamageCalculator.RollRange(hero, strong) == (4, 6), "1+3 .. 2+3+MD 1.");
            var target = Ant();
            var armored = new CombatantState("armored", "a", null, 1, false, target.Hex, 0, target.Stats with { DamageThreshold = new[] { 10, 0, 0, 0, 0, 0, 0 } }, target.Weapon, 6);
            Assert(DamageCalculator.Threshold(armored, strong) == 10 && DamageCalculator.Threshold(armored, strong with { Penetrates = true }) == 2, "Penetrating blows keep 20% of DT.");
            // A Hammer Punch (5% bonus critical) on game day 0: a plain hit, then d100 <= 5 makes it critical.
            var hammerStats = mingan with { CombatSkills = mingan.CombatSkills.Select((v, i) => i == DerivedCombatStats.Unarmed ? 80 : v).ToArray() };
            CombatWeapon hammer = CombatWeapon.UnarmedFor(hammerStats, 6);
            var fight = Fight(Hero(hammer, hammerStats), Ant(), "player", 1, 5, 1, 6);
            fight.Execute(CombatActionProposal.Attack("player", "ant"));
            Assert(hammer is { Name: "Hammer Punch", BonusCriticalChance: 5 } && fight.Log.OfType<CriticalHit>().Single() is { Roll: 5, Needed: 5, EffectRoll: 1, Multiplier: 3 }
                && fight.Log.OfType<AttackResolved>().Single() is { DamageRoll: 6, Damage: 9 }, "x3 critical before day 1, from the blow's own chance.");
            var plain = new CombatTests.Scripted(1, 6, 6);
            new CombatSession(new[] { Hero(hammer, hammerStats), Ant() }, new HexNavigation(Array.Empty<MapObject>(), 0), plain, "player").Execute(CombatActionProposal.Attack("player", "ant"));
            Assert(plain.Left == 0, "6 is above the bonus: hit, bonus roll, damage roll.");
        });
        check("bare hands switch between the left hand's punch and the right hand's kick for no AP", () =>
        {
            var mingan = DerivedCombatStats.ForCharacter(Special.Neutral with { Strength = 5, Agility = 10 });
            var hero = new CombatantState("player", "p", null, 0, true, Centre, 0, mingan, CombatWeapon.UnarmedFor(mingan, 1), mingan.MaxHitPoints)
                { AlternateWeapon = CombatWeapon.UnarmedFor(mingan, 1, kick: true) };
            var fight = Fight(hero, Ant(), "player");
            var switched = fight.Execute(CombatActionProposal.SwitchHand("player"));
            Assert(switched.Status == CombatActionStatus.Ready && switched.Events.Single() is HandSwitched { Blow: "Strong Kick" } && hero.Weapon.Kick && hero.RightHandActive
                && hero.AlternateWeapon!.Name == "Strong Punch" && hero.ActionPoints == 10, "Strong Kick, no AP spent.");
            Assert(fight.Preview(CombatActionProposal.Attack("player", "ant")).ActionPointCost == 4, "The kick costs 4 AP.");
            var single = Fight(Hero(WorldCombatTests.Spear()), Ant(), "player");
            Assert(single.Preview(CombatActionProposal.SwitchHand("player")).Status == CombatActionStatus.NoItem, "Without another hand's blow there is nothing to switch to.");
        });
        check("FO2's hand slots: a carried melee weapon is wielded in a hand, the active hand strikes, an empty hand punches or kicks, and a hand lets go of an item that leaves", () =>
        {
            var world = WorldWithSpear();
            CarriedItem spear = world.Carried.Single(c => c.Source.Header.Pid == 7), pistol = world.Carried.Single(c => c.Source.Header.Pid == 8);
            Assert(world.ActiveHand == Hand.Left && world.PlayerWeapon() is { Pid: null, Kick: false, Name: "Desarmado" } && world.HandWeapon(Hand.Right).Kick,
                "Carrying is not wielding: empty hands punch (left) and kick (right).");
            Assert(world.Wield(pistol, Hand.Left, false) == HandStatus.Unsupported && world.Wield(spear, Hand.Left, true) == HandStatus.Busy, "No firearm without ammunition; not while walking.");
            Assert(world.Wield(spear, Hand.Left, false) == HandStatus.Ready && world.PlayerWeapon() is { Pid: 7, Range: 2, ActionPoints: 4 } && world.Held(Hand.Left)!.ItemKey == "map3:spear",
                "The spear in the left hand.");
            Assert(world.Wield(spear, Hand.Right, false) == HandStatus.Ready && world.Held(Hand.Left) == null && world.HandWeapon(Hand.Left).Name == "Desarmado",
                "A single spear moves to the right hand.");
            world.SetActiveHand(Hand.Right);
            var opened = world.BeginCombat(WorldCombatTests.NearAnt(world, "map3:far-ant"), world.PlayerWeapon(), new CombatTests.Scripted());
            var player = opened.Get("player");
            Assert(player.Weapon.Pid == 7 && player.RightHandActive && player.AlternateWeapon is { Pid: null, Kick: false }, "The fight opens with the spear, the punch in the other hand.");
            for (int guard = 0; guard < 10 && opened.Active != player; guard++) CombatAi.TakeTurn(opened);
            opened.Execute(CombatActionProposal.SwitchHand("player")); opened.Execute(CombatActionProposal.LeaveCombat("player"));
            Assert(world.CommitCombat(opened) == CombatCommitStatus.Ready && world.ActiveHand == Hand.Left && world.Held(Hand.Right) != null, "The hand it left with stays active.");
            Assert(world.Unwield(Hand.Right, false) == HandStatus.Ready && world.Held(Hand.Right) == null && world.Unwield(Hand.Right, false) == HandStatus.NoItem, "Put away.");
            world.Wield(spear, Hand.Left, false);
            world.Current.Items.Restore(new[] { new TakenItem("map3:pot", "map3:pistol", 1) });
            Assert(world.Held(Hand.Left) == null && world.PlayerWeapon().Pid == null, "The spear left the inventory, and the hand.");
        });
        check("in a fight the player opens the inventory for 4 AP (CE inventoryOpen) and changes what the hands hold inside it, for nothing more", () =>
        {
            var world = WorldWithSpear();
            CarriedItem spear = world.Carried.Single(c => c.Source.Header.Pid == 7);
            var fight = world.BeginCombat(new CombatEncounter(world.Location, new[] { "map3:far-ant" }, "player"), world.PlayerWeapon(), new CombatTests.Scripted());
            var player = fight.Get("player");
            Assert(world.Wield(spear, Hand.Left, false) == HandStatus.InCombat && player.Weapon.Pid == null, "Closed inventory: no change.");
            var opened = fight.Execute(CombatActionProposal.OpenInventory("player"));
            Assert(opened.Status == CombatActionStatus.Ready && opened.Events.Single() is InventoryOpened { ActionPointsSpent: 4 } && player.ActionPoints == 3 && fight.InventoryOpen,
                "4 of 7 AP.");
            Assert(fight.Preview(CombatActionProposal.OpenInventory("player")).ActionPointCost == 0 && fight.Preview(CombatActionProposal.UseHealing("player")).Status == CombatActionStatus.NoItem,
                "Already open: nothing more to pay.");
            Assert(world.Wield(spear, Hand.Left, false) == HandStatus.Ready && player.Weapon.Pid == 7 && player.AlternateWeapon!.Kick && player.ActionPoints == 3
                && fight.Log.OfType<WeaponsChanged>().Single().OtherHand == "Chute", "The spear in the left hand, the kick in the right, no AP.");
            fight.Execute(CombatActionProposal.EndTurn("player"));
            Assert(!fight.InventoryOpen && fight.Log.OfType<InventoryClosed>().Count() == 1, "Acting otherwise closes it.");
            for (int guard = 0; guard < 10 && fight.Active != player; guard++) CombatAi.TakeTurn(fight);
            Assert(world.Unwield(Hand.Left, false) == HandStatus.InCombat, "Closed again.");
            player.ActionPoints = 3;
            Assert(fight.Preview(CombatActionProposal.OpenInventory("player")).Status == CombatActionStatus.NotEnoughActionPoints, "Below 4 AP the inventory stays shut.");
            player.ActionPoints = 7;
            fight.Execute(CombatActionProposal.OpenInventory("player"));
            Assert(world.Unwield(Hand.Left, false) == HandStatus.Ready && player.Weapon is { Pid: null, Name: "Desarmado" }
                && fight.Execute(CombatActionProposal.CloseInventory("player")).Status == CombatActionStatus.Ready && !fight.InventoryOpen, "Put away, closed.");
            Assert(fight.Preview(CombatActionProposal.OpenInventory("ghost")).Status == CombatActionStatus.UnknownCombatant, "Only the player.");
        });
        check("the checkpoint keeps the held items and the active hand, leaves them out at the defaults and refuses forged hands", () =>
        {
            var world = WorldWithSpear();
            WorldSession Load(WorldCheckpoint checkpoint) => WorldSession.Restore(_ => WorldWithSpearMap(), new[] { 3, 126 }, checkpoint, "catalog");
            Assert(world.Capture("catalog").Combat!.Hands == null, "Defaults: no field.");
            CarriedItem spear = world.Carried.Single(c => c.Source.Header.Pid == 7);
            world.Wield(spear, Hand.Left, false); world.SetActiveHand(Hand.Right);
            var saved = CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog")));
            Assert(saved.Combat!.Hands == new SavedHands(new HeldItem(3, "map3:spear"), null, true), "Spear left, right hand active.");
            var loaded = Load(saved);
            Assert(loaded.Held(Hand.Left) == new HeldItem(3, "map3:spear") && loaded.ActiveHand == Hand.Right && loaded.PlayerWeapon().Kick, "Both come back.");
            SavedHands hands = saved.Combat.Hands!;
            foreach (SavedHands bad in new[] { hands with { RightActive = false }, new SavedHands(null, null, null), hands with { Left = new HeldItem(3, "map3:pistol") },
                hands with { Left = new HeldItem(3, "map3:ghost") }, hands with { Right = hands.Left } })
                try { Load(saved with { Combat = saved.Combat with { Hands = bad } }); throw new Exception($"Forged hands accepted: {bad}."); }
                catch (InvalidDataException) { }
            Assert(Load(saved with { Combat = saved.Combat with { Hands = null } }) is { ActiveHand: Hand.Left } older && older.Held(Hand.Left) == null, "Older saves: empty hands, left active.");
            world.Reset();
            Assert(world.ActiveHand == Hand.Left && world.Held(Hand.Left) == null, "A new game starts empty-handed on the left.");
        });
    }
}
