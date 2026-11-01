using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;

internal static class CharacterProgressionTests
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }

    public static void Run(Action<string, Action> check)
    {
        check("character progression follows CE thresholds, EN/2+2 HP, 5+2xIN skill points capped at 99 and a perk every third level", () =>
        {
            Assert(new[] { 1, 2, 3, 4, 5, 6 }.Select(CharacterProgression.ExperienceForLevel).SequenceEqual(new[] { 0, 1000, 3000, 6000, 10000, 15000 }), "Thresholds.");
            Assert(CharacterProgression.ExperienceForLevel(99) == 4851000, "Level 99 threshold.");
            Assert(CharacterProgression.Level(0) == 1 && CharacterProgression.Level(999) == 1 && CharacterProgression.Level(1000) == 2
                && CharacterProgression.Level(2999) == 2 && CharacterProgression.Level(3000) == 3 && CharacterProgression.Level(int.MaxValue) == 99, "Levels.");
            var neutral = Special.Neutral;
            Assert(CharacterProgression.MaxHitPoints(neutral, 1) == 30 && CharacterProgression.MaxHitPoints(neutral, 2) == 34
                && CharacterProgression.MaxHitPoints(neutral, 3) == 38, "Neutral HP +4 per level.");
            Assert(CharacterProgression.HitPointsPerLevel(new Special(5, 5, 9, 5, 5, 5, 5)) == 6 && CharacterProgression.SkillPointsPerLevel(new Special(5, 5, 5, 5, 9, 5, 5)) == 23, "Stat formulas.");
            Assert(CharacterProgression.UnspentSkillPoints(neutral, 1) == 0 && CharacterProgression.UnspentSkillPoints(neutral, 2) == 15
                && CharacterProgression.UnspentSkillPoints(neutral, 10) == 99, "Skill points capped at 99.");
            Assert(CharacterProgression.PerksAvailable(2) == 0 && CharacterProgression.PerksAvailable(3) == 1 && CharacterProgression.PerksAvailable(6) == 2, "Perks.");
            var gains = CharacterProgression.Gains(neutral, 1, 3);
            Assert(gains.Count == 2 && gains[0] == new LevelGain(2, 4, 15, false) && gains[1] == new LevelGain(3, 4, 15, true), "Gains.");
            Assert(DerivedCombatStats.ForCharacter(neutral, level: 2).MaxHitPoints == 34, "Combat stats follow the level.");
            foreach (Action bad in new Action[] { () => CharacterProgression.ExperienceForLevel(0), () => CharacterProgression.ExperienceForLevel(100), () => CharacterProgression.Level(-1) })
                try { bad(); throw new Exception("Invalid level input accepted."); } catch (ArgumentOutOfRangeException) { }
        });
        check("a won fight that crosses 1000 XP levels the character up, raises current HP with max HP and restores level-bound HP", () =>
        {
            int Tile(int column, int row) => row * 200 + 199 - column;
            var map = WorldTests.Map(3, 126);
            var ant = CombatTests.Critter(Tile(99, 102), 0x0100017E, 6, 1, "map3:veteran-ant");
            map = map with { ObjectLayers = new[] { map.ObjectLayers[0] with { Objects = map.ObjectLayers[0].Objects.Append(ant).ToArray(), DeclaredCount = 3 } },
                Prototypes = new[] { CombatTests.AntPrototype(experience: 1000) } };
            var world = new WorldSession(_ => map, new[] { 3, 126 });
            Assert(world.Level == 1 && world.MaxHitPoints == 30 && world.HitPoints == 30 && world.UnspentSkillPoints == 0, "Level 1 start.");
            var spear = CombatWeapon.Primary("Lança", WeaponPrototypeData.Read(CombatTests.Spear()));
            var fight = world.BeginCombat(WorldCombatTests.NearAnt(world, "map3:veteran-ant"), spear, new CombatTests.Scripted(1, 4, 1, 11));
            ProvisionalMeleeBehavior.TakeTurn(fight);
            fight.Execute(CombatActionProposal.Attack("player", ant.Key));
            Assert(fight.Turn.Over && fight.Get("player").HitPoints == 26, "Bitten for 4, then the ant falls.");
            Assert(world.CommitCombat(fight) == CombatCommitStatus.Ready && world.Experience == 1000 && world.Level == 2 && world.MaxHitPoints == 34
                && world.HitPoints == 30 && world.PlayerHitPoints == 30 && world.UnspentSkillPoints == 15 && world.PerksAvailable == 0, "Level 2: +4 max and current HP, 15 skill points.");
            WorldCheckpoint saved = world.Capture("catalog");
            Assert(saved.Combat!.PlayerHitPoints == 30 && saved.Combat.Experience == 1000, "Saved HP and XP.");
            var restored = WorldSession.Restore(_ => map, new[] { 3, 126 }, saved, "catalog");
            Assert(restored.Level == 2 && restored.HitPoints == 30 && restored.MaxHitPoints == 34, "Restored level and HP.");
            Assert(WorldSession.Restore(_ => map, new[] { 3, 126 }, saved with { Combat = saved.Combat with { PlayerHitPoints = 33 } }, "catalog").HitPoints == 33, "33 HP fits level 2.");
            foreach (int hp in new[] { 34, 0 })
                try { WorldSession.Restore(_ => map, new[] { 3, 126 }, saved with { Combat = saved.Combat with { PlayerHitPoints = hp } }, "catalog"); throw new Exception($"HP {hp} accepted."); }
                catch (InvalidDataException) { }
        });
    }
}
