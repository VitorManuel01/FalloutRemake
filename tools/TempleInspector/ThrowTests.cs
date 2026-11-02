using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;

/// <summary>The spear's second attack: CE's throw rules, the line of fire, the stray shot and the spear left on the ground.</summary>
internal static class ThrowTests
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static readonly int Centre = 20100;
    static CombatWeapon Spear() => CombatWeapon.Primary("Spear", WeaponPrototypeData.Read(CombatTests.Spear()));
    static CombatWeapon Thrown() => CombatWeapon.Secondary("Spear", WeaponPrototypeData.Read(CombatTests.Spear()))!;
    static CombatantState Hero(CombatWeapon weapon, Special? special = null)
    {
        var stats = DerivedCombatStats.ForCharacter(special ?? Special.Neutral);
        return new("player", "p", null, 0, true, Centre, 0, stats, weapon, stats.MaxHitPoints);
    }
    static CombatantState AntAt(int hex, string key = "ant") =>
        CombatantState.FromMapCritter(CombatTests.Critter(hex, 0x0100017E, 6, 1, key), CritterPrototypeData.Read(CombatTests.AntPrototype()), key);
    static CombatSession Fight(HexNavigation navigation, ICombatRandom random, params CombatantState[] combatants) => new(combatants, navigation, random, "player");
    static HexNavigation Open() => new(Array.Empty<MapObject>(), 0);

    public static void Run(Action<string, Action> check)
    {
        check("the spear's second attack is CE's throw: Throwing skill, its second range and AP, no melee damage bonus, at most 3 x ST hexes away", () =>
        {
            CombatWeapon thrust = Spear(), thrown = Thrown();
            Assert(!thrust.Throws && thrust.Range == 2 && thrust.ActionPoints == 4 && thrust.SkillIndex == DerivedCombatStats.MeleeWeapons && thrust.AddsMeleeDamage, "The thrust.");
            Assert(thrown.Throws && thrown.IsSecondary && thrown.Range == 8 && thrown.ActionPoints == 6 && thrown.SkillIndex == DerivedCombatStats.Throwing
                && !thrown.AddsMeleeDamage && thrown.IsRanged && thrown.MinDamage == thrust.MinDamage && thrown.MaxDamage == thrust.MaxDamage, "The throw.");
            Assert(CombatWeapon.Secondary("Pistola", WeaponPrototypeData.Read(CombatTests.Pistol())) == null, "No second attack, no second mode.");
            Assert(CombatSession.Range(Hero(thrown)) == 8 && CombatSession.Range(Hero(thrown, new Special(2, 5, 5, 5, 5, 5, 5))) == 6
                && CombatSession.Range(Hero(thrust, new Special(1, 5, 5, 5, 5, 5, 5))) == 2, "ST 5: 8 hexes; ST 2: 6; a thrust keeps its reach.");
            var estimate = DamageCalculator.Estimate(Hero(thrown, new Special(10, 5, 5, 5, 5, 5, 5)), AntAt(Centre + 600));
            Assert(estimate.MaxRoll == thrust.MaxDamage, "Melee damage never joins a throw.");
        });
        check("a throw that hits leaves the spear on the struck creature's hex; without the world the hand falls back to its bare blow", () =>
        {
            CombatantState hero = Hero(Thrown()), ant = AntAt(Centre + 600);
            var fight = Fight(Open(), new CombatTests.Scripted(1, 5), hero, ant);
            CombatActionPreview preview = fight.Preview(CombatActionProposal.Attack("player", "ant"));
            Assert(preview.Status == CombatActionStatus.Ready && preview.ActionPointCost == 6 && preview.ToHit!.SkillIndex == DerivedCombatStats.Throwing, "Three hexes away, 6 AP.");
            fight.Execute(CombatActionProposal.Attack("player", "ant"));
            Assert(fight.Log.OfType<AttackResolved>().Single().Hit && fight.Log.OfType<WeaponThrown>().Single() is { Weapon: "Spear" } thrown && thrown.LandingHex == ant.Hex
                && hero.Weapon is { Pid: null, Name: "Desarmado" }, "Hit: on the ant's hex, the hand empty.");
        });
        check("a missed throw flies on its range past the target (CE _tile_num_beyond) and strikes the first creature beyond it, which keeps the spear", () =>
        {
            CombatantState hero = Hero(Thrown()), ant = AntAt(Centre + 600);
            int landing = OriginalStraightPath.TileBeyond(Centre, ant.Hex, 8);
            Assert(FalloutGrid.Distance(Centre, landing) == 8 && FalloutGrid.Distance(ant.Hex, landing) == 5, "Eight hexes out, five past the ant.");
            var fight = Fight(Open(), new CombatTests.Scripted(100), hero, ant);
            fight.Execute(CombatActionProposal.Attack("player", "ant"));
            Assert(!fight.Log.OfType<StrayHit>().Any() && fight.Log.OfType<WeaponThrown>().Single().LandingHex == landing, "Nobody beyond: it lands eight hexes out.");

            hero = Hero(Thrown()); ant = AntAt(Centre + 600);
            CombatantState behind = AntAt(OriginalStraightPath.TileBeyond(Centre, ant.Hex, 5), "behind");
            fight = Fight(Open(), new CombatTests.Scripted(100, 7), hero, ant, behind);
            fight.Execute(CombatActionProposal.Attack("player", "ant"));
            Assert(fight.Log.OfType<StrayHit>().Single() is { TargetId: "behind", DamageRoll: 7 } stray && behind.HitPoints == Math.Max(0, 6 - stray.Damage) && ant.HitPoints == 6
                && fight.Log.OfType<WeaponThrown>().Single().LandingHex == behind.Hex, "The ant behind takes the stray throw, which lands on its hex.");
        });
        check("CE _combat_check_bad_shot: a wall in the line refuses a throw or a reach-2 thrust; each standing creature in the way costs a throw 10 to hit", () =>
        {
            var wall = new HexNavigation(new[] { CombatTests.Blocker(Centre + 200, 3, 0x03000001, "wall") }, 0);
            Assert(Fight(wall, new CombatTests.Scripted(), Hero(Thrown()), AntAt(Centre + 600)).Preview(CombatActionProposal.Attack("player", "ant")).Status == CombatActionStatus.AimBlocked,
                "Throw over a wall: refused.");
            Assert(Fight(wall, new CombatTests.Scripted(), Hero(Spear()), AntAt(Centre + 400)).Preview(CombatActionProposal.Attack("player", "ant")).Status == CombatActionStatus.AimBlocked,
                "The spear's reach of 2 past a wall: refused.");
            var thrown = Hero(Thrown());
            int clear = Fight(Open(), new CombatTests.Scripted(), thrown, AntAt(Centre + 600)).Preview(CombatActionProposal.Attack("player", "ant")).ToHit!.DistanceModifier;
            thrown = Hero(Thrown());
            CombatActionPreview crowded = Fight(Open(), new CombatTests.Scripted(), thrown, AntAt(Centre + 600), AntAt(Centre + 200, "between"))
                .Preview(CombatActionProposal.Attack("player", "ant"));
            Assert(crowded.Status == CombatActionStatus.Ready && crowded.ToHit!.DistanceModifier == clear - 10, "One ant in the way: -10.");
            var shoot = new HexNavigation(new[] { CombatTests.Blocker(Centre + 200, 2, 0x02000001, "bush") with { Header = CombatTests.Blocker(Centre + 200, 2, 0x02000001, "bush").Header with { Flags = HexNavigation.NoBlock } } }, 0);
            Assert(shoot.Walkable(Centre + 200) && shoot.ShotBlockersAt(Centre + 200).Count == 1, "Scenery walkers pass but shots do not (no shoot-through flag).");
            var through = new HexNavigation(new[] { CombatTests.Blocker(Centre + 200, 2, 0x02000001, "grass") with { Header = CombatTests.Blocker(Centre + 200, 2, 0x02000001, "grass").Header with { Flags = HexNavigation.NoBlock | HexNavigation.ShootThru } } }, 0);
            Assert(through.ShotBlockersAt(Centre + 200).Count == 0, "Walk- and shoot-through scenery stops nothing.");
        });
        check("in the world a held spear switches to its throw; thrown, it leaves the hand and the bag for the ground, survives the checkpoint and is picked up beside it", () =>
        {
            var world = DuelRulesTests.WorldWithSpear();
            CarriedItem spear = world.Carried.Single(c => c.Source.Header.Pid == 7);
            Assert(world.ToggleAttackMode(false) == HandStatus.NoItem, "Bare hand: nothing to switch.");
            world.Wield(spear, Hand.Left, false);
            Assert(world.ToggleAttackMode(true) == HandStatus.Busy && world.ToggleAttackMode(false) == HandStatus.Ready && world.UsesSecondMode(Hand.Left)
                && world.PlayerWeapon() is { Throws: true, ActionPoints: 6 }, "The spear's throw.");
            Assert(world.ToggleAttackMode(false) == HandStatus.Ready && !world.PlayerWeapon().Throws && world.ToggleAttackMode(false) == HandStatus.Ready, "Back and forth.");
            // The closed door (hex 20300) stands between the player (20100) and the near ant (20700).
            var shut = world.BeginCombat(new CombatEncounter(world.Location, new[] { "map3:near-ant" }, "player"), world.PlayerWeapon(), new CombatTests.Scripted());
            Assert(shut.Preview(CombatActionProposal.Attack("player", "map3:near-ant")).Status == CombatActionStatus.AimBlocked, "The closed door blocks the throw.");
            world.AbandonCombat();
            Assert(world.Current.Doors.Toggle("map3:door", 20301, 0, false) == DoorUseStatus.Ready, "Door opened.");
            var fight = world.BeginCombat(new CombatEncounter(world.Location, new[] { "map3:near-ant" }, "player"), world.PlayerWeapon(), new CombatTests.Scripted(1, 5));
            CombatantState player = fight.Get("player"), ant = fight.Get("map3:near-ant");
            CombatActionPreview throwing = fight.Preview(CombatActionProposal.Attack("player", ant.Id));
            Assert(throwing.Status == CombatActionStatus.Ready && throwing.ActionPointCost == 6, $"A throw at the near ant ({throwing.Status}).");
            fight.Execute(CombatActionProposal.Attack("player", ant.Id));
            WeaponThrown thrown = fight.Log.OfType<WeaponThrown>().Single();
            var (map, row) = world.GroundItems.Single();
            Assert(thrown.LandingHex == ant.Hex && world.Held(Hand.Left) == null && player.Weapon.Pid == null && !world.Carried.Any(c => c.Source.Header.Pid == 7)
                && map == 3 && row.Place == new ActorLocation(3, 0, ant.Hex, 0) && row.Quantity == 1
                && world.Current.Items.Balance(row.OwnerKey, row.ItemKey) is { Carried: 0, OnGround: 1 }, "Out of the hand and the bag, on the ant's hex.");
            Assert(world.CanPickUp(3, world.GroundItems.Single().Item, false) == ItemTakeStatus.TooFar, "Three hexes away: out of reach.");
            world.AbandonCombat();
            var saved = CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog")));
            WorldSession Load(WorldCheckpoint checkpoint) => WorldSession.Restore(_ => DuelRulesTests.WorldWithSpearMap(), new[] { 3, 126 }, checkpoint, "catalog");
            SavedArea area = saved.Areas.Single(a => a.Map == 3);
            Assert(area.Ground is { Length: 1 } && Load(saved).GroundItems.Single().Item == row, "The ground row comes back.");
            foreach (GroundItem[]? bad in new[] { new[] { row with { Quantity = 2 } }, new[] { row with { Place = row.Place with { Map = 99 } } },
                new[] { row with { Place = row.Place with { Rotation = 1 } } }, new[] { row, row }, Array.Empty<GroundItem>() })
                try { Load(saved with { Areas = saved.Areas.Select(a => a.Map == 3 ? a with { Ground = bad } : a).ToArray() }); throw new Exception("Forged ground accepted."); }
                catch (InvalidDataException) { }
            world.Current.Travel.Arrive(new(3, 0, 20100, 0));
            Assert(world.CanPickUp(3, row, false) == ItemTakeStatus.TooFar && world.CanPickUp(3, row, true) == ItemTakeStatus.Busy, "Too far, or walking.");
            world.Current.Travel.Arrive(new(3, 0, 20500, 0));
            ItemTakeStatus picked = world.PickUp(3, row, false);
            Assert(picked == ItemTakeStatus.Ready && world.GroundItems.Length == 0 && world.Carried.Any(c => c.Source.Header.Pid == 7)
                && world.Held(Hand.Left) == null, $"Picked up into the bag, not the hand ({picked}).");
            world.Reset();
            Assert(world.GroundItems.Length == 0, "A new game leaves nothing on the ground.");
        });
    }
}
