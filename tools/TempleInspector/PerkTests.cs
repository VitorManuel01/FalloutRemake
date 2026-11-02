using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;

/// <summary>CE perks: the table, perkCanAdd, the stat and skill bonuses, the per-level history and the combat rules ported.</summary>
internal static class PerkTests
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static readonly int Centre = 20100;
    static PerkRanks Ranks(params int[] perks) => new(perks.Select(p => new ChosenPerk(p, 30)));
    static DerivedCombatStats Stats(Special? special = null, params int[] perks) =>
        DerivedCombatStats.ForCharacter(special ?? Special.Neutral) with { Perks = Ranks(perks) };
    static CombatantState Hero(CombatWeapon weapon, DerivedCombatStats stats) => new("player", "p", null, 0, true, Centre, 0, stats, weapon, stats.MaxHitPoints);
    static CombatantState AntAt(int hex, string key = "ant") =>
        CombatantState.FromMapCritter(CombatTests.Critter(hex, 0x0100017E, 6, 1, key), CritterPrototypeData.Read(CombatTests.AntPrototype()), key);
    static CombatWeapon Punch(DerivedCombatStats stats) => CombatWeapon.UnarmedFor(stats, 1);
    static CombatWeapon Spear() => CombatWeapon.Primary("Spear", WeaponPrototypeData.Read(CombatTests.Spear()));

    public static void Run(Action<string, Action> check)
    {
        check("the CE perk table: 119 rows, 81 the editor offers, the per-rank stat bonuses and requirements of gPerkDescriptions", () =>
        {
            Assert(PerkCatalog.Rows.Length == PerkCatalog.Count && PerkCatalog.Rows.Select((r, i) => r.Id == i).All(x => x)
                && PerkCatalog.Rows.Count(r => r.Selectable) == 81, "119 rows, 81 selectable.");
            Assert(PerkCatalog.Get(Perk.Toughness) is { MaxRank: 3, MinLevel: 3, Stat: 24, StatModifier: 10 }
                && PerkCatalog.Get(Perk.ActionBoy) is { MaxRank: 2, MinLevel: 12, Stat: 8, StatModifier: 1 }
                && PerkCatalog.Get(Perk.MasterThief) is { Param1: CharacterSkill.Steal, Value1: 50, ParamMode: PerkCatalog.Both, Param2: CharacterSkill.Lockpick, Value2: 50 }
                && PerkCatalog.Get(Perk.AdrenalineRush).Stats[0] == -10 && !PerkCatalog.Get(Perk.PoweredArmor).Selectable, "Rows.");
            Assert(PerkCatalog.Effect(Perk.Toughness) == PerkEffect.Applied && PerkCatalog.Effect(Perk.Medic) == PerkEffect.SheetOnly
                && PerkCatalog.Effect(Perk.SwiftLearner) == PerkEffect.NotModeled, "What each does here.");
        });
        check("CE perkCanAdd: level, rank, SPECIAL (negative means below), skills by parameter mode; perks not modeled here are refused", () =>
        {
            PlayerCharacter neutral = PlayerCharacter.Neutral;
            Assert(PerkRules.CanAdd(Perk.QuickPockets, neutral, 3, PerkRanks.None) == PerkChoiceStatus.Ready
                && PerkRules.CanAdd(Perk.QuickPockets, neutral, 2, PerkRanks.None) == PerkChoiceStatus.Level
                && PerkRules.CanAdd(Perk.QuickPockets, neutral, 3, Ranks(Perk.QuickPockets)) == PerkChoiceStatus.MaxRank, "Level 3, one rank.");
            Assert(PerkRules.CanAdd(Perk.BonusHtHDamage, neutral, 3, PerkRanks.None) == PerkChoiceStatus.Special, "ST 6 and AG 6: neutral 5 is short.");
            Assert(PerkRules.CanAdd(Perk.Awareness, neutral, 3, PerkRanks.None) == PerkChoiceStatus.NotModeled, "Awareness is not modeled yet.");
            Assert(PerkRules.CanAdd(Perk.PoweredArmor, neutral, 30, PerkRanks.None) == PerkChoiceStatus.NotSelectable, "Armor perks are not offered.");
            int[] raises = new int[CharacterSkill.Count];
            raises[CharacterSkill.Steal] = 50; PlayerCharacter steal = neutral.WithSkillRaises(raises);
            Assert(PerkRules.CanAdd(Perk.MasterThief, steal, 12, PerkRanks.None) == PerkChoiceStatus.Requirement, "Both skills: Steal alone is not enough.");
            raises[CharacterSkill.Lockpick] = 50;
            Assert(PerkRules.CanAdd(Perk.MasterThief, neutral.WithSkillRaises(raises), 12, PerkRanks.None) == PerkChoiceStatus.Ready, "Steal and Lockpick 50.");
            var strong = PlayerCharacter.Create("Forte", new Special(10, 5, 5, 5, 5, 5, 5), Array.Empty<CharacterTrait>(), new[] { 0, 1, 2 }, 25, 0);
            Assert(PerkRules.CanAdd(Perk.AdrenalineRush, strong, 6, PerkRanks.None) == PerkChoiceStatus.Special, "Adrenaline Rush wants ST below 10.");
        });
        check("chosen perks join the sheet as CE perkAddEffect and perkGetSkillModifier: stat bonuses per rank, Gain SPECIAL, skill bonuses", () =>
        {
            PlayerCharacter bare = PlayerCharacter.Neutral;
            PlayerCharacter sheet = bare.WithPerks(Ranks(Perk.Toughness, Perk.Toughness, Perk.Dodger, Perk.ActionBoy, Perk.EarlierSequence, Perk.StrongBack,
                Perk.PackRat, Perk.MoreCriticals, Perk.BetterCriticals, Perk.Snakeater, Perk.BonusHtHDamage, Perk.GainStrength, Perk.Thief, Perk.MasterThief, Perk.Medic));
            Assert(sheet.ArmorClass == bare.ArmorClass + 5 && sheet.MaxActionPoints == bare.MaxActionPoints + 1 && sheet.Sequence == bare.Sequence + 2 + 0
                && sheet.CriticalChance == bare.CriticalChance + 5 && sheet.BetterCriticals == 20 && sheet.PoisonResistance == bare.PoisonResistance + 25
                && sheet.ResistanceBonus(0) == 20, "AC +5, AP +1, sequence +2, crit +5, better crits 20, poison +25, normal DR +20.");
            Assert(sheet.Effective.Strength == 6 && sheet.CarryWeight == 25 + 25 * 6 + 100 && sheet.MeleeDamage == 1 + 2 && sheet.BaseMaxHitPoints == bare.BaseMaxHitPoints,
                "Gain Strength: ST 6 (carry 175 + 100 from Strong Back and Pack Rat), melee 1 + 2; max HP keeps the base ST.");
            Assert(sheet.Skill(CharacterSkill.Lockpick) == bare.Skill(CharacterSkill.Lockpick) + 25 && sheet.Skill(CharacterSkill.FirstAid) == bare.Skill(CharacterSkill.FirstAid) + 10,
                "Thief + Master Thief: Lockpick +25; Medic: First Aid +10.");
            Assert(DerivedCombatStats.ForPlayer(sheet).DamageResistance[0] == 20 && DerivedCombatStats.ForPlayer(sheet).BetterCriticals == 20, "The fight reads them.");
            Assert(ReferenceEquals(sheet.Creation, bare) && sheet.Capture().Special.SequenceEqual(bare.Capture().Special), "Never saved with the character.");
        });
        check("Lifegiver and Educated give at once when chosen (character editor) and again at every later level (pcAddExperience)", () =>
        {
            PlayerCharacter bare = PlayerCharacter.Neutral, life = bare.WithPerks(new PerkRanks(new[] { new ChosenPerk(Perk.Lifegiver, 12) }));
            Assert(CharacterProgression.MaxHitPoints(life, 12) == CharacterProgression.MaxHitPoints(bare, 12) + 4
                && CharacterProgression.MaxHitPoints(life, 14) == CharacterProgression.MaxHitPoints(bare, 14) + 12, "+4 at once, +4 at levels 13 and 14.");
            PlayerCharacter educated = bare.WithPerks(new PerkRanks(new[] { new ChosenPerk(Perk.Educated, 6), new ChosenPerk(Perk.Educated, 9) }));
            Assert(educated.SkillPointsForLevel(6) == bare.SkillPointsPerLevel && educated.SkillPointsForLevel(7) == bare.SkillPointsPerLevel + 2
                && educated.SkillPointsForLevel(10) == bare.SkillPointsPerLevel + 4, "Educated: +2 per rank from the next level on.");
        });
        check("combat perks follow CE: Bonus HtH Attacks, Bonus Move, Quick Pockets, Quick Recovery, HtH Evade, Weapon Handling, Heave Ho!, Sharpshooter", () =>
        {
            DerivedCombatStats attacks = Stats(null, Perk.BonusHtHAttacks);
            Assert(CombatSession.AttackCost(Hero(Punch(attacks), attacks), HitLocation.Uncalled) == 2
                && CombatSession.AttackCost(Hero(Spear(), attacks), HitLocation.Uncalled) == 3, "One AP off the punch and the spear.");

            DerivedCombatStats move = Stats(null, Perk.BonusMove, Perk.BonusMove);
            CombatantState runner = Hero(Punch(move), move);
            var fight = new CombatSession(new[] { runner, AntAt(Centre + 2000) }, new HexNavigation(Array.Empty<MapObject>(), 0), new CombatTests.Scripted(), "player");
            int ap = runner.ActionPoints;
            Assert(runner.FreeMove == 4 && fight.Preview(CombatActionProposal.Move("player", Centre + 600)).ActionPointCost == 0, "Two ranks: 4 free hexes.");
            fight.Execute(CombatActionProposal.Move("player", Centre + 1000));
            Assert(runner.ActionPoints == ap - 1 && runner.FreeMove == 0, "Five hexes: four free, one AP.");

            DerivedCombatStats pockets = Stats(null, Perk.QuickPockets);
            Assert(CombatSession.InventoryCost(Hero(Punch(pockets), pockets)) == 2, "Quick Pockets: the inventory for 2 AP.");

            DerivedCombatStats evade = Stats(null, Perk.HtHEvade);
            CombatantState bare = Hero(Punch(evade), evade) , armed = Hero(Spear(), evade);
            bare.ActionPoints = armed.ActionPoints = 5;
            Assert(bare.BonusArmorClass == 10 + evade.Skill(DerivedCombatStats.Unarmed) / 12 && armed.BonusArmorClass == 5, "HtH Evade: bare-handed AP twice plus Unarmed / 12.");

            DerivedCombatStats weak = Stats(new Special(2, 5, 5, 5, 5, 5, 5), Perk.WeaponHandling);
            Assert(CombatResolver.StrengthShortfall(Hero(Spear(), weak)) == 0 && CombatResolver.StrengthShortfall(Hero(Spear(), Stats(new Special(2, 5, 5, 5, 5, 5, 5)))) == 2,
                "Weapon Handling: ST 2 + 3 lifts the spear's 4.");
            var thrown = CombatWeapon.Secondary("Spear", WeaponPrototypeData.Read(CombatTests.Spear()))!;
            Assert(CombatSession.Range(Hero(thrown, Stats(new Special(2, 5, 5, 5, 5, 5, 5), Perk.HeaveHo))) == 8
                && CombatSession.Range(Hero(thrown, Stats(new Special(2, 5, 5, 5, 5, 5, 5)))) == 6, "Heave Ho!: ST 2 + 2 throws 12, capped by the spear's 8.");
            int plain = CombatResolver.ToHit(Hero(thrown, Stats()), AntAt(Centre + 1400)).DistanceModifier;
            Assert(CombatResolver.ToHit(Hero(thrown, Stats(null, Perk.Sharpshooter)), AntAt(Centre + 1400)).DistanceModifier == plain + 16, "Sharpshooter: +2 PE, 2 x 2 hexes x 4 = +16 in the range term.");
        });
        check("damage and critical perks: Living Anatomy +5 against the living, Slayer makes every melee hit critical even on day 0, Toughness", () =>
        {
            DerivedCombatStats anatomy = Stats(null, Perk.LivingAnatomy);
            CombatantState hero = Hero(Spear(), anatomy), ant = AntAt(Centre + 200);
            DamageEstimate plain = DamageCalculator.Estimate(Hero(Spear(), Stats()), ant), living = DamageCalculator.Estimate(hero, ant);
            Assert(living.MinDamage == plain.MinDamage + 5 && living.MaxDamage == plain.MaxDamage + 5, "+5 against the ant.");
            var robot = new CombatantState("robot", "r", null, 1, false, Centre + 200, 0, DerivedCombatStats.ForCharacter(Special.Neutral), Spear(), 30) { KillType = 10 };
            Assert(DamageCalculator.Estimate(hero, robot).MinDamage == DamageCalculator.Estimate(Hero(Spear(), Stats()), robot).MinDamage, "Nothing against a robot.");

            DerivedCombatStats slayer = Stats(null, Perk.Slayer);
            var killer = Hero(Spear(), slayer);
            var duel = new CombatSession(new[] { killer, AntAt(Centre + 200) }, new HexNavigation(Array.Empty<MapObject>(), 0), new CombatTests.Scripted(1, 50, 5), "player");
            duel.Execute(CombatActionProposal.Attack("player", "ant"));
            Assert(!CriticalRules.CriticalsAllowed(duel.GameTime) && duel.Log.OfType<CriticalHit>().Any(c => c.AttackerId == "player"), "Day 0, still critical.");
        });
        check("Jinxed (CE attackCompute): with the player Jinxed, half of anyone's plain misses turn into critical failures; the player's still wait for day 6", () =>
        {
            DerivedCombatStats jinxed = DerivedCombatStats.ForCharacter(Special.Neutral) with { Jinxed = true };
            var player = PlayerCharacter.Create("Azarado", Special.Neutral with { Luck = 10 }, new[] { CharacterTrait.Jinxed }, new[] { 0, 1, 2 }, 25, 0);
            Assert(DerivedCombatStats.ForPlayer(player).Jinxed && !DerivedCombatStats.ForPlayer(PlayerCharacter.Neutral).Jinxed
                && DerivedCombatStats.ForPlayer(PlayerCharacter.Neutral.WithPerks(Ranks(Perk.Jinxed))).Jinxed, "The trait or the perk.");
            // The ant misses (roll 100), the coin falls on 1: a critical failure on day 6 (failure table of a natural attack).
            CombatantState hero = Hero(Spear(), jinxed), ant = AntAt(Centre + 200);
            long day6 = 6 * CriticalRules.TicksPerDay;
            var fight = new CombatSession(new[] { hero, ant }, new HexNavigation(Array.Empty<MapObject>(), 0), new CombatTests.Scripted(100, 50, 1, 50), "ant", day6);
            Assert(fight.Jinxed, "The fight knows.");
            fight.Execute(CombatActionProposal.Attack("ant", "player"));
            Assert(fight.Log.OfType<CriticalFailure>().Single() is { AttackerId: "ant" }, "The ant's miss became a critical failure.");
            var calm = new CombatSession(new[] { Hero(Spear(), Stats()), AntAt(Centre + 200) }, new HexNavigation(Array.Empty<MapObject>(), 0), new CombatTests.Scripted(100, 50), "ant", day6);
            calm.Execute(CombatActionProposal.Attack("ant", "player"));
            Assert(!calm.Jinxed && !calm.Log.OfType<CriticalFailure>().Any(), "Without Jinxed, a plain miss (and the second d100 above the needed) stays a miss.");
            var early = new CombatSession(new[] { Hero(Spear(), jinxed), AntAt(Centre + 200) }, new HexNavigation(Array.Empty<MapObject>(), 0), new CombatTests.Scripted(100, 1), "player");
            early.Execute(CombatActionProposal.Attack("player", "ant"));
            Assert(!early.Log.OfType<CriticalFailure>().Any(), "Day 0: the player's own failure does nothing yet.");
        });
        check("Tag! (CE perkDialogHandleTagPerk, skillGetValue): a fourth skill not yet tagged doubles its points but gets no +20; saved with the perk", () =>
        {
            var tagger = PlayerCharacter.Create("Marcador", new Special(6, 6, 6, 6, 6, 5, 5), Array.Empty<CharacterTrait>(), new[] { 0, 1, 2 }, 25, 0);
            var world = SkillSpendingTests.Leveled(SkillSpendingTests.Map(78000), tagger);
            Assert(world.Level == 13 && world.PerksAvailable == 1, "Level 13: one free perk (the editor's flag, however many perk levels were crossed).");
            int lockpick = world.Sheet.Skill(CharacterSkill.Lockpick);
            Assert(world.CanChoosePerk(Perk.Tag) == PerkChoiceStatus.SkillChoice && world.CanChoosePerk(Perk.Tag, CharacterSkill.SmallGuns) == PerkChoiceStatus.SkillChoice
                && world.CanChoosePerk(Perk.Thief, CharacterSkill.Lockpick) == PerkChoiceStatus.SkillChoice, "Tag! wants an untagged skill; no other perk takes one.");
            SkillSpending spending = world.BeginSkillSpending();
            for (int i = 0; i < 5; i++) spending.Add(CharacterSkill.Lockpick);
            Assert(world.CommitSkillSpending(spending) == SkillCommitStatus.Ready && world.Sheet.Skill(CharacterSkill.Lockpick) == lockpick + 5, "Five points in Lockpick.");
            Assert(world.ChoosePerk(Perk.Tag, CharacterSkill.Lockpick) == PerkChoiceStatus.Ready && world.Sheet.IsTagged(CharacterSkill.Lockpick)
                && world.Sheet.Skill(CharacterSkill.Lockpick) == lockpick + 10, "Tagged by Tag!: the five points count twice, no +20.");
            Assert(world.PerksAvailable == 0 && world.ChoosePerk(Perk.Educated) == PerkChoiceStatus.NoPerkAvailable, "The flag is spent.");
            var saved = CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog")));
            WorldSession Load(WorldCheckpoint checkpoint) => WorldSession.Restore(_ => SkillSpendingTests.Map(78000), new[] { 3, 126 }, checkpoint, "catalog");
            var loaded = Load(saved);
            Assert(loaded.Perks.TagSkill == CharacterSkill.Lockpick && loaded.Sheet.Skill(CharacterSkill.Lockpick) == lockpick + 10, "Tag! comes back with its skill.");
            ChosenPerk tag = saved.Skills!.Perks!.Single(c => c.Perk == Perk.Tag);
            foreach (ChosenPerk bad in new[] { tag with { Skill = null }, tag with { Skill = CharacterSkill.SmallGuns }, tag with { Skill = 18 } })
                try { Load(saved with { Skills = saved.Skills with { Perks = saved.Skills.Perks!.Select(c => c.Perk == Perk.Tag ? bad : c).ToArray() } }); throw new Exception($"Forged Tag! accepted: {bad}."); }
                catch (InvalidDataException) { }
        });
        check("CE's free-perk flag: crossing perk levels without choosing earns one perk, not one each; it survives the checkpoint and a forged flag is refused", () =>
        {
            var smart = PlayerCharacter.Create("Estudioso", new Special(6, 6, 6, 6, 6, 5, 5), Array.Empty<CharacterTrait>(), new[] { 0, 1, 2 }, 25, 0);
            var world = SkillSpendingTests.Leveled(SkillSpendingTests.Map(21000), smart);
            Assert(world.Level == 7 && world.PerksAvailable == 1, "Levels 3 and 6 crossed: one perk.");
            int unspent = world.UnspentSkillPoints;
            Assert(world.ChoosePerk(Perk.Educated) == PerkChoiceStatus.Ready && world.UnspentSkillPoints == Math.Min(99, unspent + 2) && world.PerksAvailable == 0,
                "Educated: 2 points at once; the flag is down.");
            WorldSession Load(WorldCheckpoint checkpoint) => WorldSession.Restore(_ => SkillSpendingTests.Map(21000), new[] { 3, 126 }, checkpoint, "catalog");
            var saved = CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog")));
            Assert(saved.Skills!.FreePerk == false && Load(saved).PerksAvailable == 0, "No perk waiting after the choice.");
            try { Load(saved with { Skills = saved.Skills with { FreePerk = true } }); throw new Exception("A forged free perk was accepted."); }
            catch (InvalidDataException) { }
            try { Load(saved with { Skills = saved.Skills with { Perks = saved.Skills.Perks!.Select(c => c with { Skill = 3 }).ToArray() } }); throw new Exception("A skill on another perk accepted."); }
            catch (InvalidDataException) { }
            var fresh = SkillSpendingTests.Leveled(SkillSpendingTests.Map(21000), smart);
            var waiting = CheckpointFile.Decode(CheckpointFile.Encode(fresh.Capture("catalog")));
            Assert(waiting.Skills!.FreePerk == true && Load(waiting).PerksAvailable == 1 && Load(waiting with { Skills = waiting.Skills with { FreePerk = null } }).PerksAvailable == 1,
                "Waiting, saved and restored; an older save without the flag keeps its earned perk.");
        });
        check("Here and Now (CE perkAddEffect) grants the experience left to the next level, which brings its HP and skill points; saved with the perk", () =>
        {
            var world = SkillSpendingTests.Leveled(SkillSpendingTests.Map(3000));
            int hp = world.MaxHitPoints, unspent = world.UnspentSkillPoints;
            Assert(world.Level == 3 && world.ChoosePerk(Perk.HereAndNow) == PerkChoiceStatus.Ready && world.Experience == 6000 && world.Level == 4
                && world.MaxHitPoints == hp + world.Sheet.HitPointsForLevel(4) && world.UnspentSkillPoints == unspent + world.Sheet.SkillPointsForLevel(4)
                && world.PerksAvailable == 0, "3000 more XP: level 4, its HP and points, no perk at 4.");
            WorldSession Load(WorldCheckpoint checkpoint) => WorldSession.Restore(_ => SkillSpendingTests.Map(3000), new[] { 3, 126 }, checkpoint, "catalog");
            var saved = CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog")));
            Assert(saved.Skills!.Perks is [{ Perk: Perk.HereAndNow, Level: 3, Experience: 3000 }] && Load(saved).Experience == 6000 && Load(saved).Level == 4, "Restored at level 4.");
            foreach (int? xp in new int?[] { null, 0, 3001 })
                try { Load(saved with { Skills = saved.Skills with { Perks = new[] { saved.Skills.Perks![0] with { Experience = xp } } } }); throw new Exception($"Forged Here and Now XP {xp} accepted."); }
                catch (InvalidDataException) { }
        });
        check("Mutate! (CE perkDialogHandleMutatePerk) trades one trait for one the character never had; later levels follow the new traits", () =>
        {
            var skilled = PlayerCharacter.Create("Mutante", Special.Neutral with { Luck = 10 }, new[] { CharacterTrait.Skilled, CharacterTrait.Gifted }, new[] { 0, 1, 2 }, 25, 0);
            var world = SkillSpendingTests.Leveled(SkillSpendingTests.Map(36000), skilled);
            Assert(world.Level == 9 && world.PerksAvailable == 1 && world.Sheet.PerkInterval == 4, "Skilled: perk levels 4 and 8, one flag.");
            Assert(world.CanChoosePerk(Perk.Mutate) == PerkChoiceStatus.TraitChoice && world.CanChoosePerk(Perk.Mutate, lose: CharacterTrait.Bruiser, gain: CharacterTrait.Kamikaze) == PerkChoiceStatus.TraitChoice
                && world.CanChoosePerk(Perk.Mutate, lose: CharacterTrait.Skilled, gain: CharacterTrait.Gifted) == PerkChoiceStatus.TraitChoice
                && world.CanChoosePerk(Perk.Thief, lose: CharacterTrait.Skilled) == PerkChoiceStatus.TraitChoice, "Lose one it has, gain one it lacks; no other perk swaps traits.");
            int hp = world.MaxHitPoints;
            Assert(world.ChoosePerk(Perk.Mutate, lose: CharacterTrait.Skilled, gain: CharacterTrait.Kamikaze) == PerkChoiceStatus.Ready
                && !world.Sheet.Has(CharacterTrait.Skilled) && world.Sheet.Has(CharacterTrait.Kamikaze) && world.Sheet.ArmorClass == 0 && world.MaxHitPoints == hp,
                "Skilled out, Kamikaze in: AC 0 now; past levels keep their HP.");
            Assert(world.Sheet.PerkLevel(12) && world.Sheet.PerkLevel(8) && world.Sheet.SkillPointsForLevel(10) == world.Sheet.SkillPointsForLevel(9) - 5,
                "From level 10 on: perks every third level, 5 fewer skill points (level 8 stays a Skilled perk level).");
            WorldSession Load(WorldCheckpoint checkpoint) => WorldSession.Restore(_ => SkillSpendingTests.Map(36000), new[] { 3, 126 }, checkpoint, "catalog");
            var saved = CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog")));
            Assert(Load(saved).Sheet.Has(CharacterTrait.Kamikaze) && !Load(saved).Sheet.Has(CharacterTrait.Skilled) && Load(saved).Character.Has(CharacterTrait.Skilled)
                && Load(saved).Character.Traits.Contains(CharacterTrait.Skilled), "The swap comes back; the saved character keeps its original traits.");
            ChosenPerk mutation = saved.Skills!.Perks!.Single();
            foreach (ChosenPerk bad in new[] { mutation with { TraitLost = (int)CharacterTrait.Bruiser }, mutation with { TraitGained = (int)CharacterTrait.Gifted },
                mutation with { TraitGained = null }, mutation with { TraitGained = 16 } })
                try { Load(saved with { Skills = saved.Skills with { Perks = new[] { bad } } }); throw new Exception($"Forged Mutate! accepted: {bad}."); }
                catch (InvalidDataException) { }
        });
        check("in the world a perk is chosen once the level earns it, joins the sheet and the checkpoint, and forged choices are refused", () =>
        {
            var world = SkillSpendingTests.Leveled(SkillSpendingTests.Map(3000));
            Assert(world.Level == 3 && world.PerksAvailable == 1, "Level 3: one perk.");
            Assert(world.CanChoosePerk(Perk.Awareness) == PerkChoiceStatus.NotModeled && world.CanChoosePerk(Perk.BonusHtHDamage) == PerkChoiceStatus.Special, "Refusals.");
            int lockpick = world.Sheet.Skill(CharacterSkill.Lockpick);
            Assert(world.ChoosePerk(Perk.Thief) == PerkChoiceStatus.Ready && world.PerksAvailable == 0 && world.Perks.Has(Perk.Thief)
                && world.Sheet.Skill(CharacterSkill.Lockpick) == lockpick + 10, "Thief: Lockpick +10.");
            Assert(world.ChoosePerk(Perk.QuickPockets) == PerkChoiceStatus.NoPerkAvailable, "One per earned perk.");
            var saved = CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog")));
            WorldSession Load(WorldCheckpoint checkpoint) => WorldSession.Restore(_ => SkillSpendingTests.Map(3000), new[] { 3, 126 }, checkpoint, "catalog");
            Assert(saved.Skills!.Perks is [{ Perk: Perk.Thief, Level: 3 }] && Load(saved).Perks.Has(Perk.Thief) && Load(saved).PerksAvailable == 0, "Saved and restored.");
            foreach (ChosenPerk[] bad in new[] { new[] { new ChosenPerk(Perk.Awareness, 3) }, new[] { new ChosenPerk(Perk.Thief, 2) }, new[] { new ChosenPerk(Perk.Thief, 4) },
                new[] { new ChosenPerk(Perk.Thief, 3), new ChosenPerk(Perk.Thief, 3) }, new[] { new ChosenPerk(Perk.Thief, 3), new ChosenPerk(Perk.QuickPockets, 3) },
                Array.Empty<ChosenPerk>(), new[] { new ChosenPerk(Perk.PoweredArmor, 3) } })
                try { Load(saved with { Skills = saved.Skills with { Perks = bad } }); throw new Exception($"Forged perks accepted: {string.Join(",", bad.Select(b => b.ToString()))}."); }
                catch (InvalidDataException) { }
            world.Reset();
            Assert(world.Perks.Count == 0 && world.Sheet.Skill(CharacterSkill.Lockpick) < lockpick + 10, "A new game starts without perks.");
        });
    }
}
