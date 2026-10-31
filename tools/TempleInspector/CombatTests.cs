using System.Buffers.Binary;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime.Combat;

internal static class CombatTests
{
    internal sealed class Scripted : ICombatRandom
    {
        private readonly Queue<int> _values;
        public Scripted(params int[] values) => _values = new Queue<int>(values);
        public int Between(int min, int max)
        {
            int value = _values.Count > 0 ? _values.Dequeue() : throw new Exception("Scripted random exhausted.");
            return value >= min && value <= max ? value : throw new Exception($"Scripted value {value} outside {min}..{max}.");
        }
        public int Left => _values.Count;
    }

    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static int Tile(int column, int row) => row * 200 + 199 - column;

    static Prototype Item(int pid, int subtype, int length, int extendedFlags, Action<Func<int, int, int>> fill)
    {
        var bytes = new byte[length];
        int Put(int offset, int value) { BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(offset, 4), value); return value; }
        Put(0, pid); Put(24, extendedFlags); Put(28, -1); Put(32, subtype); Put(44, 3);
        fill(Put);
        return PrototypeCatalog.Read(new GameResource($"proto/items/{pid}.pro", "synthetic", bytes), pid);
    }
    internal static Prototype Spear() => Item(7, 3, 122, 0xA054, put =>
    { put(57, 4); put(61, 3); put(65, 10); put(69, 0); put(73, 2); put(77, 8); put(81, 0x5000007); put(85, 4); put(89, 4); put(93, 6); put(101, -1); put(113, -1); });
    static Prototype Pistol() => Item(8, 3, 122, 0x6, put =>
    { put(61, 5); put(65, 12); put(73, 25); put(85, 3); put(89, 5); put(105, 1); put(109, 8); put(113, 29); put(117, 12); });
    static Prototype Jhp() => Item(29, 4, 81, 0, put => { put(57, 8); put(61, 24); put(65, -5); put(69, 25); put(73, 2); put(77, 1); });

    // Giant Ant layout from the shipped PRO: base stats derived from SPECIAL plus the creature's bonus block.
    static CritterPrototypeData Ant(int unarmedPoints = 25) => CritterPrototypeData.Read(AntPrototype(unarmedPoints));
    internal static Prototype AntPrototype(int unarmedPoints = 25)
    {
        var bytes = new byte[416];
        void Put(int offset, int value) => BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(offset, 4), value);
        Put(0, 0x0100017E); Put(28, -1); Put(40, 1);
        int[] special = { 1, 2, 1, 1, 1, 4, 1 };
        for (int i = 0; i < 7; i++) Put(48 + 4 * i, special[i]);
        Put(48 + 4 * 7, 18); Put(48 + 4 * 8, 7); Put(48 + 4 * 9, 4); Put(48 + 4 * 11, 1); Put(48 + 4 * 13, 4); Put(48 + 4 * 15, 1);
        Put(188 + 4 * 7, -12); Put(188 + 4 * 8, -2); Put(188 + 4 * 11, 1); Put(188 + 4 * 13, 5); Put(188 + 4 * 29, 500);
        Put(328 + 4 * 3, unarmedPoints); Put(404, 25); Put(412, 0);
        return PrototypeCatalog.Read(new GameResource("proto/critters/00000382.pro", "synthetic", bytes), 0x0100017E);
    }

    internal static MapObject Critter(int tile, int pid, int hp, int team, string key) => new(key, tile, 0, 0, 0,
        new ObjectHeader(42, tile, 0, 0, 0, 0, 0, 0, 0x01000000, 0, 0, pid, -1, 0, 0, 0, -1, -1),
        0, 0, 0, null, new Dictionary<string, int> { ["hp"] = hp, ["team"] = team }, Array.Empty<InventoryEntry>());
    internal static MapObject Blocker(int tile, int type, int pid, string key) => new(key, tile, 0, 0, 0,
        new ObjectHeader(43, tile, 0, 0, 0, 0, 0, 0, type << 24, 0, 0, pid, -1, 0, 0, 0, -1, -1),
        0, 0, 0, null, new Dictionary<string, int>(), Array.Empty<InventoryEntry>());

    static CombatantState Player(int hex, CombatWeapon? weapon = null, Special? special = null)
    {
        var stats = DerivedCombatStats.ForCharacter(special ?? Special.Neutral);
        return new("player", "Escolhido", null, 0, true, hex, 0, stats, weapon ?? CombatWeapon.Primary("Lança", WeaponPrototypeData.Read(Spear())), stats.MaxHitPoints);
    }
    static CombatantState AntAt(int hex, string key = "ant", int hp = 6) =>
        CombatantState.FromMapCritter(Critter(hex, 0x0100017E, hp, 1, key), Ant(), "Formiga gigante");
    static HexNavigation Open(params MapObject[] objects) => new(objects, 0);

    public static void Run(Action<string, Action> check)
    {
        check("combat ARM-001 reads weapon, armor and ammo PRO blocks at CE offsets and refuses other layouts", () =>
        {
            var spear = WeaponPrototypeData.Read(Spear());
            Assert(spear.MinDamage == 3 && spear.MaxDamage == 10 && spear.PrimaryRange == 2 && spear.SecondaryRange == 8
                && spear.PrimaryActionPoints == 4 && spear.SecondaryActionPoints == 6 && spear.MinStrength == 4 && spear.ProjectilePid == 0x5000007, "Spear fields.");
            Assert(spear.PrimaryMode == WeaponAttackMode.Thrust && spear.SecondaryMode == WeaponAttackMode.Throw
                && WeaponPrototypeData.TypeOf(spear.PrimaryMode) == WeaponAttackType.Melee && WeaponPrototypeData.TypeOf(spear.SecondaryMode) == WeaponAttackType.Throw, "Spear modes.");
            var pistol = WeaponPrototypeData.Read(Pistol());
            Assert(pistol.PrimaryMode == WeaponAttackMode.Single && pistol.AmmoCapacity == 12 && pistol.Caliber == 8 && pistol.AmmoPid == 29 && pistol.BurstRounds == 1, "Pistol fields.");
            var armor = ArmorPrototypeData.Read(Item(1, 0, 129, 0, put =>
            { put(57, 15); for (int i = 0; i < 7; i++) { put(61 + 4 * i, 20 + i); put(89 + 4 * i, i); } put(117, -1); put(121, 7); put(125, 8); }));
            Assert(armor.ArmorClass == 15 && armor.DamageResistance.SequenceEqual(new[] { 20, 21, 22, 23, 24, 25, 26 })
                && armor.DamageThreshold.SequenceEqual(new[] { 0, 1, 2, 3, 4, 5, 6 }) && armor.MaleFid == 7 && armor.FemaleFid == 8, "Armor fields.");
            armor.DamageResistance[0] = 99; Assert(armor.DamageResistance[0] == 20, "Armor arrays are copies.");
            var ammo = AmmoPrototypeData.Read(Jhp());
            Assert(ammo.Caliber == 8 && ammo.Quantity == 24 && ammo.ArmorClassModifier == -5 && ammo.DamageResistanceModifier == 25
                && ammo.DamageMultiplier == 2 && ammo.DamageDivisor == 1, "Ammo fields.");
            foreach (Action wrong in new Action[] { () => WeaponPrototypeData.Read(Jhp()), () => ArmorPrototypeData.Read(Spear()), () => AmmoPrototypeData.Read(Spear()) })
                try { wrong(); throw new Exception("Wrong item subtype accepted."); } catch (InvalidDataException) { }
            try { WeaponPrototypeData.Read(Item(9, 3, 122, 0x9, put => { put(61, 5); put(65, 1); })); throw new Exception("Inverted damage accepted."); }
            catch (InvalidDataException) { }
        });
        check("combat COM-001 derives the neutral level-1 character like CE critterUpdateDerivedStats and skillGetValue", () =>
        {
            var stats = DerivedCombatStats.ForCharacter(Special.Neutral);
            Assert(stats.MaxHitPoints == 30 && stats.MaxActionPoints == 7 && stats.ArmorClass == 5 && stats.MeleeDamage == 1
                && stats.Sequence == 10 && stats.CriticalChance == 5, "Neutral derived stats.");
            Assert(stats.CombatSkills.SequenceEqual(new[] { 25, 10, 10, 50, 40, 20 }), "Neutral combat skills (unarmed 50, melee 40).");
            var strong = DerivedCombatStats.ForCharacter(new Special(9, 8, 2, 5, 5, 3, 7));
            Assert(strong.MaxHitPoints == 28 && strong.MaxActionPoints == 6 && strong.MeleeDamage == 4 && strong.Sequence == 16, "Derived formulas.");
            var armor = ArmorPrototypeData.Read(Item(1, 0, 129, 0, put => { put(57, 15); put(61, 25); put(89, 2); }));
            var armored = DerivedCombatStats.ForCharacter(Special.Neutral, armor);
            Assert(armored.ArmorClass == 20 && armored.Threshold(DamageType.Normal) == 2 && armored.Resistance(DamageType.Normal) == 25, "Worn armor.");
            try { DerivedCombatStats.ForCharacter(new Special(0, 5, 5, 5, 5, 5, 5)); throw new Exception("SPECIAL 0 accepted."); } catch (InvalidDataException) { }
        });
        check("combat COM-001 critter stats are PRO base plus bonus: shipped ant layout gives 6 HP, 5 AP, AC 4, Sequence 9", () =>
        {
            var stats = DerivedCombatStats.FromCritter(Ant());
            Assert(stats.MaxHitPoints == 6 && stats.MaxActionPoints == 5 && stats.ArmorClass == 4 && stats.Sequence == 9
                && stats.MeleeDamage == 2 && stats.CriticalChance == 1, $"Ant stats {stats.MaxHitPoints}/{stats.MaxActionPoints}/{stats.ArmorClass}/{stats.Sequence}.");
            Assert(stats.Skill(DerivedCombatStats.Unarmed) == 65 && stats.Resistance(DamageType.Emp) == 100, "Ant unarmed 30+2x(4+1)+25 and EMP 500 clamped to 100.");
            var ant = AntAt(Tile(100, 100));
            Assert(ant.HitPoints == 6 && ant.Team == 1 && ant.Experience == 25 && ant.Weapon.AttackType == WeaponAttackType.Unarmed
                && ant.Weapon.ActionPoints == 3 && ant.Weapon.Range == 1 && ant.Id == "ant" && !ant.IsPlayer, "Map critter combatant.");
            Assert(CombatantState.FromMapCritter(Critter(Tile(100, 100), 0x0100017E, 4, 1, "hurt"), Ant(), "x").HitPoints == 4, "Instance HP kept.");
            foreach (var bad in new[] { Critter(Tile(100, 100), 0x0100017E, 0, 1, "a"), Critter(Tile(100, 100), 0x0100017E, 7, 1, "b"),
                Critter(Tile(100, 100), 0x0100017E, 5, 0, "c"), Critter(Tile(100, 100), 0x01000005, 5, 1, "d") })
                try { CombatantState.FromMapCritter(bad, Ant(), "x"); throw new Exception("Invalid critter instance accepted."); } catch (InvalidDataException) { }
        });
        check("combat COM-002 orders by Sequence then Luck, breaks later ties deterministically and honors the CE initiator", () =>
        {
            CombatantState Make(string id, int seq, int luck, int agility, int hex) =>
                new(id, id, null, id == "p" ? 0 : 1, id == "p", hex, 0,
                    DerivedCombatStats.ForCharacter(new Special(5, Math.Max(1, seq / 2), 5, 5, 5, agility, luck)), CombatWeapon.Unarmed(), 10);
            var list = new[] { Make("b", 10, 5, 5, 100), Make("a", 10, 5, 5, 101), Make("c", 10, 8, 5, 102), Make("d", 16, 1, 5, 103), Make("e", 10, 5, 9, 104), Make("p", 4, 5, 5, 105) };
            Assert(CombatInitiative.Order(list).SequenceEqual(new[] { "d", "c", "e", "a", "b", "p" }), string.Join(",", CombatInitiative.Order(list)));
            Assert(CombatInitiative.Order(list, "p")[0] == "p" && CombatInitiative.Order(list, "p").Length == 6, "Initiator first.");
            Assert(CombatInitiative.Order(list.Reverse()).SequenceEqual(CombatInitiative.Order(list)), "Input order changed initiative.");
        });
        check("combat COM-002 movement spends one AP per hex, routes around living combatants and source blockers, never enters exits", () =>
        {
            int start = Tile(100, 100), wallHex = Tile(100, 102), exitHex = Tile(101, 103);
            var ant = AntAt(Tile(100, 103));
            var player = Player(start);
            var nav = Open(Blocker(wallHex, 3, 0x03000001, "wall"), Blocker(exitHex, 5, 0x05000010, "exit"),
                Critter(ant.Hex, 0x0100017E, 6, 1, "ant"));
            var session = new CombatSession(new[] { player, ant }, nav, new Scripted(), initiatorId: "player");
            var movement = new CombatMovement(nav, session.Combatants);
            Assert(!movement.Passable(wallHex, player) && !movement.Passable(exitHex, player) && !movement.Passable(ant.Hex, player), "Blocked hexes.");
            var preview = session.Preview(CombatActionProposal.Move("player", Tile(100, 104)));
            Assert(preview.Status == CombatActionStatus.Ready && preview.ActionPointCost == preview.Path.Length && preview.Path.Length > 4
                && !preview.Path.Contains(wallHex) && !preview.Path.Contains(ant.Hex) && !preview.Path.Contains(exitHex), $"Route around ({preview.Path.Length}).");
            Assert(preview.Path.Zip(preview.Path.Skip(1)).All(p => FalloutGrid.Distance(p.First, p.Second) == 1)
                && FalloutGrid.Distance(start, preview.Path[0]) == 1, "Route is contiguous.");
            var far = session.Preview(CombatActionProposal.Move("player", Tile(110, 100)));
            Assert(far.Status == CombatActionStatus.NotEnoughActionPoints && far.ActionPointCost == 10 && far.ActionPointsAfter == -3, "Too far for 7 AP.");
            Assert(session.Execute(CombatActionProposal.Move("player", Tile(110, 100))).Events.Length == 0 && player.Hex == start && player.ActionPoints == 7, "Refused move changed state.");
            Assert(session.Preview(CombatActionProposal.Move("player", ant.Hex)).Status == CombatActionStatus.Unreachable, "Occupied target.");
            Assert(session.Preview(CombatActionProposal.Move("player", start)).Status == CombatActionStatus.AlreadyThere, "Same hex.");
            var moved = session.Execute(CombatActionProposal.Move("player", Tile(100, 101)));
            Assert(moved.Status == CombatActionStatus.Ready && player.Hex == Tile(100, 101) && player.ActionPoints == 6
                && moved.Events.Single() is CombatantMoved { ActionPointsSpent: 1 }, "One hex, one AP.");
            Assert(session.Preview(CombatActionProposal.Move("ant", Tile(99, 103))).Status == CombatActionStatus.NotActive, "Only the active combatant moves.");
        });
        check("combat COM-003 hit chance follows CE terms (skill, ranged distance and perception, strength, AC with ammo) and the 5–95 clamp", () =>
        {
            var player = Player(Tile(100, 100)); var ant = AntAt(Tile(100, 101));
            var melee = CombatResolver.ToHit(player, ant);
            Assert(melee.Skill == 40 && melee.SkillIndex == DerivedCombatStats.MeleeWeapons && melee.TargetArmorClass == 4 && melee.Raw == 36 && melee.Chance == 36, "Spear vs ant 40-4.");
            ant.BonusArmorClass = 3; Assert(CombatResolver.ToHit(player, ant).Raw == 33, "Unspent AP raise AC."); ant.BonusArmorClass = 0;
            var pistol = CombatWeapon.Primary("Pistola", WeaponPrototypeData.Read(Pistol()));
            Assert(pistol.SkillIndex == DerivedCombatStats.SmallGuns && pistol.IsRanged && pistol.Range == 25, "Pistol profile.");
            var shooter = Player(Tile(100, 90), pistol);
            var far = CombatResolver.ToHit(shooter, AntAt(Tile(100, 100)));
            Assert(far.DistanceModifier == -16 && far.Raw == 25 - 16 - 4 && far.Chance == 5, $"Distance 10: (10-2x(5-2))x-4 = -16, clamp to 5 ({far.Raw}).");
            var near = CombatResolver.ToHit(Player(Tile(100, 99), pistol), AntAt(Tile(100, 100)));
            Assert(near.DistanceModifier == 20, "Close range bonus capped at 2xPE: max(1-6,-10)x-4 = +20.");
            var npc = new CombatantState("npc", "npc", null, 1, false, Tile(100, 90), 0, DerivedCombatStats.ForCharacter(Special.Neutral), pistol, 30);
            Assert(CombatResolver.ToHit(npc, Player(Tile(100, 100))).DistanceModifier == 0, "Non-player perception term is 2xPE.");
            var weak = Player(Tile(100, 100), special: new Special(2, 5, 5, 5, 5, 5, 5));
            Assert(CombatResolver.ToHit(weak, ant).StrengthPenalty == 40, "Spear needs ST 4: 20 per missing point.");
            var jhp = CombatWeapon.Primary("Pistola JHP", WeaponPrototypeData.Read(Pistol()), AmmoPrototypeData.Read(Jhp()));
            Assert(CombatResolver.ToHit(Player(Tile(100, 99), jhp), AntAt(Tile(100, 100))).TargetArmorClass == 0, "Ammo AC modifier, floor 0.");
            var master = Player(Tile(100, 100), special: new Special(10, 5, 5, 5, 5, 10, 5));
            Assert(CombatResolver.ToHit(master, ant).Raw == 56 && CombatResolver.ToHit(new CombatantState("m", "m", null, 0, true, Tile(100, 100), 0,
                DerivedCombatStats.ForCharacter(Special.Neutral) with { CombatSkills = new[] { 0, 0, 0, 0, 200, 0 } }, master.Weapon, 30), ant).Chance == 95, "Upper clamp 95.");
            Assert(CombatResolver.Hits(36, 36) && !CombatResolver.Hits(36, 37), "Roll at or under the chance hits.");
        });
        check("combat COM-003 damage keeps CE order: Melee Damage on the maximum, x2/2 with ammo, DT, then DR subtracted with truncation", () =>
        {
            var player = Player(Tile(100, 100)); var ant = AntAt(Tile(100, 101));
            Assert(DamageCalculator.RollRange(player, player.Weapon) == (3, 11), "Spear 3-10 plus Melee Damage 1 on the maximum.");
            var spear = player.Weapon;
            Assert(DamageCalculator.Apply(7, spear, 0, 30) == 5, "7 - 7x30/100 = 5 (CE), not floor(7x0.7) = 4.");
            Assert(DamageCalculator.Apply(7, spear, 7, 0) == 0 && DamageCalculator.Apply(7, spear, 9, 50) == 0, "DT absorbs the hit.");
            Assert(DamageCalculator.Apply(10, spear, 2, 25) == 6, "(10-2)=8, 8-2 = 6.");
            var jhp = CombatWeapon.Primary("Pistola JHP", WeaponPrototypeData.Read(Pistol()), AmmoPrototypeData.Read(Jhp()));
            Assert(DamageCalculator.Apply(10, jhp, 0, 0) == 20, "JHP doubles: 10x2x2/1/2.");
            var target = Player(Tile(100, 101), special: Special.Neutral);
            Assert(DamageCalculator.Resistance(target, jhp) == 25, "JHP adds its DR modifier, clamped at 0..100.");
            var estimate = DamageCalculator.Estimate(player, ant);
            Assert(estimate.MinRoll == 3 && estimate.MaxRoll == 11 && estimate.MinDamage == 3 && estimate.MaxDamage == 11 && estimate.Threshold == 0, "Estimate.");
            var bite = DamageCalculator.Estimate(ant, player);
            Assert(bite.MinRoll == 1 && bite.MaxRoll == 4 && bite.Type == DamageType.Normal, "Ant bite 1-2 plus Melee Damage 2 on the maximum.");
        });
        check("combat COM-003 session resolves hits and misses with preview parity, kills award PRO XP once and end the fight", () =>
        {
            var player = Player(Tile(100, 100)); var ant = AntAt(Tile(100, 101));
            var random = new Scripted(36, 5, 90, 30, 8);
            var session = new CombatSession(new[] { player, ant }, Open(), random, initiatorId: "player");
            var preview = session.Preview(CombatActionProposal.Attack("player", "ant"));
            Assert(preview.Status == CombatActionStatus.Ready && preview.ActionPointCost == 4 && preview.ToHit!.Chance == 36 && preview.Damage!.MaxDamage == 11, "Attack preview.");
            var hit = session.Execute(CombatActionProposal.Attack("player", "ant"));
            var resolved = (AttackResolved)hit.Events.Single();
            Assert(resolved.Hit && resolved.Roll == 36 && resolved.Chance == preview.ToHit!.Chance && resolved.DamageRoll == 5 && resolved.Damage == 5
                && ant.HitPoints == 1 && player.ActionPoints == 3, "Hit for 5.");
            Assert(session.Preview(CombatActionProposal.Attack("player", "ant")).Status == CombatActionStatus.NotEnoughActionPoints, "3 AP left for a 4 AP thrust.");
            session.Execute(CombatActionProposal.EndTurn("player"));
            Assert(player.BonusArmorClass == 3 && session.Active == ant && ant.ActionPoints == 5, "Unspent AP to AC; ant turn.");
            var bite = session.Execute(CombatActionProposal.Attack("ant", "player"));
            var biteResult = (AttackResolved)bite.Events.Single();
            Assert(biteResult.Chance == 65 - 8 && !biteResult.Hit && biteResult.Roll == 90 && player.HitPoints == 30, "Ant misses vs AC 5+3.");
            Assert(session.Execute(CombatActionProposal.Attack("ant", "player")).Status == CombatActionStatus.NotEnoughActionPoints, "Second bite needs 3 AP of 2.");
            session.Execute(CombatActionProposal.EndTurn("ant"));
            Assert(session.Turn.Round == 2 && session.Active == player && player.BonusArmorClass == 0 && ant.BonusArmorClass == 2, "Round 2 re-sorted: player Seq 10 > ant 9.");
            var kill = session.Execute(CombatActionProposal.Attack("player", "ant"));
            Assert(kill.Events.Length == 3 && kill.Events[1] is CombatantDied { Experience: 25, KillerId: "player" } && kill.Events[2] is CombatEnded { WinningTeam: 0, ExperienceAwarded: 25 }
                && !ant.Alive && ant.HitPoints == 0 && session.Turn.Over && session.Active == null && random.Left == 0, "Kill, XP, end.");
            Assert(session.Execute(CombatActionProposal.EndTurn("player")).Status == CombatActionStatus.CombatOver && session.Turn.ExperienceAwarded == 25, "Nothing after the end.");
            Assert(session.Log.OfType<CombatantDied>().Count() == 1 && session.Log[0] is CombatStarted { Order: ["player", "ant"] }, "Log.");
        });
        check("combat sessions refuse invalid setups and targets atomically; a corpse frees its hex", () =>
        {
            var player = Player(Tile(100, 100)); var ant = AntAt(Tile(100, 101)); var other = AntAt(Tile(100, 102), "ant2");
            foreach (Func<CombatSession> bad in new Func<CombatSession>[] {
                () => new CombatSession(new[] { player }, Open(), new Scripted()),
                () => new CombatSession(new[] { ant, other }, Open(), new Scripted()),
                () => new CombatSession(new[] { player, AntAt(Tile(100, 100), "same") }, Open(), new Scripted()),
                () => new CombatSession(new[] { player, ant }, new HexNavigation(Array.Empty<MapObject>(), 1), new Scripted()),
                () => new CombatSession(new[] { player, ant }, Open(), new Scripted(), "nobody") })
                try { bad(); throw new Exception("Invalid fight accepted."); } catch (InvalidDataException) { }
            var session = new CombatSession(new[] { player, ant, other }, Open(), new Scripted(1, 11, 1, 11), "player");
            Assert(session.Preview(CombatActionProposal.Attack("player", "player")).Status == CombatActionStatus.InvalidTarget
                && session.Preview(CombatActionProposal.Attack("player", "ghost")).Status == CombatActionStatus.InvalidTarget
                && session.Preview(CombatActionProposal.Attack("ghost", "ant")).Status == CombatActionStatus.UnknownCombatant
                && session.Preview(CombatActionProposal.Move("player", 40000)).Status == CombatActionStatus.InvalidTarget, "Refusals.");
            var spear = session.Preview(CombatActionProposal.Attack("player", "ant2"));
            Assert(spear.Status == CombatActionStatus.Ready, "Spear reaches 2 hexes.");
            session.Execute(CombatActionProposal.Attack("player", "ant"));
            Assert(!ant.Alive && !session.Turn.Over && session.Preview(CombatActionProposal.Attack("player", "ant")).Status == CombatActionStatus.InvalidTarget, "Dead target.");
            Assert(session.Preview(CombatActionProposal.Move("player", ant.Hex)).Status == CombatActionStatus.Ready, "Corpse hex is walkable.");
            session.Execute(CombatActionProposal.EndTurn("player"));
            Assert(session.Active == other && session.Turn.Order.Contains("ant") && !session.Log.OfType<TurnStarted>().Any(t => t.CombatantId == "ant"), "Dead skipped.");
        });
        check("combat provisional melee behavior closes in, bites while AP last and ends its turn; seeded fights replay exactly", () =>
        {
            var player = Player(Tile(100, 100)); var ant = AntAt(Tile(100, 103));
            var session = new CombatSession(new[] { player, ant }, Open(), new Scripted(90), "ant");
            var events = ProvisionalMeleeBehavior.TakeTurn(session);
            Assert(events.OfType<CombatantMoved>().Single().ActionPointsSpent == 2 && ant.Hex == Tile(100, 101)
                && events.OfType<AttackResolved>().Single() is { Hit: false, Roll: 90 } && events[^2] is TurnEnded { UnusedActionPoints: 0 }
                && session.Active == player, "Approach 2, bite 3, end.");
            try { ProvisionalMeleeBehavior.TakeTurn(session); throw new Exception("Player turn automated."); } catch (InvalidOperationException) { }
            string Fight(ulong seed)
            {
                var p = Player(Tile(100, 100)); var a = AntAt(Tile(100, 104));
                var s = new CombatSession(new[] { p, a }, Open(), new SeededCombatRandom(seed));
                for (int i = 0; i < 200 && !s.Turn.Over; i++)
                {
                    if (!s.Active!.IsPlayer) { ProvisionalMeleeBehavior.TakeTurn(s); continue; }
                    var attack = s.Preview(CombatActionProposal.Attack("player", "ant"));
                    if (attack.Status == CombatActionStatus.Ready) s.Execute(CombatActionProposal.Attack("player", "ant"));
                    else if (attack.Status == CombatActionStatus.OutOfRange && p.ActionPoints > 0 && s.Movement.Path(p, h => FalloutGrid.Distance(h, a.Hex) <= 2) is { Length: > 0 } path)
                        s.Execute(CombatActionProposal.Move("player", path[Math.Min(path.Length, p.ActionPoints) - 1]));
                    else s.Execute(CombatActionProposal.EndTurn("player"));
                }
                return string.Join("|", s.Log.Select(e => e.ToString()));
            }
            Assert(Fight(7) == Fight(7) && Fight(7) != Fight(8) && Fight(7).Contains("CombatEnded"), "Seeded replay.");
            var random = new SeededCombatRandom(42);
            Assert(Enumerable.Range(0, 2000).Select(_ => random.Between(1, 100)).All(v => v is >= 1 and <= 100) && random.Between(5, 5) == 5, "Random range.");
        });
    }
}
