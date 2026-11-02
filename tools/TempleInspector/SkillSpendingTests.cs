using System.Text;
using System.Text.Json.Nodes;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;

internal static class SkillSpendingTests
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Reject(Action action, string message) { try { action(); } catch (InvalidDataException) { return; } throw new Exception(message); }

    static int Tile(int column, int row) => row * 200 + 199 - column;
    // A veteran ant whose PRO experience levels the character up, and a second one left alive.
    internal static MapDocument Map(int experience)
    {
        var map = WorldTests.Map(3, 126);
        var ant = CombatTests.Critter(Tile(99, 102), 0x0100017E, 6, 1, "map3:veteran-ant");
        var other = CombatTests.Critter(Tile(80, 120), 0x0100017E, 6, 1, "map3:other-ant");
        return map with { ObjectLayers = new[] { map.ObjectLayers[0] with { Objects = map.ObjectLayers[0].Objects.Append(ant).Append(other).ToArray(), DeclaredCount = 4 } },
            Prototypes = new[] { CombatTests.AntPrototype(experience: experience) } };
    }

    // The ant bites for 4, then one spear hit kills it: its PRO experience levels the character up.
    internal static WorldSession Leveled(MapDocument map, PlayerCharacter? character = null, Func<string, CharacterFile>? premade = null)
    {
        var world = new WorldSession(_ => map, new[] { 3, 126 }, premade: premade);
        if (character != null) world.StartNewGame(character);
        var spear = CombatWeapon.Primary("Lança", WeaponPrototypeData.Read(CombatTests.Spear()));
        var fight = world.BeginCombat(new CombatEncounter(world.Location, new[] { "map3:veteran-ant" }, "map3:veteran-ant"), spear, new CombatTests.Scripted(1, 4, 1, 11));
        ProvisionalMeleeBehavior.TakeTurn(fight);
        fight.Execute(CombatActionProposal.Attack("player", "map3:veteran-ant"));
        if (world.CommitCombat(fight) != CombatCommitStatus.Ready) throw new Exception("Leveling fight failed.");
        return world;
    }

    // Narg with base points that put tagged Melee Weapons (64% bare) at exactly 100%.
    static GameResource TrainedNarg() => PlayerCharacterTests.Gcd(skillPoints: Enumerable.Range(0, 18).Select(i => i == CharacterSkill.MeleeWeapons ? 18 : 0).ToArray());

    public static void Run(Action<string, Action> check)
    {
        check("skill point cost follows CE skillsGetCost brackets of the current value", () =>
        {
            int[] values = { 0, 100, 101, 125, 126, 150, 151, 175, 176, 200, 201, 299 }, costs = { 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6 };
            Assert(values.Select(SkillSpendingRules.Cost).SequenceEqual(costs), "Cost brackets.");
        });
        check("level-up points are spent with plus and refunded with minus only above the value the sheet opened with", () =>
        {
            var world = Leveled(Map(1000));
            Assert(world.Level == 2 && world.UnspentSkillPoints == 15, "Level 2 grants 15 points.");
            SkillSpending sheet = world.BeginSkillSpending();
            Assert(sheet.Subtract(CharacterSkill.Lockpick) == SkillSpendStatus.AtMinimum, "Nothing to refund at the opening value.");
            Assert(sheet.Add(CharacterSkill.Lockpick) == SkillSpendStatus.Ready && sheet.Sheet.Skill(CharacterSkill.Lockpick) == 21 && sheet.Unspent == 14, "Plus.");
            Assert(sheet.Subtract(CharacterSkill.Lockpick) == SkillSpendStatus.Ready && sheet.Sheet.Skill(CharacterSkill.Lockpick) == 20 && sheet.Unspent == 15, "Minus.");
            for (int i = 0; i < 15; i++) Assert(sheet.Add(CharacterSkill.Unarmed) == SkillSpendStatus.Ready, "Spend all.");
            Assert(sheet.Add(CharacterSkill.Unarmed) == SkillSpendStatus.NoPoints && sheet.Unspent == 0 && sheet.Sheet.Skill(CharacterSkill.Unarmed) == 65, "Out of points.");
            Assert(world.UnspentSkillPoints == 15 && world.Sheet.Skill(CharacterSkill.Unarmed) == 50, "Nothing applied before Done.");
            Assert(world.CommitSkillSpending(sheet) == SkillCommitStatus.Ready && world.UnspentSkillPoints == 0 && world.Sheet.Skill(CharacterSkill.Unarmed) == 65
                && world.Character == PlayerCharacter.Neutral && world.Sheet.SkillRaises[CharacterSkill.Unarmed] == 15, "Done applies the spending; the creation sheet stays.");
            Assert(world.CommitSkillSpending(sheet) == SkillCommitStatus.Stale, "A sheet applies once.");
            // The next opening starts from the new values: what was bought before can no longer be refunded.
            SkillSpending again = world.BeginSkillSpending();
            Assert(again.Subtract(CharacterSkill.Unarmed) == SkillSpendStatus.AtMinimum && world.CommitSkillSpending(again) == SkillCommitStatus.Unchanged, "Earlier purchases are final.");
            Assert(DerivedCombatStats.ForPlayer(world.Sheet, world.Level).Skill(CharacterSkill.Unarmed) == 65, "Combat stats use the raised sheet.");
            SkillSpending stale = world.BeginSkillSpending(); world.Reset();
            Assert(world.CommitSkillSpending(stale) == SkillCommitStatus.Stale && world.UnspentSkillPoints == 0 && world.Sheet == PlayerCharacter.Neutral && world.Level == 1, "Reset clears raises and drops open sheets.");
        });
        check("tagged skills rise 2% per point and refund across a cost bracket as CE skillSub; untagged cost the bracket of their value", () =>
        {
            CharacterFile gcd = CharacterFile.Read(TrainedNarg());
            var narg = PlayerCharacter.FromPremade(gcd);
            Assert(narg.Skill(CharacterSkill.MeleeWeapons) == 100, "Trained Narg at 100%.");
            var world = Leveled(Map(1000), narg, _ => gcd);
            Assert(world.UnspentSkillPoints == 10, "Narg gains 10 points per level (IN 5 with Gifted: 5 + 10 - 5).");
            SkillSpending sheet = world.BeginSkillSpending();
            Assert(sheet.Add(CharacterSkill.MeleeWeapons) == SkillSpendStatus.Ready && sheet.Sheet.Skill(CharacterSkill.MeleeWeapons) == 102 && sheet.Unspent == 9, "100% costs 1, tagged +2.");
            Assert(sheet.Add(CharacterSkill.MeleeWeapons) == SkillSpendStatus.Ready && sheet.Sheet.Skill(CharacterSkill.MeleeWeapons) == 104 && sheet.Unspent == 7, "102% costs 2.");
            Assert(sheet.Subtract(CharacterSkill.MeleeWeapons) == SkillSpendStatus.Ready && sheet.Unspent == 9, "Refund of the 2-point purchase.");
            Assert(sheet.Subtract(CharacterSkill.MeleeWeapons) == SkillSpendStatus.Ready && sheet.Unspent == 10 && sheet.Sheet.Skill(CharacterSkill.MeleeWeapons) == 100,
                "Across the bracket skillSub refunds cost(101) = 2 and takes the extra point back.");
            Assert(world.CommitSkillSpending(sheet) == SkillCommitStatus.Unchanged, "Back to the opening values.");
            SkillSpending fight = world.BeginSkillSpending();
            Assert(fight.Add(CharacterSkill.Speech) == SkillSpendStatus.Ready && fight.Sheet.Skill(CharacterSkill.Speech) == 11, "Untagged +1.");
            world.BeginCombat(new CombatEncounter(world.Location, new[] { "map3:other-ant" }, "map3:other-ant"), CombatWeapon.Unarmed(), new CombatTests.Scripted());
            Assert(world.CommitSkillSpending(fight) == SkillCommitStatus.InCombat && world.Sheet.Skill(CharacterSkill.Speech) == 10, "The sheet stays fixed during a fight.");
            world.AbandonCombat();
            Assert(world.CommitSkillSpending(fight) == SkillCommitStatus.Ready && world.Sheet.Skill(CharacterSkill.Speech) == 11 && world.UnspentSkillPoints == 9, "Applied after the fight.");
        });
        check("the unspent pool keeps at most 99 points and checkpoint v13 keeps the spending within what the levels granted", () =>
        {
            var map = Map(CharacterProgression.ExperienceForLevel(10));
            var world = Leveled(map);
            Assert(world.Level == 10 && world.UnspentSkillPoints == 99, "Nine levels of 15 points stop at 99.");
            SkillSpending sheet = world.BeginSkillSpending();
            for (int i = 0; i < 40; i++) sheet.Add(CharacterSkill.Lockpick);
            Assert(world.CommitSkillSpending(sheet) == SkillCommitStatus.Ready && world.UnspentSkillPoints == 59 && world.Sheet.Skill(CharacterSkill.Lockpick) == 60, "40 points into Lockpick.");
            WorldCheckpoint saved = CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog")));
            Assert(saved.Version == 13 && saved.Skills is { Unspent: 59 } skills && skills.Raises[CharacterSkill.Lockpick] == 40, "Captured.");
            WorldSession Load(WorldCheckpoint checkpoint) => WorldSession.Restore(_ => map, new[] { 3, 126 }, checkpoint, "catalog");
            var restored = Load(saved);
            Assert(restored.UnspentSkillPoints == 59 && restored.Sheet.Skill(CharacterSkill.Lockpick) == 60 && restored.Character == PlayerCharacter.Neutral, "Restored.");
            var legacy = Load(saved with { Version = 12, Profile = "neutral-v12", Skills = null });
            Assert(legacy.UnspentSkillPoints == 99 && legacy.Sheet == PlayerCharacter.Neutral && legacy.Capture("catalog").Version == 13, "v12 migrates with nothing spent.");
            int[] Raise(int skill, int points) => Enumerable.Range(0, 18).Select(i => i == skill ? points : 0).ToArray();
            var forged = new[]
            {
                saved with { Skills = saved.Skills! with { Unspent = 100 } },
                saved with { Skills = saved.Skills! with { Unspent = -1 } },
                saved with { Skills = saved.Skills! with { Raises = Raise(CharacterSkill.Lockpick, 136) } },
                saved with { Skills = saved.Skills! with { Raises = Raise(CharacterSkill.Lockpick, -1), Unspent = 0 } },
                saved with { Skills = saved.Skills! with { Raises = new int[17] } },
                saved with { Skills = null },
                saved with { Version = 12, Profile = "neutral-v12" }
            };
            for (int i = 0; i < forged.Length; i++) Reject(() => Load(forged[i]), $"Forged skill spending {i} accepted.");
            // Up to what the levels granted is accepted: 107 raises of Lockpick cost 81 x 1 + 25 x 2 + 3 = 134 of the 135 points.
            Assert(Load(saved with { Skills = new SavedSkills(Raise(CharacterSkill.Lockpick, 107), 1) }).Sheet.Skill(CharacterSkill.Lockpick) == 127, "All granted points spent.");
            Reject(() => Load(saved with { Skills = new SavedSkills(Raise(CharacterSkill.Lockpick, 107), 2) }), "One point more than granted accepted.");
            var level1 = Leveled(Map(0));
            Reject(() => Load(level1.Capture("catalog") with { Skills = new SavedSkills(Raise(CharacterSkill.Lockpick, 1), 0) }), "Spending at level 1 accepted.");
            var node = JsonNode.Parse(CheckpointFile.Encode(saved))!; node.AsObject().Remove("Skills");
            Reject(() => CheckpointFile.Decode(Encoding.UTF8.GetBytes(node.ToJsonString())), "v13 without Skills decoded.");
        });
    }
}
