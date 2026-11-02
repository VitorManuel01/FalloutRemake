using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;

/// <summary>Firearms in combat: CE weapon profiles, ammunition spent and reloaded, the burst's spray and the weapons' perks.</summary>
internal static class FirearmTests
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    const int Centre = 20100;

    // Fields of the shipped PROs (--firearm-report): 10mm SMG single 5 AP / burst 6 AP of 10, range 25/20, 30 rounds of
    // caliber 8, 10mm JHP by default; 10mm JHP and AP; the Minigun's Big Gun flag; the Laser Pistol's laser damage.
    static Prototype Gun(int pid, int flags, int min, int max, int damageType, int range1, int range2, int strength, int ap1, int ap2, int burst,
        int caliber, int ammo, int capacity, int perk = -1, int failure = 0) => CombatTests.Item(pid, 3, 122, flags, put =>
    { put(97, failure); put(61, min); put(65, max); put(69, damageType); put(73, range1); put(77, range2); put(85, strength); put(89, ap1); put(93, ap2); put(101, perk); put(105, burst); put(109, caliber); put(113, ammo); put(117, capacity); });
    static WeaponPrototypeData Smg() => WeaponPrototypeData.Read(Gun(9, 0x76, 5, 12, 0, 25, 20, 4, 5, 6, 10, 8, 29, 30));
    static WeaponPrototypeData Pistol(int perk = -1, int failure = 0) => WeaponPrototypeData.Read(Gun(8, 0x6, 5, 12, 0, 25, 0, 3, 5, 0, 1, 8, 29, 12, perk, failure));
    static AmmoPrototypeData Jhp() => AmmoPrototypeData.Read(CombatTests.Item(29, 4, 81, 0, put => { put(57, 8); put(61, 24); put(65, 0); put(69, 25); put(73, 2); put(77, 1); }));
    static AmmoPrototypeData ArmorPiercing() => AmmoPrototypeData.Read(CombatTests.Item(30, 4, 81, 0, put => { put(57, 8); put(61, 24); put(65, 0); put(69, -25); put(73, 1); put(77, 2); }));

    static CombatantState Hero(CombatWeapon weapon, Func<DerivedCombatStats, DerivedCombatStats>? stats = null, params CombatAmmo[] ammo)
    {
        var s = (stats ?? (x => x))(DerivedCombatStats.ForCharacter(Special.Neutral));
        return new("player", "p", null, 0, true, Centre, 0, s, weapon, s.MaxHitPoints) { Ammunition = ammo };
    }
    static CombatantState AntAt(int hex, string key = "ant") =>
        CombatantState.FromMapCritter(CombatTests.Critter(hex, 0x0100017E, 6, 1, key), CritterPrototypeData.Read(CombatTests.AntPrototype()), key);
    static CombatSession Fight(ICombatRandom random, long time, params CombatantState[] combatants) =>
        new(combatants, new HexNavigation(Array.Empty<MapObject>(), 0), random, "player", time);

    // Every roll at the bottom of its range: each d100 hits, each damage roll is the minimum.
    sealed class Low : ICombatRandom { public int Between(int min, int max) => min; }
    sealed class High : ICombatRandom { public int Between(int min, int max) => max; }

    static bool OnLine(int from, int end, int hex) => OriginalStraightPath.FirstBlocked(from, end, t => t == hex, nodeStep: 32) == hex;

    public static void Run(Action<string, Action> check)
    {
        check("CE weapon profiles: the SMG's single and burst modes share its 30 rounds; Big Gun PROs train Big Guns, laser Energy Weapons; no burst aims", () =>
        {
            CombatWeapon single = CombatWeapon.Primary("SMG", Smg(), Jhp(), 7), burst = CombatWeapon.Secondary("SMG", Smg(), Jhp(), 7)!;
            Assert(single is { SkillIndex: DerivedCombatStats.SmallGuns, UsesAmmo: true, AmmoCapacity: 30, Rounds: 7, Caliber: 8, AmmoPid: 29, Burst: false, ActionPoints: 5, Range: 25 }
                && burst is { Burst: true, BurstRounds: 10, ActionPoints: 6, Range: 20, IsSecondary: true, IsRanged: true }, "SMG modes.");
            var minigun = WeaponPrototypeData.Read(Gun(12, 0x107, 7, 11, 0, 35, 0, 7, 6, 0, 40, 6, 35, 120));
            var laser = WeaponPrototypeData.Read(Gun(16, 0x6, 10, 22, 1, 35, 0, 3, 5, 0, 1, 3, 38, 12));
            Assert(CombatWeapon.Primary("Minigun", minigun) is { SkillIndex: DerivedCombatStats.BigGuns, Burst: true, BurstRounds: 40 }
                && CombatWeapon.Primary("Laser", laser).SkillIndex == DerivedCombatStats.EnergyWeapons, "Big Guns, Energy Weapons.");
            var flamer = WeaponPrototypeData.Read(Gun(11, 0x8, 45, 90, 2, 5, 0, 6, 6, 0, 0, 2, 32, 5));
            try { CombatWeapon.Primary("Flamer", flamer); throw new Exception("A continuous mode was accepted."); } catch (InvalidDataException) { }
            try { CombatWeapon.Primary("SMG", Smg(), Jhp(), 31); throw new Exception("31 rounds in a 30-round magazine."); } catch (InvalidDataException) { }
            Assert(!CombatSession.CanAim(Hero(burst)) && CombatSession.CanAim(Hero(single)), "critterCanAim refuses ANIM_FIRE_BURST.");
            Assert(CombatSession.AttackCost(Hero(burst), HitLocation.Uncalled) == 6 && CombatSession.AttackCost(Hero(burst, s => s with { FastShot = true }), HitLocation.Uncalled) == 5
                && CombatSession.AttackCost(Hero(burst, s => s with { Perks = new PerkRanks(new[] { new ChosenPerk(Perk.BonusRateOfFire, 15) }) }), HitLocation.Uncalled) == 5,
                "Fast Shot and Bonus Rate of Fire take 1 AP off.");
        });
        check("a shot spends one round, an empty gun is refused (COMBAT_BAD_SHOT_NO_AMMO), and a mode change keeps the rounds", () =>
        {
            CombatantState hero = Hero(CombatWeapon.Primary("Pistola", Pistol(), Jhp(), 2)), ant = AntAt(Centre + 1000);
            var fight = Fight(new Low(), CriticalRules.GameStartTicks, hero, ant);
            var events = fight.Execute(CombatActionProposal.Attack("player", "ant")).Events;
            Assert(events.OfType<AmmoSpent>().Single() == new AmmoSpent("player", 1, 1) && hero.Weapon.Rounds == 1 && !ant.Alive, "One round; JHP kills the ant.");
            var dry = Fight(new Low(), CriticalRules.GameStartTicks, Hero(CombatWeapon.Primary("Pistola", Pistol(), Jhp(), 0)), AntAt(Centre + 1000));
            Assert(dry.Preview(CombatActionProposal.Attack("player", "ant")).Status == CombatActionStatus.NoAmmo, "No ammo.");
            CombatantState gunner = Hero(CombatWeapon.Primary("SMG", Smg(), Jhp(), 7));
            var modes = Fight(new Low(), CriticalRules.GameStartTicks, gunner, AntAt(Centre + 1000));
            Assert(modes.ChangeMode(CombatWeapon.Secondary("SMG", Smg())!) && gunner.Weapon is { Burst: true, Rounds: 7, AmmoPid: 29 } && gunner.Weapon.Ammo?.Pid == 29, "Burst keeps the 7 JHP rounds.");
        });
        check("reload (_intface_item_reload): 2 AP, the loaded type first, another caliber-8 type only into an empty gun, until full; Fast Reload costs 1", () =>
        {
            var jhp = new CombatAmmo("jhp", Jhp(), 5); var ap = new CombatAmmo("ap", ArmorPiercing(), 30);
            CombatantState hero = Hero(CombatWeapon.Primary("Pistola", Pistol(), null, 0), null, ap, jhp);
            var fight = Fight(new Low(), CriticalRules.GameStartTicks, hero, AntAt(Centre + 1000));
            Assert(fight.Preview(CombatActionProposal.Reload("player")) is { Status: CombatActionStatus.Ready, ActionPointCost: 2 }, "2 AP.");
            var loaded = fight.Execute(CombatActionProposal.Reload("player")).Events.OfType<WeaponReloaded>().ToArray();
            Assert(loaded.Length == 1 && loaded[0] == new WeaponReloaded("player", "jhp", 29, 5, 5, 2) && jhp.Rounds == 0 && ap.Rounds == 30
                && hero.Weapon is { Rounds: 5, AmmoPid: 29 } && hero.ActionPoints == hero.Stats.MaxActionPoints - 2, "The PRO's JHP first; AP will not mix into a loaded gun.");
            Assert(fight.Preview(CombatActionProposal.Reload("player")).Status == CombatActionStatus.NoItem, "Nothing else fits.");
            CombatantState empty = Hero(CombatWeapon.Primary("Pistola", Pistol(), null, 0), null, new CombatAmmo("ap", ArmorPiercing(), 30));
            var other = Fight(new Low(), CriticalRules.GameStartTicks, empty, AntAt(Centre + 1000));
            other.Execute(CombatActionProposal.Reload("player"));
            Assert(empty.Weapon is { Rounds: 12, AmmoPid: 30 } && empty.Weapon.Ammo?.DamageDivisor == 2 && empty.Ammunition[0].Rounds == 18
                && other.Preview(CombatActionProposal.Reload("player")).Status == CombatActionStatus.AlreadyLoaded, "An empty gun takes AP rounds; full.");
            CombatantState quick = Hero(CombatWeapon.Primary("Speed Load", Pistol(Perk.WeaponFastReload), null, 0), null, new CombatAmmo("jhp", Jhp(), 24));
            Assert(Fight(new Low(), CriticalRules.GameStartTicks, quick, AntAt(Centre + 1000)).Preview(CombatActionProposal.Reload("player")).ActionPointCost == 1, "Fast Reload.");
        });
        check("a burst (_compute_spray) of 10: one round at the target, 2 more down the center, 3 left, 4 right; lines strike whoever they meet", () =>
        {
            CombatantState hero = Hero(CombatWeapon.Secondary("SMG", Smg(), Jhp(), 30)!), ant = AntAt(Centre + 1000);
            int range = 20, centerTile = ant.Hex, rotation = OriginalStraightPath.RotationTo(centerTile, Centre);
            int leftEnd = OriginalStraightPath.TileBeyond(Centre, FalloutGrid.Neighbor(centerTile, (rotation + 1) % 6)!.Value, range);
            int rightEnd = OriginalStraightPath.TileBeyond(Centre, FalloutGrid.Neighbor(centerTile, (rotation + 5) % 6)!.Value, range);
            // A bystander on the left line, between the shooter and the target's side.
            int bystanderHex = -1;
            OriginalStraightPath.FirstBlocked(Centre, leftEnd, t => { if (FalloutGrid.Distance(Centre, t) == 3) { bystanderHex = t; return true; } return false; }, nodeStep: 32);
            CombatantState bystander = AntAt(bystanderHex, "bystander");
            var fight = Fight(new Low(), CriticalRules.GameStartTicks, hero, ant, bystander);
            var events = fight.Execute(CombatActionProposal.Attack("player", "ant")).Events;
            BurstFired burst = events.OfType<BurstFired>().Single();
            int rightHits = OnLine(Centre, rightEnd, ant.Hex) && !OnLine(Centre, rightEnd, bystanderHex) ? 4 : 0;
            Assert(burst.RoundsFired == 10 && burst.MainRounds == 1 && burst.MainHits == 1 + 2 + rightHits, $"1 + 2 center hits on the target ({burst.MainHits}).");
            Assert(burst.Others.Length == 1 && burst.Others[0].TargetId == "bystander" && burst.Others[0].Rounds == 3, "The left line's 3 rounds strike the bystander.");
            DamageEstimate perRound = DamageCalculator.Estimate(hero, ant);
            Assert(events.OfType<AttackResolved>().Single().Damage == burst.MainHits * perRound.MinDamage && burst.Others[0].Damage == 3 * perRound.MinDamage, "Damage per round, added up.");
            Assert(!ant.Alive && !bystander.Alive && events.OfType<CombatantDied>().Select(d => d.CombatantId).SequenceEqual(new[] { "ant", "bystander" })
                && fight.Turn.ExperienceAwarded == 2 * 25 && events.OfType<AmmoSpent>().Single() == new AmmoSpent("player", 10, 20), "Both die; 10 rounds spent.");
        });
        check("a burst that hits nothing is a miss (one stray round), a short magazine fires what it holds, and a critical failure with LoseAmmo empties the gun", () =>
        {
            CombatantState hero = Hero(CombatWeapon.Secondary("SMG", Smg(), Jhp(), 4)!), ant = AntAt(Centre + 1000);
            var fight = Fight(new High(), CriticalRules.GameStartTicks, hero, ant);
            var events = fight.Execute(CombatActionProposal.Attack("player", "ant")).Events;
            Assert(events.OfType<BurstFired>().Single() is { RoundsFired: 4, MainRounds: 1, MainHits: 0, Others.Length: 0 } && !events.OfType<AttackResolved>().Single().Hit
                && ant.Alive && events.OfType<AmmoSpent>().Single() == new AmmoSpent("player", 4, 0), "4 rounds, no hit.");
            // Day 6: the dude's critical failures bite. d100 100 misses, the critical die 1 makes it critical, effect roll 30 is
            // column 1 of failure table 2: LoseAmmo.
            CombatantState shooter = Hero(CombatWeapon.Primary("Pistola", Pistol(failure: 2), Jhp(), 9));
            var late = Fight(new CombatTests.Scripted(100, 1, 30), CriticalRules.GameStartTicks + 6 * CriticalRules.TicksPerDay, shooter, AntAt(Centre + 1000));
            var failed = late.Execute(CombatActionProposal.Attack("player", "ant")).Events;
            Assert(failed.OfType<CriticalFailure>().Single().Flags == DamageFlags.LoseAmmo && failed.OfType<AmmoSpent>().Single() == new AmmoSpent("player", 9, 0)
                && shooter.Weapon.Rounds == 0, "The magazine is lost: ");
        });
        check("weapon perks: Long Range x4 PE, Scope Range x5 PE beyond 8 hexes and the missing hexes closer, Accurate +20, Penetrate keeps 20% of DT", () =>
        {
            CombatantState Shooter(int perk) => Hero(CombatWeapon.Primary("Gun", Pistol(perk), Jhp(), 12));
            CombatantState far = AntAt(Centre + 2000), near = AntAt(Centre + 1000);
            Assert(FalloutGrid.Distance(Centre, far.Hex) == 10 && FalloutGrid.Distance(Centre, near.Hex) == 5, "10 and 5 hexes.");
            Assert(CombatResolver.ToHit(Shooter(-1), far).DistanceModifier == -16, "(10 - 2x3) x -4.");
            Assert(CombatResolver.ToHit(Shooter(Perk.WeaponLongRange), far).DistanceModifier == 8, "max(10 - 4x3, -10) x -4 = +8.");
            Assert(CombatResolver.ToHit(Shooter(Perk.WeaponScopeRange), far).DistanceModifier == 20 && CombatResolver.ToHit(Shooter(Perk.WeaponScopeRange), near).DistanceModifier == -52,
                "Scope: (10 - 15) x -4 = +20; 5 hexes is under 8: (5 + 8) x -4.");
            var accurate = CombatResolver.ToHit(Shooter(Perk.WeaponAccurate), far);
            Assert(accurate.AccurateBonus == 20 && accurate.Raw == CombatResolver.ToHit(Shooter(-1), far).Raw + 20, "Accurate.");
            Assert(Shooter(Perk.WeaponPenetrate).Weapon.Penetrates && !Shooter(-1).Weapon.Penetrates, "Penetrate.");
        });
    }
}
