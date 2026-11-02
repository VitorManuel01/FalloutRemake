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
    // Every roll lands mid-range: enough for an attack whose outcome the test does not inspect.
    sealed class Middle : ICombatRandom { public int Between(int min, int max) => (min + max) / 2; }

    // WorldCombatTests' map 3 with a pot holding the spear and an unloaded pistol, both carried.
    internal static MapDocument WorldWithSpearMap()
    {
        // Unloaded, as the original weapons are (CarryWeight reads a weapon's ammoQuantity).
        MapObject Item(string key, int pid) => CombatTests.Blocker(-1, 0, pid, key) with { Subtype = 3, Header = CombatTests.Blocker(-1, 0, pid, key).Header with { Tile = -1 },
            State = new Dictionary<string, int> { ["ammoQuantity"] = 0 } };
        var pot = CombatTests.Blocker(20000 + 199 - 97, 0, 0x000000FA, "map3:pot") with { Subtype = 1, InventoryCount = 2, State = new Dictionary<string, int> { ["flags"] = 0 },
            Inventory = new[] { new InventoryEntry(0, 1, Item("map3:spear", 7)), new InventoryEntry(0, 1, Item("map3:pistol", 8)) } };
        var baseMap = WorldCombatTests.Map();
        return baseMap with { ObjectLayers = new[] { baseMap.ObjectLayers[0] with { Objects = baseMap.ObjectLayers[0].Objects.Append(pot).ToArray(),
                DeclaredCount = baseMap.ObjectLayers[0].Objects.Length + 1 } },
            Prototypes = baseMap.Prototypes.Append(CombatTests.Spear()).Append(CombatTests.Pistol()).Append(CombatTests.Item(0xFA, 1, 65, 0, _ => { })).ToArray() };
    }
    internal static WorldSession WorldWithSpear()
    {
        var world = new WorldSession(_ => WorldWithSpearMap(), new[] { 3, 126 });
        world.Current.Items.Restore(new[] { new TakenItem("map3:pot", "map3:pistol", 1), new TakenItem("map3:pot", "map3:spear", 1) });
        return world;
    }

    // Synthetic armor PROs (no armor lies in maps 3, 4 or 126): leather-like (AC 15, normal DR 25% and DT 2, no perk), a
    // power armor with the Powered Armor perk (62) and one naming a perk no original armor uses (70).
    static Prototype Armor(int pid = 1, int armorClass = 15, int resistance = 25, int threshold = 2, int perk = -1) =>
        CombatTests.Item(pid, 0, 129, 0, put => { put(57, armorClass); put(61, resistance); put(89, threshold); put(117, perk); });
    static MapDocument WorldWithArmorMap()
    {
        MapDocument map = WorldWithSpearMap();
        MapObject pot = map.ObjectLayers[0].Objects.Single(o => o.Key == "map3:pot"), spear = pot.Inventory.First().Object;
        InventoryEntry Worn(string key, int pid) => new(0, 1, spear with { Key = key, Subtype = 0, Header = spear.Header with { Pid = pid } });
        pot = pot with { InventoryCount = 5, Inventory = pot.Inventory.Concat(new[] { Worn("map3:armor", 1), Worn("map3:power", 3), Worn("map3:odd", 2) }).ToArray() };
        return map with { ObjectLayers = new[] { map.ObjectLayers[0] with { Objects = map.ObjectLayers[0].Objects.Select(o => o.Key == pot.Key ? pot : o).ToArray() } },
            Prototypes = map.Prototypes.Append(Armor()).Append(Armor(3, 25, 40, 12, ArmorPerks.PoweredArmor)).Append(Armor(2, 10, 30, 4, 70)).ToArray() };
    }
    static void TakeArmor(WorldSession world) => world.Current.Items.Restore(new[] { new TakenItem("map3:pot", "map3:spear", 1), new TakenItem("map3:pot", "map3:armor", 1),
        new TakenItem("map3:pot", "map3:power", 1), new TakenItem("map3:pot", "map3:odd", 1) });
    static WorldSession WorldWithArmor()
    {
        var world = new WorldSession(_ => WorldWithArmorMap(), new[] { 3, 126 });
        world.Current.Items.Restore(new[] { new TakenItem("map3:pot", "map3:spear", 1), new TakenItem("map3:pot", "map3:armor", 1) });
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
            Assert(world.CanWield(pistol, Hand.Left, false) == HandStatus.Ready && world.HandWeapon(Hand.Left).Pid == null && world.Wield(spear, Hand.Left, true) == HandStatus.Busy,
                "An empty firearm may be wielded (it fires once loaded); not while walking.");
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
            foreach (SavedHands bad in new[] { hands with { RightActive = false }, new SavedHands(null, null, null), hands with { Left = new HeldItem(126, "map3:spear") },
                hands with { Left = new HeldItem(3, "map3:ghost") }, hands with { Right = hands.Left } })
                try { Load(saved with { Combat = saved.Combat with { Hands = bad } }); throw new Exception($"Forged hands accepted: {bad}."); }
                catch (InvalidDataException) { }
            Assert(Load(saved with { Combat = saved.Combat with { Hands = null } }) is { ActiveHand: Hand.Left } older && older.Held(Hand.Left) == null, "Older saves: empty hands, left active.");
            world.Reset();
            Assert(world.ActiveHand == Hand.Left && world.Held(Hand.Left) == null, "A new game starts empty-handed on the left.");
        });
        check("the player wears one carried armor: its AC, DR and DT join the combat stats (CE _adjust_ac), in a fight only inside the open inventory", () =>
        {
            var world = WorldWithArmor();
            CarriedItem spear = world.Carried.Single(c => c.Source.Header.Pid == 7), armor = world.Carried.Single(c => c.Source.Header.Pid == 1);
            DerivedCombatStats bare = world.PlayerStats();
            Assert(world.Worn == null && world.WornArmor() == null && bare.Resistance(DamageType.Normal) == 0, "Nothing worn at first.");
            Assert(world.Wear(spear, false) == HandStatus.NotArmor && world.Wear(armor, true) == HandStatus.Busy && world.Wield(armor, Hand.Left, false) == HandStatus.NotWeapon,
                "Only armor, not while walking; armor is not held in a hand.");
            Assert(world.Wear(armor, false) == HandStatus.Ready && world.Worn == new HeldItem(3, "map3:armor"), "Worn.");
            DerivedCombatStats worn = world.PlayerStats();
            Assert(worn.ArmorClass == bare.ArmorClass + 15 && worn.Resistance(DamageType.Normal) == 25 && worn.Threshold(DamageType.Normal) == 2
                && worn.Resistance(DamageType.Laser) == 0 && worn.MaxHitPoints == bare.MaxHitPoints, "AC +15, normal DR 25% and DT 2.");
            var fight = world.BeginCombat(new CombatEncounter(world.Location, new[] { "map3:far-ant" }, "player"), world.PlayerWeapon(), new CombatTests.Scripted());
            var player = fight.Get("player");
            Assert(player.Stats.ArmorClass == worn.ArmorClass && player.Stats.Resistance(DamageType.Normal) == 25, "The fight starts with the armor on.");
            Assert(world.TakeOff(false) == HandStatus.InCombat && world.Worn != null, "Closed inventory: it stays on.");
            for (int guard = 0; guard < 10 && fight.Active != player; guard++) CombatAi.TakeTurn(fight);
            fight.Execute(CombatActionProposal.OpenInventory("player"));
            int ap = player.ActionPoints;
            Assert(world.TakeOff(false) == HandStatus.Ready && player.Stats.ArmorClass == bare.ArmorClass && player.Stats.Resistance(DamageType.Normal) == 0
                && fight.Log.OfType<ArmorChanged>().Last() is { Armor: null } off && off.ArmorClass == bare.ArmorClass && player.ActionPoints == ap, "Off, for nothing more.");
            Assert(world.Wear(armor, false) == HandStatus.Ready && player.Stats.ArmorClass == worn.ArmorClass && fight.Log.OfType<ArmorChanged>().Last().Armor == "PID 1", "On again.");
            Assert(player.HitPoints <= player.Stats.MaxHitPoints && player.ActionPoints == ap, "HP and AP untouched.");
        });
        check("worn armor leaves the slot with the inventory and survives the checkpoint; forged armor is refused", () =>
        {
            var world = WorldWithArmor();
            WorldSession Load(WorldCheckpoint checkpoint) => WorldSession.Restore(_ => WorldWithArmorMap(), new[] { 3, 126 }, checkpoint, "catalog");
            world.Wear(world.Carried.Single(c => c.Source.Header.Pid == 1), false);
            var saved = CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog")));
            Assert(saved.Combat!.Hands == new SavedHands(null, null, null, new HeldItem(3, "map3:armor")), "Armor alone is saved.");
            Assert(Load(saved) is var loaded && loaded.Worn == new HeldItem(3, "map3:armor") && loaded.PlayerStats().Resistance(DamageType.Normal) == 25, "It comes back worn.");
            SavedHands hands = saved.Combat.Hands!;
            foreach (SavedHands bad in new[] { hands with { Armor = new HeldItem(3, "map3:spear") }, hands with { Armor = new HeldItem(3, "map3:ghost") } })
                try { Load(saved with { Combat = saved.Combat with { Hands = bad } }); throw new Exception($"Forged armor accepted: {bad}."); }
                catch (InvalidDataException) { }
            world.Current.Items.Restore(new[] { new TakenItem("map3:pot", "map3:spear", 1) });
            Assert(world.Worn == null && world.PlayerStats().Resistance(DamageType.Normal) == 0, "Returned to the pot: no longer worn.");
            world.Wear(world.Carried.Single(c => c.Source.Header.Pid == 7), false);
            world.Reset();
            Assert(world.Worn == null, "A new game starts without armor.");
        });
        check("a worn armor's perk adds CE perkAddEffect's bonus while worn: power armor's +3 ST raises skills, melee damage and carry weight but not max HP", () =>
        {
            var world = WorldWithArmor(); TakeArmor(world);
            CarriedItem Item(int pid) => world.Carried.Single(c => c.Source.Header.Pid == pid);
            PlayerCharacter bare = world.Sheet; int hp = world.MaxHitPoints, capacity = world.CarryCapacity;
            Assert(world.Wear(Item(2), false) == HandStatus.Unsupported && world.Worn == null, "A perk no original armor names is refused.");
            Assert(world.Wear(Item(3), false) == HandStatus.Ready && world.WornPerk() == ArmorPerks.Effect(ArmorPerks.PoweredArmor), "Power armor on.");
            PlayerCharacter sheet = world.Sheet; DerivedCombatStats stats = world.PlayerStats();
            Assert(sheet.Effective.Strength == bare.Effective.Strength + 3 && sheet.Skill(DerivedCombatStats.Unarmed) == bare.Skill(DerivedCombatStats.Unarmed) + 6
                && sheet.Skill(DerivedCombatStats.MeleeWeapons) == bare.Skill(DerivedCombatStats.MeleeWeapons) + 6 && sheet.MeleeDamage == 3 && bare.MeleeDamage == 1,
                "ST 5 to 8: Unarmed and Melee Weapons +6, melee damage 1 to 3.");
            Assert(world.CarryCapacity == capacity + 75 && sheet.RadiationResistance == bare.RadiationResistance + 30 && world.MaxHitPoints == hp
                && stats.MaxHitPoints == hp && stats.MeleeDamage == 3 && stats.ArmorClass == bare.ArmorClass + 25, "Carry +75, radiation +30, max HP unchanged.");
            Assert(world.Character.Armor == ArmorPerk.None && world.Character.Capture().Special.SequenceEqual(bare.Capture().Special), "The character itself never keeps the perk.");
            Assert(world.Wear(Item(1), false) == HandStatus.Ready && world.Sheet.Effective.Strength == bare.Effective.Strength && world.CarryCapacity == capacity,
                "Leather armor has no perk: the bonus leaves with the power armor.");
            world.Wear(Item(3), false);
            var saved = CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog")));
            Assert(WorldSession.Restore(_ => WorldWithArmorMap(), new[] { 3, 126 }, saved, "catalog").Sheet.Effective.Strength == 8, "The perk comes back with the worn armor.");
        });
        check("the armor perk follows CE critterGetStat: the bonus is clamped with SPECIAL, Fast Metabolism keeps it, max HP ignores it", () =>
        {
            var strong = PlayerCharacter.Create("Forte", new Special(9, 5, 5, 5, 5, 5, 6), new[] { CharacterTrait.FastMetabolism }, new[] { 0, 1, 2 }, 25, 0);
            PlayerCharacter worn = strong.Wearing(ArmorPerks.Effect(ArmorPerks.AdvancedArmorI));
            Assert(worn.Effective.Strength == 10 && worn.BaseMaxHitPoints == strong.BaseMaxHitPoints && strong.RadiationResistance == 0 && worn.RadiationResistance == 60,
                "ST 9 + 4 stops at 10; Fast Metabolism zeroes only the base radiation resistance.");
            Assert(ReferenceEquals(worn.Creation, strong) && worn.Wearing(ArmorPerk.None).Effective == strong.Effective && strong.Wearing(ArmorPerk.None) == strong,
                "Taking it off restores the sheet.");
            Assert(ArmorPerks.Effect(ArmorPerks.AdvancedArmorII) is { Strength: 4, RadiationResistance: 75 } && ArmorPerks.Effect(ArmorPerks.CombatArmor) is { Strength: 0, RadiationResistance: 20 },
                "fallout2.exe rows.");
            try { ArmorPerks.Effect(70); throw new Exception("Unknown perk accepted."); } catch (InvalidDataException) { }
        });
        check("the player's aimed blow costs 1 AP more on any location it names, the torso included; a critter pays it only off the torso (CE _combat_attack)", () =>
        {
            CombatWeapon punch = CombatWeapon.UnarmedFor(DerivedCombatStats.ForCharacter(Special.Neutral), 1);
            CombatantState hero = Hero(punch), ant = Ant();
            Assert(CombatSession.AttackCost(hero, HitLocation.Uncalled) == 3 && CombatSession.AttackCost(hero, HitLocation.Torso) == 4
                && CombatSession.AttackCost(hero, HitLocation.Eyes) == 4, "Player: 3 plain, 4 aimed (torso too).");
            Assert(CombatSession.AttackCost(ant, HitLocation.Torso) == ant.Weapon.ActionPoints && CombatSession.AttackCost(ant, HitLocation.Head) == ant.Weapon.ActionPoints + 1,
                "Critter: its plain blow names the torso.");
            Assert(CombatResolver.ToHit(hero, ant, HitLocation.Eyes).Raw == CombatResolver.ToHit(hero, ant).Raw + CriticalRules.Penalty(HitLocation.Eyes) / 2,
                "Eyes: half the location penalty in melee.");
            var fight = new CombatSession(new[] { hero, ant }, new HexNavigation(Array.Empty<MapObject>(), 0), new Middle(), "player");
            int before = hero.ActionPoints;
            CombatActionResult aimed = fight.Execute(CombatActionProposal.Attack("player", "ant", HitLocation.Eyes));
            Assert(aimed.Status == CombatActionStatus.Ready && hero.ActionPoints == before - 4 && fight.Log.OfType<AttackResolved>().Single() is { Location: HitLocation.Eyes, ActionPointsSpent: 4 },
                $"Aimed at the eyes for 4 AP ({aimed.Status}, {before} -> {hero.ActionPoints}).");
        });
        check("a creature's critical row follows the location aimed at (gCriticalHitTables[killType][location]); uncalled reads the torso", () =>
        {
            const int ant = 17, robot = 10;
            Assert(CriticalRules.Critter(ant, HitLocation.Uncalled, 3) == CriticalRules.Critter(ant, HitLocation.Torso, 3)
                && CriticalRules.Critter(ant, HitLocation.Torso, 3) == CriticalRules.Torso(false, ant, 3), "Uncalled is the torso.");
            Assert(Enumerable.Range(0, 6).Any(e => CriticalRules.Critter(ant, HitLocation.Eyes, e) != CriticalRules.Critter(ant, HitLocation.Torso, e)), "The ant's eyes have rows of their own.");
            Assert(CriticalRules.Critter(robot, HitLocation.Groin, 4) is { MassiveStat: 2, MassiveFlags: DamageFlags.CrippleRandom }, "fallout2.exe's robot groin row (CE's source drops the flag).");
            Assert(CriticalRules.KillTypeTable.Length == CriticalRules.KillTypeCount && CriticalRules.KillTypeTable.All(k => k.Length == 9 && k.All(l => l.Length == 6)), "19 x 9 x 6.");
        });
        check("CE critterCanAim: Fast Shot never aims (and saves 1 AP beyond range 2), nor does explosive, fire or EMP damage", () =>
        {
            DerivedCombatStats fast = DerivedCombatStats.ForCharacter(Special.Neutral) with { FastShot = true };
            var spear = CombatWeapon.Primary("Spear", WeaponPrototypeData.Read(CombatTests.Spear()));
            var pistol = CombatWeapon.Primary("Pistola", WeaponPrototypeData.Read(CombatTests.Pistol()));
            CombatantState quick = Hero(spear, fast), ant = Ant();
            var fight = Fight(quick, ant, "player");
            Assert(!CombatSession.CanAim(quick) && fight.Preview(CombatActionProposal.Attack("player", "ant", HitLocation.Head)).Status == CombatActionStatus.CannotAim
                && fight.Preview(CombatActionProposal.Attack("player", "ant")).Status == CombatActionStatus.Ready, "Fast Shot: the plain blow only.");
            Assert(CombatSession.AttackCost(quick, HitLocation.Uncalled) == spear.ActionPoints && CombatSession.AttackCost(Hero(pistol, fast), HitLocation.Uncalled) == pistol.ActionPoints - 1
                && CombatSession.AttackCost(Hero(pistol), HitLocation.Uncalled) == pistol.ActionPoints, "Fast Shot: 1 AP less only beyond range 2.");
            var player = PlayerCharacter.Create("Rapido", Special.Neutral with { Agility = 10 }, new[] { CharacterTrait.FastShot }, new[] { 0, 1, 2 }, 25, 0);
            Assert(DerivedCombatStats.ForPlayer(player).FastShot && !DerivedCombatStats.ForPlayer(PlayerCharacter.Neutral).FastShot, "The sheet carries the trait.");
            foreach (DamageType type in new[] { DamageType.Fire, DamageType.Explosion, DamageType.Emp })
                Assert(!CombatSession.CanAim(Hero(spear with { DamageType = type })) && !CombatSession.CanAim(new CombatantState("npc", "npc", null, 1, false, Centre, 0,
                    DerivedCombatStats.ForCharacter(Special.Neutral), spear with { DamageType = type }, 30)), $"{type} is never aimed.");
            Assert(CombatSession.CanAim(Hero(spear)) && CombatSession.CanAim(Hero(spear with { DamageType = DamageType.Laser })), "Normal and laser damage aim.");
        });
        check("a weapon's minimum Strength costs 20 to hit per missing point, the player is warned on each such attack (combat.msg 107) and a worn armor's ST closes the gap", () =>
        {
            var world = WorldWithArmor();
            world.StartNewGame(PlayerCharacter.Create("Fraco", new Special(2, 5, 5, 5, 5, 8, 10), Array.Empty<CharacterTrait>(), new[] { 0, 1, 2 }, 25, 0));
            TakeArmor(world);
            Assert(world.Wield(world.Carried.Single(c => c.Source.Header.Pid == 7), Hand.Left, false) == HandStatus.Ready, "Spear (ST 4) in the left hand.");
            var fight = world.BeginCombat(new CombatEncounter(world.Location, new[] { "map3:far-ant" }, "player"), world.PlayerWeapon(), new CombatTests.Scripted());
            var player = fight.Get("player"); var ant = fight.Combatants.Single(c => !c.IsPlayer);
            Assert(CombatResolver.StrengthShortfall(player) == 2 && CombatResolver.ToHit(player, ant).StrengthPenalty == 40, "ST 2 for a spear that needs 4: -40.");
            for (int guard = 0; guard < 10 && fight.Active != player; guard++) CombatAi.TakeTurn(fight);
            fight.Execute(CombatActionProposal.OpenInventory("player"));
            Assert(world.Wear(world.Carried.Single(c => c.Source.Header.Pid == 3), false) == HandStatus.Ready && player.Stats.Special.Strength == 5
                && CombatResolver.ToHit(player, ant).StrengthPenalty == 0, "Power armor: ST 5, no penalty.");
            world.TakeOff(false);
            Assert(CombatResolver.ToHit(player, ant).StrengthPenalty == 40, "Off again: -40.");

            // The warning rides on the attack event: the player's weapon only, never bare hands or a critter.
            var weak = new CombatantState("player", "p", null, 0, true, Centre, 0, DerivedCombatStats.ForCharacter(new Special(2, 5, 5, 5, 5, 5, 5)),
                CombatWeapon.Primary("Spear", WeaponPrototypeData.Read(CombatTests.Spear())), 20);
            var duel = new CombatSession(new[] { weak, Ant() }, new HexNavigation(Array.Empty<MapObject>(), 0), new Middle(), "player");
            duel.Execute(CombatActionProposal.Attack("player", "ant"));
            Assert(duel.Log.OfType<AttackResolved>().Single() is { UnderStrength: true, AttackerId: "player" }, "Spear with ST 2: warned.");
            var fist = Hero(CombatWeapon.UnarmedFor(DerivedCombatStats.ForCharacter(new Special(1, 5, 5, 5, 5, 5, 5)), 1), DerivedCombatStats.ForCharacter(new Special(1, 5, 5, 5, 5, 5, 5)));
            var bare = new CombatSession(new[] { fist, Ant() }, new HexNavigation(Array.Empty<MapObject>(), 0), new Middle(), "player");
            bare.Execute(CombatActionProposal.Attack("player", "ant"));
            Assert(bare.Log.OfType<AttackResolved>().Single() is { UnderStrength: false } && CombatResolver.StrengthShortfall(fist) == 0, "Bare hands need no Strength.");
        });
        check("One Hander (CE attackDetermineToHit): the player takes -40 with a two-handed weapon and +20 with any other, never bare-handed nor for critters", () =>
        {
            DerivedCombatStats one = DerivedCombatStats.ForCharacter(Special.Neutral) with { OneHander = true };
            var spear = CombatWeapon.Primary("Spear", WeaponPrototypeData.Read(CombatTests.Spear()));
            CombatWeapon pole = spear with { TwoHanded = true };
            int plain = CombatResolver.ToHit(Hero(spear), Ant()).Raw;
            Assert(!spear.TwoHanded && CombatResolver.ToHit(Hero(spear, one), Ant()) is { OneHanderModifier: 20 } light && light.Raw == plain + 20, "One-handed spear: +20.");
            Assert(CombatResolver.ToHit(Hero(pole, one), Ant()) is { OneHanderModifier: -40 } heavy && heavy.Raw == plain - 40, "Two-handed: -40.");
            Assert(CombatResolver.ToHit(Hero(CombatWeapon.UnarmedFor(one, 1), one), Ant()).OneHanderModifier == 0, "Bare hands: nothing.");
            var critter = new CombatantState("npc", "npc", null, 1, false, Centre, 0, one, spear, 30);
            Assert(CombatResolver.ToHit(critter, Hero(spear)).OneHanderModifier == 0, "Only the player has traits.");
            var chitsa = PlayerCharacter.Create("Uma", Special.Neutral with { Agility = 10 }, new[] { CharacterTrait.OneHander }, new[] { 0, 1, 2 }, 25, 1);
            Assert(DerivedCombatStats.ForPlayer(chitsa).OneHander && !DerivedCombatStats.ForPlayer(PlayerCharacter.Neutral).OneHander, "The sheet carries the trait.");
        });
        check("in a fight the perk's Strength can open an unarmed blow: power armor put on in the open inventory turns the punch into a Strong Punch", () =>
        {
            var world = WorldWithArmor();
            world.StartNewGame(PlayerCharacter.Create("Agil", new Special(5, 5, 5, 5, 5, 6, 9), Array.Empty<CharacterTrait>(), new[] { 0, 1, 2 }, 25, 0));
            TakeArmor(world);
            Assert(world.PlayerWeapon().Name == "Desarmado" && world.Sheet.Skill(DerivedCombatStats.Unarmed) == 52, "Unarmed 52: below the Strong Punch's 55.");
            var fight = world.BeginCombat(new CombatEncounter(world.Location, new[] { "map3:far-ant" }, "player"), world.PlayerWeapon(), new CombatTests.Scripted());
            var player = fight.Get("player");
            for (int guard = 0; guard < 10 && fight.Active != player; guard++) CombatAi.TakeTurn(fight);
            fight.Execute(CombatActionProposal.OpenInventory("player"));
            Assert(world.Wear(world.Carried.Single(c => c.Source.Header.Pid == 3), false) == HandStatus.Ready && player.Weapon.Name == "Strong Punch"
                && player.Stats.Skill(DerivedCombatStats.Unarmed) == 58 && fight.Log.OfType<WeaponsChanged>().Last().Blow == "Strong Punch", "ST 8: Unarmed 58, Strong Punch.");
            Assert(world.TakeOff(false) == HandStatus.Ready && player.Weapon.Name == "Desarmado" && player.Stats.MeleeDamage == 1, "Off again: the plain punch.");
        });
    }
}
