using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;

internal static class PlayerCharacterTests
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Reject(Action action, string message) { try { action(); } catch (InvalidDataException) { return; } throw new Exception(message); }

    // A GCD with the real 432-byte layout. Defaults reproduce Narg (premade/combat.gcd): derived base stats as the editor stored them.
    internal static GameResource Gcd(string path = "premade/combat.gcd", string name = "Narg", int[]? special = null, int[]? tags = null, int[]? traits = null,
        int points = 0, int[]? skillPoints = null, int age = 21, int gender = 0, int length = CharacterFile.Length)
    {
        var bytes = new byte[length];
        void Put(int offset, int value) { if (offset + 4 <= bytes.Length) BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(offset, 4), value); }
        int[] s = special ?? new[] { 8, 5, 9, 3, 4, 7, 4 };
        for (int i = 0; i < 7; i++) Put(4 + 4 * i, s[i]);
        Put(4 + 4 * 7, 44); Put(4 + 4 * 8, 9); Put(4 + 4 * 9, 8); Put(4 + 4 * 33, age); Put(4 + 4 * 34, gender);
        for (int i = 0; i < 18; i++) Put(284 + 4 * i, skillPoints?[i] ?? 0);
        Encoding.Latin1.GetBytes(name).AsSpan(0, Math.Min(name.Length, 32)).CopyTo(bytes.AsSpan(372));
        int[] t = tags ?? new[] { 4, 0, 5, -1 }, r = traits ?? new[] { 6, 15 };
        for (int i = 0; i < 4; i++) Put(404 + 4 * i, i < t.Length ? t[i] : -1);
        for (int i = 0; i < 2; i++) Put(420 + 4 * i, i < r.Length ? r[i] : -1);
        Put(428, points);
        return new GameResource(path, "synthetic", bytes);
    }
    static PlayerCharacter Narg() => PlayerCharacter.FromPremade(CharacterFile.Read(Gcd()));
    static PlayerCharacter With(params CharacterTrait[] traits) =>
        PlayerCharacter.Create("Teste", new Special(5, 5, 5, 5, 5, 10, 5), traits, new[] { 0, 3, 14 }, 25, 1);

    public static void Run(Action<string, Action> check)
    {
        check("neutral sheet keeps every earlier value and matches the neutral combat derivation", () =>
        {
            var neutral = PlayerCharacter.Neutral;
            Assert(neutral.Kind == CharacterKind.TestNeutral && neutral.BaseMaxHitPoints == 30 && neutral.MaxActionPoints == 7 && neutral.ArmorClass == 5
                && neutral.MeleeDamage == 1 && neutral.CarryWeight == 150 && neutral.Sequence == 10 && neutral.CriticalChance == 5 && neutral.HealingRate == 1, "Derived.");
            Assert(neutral.Skill(CharacterSkill.Unarmed) == 50 && neutral.Skill(CharacterSkill.MeleeWeapons) == 40 && neutral.Skill(CharacterSkill.SmallGuns) == 25
                && neutral.Skill(CharacterSkill.Lockpick) == 20 && neutral.Skill(CharacterSkill.Speech) == 25, "Skills.");
            DerivedCombatStats a = DerivedCombatStats.ForPlayer(neutral, 3), b = DerivedCombatStats.ForCharacter(Special.Neutral, level: 3);
            Assert(a.MaxHitPoints == b.MaxHitPoints && a.MaxActionPoints == b.MaxActionPoints && a.ArmorClass == b.ArmorClass && a.Sequence == b.Sequence
                && a.MeleeDamage == b.MeleeDamage && a.CombatSkills.SequenceEqual(b.CombatSkills), "Same combat stats as ForCharacter.");
            Reject(() => PlayerCharacter.Create("Neutro", Special.Neutral, Array.Empty<CharacterTrait>(), new[] { 0, 1, 2 }, 25, 0), "Neutral is not creatable.");
        });
        check("GCD reader follows the 432-byte premade layout and refuses other sizes, tags, traits and names", () =>
        {
            var file = CharacterFile.Read(Gcd());
            Assert(file.Name == "Narg" && file.BaseStats[7] == 44 && file.TaggedSkills.SequenceEqual(new[] { 4, 0, 5, -1 }) && file.Traits.SequenceEqual(new[] { 6, 15 })
                && file.CharacterPoints == 0 && file.BaseStats[33] == 21, "Fields.");
            Reject(() => CharacterFile.Read(Gcd(length: 428)), "Short GCD.");
            Reject(() => CharacterFile.Read(Gcd(tags: new[] { 18, 0, 5 })), "Tag out of range.");
            Reject(() => CharacterFile.Read(Gcd(traits: new[] { 16, -1 })), "Trait out of range.");
            Reject(() => CharacterFile.Read(Gcd(name: "")), "Empty name.");
            Reject(() => PlayerCharacter.FromPremade(CharacterFile.Read(Gcd(points: 5, special: new[] { 5, 5, 5, 5, 5, 5, 5 }))), "Unspent points in a premade.");
        });
        check("premade sheet applies Gifted and Heavy Handed as CE: Narg has 44 HP, 9 AP, MD 8 and tagged Melee 64%", () =>
        {
            var narg = Narg();
            Assert(narg.Kind == CharacterKind.Premade && narg.Premade == "premade/combat.gcd" && narg.Effective.ToArray().SequenceEqual(new[] { 9, 6, 10, 4, 5, 8, 5 }), "Gifted SPECIAL.");
            Assert(narg.BaseMaxHitPoints == 44 && narg.MaxActionPoints == 9 && narg.ArmorClass == 8 && narg.MeleeDamage == 8 && narg.CarryWeight == 250
                && narg.Sequence == 12 && narg.HealingRate == 3, "Derived.");
            Assert(narg.Skill(CharacterSkill.MeleeWeapons) == 64 && narg.Skill(CharacterSkill.SmallGuns) == 47 && narg.Skill(CharacterSkill.Unarmed) == 54
                && narg.Skill(CharacterSkill.Speech) == 10, "Skills with tags and Gifted.");
            Assert(narg.SkillPointsPerLevel == 10 && narg.HitPointsPerLevel == 7 && narg.PerkInterval == 3, "Progression.");
            var trained = PlayerCharacter.FromPremade(CharacterFile.Read(Gcd(skillPoints: Enumerable.Range(0, 18).Select(i => i == 4 ? 10 : i == 3 ? 7 : 0).ToArray())));
            Assert(trained.Skill(CharacterSkill.MeleeWeapons) == 64 + 20 && trained.Skill(CharacterSkill.Unarmed) == 54 + 7, "Tagged points count twice.");
        });
        check("traits change SPECIAL, derived stats, skills and progression as trait.cc; SPECIAL stays within 1-10", () =>
        {
            Assert(With(CharacterTrait.Bruiser).Effective.Strength == 7 && With(CharacterTrait.Bruiser).MaxActionPoints == 8, "Bruiser.");
            Assert(With(CharacterTrait.SmallFrame).Effective.Agility == 10 && With(CharacterTrait.SmallFrame).CarryWeight == 100, "Small Frame (AG capped at 10).");
            Assert(With(CharacterTrait.Kamikaze).ArmorClass == 0 && With(CharacterTrait.Kamikaze).Sequence == 15, "Kamikaze.");
            Assert(With(CharacterTrait.Finesse).CriticalChance == 15 && With(CharacterTrait.HeavyHanded).MeleeDamage == 5, "Finesse, Heavy Handed.");
            var fast = With(CharacterTrait.FastMetabolism);
            Assert(fast.HealingRate == 3 && fast.RadiationResistance == 0 && fast.PoisonResistance == 0, "Fast Metabolism.");
            var good = With(CharacterTrait.GoodNatured); var plain = With();
            Assert(good.Skill(CharacterSkill.Speech) == plain.Skill(CharacterSkill.Speech) + 15 && good.Skill(CharacterSkill.SmallGuns) == plain.Skill(CharacterSkill.SmallGuns) - 10
                && good.Skill(CharacterSkill.Lockpick) == plain.Skill(CharacterSkill.Lockpick), "Good Natured.");
            var gifted = With(CharacterTrait.Gifted);
            Assert(gifted.Effective.Agility == 10 && gifted.Effective.Strength == 6 && gifted.Skill(CharacterSkill.Science) == 4 * 6 - 10
                && gifted.SkillPointsPerLevel == 5 + 12 - 5, "Gifted.");
            var skilled = With(CharacterTrait.Skilled);
            Assert(skilled.SkillPointsPerLevel == 20 && skilled.PerkInterval == 4 && CharacterProgression.PerksAvailable(skilled, 4) == 1
                && CharacterProgression.PerksAvailable(skilled, 3) == 0 && CharacterProgression.Gains(skilled, 1, 4).Count(g => g.PerkAvailable) == 1, "Skilled.");
        });
        check("creation needs 40 SPECIAL points, up to two distinct traits, exactly three distinct tags, age 16-35 and a name", () =>
        {
            var ok = new Special(5, 5, 5, 5, 5, 10, 5);
            string? Problem(Special s, CharacterTrait[]? traits = null, int[]? tags = null, int age = 25, int gender = 0, string name = "Ana") =>
                PlayerCharacter.CreationProblem(name, s, traits ?? Array.Empty<CharacterTrait>(), tags ?? new[] { 0, 1, 2 }, age, gender);
            Assert(Problem(ok) == null, "Valid.");
            Assert(Problem(Special.Neutral)!.Contains("5 ponto"), "Unspent points.");
            Assert(Problem(new Special(6, 5, 5, 5, 5, 10, 5)) != null && Problem(new Special(0, 6, 5, 5, 5, 10, 9)) != null, "Too many or out of range.");
            Assert(Problem(ok, new[] { CharacterTrait.Bruiser, CharacterTrait.Gifted, CharacterTrait.Skilled }) != null
                && Problem(ok, new[] { CharacterTrait.Bruiser, CharacterTrait.Bruiser }) != null, "Traits.");
            Assert(Problem(ok, tags: new[] { 0, 1 })!.Contains("3") && Problem(ok, tags: new[] { 0, 1, 2, 3 }) != null && Problem(ok, tags: new[] { 0, 0, 1 }) != null
                && Problem(ok, tags: new[] { 0, 1, 18 }) != null, "Tags.");
            Assert(Problem(ok, age: 15) != null && Problem(ok, age: 36) != null && Problem(ok, gender: 2) != null, "Age and gender.");
            Assert(Problem(ok, name: " ") != null && Problem(ok, name: new string('a', 32)) != null && Problem(ok, name: "a\tb") != null, "Name.");
            var made = PlayerCharacter.Create("  Ana ", ok, new[] { CharacterTrait.SmallFrame }, new[] { 9, 8, 10 }, 30, 1);
            Assert(made.Kind == CharacterKind.Created && made.Name == "Ana" && made.SkillPoints.All(p => p == 0) && made.Skill(CharacterSkill.Lockpick) == 10 + 5 + 10 + 20, "Created.");
        });
        check("a new game with a character resets the world and plays with its HP, carry weight, combat stats and level rules", () =>
        {
            var map = WorldCombatTests.Map(); var world = new WorldSession(_ => map, new[] { 3, 126 });
            world.Current.Items.Restore(Array.Empty<TakenItem>());
            var narg = Narg(); world.StartNewGame(narg);
            Assert(world.Character == narg && world.Location.Hex == 20100 && world.MaxHitPoints == 44 && world.HitPoints == 44 && world.CarryCapacity == 250, "Started.");
            var fight = world.BeginCombat(world.FindEncounter(world.Location)!, CombatWeapon.Unarmed(), new CombatTests.Scripted());
            var hero = fight.Get("player");
            Assert(hero.Name == "Narg" && hero.HitPoints == 44 && hero.Stats.MaxActionPoints == 9 && hero.Stats.MeleeDamage == 8 && hero.Stats.Skill(DerivedCombatStats.MeleeWeapons) == 64, "Fighter.");
            world.AbandonCombat(); world.Reset();
            Assert(world.Character == narg, "Reset keeps the character.");
        });
        check("checkpoint v12 keeps the character, migrates v11 as the neutral profile and refuses forged or orphaned characters", () =>
        {
            var gcds = new Dictionary<string, CharacterFile> { ["premade/combat.gcd"] = CharacterFile.Read(Gcd()) };
            CharacterFile Premade(string path) => gcds.TryGetValue(path, out var file) ? file : throw new FileNotFoundException(path);
            var map = WorldCombatTests.Map();
            WorldSession Load(WorldCheckpoint saved, Func<string, CharacterFile>? premade = null) =>
                WorldSession.Restore(_ => map, new[] { 3, 126 }, saved, "catalog", premade: premade ?? Premade);
            var world = new WorldSession(_ => map, new[] { 3, 126 }, premade: Premade); world.StartNewGame(Narg());
            WorldCheckpoint saved = CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog")));
            Assert(saved.Version == 12 && saved.Profile == "neutral-v12" && saved.Character is { Kind: CharacterKind.Premade, Name: "Narg", Premade: "premade/combat.gcd" }, "Captured.");
            var restored = Load(saved);
            Assert(restored.Character.Name == "Narg" && restored.MaxHitPoints == 44 && restored.Character.Traits.SequenceEqual(new[] { CharacterTrait.HeavyHanded, CharacterTrait.Gifted }), "Premade restored.");
            var made = new WorldSession(_ => map, new[] { 3, 126 }); made.StartNewGame(With(CharacterTrait.SmallFrame));
            var madeSaved = made.Capture("catalog");
            Assert(Load(madeSaved).Character is { Kind: CharacterKind.Created, Name: "Teste", CarryWeight: 100 }, "Created restored.");
            var legacy = Load(saved with { Version = 11, Profile = "neutral-v11", Character = null, Duel = null });
            Assert(legacy.Character == PlayerCharacter.Neutral && legacy.Capture("catalog").Character!.Kind == CharacterKind.TestNeutral, "v11 migrates as neutral.");
            var forged = new[]
            {
                saved with { Character = saved.Character! with { Special = new[] { 9, 5, 9, 3, 4, 7, 3 } } },
                saved with { Character = saved.Character! with { Premade = "premade/stealth.gcd" } },
                saved with { Character = saved.Character! with { Kind = CharacterKind.Created } },
                saved with { Character = saved.Character! with { TaggedSkills = new[] { 4, 0 } } },
                madeSaved with { Character = madeSaved.Character! with { Special = new[] { 5, 5, 5, 5, 5, 5, 5 } } },
                madeSaved with { Character = madeSaved.Character! with { SkillPoints = Enumerable.Repeat(1, 18).ToArray() } },
                madeSaved with { Character = PlayerCharacter.Neutral.Capture() with { Name = "Outro" } },
                saved with { Character = null, Duel = null },
                saved with { Version = 11, Profile = "neutral-v11" }
            };
            for (int i = 0; i < forged.Length; i++) Reject(() => Load(forged[i]), $"Forged character {i} accepted.");
            Reject(() => Load(saved, _ => throw new FileNotFoundException()), "Premade without its GCD accepted.");
            var node = JsonNode.Parse(CheckpointFile.Encode(saved))!; node.AsObject().Remove("Character");
            Reject(() => CheckpointFile.Decode(Encoding.UTF8.GetBytes(node.ToJsonString())), "v12 without Character decoded.");
        });
    }
}
