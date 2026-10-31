using System.Text;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime.Combat;

/// <summary>Front B phase 2 on original data: derived temple critters, weapon/armor/ammo PROs and a seeded read-only duel.</summary>
internal static class CombatReport
{
    public static object Create(GameData data)
    {
        MapDocument map = MapDocument.Read(data.Read("maps/arcaves.map"), data.Read); MapLayoutWriter.Verify(map);
        var resources = new ResourceCatalog(data.Read); var prototypes = new PrototypeCatalog(resources);
        string Name(string file, int id)
        {
            string text = Encoding.Latin1.GetString(resources.Read("text/english/game/" + file).Bytes);
            string? line = text.Split('\n').FirstOrDefault(l => l.StartsWith("{" + id + "}"));
            return line == null ? "?" : line.Split('{', '}')[^2].Trim();
        }
        var critters = new[] { 0x0100017E, 0x01000005, 0x01000112 }.Select(pid =>
        {
            var proto = prototypes.Get(pid); var critter = CritterPrototypeData.Read(proto); var s = DerivedCombatStats.FromCritter(critter);
            var instances = map.ObjectLayers.SelectMany(l => l.Objects).Where(o => o.Header.Pid == pid).ToArray();
            return new { Pid = $"0x{pid:X8}", Name = Name("pro_crit.msg", proto.MessageId), Instances = instances.Length,
                Elevations = instances.Select(o => o.Header.Elevation).Distinct().OrderBy(e => e).ToArray(),
                InstanceHitPoints = instances.Select(o => o.State.GetValueOrDefault("hp")).Distinct().ToArray(),
                Special = s.Special.ToArray(), s.MaxHitPoints, s.MaxActionPoints, s.ArmorClass, s.Sequence, s.MeleeDamage, s.CriticalChance,
                Unarmed = s.Skill(DerivedCombatStats.Unarmed), Melee = s.Skill(DerivedCombatStats.MeleeWeapons), s.DamageThreshold, s.DamageResistance,
                critter.Experience, PrototypeBase = new { HitPoints = critter.BaseStat(7), ActionPoints = critter.BaseStat(8), Sequence = critter.BaseStat(13) } };
        }).ToArray();
        var ant = critters[0];
        if (ant.MaxHitPoints != 6 || ant.MaxActionPoints != 5 || ant.ArmorClass != 4 || ant.Sequence != 9 || ant.Unarmed != 65 || ant.Experience != 25
            || !ant.InstanceHitPoints.SequenceEqual(new[] { 6 })) throw new InvalidDataException("Giant Ant effective stats changed.");
        var player = DerivedCombatStats.ForCharacter(Special.Neutral);
        if (player.MaxHitPoints != 30 || player.MaxActionPoints != 7 || player.ArmorClass != 5 || player.Sequence != 10 || player.Skill(DerivedCombatStats.Unarmed) != 50)
            throw new InvalidDataException("Neutral character derivation changed.");

        object Weapon(int pid)
        {
            var proto = prototypes.Get(pid); var w = WeaponPrototypeData.Read(proto); var profile = CombatWeapon.Primary(Name("pro_item.msg", proto.MessageId), w);
            return new { Pid = pid, profile.Name, w.MinDamage, w.MaxDamage, DamageType = profile.DamageType.ToString(), w.PrimaryRange, w.SecondaryRange,
                w.PrimaryActionPoints, w.SecondaryActionPoints, w.MinStrength, Primary = w.PrimaryMode.ToString(), Secondary = w.SecondaryMode.ToString(),
                AttackType = profile.AttackType.ToString(), Skill = profile.SkillIndex, w.AmmoCapacity, w.Caliber, w.AmmoPid };
        }
        var spearData = WeaponPrototypeData.Read(prototypes.Get(7));
        if (spearData.MinDamage != 3 || spearData.MaxDamage != 10 || spearData.PrimaryRange != 2 || spearData.PrimaryActionPoints != 4)
            throw new InvalidDataException("Spear PRO no longer matches the roadmap acceptance values.");
        var armorProto = prototypes.Get(1); var armor = ArmorPrototypeData.Read(armorProto);
        var ammoProto = prototypes.Get(29); var ammo = AmmoPrototypeData.Read(ammoProto);

        // Seeded read-only duels: the first elevation-0 critter of each encounter PID, the neutral character with the spear five hexes away.
        var navigation = HexNavigation.FromMap(map, 0);
        var spear = CombatWeapon.Primary(Name("pro_item.msg", prototypes.Get(7).MessageId), spearData);
        object Duel(int pid, ulong seed)
        {
            MapObject source = map.ObjectLayers.Single(l => l.Elevation == 0).Objects.Where(o => o.Header.Pid == pid).OrderBy(o => o.Offset).First();
            string before = string.Join(",", source.State.Select(kv => $"{kv.Key}={kv.Value}"));
            string name = Name("pro_crit.msg", prototypes.Get(pid).MessageId);
            CombatSession? session = null; CombatantState? hero = null, foe = null; int start = -1;
            foreach (int tile in Enumerable.Range(0, 40000).Where(t => FalloutGrid.Distance(t, source.Header.Tile) == 5 && navigation.Walkable(t)))
            {
                foe = CombatantState.FromMapCritter(source, CritterPrototypeData.Read(prototypes.Get(pid)), name);
                hero = new CombatantState("player", "Escolhido", null, 0, true, tile, 0, player, spear, player.MaxHitPoints);
                var candidate = new CombatSession(new[] { hero, foe }, navigation, new SeededCombatRandom(seed));
                if (candidate.Movement.Path(hero, h => FalloutGrid.Distance(h, foe.Hex) <= spear.Range) is { Length: > 0 }) { session = candidate; start = tile; break; }
            }
            if (session == null || hero == null || foe == null) throw new InvalidDataException($"No reachable duel position near PID {pid:X8}.");
            for (int guard = 0; guard < 400 && !session.Turn.Over; guard++)
            {
                if (!session.Active!.IsPlayer) { ProvisionalMeleeBehavior.TakeTurn(session); continue; }
                var attack = session.Preview(CombatActionProposal.Attack("player", foe.Id));
                if (attack.Status == CombatActionStatus.Ready) session.Execute(CombatActionProposal.Attack("player", foe.Id));
                else if (attack.Status == CombatActionStatus.OutOfRange && hero.ActionPoints > 0
                    && session.Movement.Path(hero, h => FalloutGrid.Distance(h, foe.Hex) <= spear.Range) is { Length: > 0 } path)
                    session.Execute(CombatActionProposal.Move("player", path[Math.Min(path.Length, hero.ActionPoints) - 1]));
                else session.Execute(CombatActionProposal.EndTurn("player"));
            }
            if (!session.Turn.Over) throw new InvalidDataException("Seeded duel did not finish.");
            MapLayoutWriter.Verify(map);
            if (before != string.Join(",", source.State.Select(kv => $"{kv.Key}={kv.Value}"))) throw new InvalidDataException("Duel mutated the source critter.");
            return new
            {
                Seed = seed, Critter = name, Key = source.Key, Hex = source.Header.Tile, PlayerHex = start, Order = session.Log.OfType<CombatStarted>().Single().Order,
                Rounds = session.Turn.Round, Winner = session.Turn.WinningTeam, session.Turn.ExperienceAwarded, PlayerHitPoints = hero.HitPoints, CritterHitPoints = foe.HitPoints,
                FirstPlayerAttack = session.Log.OfType<AttackResolved>().FirstOrDefault(a => a.AttackerId == "player"),
                FirstCritterAttack = session.Log.OfType<AttackResolved>().FirstOrDefault(a => a.AttackerId == foe.Id),
                Attacks = session.Log.OfType<AttackResolved>().Count(), Moves = session.Log.OfType<CombatantMoved>().Count(), SourceUnchanged = true
            };
        }

        return new
        {
            Status = "COMBAT_CORE_OK",
            Scope = "Front B combat domain on original data; the playable slice (COM-004) is validated in-engine by --preview-test",
            Player = new { Special = player.Special.ToArray(), player.MaxHitPoints, player.MaxActionPoints, player.ArmorClass, player.Sequence, player.MeleeDamage,
                player.CriticalChance, Skills = player.CombatSkills },
            Critters = critters,
            Weapons = new[] { Weapon(7), Weapon(4), Weapon(8), Weapon(9) },
            Armor = new { Pid = 1, Name = Name("pro_item.msg", armorProto.MessageId), armor.ArmorClass, armor.DamageThreshold, armor.DamageResistance },
            Ammo = new { Pid = 29, Name = Name("pro_item.msg", ammoProto.MessageId), ammo.Caliber, ammo.Quantity, ammo.ArmorClassModifier,
                ammo.DamageResistanceModifier, ammo.DamageMultiplier, ammo.DamageDivisor },
            RoadmapCorrections = new[]
            {
                "Giant Ant: roadmap 18 HP / 7 AP / Sequence 50; PRO base+bonus gives 6 HP / 5 AP / Sequence 9 (every map instance stores hp=6).",
                "Radscorpion (PID 5): roadmap 32 HP / 6 AP / Sequence 150; base+bonus gives 10 HP / 5 AP / Sequence 4.",
                "Neutral character Unarmed is 50% (30 + 2x(AG+ST)), not 35%; Melee Weapons is 40%.",
                "10mm Pistol is PID 8; PID 4 is the Knife.",
                "CE initiative is Sequence then Luck (_compare_faster), no D6; the project breaks remaining ties by Agility, Perception and id.",
                "CE DR step is damage - damage x DR / 100 (truncating the subtracted part), not floor(damage x (1 - DR/100)).",
                "Melee Damage raises only the maximum of the damage roll (weaponGetDamage)."
            },
            ProjectRules = new[] { "Hit chance clamped to 5..95 (roadmap); CE caps at 95 only.", "Unspent AP add to AC until the combatant's next turn.",
                "Turns end explicitly; running out of AP does not end them.", "Non-player turns use ProvisionalMeleeBehavior until AI-001." },
            NotModeled = new[] { "Critical hits and failures", "Aimed shots", "Burst fire", "Line of fire and cover", "Lighting penalty", "Perks, traits, tags",
                "Difficulty other than normal", "Knockback, knockdown, crippling", "Reload and ammo consumption", "Persistence in checkpoints" },
            Duels = new[] { Duel(0x0100017E, 2026), Duel(0x01000005, 2026) },

            AdditionalDependencies = resources.Identities,
            OriginalReconstruction = "verified"
        };
    }
}
