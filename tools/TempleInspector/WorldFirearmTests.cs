using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;

/// <summary>Firearms in the world: CE ammunition stacks taken and returned, reloads, loaded weight, fights and checkpoints.</summary>
internal static class WorldFirearmTests
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Reject(Action action, string message) { try { action(); } catch (InvalidDataException) { return; } throw new Exception(message); }

    static Prototype Jhp() => CombatTests.Item(29, 4, 81, 0, put => { put(57, 8); put(61, 24); put(65, 0); put(69, 25); put(73, 2); put(77, 1); });

    // WorldCombatTests' map 3 with a pot holding an empty 10mm pistol (12 rounds, JHP by default), 10mm JHP boxes of 24
    // (one with 11 rounds, a stack of 3 whose top has 10) and the spear.
    static MapDocument Map()
    {
        MapObject Item(string key, int pid, int subtype, Dictionary<string, int> state) => CombatTests.Blocker(-1, 0, pid, key) with
            { Subtype = subtype, Header = CombatTests.Blocker(-1, 0, pid, key).Header with { Tile = -1 }, State = state };
        var pot = CombatTests.Blocker(20000 + 199 - 97, 0, 0x000000FA, "map3:pot") with { Subtype = 1, InventoryCount = 4, State = new Dictionary<string, int> { ["flags"] = 0 },
            Inventory = new[]
            {
                new InventoryEntry(0, 1, Item("map3:pistol", 8, 3, new() { ["ammoQuantity"] = 0, ["ammoTypePid"] = 29 })),
                new InventoryEntry(0, 1, Item("map3:jhp-a", 29, 4, new() { ["ammoQuantity"] = 11 })),
                new InventoryEntry(0, 3, Item("map3:jhp-b", 29, 4, new() { ["ammoQuantity"] = 10 })),
                new InventoryEntry(0, 1, Item("map3:spear", 7, 3, new() { ["ammoQuantity"] = 0 }))
            } };
        var baseMap = WorldCombatTests.Map();
        return baseMap with { ObjectLayers = new[] { baseMap.ObjectLayers[0] with { Objects = baseMap.ObjectLayers[0].Objects.Append(pot).ToArray(),
                DeclaredCount = baseMap.ObjectLayers[0].Objects.Length + 1 } },
            Prototypes = baseMap.Prototypes.Append(CombatTests.Spear()).Append(CombatTests.Pistol()).Append(Jhp()).Append(CombatTests.Item(0xFA, 1, 65, 0, _ => { })).ToArray() };
    }
    // Beside the pot, off the exit grid where the player starts.
    static WorldSession World()
    {
        var world = new WorldSession(_ => Map(), new[] { 3, 126 });
        world.Current.Travel.Arrive(new(3, 0, 20101, 0));
        return world;
    }
    static CarriedItem Carried(WorldSession world, string key) => world.Carried.Single(c => c.Source.Key == key);
    static int Rounds(WorldSession world, string key) => world.Carried.SingleOrDefault(c => c.Source.Key == key) is { } c ? world.CarriedRounds(c) : 0;
    static WorldSession Load(WorldCheckpoint checkpoint) => WorldSession.Restore(_ => Map(), new[] { 3, 126 }, checkpoint, "catalog");
    static WorldCheckpoint Saved(WorldSession world) => CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog")));

    public static void Run(Action<string, Action> check)
    {
        check("ammunition stacks follow CE itemRemove and itemAdd: the taker gets the partial top box, the rest stay full, and the rounds add up", () =>
        {
            var world = World();
            Assert(world.TakeItem("map3:pot", "map3:jhp-b", 1, false) == ItemTakeStatus.Ready && Rounds(world, "map3:jhp-b") == 10, "The top box (10 rounds) comes first.");
            Assert(world.TakeItem("map3:pot", "map3:jhp-b", 1, false) == ItemTakeStatus.Ready && Rounds(world, "map3:jhp-b") == 34, "Then a full box: 10 + 24.");
            Assert(world.ReturnItem(3, "map3:pot", "map3:jhp-b", 1, false) == ItemTakeStatus.Ready && Rounds(world, "map3:jhp-b") == 24, "The partial box goes back with the return.");
            Assert(world.TakeItem("map3:pot", "map3:jhp-b", 2, false) == ItemTakeStatus.Ready && Rounds(world, "map3:jhp-b") == 58
                && world.Current.Items.Balance("map3:pot", "map3:jhp-b") is { Carried: 3, Remaining: 0, Consumed: 0 }, "All three boxes: 10 + 24 + 24.");
        });
        check("a reload outside a fight costs nothing, fills the gun from the loaded type, drops emptied boxes and makes the gun heavier", () =>
        {
            var world = World();
            world.TakeItem("map3:pot", "map3:pistol", 1, false); world.TakeItem("map3:pot", "map3:jhp-b", 3, false);
            CarriedItem pistol = Carried(world, "map3:pistol");
            long empty = world.UnitWeight(3, pistol.Source)!.Value;
            Assert(world.Wield(pistol, Hand.Left, false) == HandStatus.Ready && world.HandWeapon(Hand.Left) is { Rounds: 0, AmmoCapacity: 12, AmmoPid: 29 }, "Wielded empty.");
            Assert(world.CanReload(Hand.Left, true) == ReloadStatus.Busy && world.CanReload(Hand.Right, false) == ReloadStatus.NoWeapon, "Not while walking; the bare hand has nothing.");
            Assert(world.Reload(Hand.Left, false) == ReloadStatus.Ready && world.HandWeapon(Hand.Left) is { Rounds: 12 } && world.HandWeapon(Hand.Left).Ammo?.DamageMultiplier == 2
                && Rounds(world, "map3:jhp-b") == 46 && world.Current.Items.Balance("map3:pot", "map3:jhp-b") is { Carried: 2, Consumed: 1 }, "12 of 58: the 10-round box is emptied and dropped.");
            Assert(world.CanReload(Hand.Left, false) == ReloadStatus.AlreadyLoaded, "Full.");
            int box = Jhp().BaseWeight!.Value;
            Assert(world.UnitWeight(3, pistol.Source) == empty + box, "itemGetWeight: 12 JHP rounds weigh one box.");
        });
        check("two partial tops that fit one box merge into it (itemAdd), when taking and when returning: one box object goes away", () =>
        {
            var world = World();
            world.TakeItem("map3:pot", "map3:jhp-b", 2, false);
            WorldCheckpoint saved = Saved(world);
            // The player's top box has 12 rounds (12 missing), the pot's top 4 (20 missing).
            var pools = new SavedFirearms(Ammo: new[] { new SavedAmmoPool(3, "map3:jhp-b", 12, 20) });
            var taking = Load(saved with { Firearms = pools });
            Assert(Rounds(taking, "map3:jhp-b") == 36 && taking.TakeItem("map3:pot", "map3:jhp-b", 1, false) == ItemTakeStatus.Ready
                && Rounds(taking, "map3:jhp-b") == 40 && taking.Current.Items.Balance("map3:pot", "map3:jhp-b") is { Carried: 2, Consumed: 1 }, "12 + 24 + 4: two boxes, top 16.");
            var returning = Load(saved with { Firearms = pools });
            Assert(returning.ReturnItem(3, "map3:pot", "map3:jhp-b", 1, false) == ItemTakeStatus.Ready && Rounds(returning, "map3:jhp-b") == 24
                && returning.Current.Items.Balance("map3:pot", "map3:jhp-b") is { Carried: 1, Remaining: 1, Consumed: 1 }, "The 12-round top joins the pot's 4: one box of 16 stays in the pot.");
            Assert(returning.Capture("catalog").Firearms!.Ammo!.Single() == new SavedAmmoPool(3, "map3:jhp-b", 0, 8), "The pot's top misses 8.");
        });
        check("a fight reloads from the carried boxes (2 AP) and its shots and reloads come back as the gun's load and the boxes left", () =>
        {
            var world = World();
            world.TakeItem("map3:pot", "map3:pistol", 1, false); world.TakeItem("map3:pot", "map3:jhp-a", 1, false);
            world.Wield(Carried(world, "map3:pistol"), Hand.Left, false); world.Reload(Hand.Left, false);
            Assert(world.HandWeapon(Hand.Left).Rounds == 11 && world.Carried.All(c => c.Source.Key != "map3:jhp-a"), "The 11-round box fills 11 and is gone.");
            world.TakeItem("map3:pot", "map3:jhp-b", 3, false);
            var fight = world.BeginCombat(WorldCombatTests.NearAnt(world), world.PlayerWeapon(), new Low());
            bool reloaded = false;
            for (int guard = 0; guard < 40 && !fight.Turn.Over; guard++)
            {
                if (fight.Active is not { IsPlayer: true }) { ProvisionalMeleeBehavior.TakeTurn(fight); continue; }
                if (!reloaded)
                {
                    var events = fight.Execute(CombatActionProposal.Reload("player")).Events;
                    Assert(events.OfType<WeaponReloaded>().Single() is { RoundsLoaded: 1, Rounds: 12, ActionPointsSpent: 2, AmmoPid: 29 }, "One round from the 10-round top.");
                    reloaded = true;
                }
                var shot = fight.Execute(CombatActionProposal.Attack("player", "map3:near-ant"));
                if (shot.Status != CombatActionStatus.Ready) fight.Execute(CombatActionProposal.EndTurn("player"));
            }
            Assert(fight.Turn.Over && world.CommitCombat(fight) == CombatCommitStatus.Ready, "Won.");
            Assert(world.HandWeapon(Hand.Left).Rounds == 11 && Rounds(world, "map3:jhp-b") == 57, "Reloaded to 12, one shot: 11; the stack gave 1 of 58.");
            WorldCheckpoint saved = Saved(world);
            Assert(saved.Firearms!.Weapons!.Single() == new SavedWeaponLoad(3, "map3:pistol", 29, 11), "The load is saved.");
            var restored = Load(saved);
            Assert(restored.HandWeapon(Hand.Left).Rounds == 11 && Rounds(restored, "map3:jhp-b") == 57, "Restored.");
            WorldCheckpoint With(SavedWeaponLoad load) => saved with { Firearms = saved.Firearms with { Weapons = new[] { load } } };
            WorldCheckpoint Pool(SavedAmmoPool pool) => saved with { Firearms = saved.Firearms with { Ammo = new[] { pool } } };
            SavedAmmoPool pool = saved.Firearms.Ammo!.Single(p => p.ItemKey == "map3:jhp-b");
            foreach (WorldCheckpoint bad in new[]
            {
                With(new(3, "map3:pistol", 29, 13)), With(new(3, "map3:pistol", 30, 5)), With(new(3, "map3:spear", 29, 1)), With(new(3, "map3:ghost", 29, 1)),
                Pool(pool with { PlayerDeficit = 24 }), Pool(pool with { ContainerDeficit = -1 }), Pool(pool with { ItemKey = "map3:pistol" }),
                saved with { Firearms = new SavedFirearms() }, saved with { Firearms = new SavedFirearms(Array.Empty<SavedWeaponLoad>()) }
            })
                Reject(() => Load(bad), "Forged firearms accepted.");
        });
        check("a fight dropped without being applied leaves the gun and the boxes as they were", () =>
        {
            var world = World();
            world.TakeItem("map3:pot", "map3:pistol", 1, false); world.TakeItem("map3:pot", "map3:jhp-b", 1, false);
            world.Wield(Carried(world, "map3:pistol"), Hand.Left, false); world.Reload(Hand.Left, false);
            var fight = world.BeginCombat(WorldCombatTests.NearAnt(world), world.PlayerWeapon(), new Low());
            for (int guard = 0; guard < 40 && fight.Active is not { IsPlayer: true }; guard++) ProvisionalMeleeBehavior.TakeTurn(fight);
            fight.Execute(CombatActionProposal.Attack("player", "map3:near-ant"));
            world.AbandonCombat();
            Assert(world.HandWeapon(Hand.Left).Rounds == 10 && Rounds(world, "map3:jhp-b") == 0, "Still the 10 rounds loaded before.");
        });
    }

    sealed class Low : ICombatRandom { public int Between(int min, int max) => min; }
}
