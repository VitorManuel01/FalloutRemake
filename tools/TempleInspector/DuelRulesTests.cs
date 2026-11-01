using FalloutRemake.Geometry;
using FalloutRemake.Import;
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
    }
}
