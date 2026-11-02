using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;

/// <summary>First Aid and Doctor on the player (CE skillUse, skillGetFreeUsageSlot, _protinst_use_item_on).</summary>
internal static class MedicalTests
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Reject(Action action, string message) { try { action(); } catch (InvalidDataException) { return; } throw new Exception(message); }

    // Level 2 after the veteran ant's fight: 30 of 34 HP, day 0 (no critical die); First Aid 20%, Doctor 15%, critical chance 5.
    static WorldSession Hurt() => SkillSpendingTests.Leveled(SkillSpendingTests.Map(1000));
    static WorldSession Load(WorldCheckpoint checkpoint) => WorldSession.Restore(_ => SkillSpendingTests.Map(1000), new[] { 3, 126 }, checkpoint, "catalog");
    static WorldCheckpoint Saved(WorldSession world) => CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog")));

    // The Healing Powder fixture with a First Aid Kit (PID 47) carried instead.
    static WorldSession KitWorld()
    {
        MapDocument map = WorldDrugTests.Map();
        MapObject owner = map.ObjectLayers[0].Objects[^1];
        MapObject kit = owner.Inventory[0].Object with { Key = "kit", Subtype = 5, Header = owner.Inventory[0].Object.Header with { Pid = 47 } };
        owner = owner with { Inventory = new[] { owner.Inventory[0] with { Quantity = 2, Object = kit } } };
        var prototype = new Prototype(47, 0, 5, 0, -1, new("proto/items/00000047.pro", "synthetic kit", 125, new string('0', 64)), new byte[125]);
        map = map with { ObjectLayers = new[] { map.ObjectLayers[0] with { Objects = map.ObjectLayers[0].Objects[..^1].Append(owner).ToArray() } },
            Prototypes = map.Prototypes.Where(p => p.Pid != HealingDrugProfile.Pid).Append(prototype).ToArray() };
        var world = new WorldSession(_ => map, new[] { 3, 126 });
        world.Current.Items.Restore(new[] { new TakenItem("owner", "kit", 1) });
        var fight = WorldDrugTests.Hurt(world, 99, 1, 11); WorldDrugTests.Win(fight);
        if (world.CommitCombat(fight) != CombatCommitStatus.Ready || world.HitPoints != 26) throw new Exception("Kit setup.");
        return world;
    }

    public static void Run(Action<string, Action> check)
    {
        check("First Aid rolls skill + critical chance, heals 1-5 for 25 XP, takes 30 minutes even unhurt and allows three successes a day", () =>
        {
            var world = Hurt(); long time = world.GameTime; int xp = world.Experience;
            Assert(world.Sheet.Skill(CharacterSkill.FirstAid) == 20 && world.Sheet.CriticalChance == 5 && world.MedicalUsesLeft(CharacterSkill.FirstAid) == 3, "20% + 5.");
            Assert(world.UseMedicalSkill(CharacterSkill.FirstAid, new CombatTests.Scripted(), true).Status == MedicalStatus.Busy, "Not while walking.");
            Assert(world.UseMedicalSkill(CharacterSkill.FirstAid, new CombatTests.Scripted(26), false) is { Status: MedicalStatus.Ready, Healing: false, Minutes: 30, Experience: 0 }
                && world.HitPoints == 30 && world.GameTime == time + 18000 && world.MedicalUsesLeft(CharacterSkill.FirstAid) == 3, "26 > 25: no healing, no use spent.");
            Assert(world.UseMedicalSkill(CharacterSkill.FirstAid, new CombatTests.Scripted(25, 5), false) is { Healing: true, Rolled: 5, Healed: 4, Experience: 25 }
                && world.HitPoints == 34 && world.Experience == xp + 25 && world.MedicalExperience == 25 && world.MedicalUsesLeft(CharacterSkill.FirstAid) == 2,
                "25 <= 25: 5 rolled, 4 healed (capped), 25 XP, one use.");
            Assert(world.UseMedicalSkill(CharacterSkill.FirstAid, new CombatTests.Scripted(), false) is { AlreadyHealthy: true, Minutes: 30, Experience: 0 }
                && world.GameTime == time + 3 * 18000, "Unhurt: no roll, still 30 minutes.");
        });
        check("a fourth success needs 25 whole hours since the oldest of three (skillGetFreeUsageSlot), and the slots then roll over", () =>
        {
            var world = Hurt(); long start = world.GameTime;
            for (int i = 0; i < 3; i++) Assert(world.UseMedicalSkill(CharacterSkill.FirstAid, new CombatTests.Scripted(1, 1), false).Healed == 1, "Three successes.");
            Assert(world.HitPoints == 33 && world.MedicalUses(CharacterSkill.FirstAid).SequenceEqual(new[] { start, start + 18000, start + 36000 }), "Stamped before the 30 minutes.");
            long time = world.GameTime;
            Assert(world.UseMedicalSkill(CharacterSkill.FirstAid, new CombatTests.Scripted(), false).Status == MedicalStatus.Tired && world.GameTime == time, "Tired: nothing happens.");
            Assert(world.CanUseMedicalSkill(CharacterSkill.Doctor, false) == MedicalStatus.Ready, "Doctor keeps its own slots.");
            world.AdvanceTime((int)(start + 25 * 36000 - 1 - world.GameTime));
            Assert(world.CanUseMedicalSkill(CharacterSkill.FirstAid, false) == MedicalStatus.Tired, "24 whole hours and 59 minutes: still tired.");
            world.AdvanceTime(1); long now = world.GameTime;
            // Day 1 now: randomRoll throws the critical die as well.
            Assert(world.UseMedicalSkill(CharacterSkill.FirstAid, new CombatTests.Scripted(1, 50, 1), false).Healed == 1
                && world.MedicalUses(CharacterSkill.FirstAid).SequenceEqual(new[] { start + 18000, start + 36000, now }) && world.HitPoints == 34, "Rolled over.");
        });
        check("Doctor treats each crippled limb with skill + critical chance, an hour each, then rolls the bare skill for 4-10 HP; XP repeats after a treated limb", () =>
        {
            var hurt = Hurt(); WorldCheckpoint saved = Saved(hurt);
            var world = Load(saved with { Combat = saved.Combat! with { PlayerCrippled = (int)(DamageFlags.Blind | DamageFlags.CrippledRightLeg) } });
            long time = world.GameTime; int xp = world.Experience;
            Assert(world.Sheet.Skill(CharacterSkill.Doctor) == 15 && world.HitPoints == 30, "15%.");
            MedicalResult first = world.UseMedicalSkill(CharacterSkill.Doctor, new CombatTests.Scripted(20, 21, 15, 6), false);
            Assert(first is { LimbsHealed: DamageFlags.Blind, LimbsFailed: DamageFlags.CrippledRightLeg, Healing: true, Rolled: 6, Healed: 4, Minutes: 180, Experience: 150 }
                && world.PlayerCrippled == DamageFlags.CrippledRightLeg && world.HitPoints == 34 && world.GameTime == time + 3 * 36000 && world.Experience == xp + 150
                && world.MedicalUsesLeft(CharacterSkill.Doctor) == 2, "Eye 20 <= 20, leg 21 > 20 (still 50 XP), HP 15 <= 15 bare skill: one use.");
            MedicalResult second = world.UseMedicalSkill(CharacterSkill.Doctor, new CombatTests.Scripted(99, 1, 4), false);
            Assert(second is { LimbsFailed: DamageFlags.CrippledRightLeg, Healing: true, Healed: 0, Minutes: 120, Experience: 50 } && world.MedicalUsesLeft(CharacterSkill.Doctor) == 1,
                "At full HP the HP roll still runs, heals nothing and counts.");
            Assert(world.UseMedicalSkill(CharacterSkill.Doctor, new CombatTests.Scripted(1, 1, 4), false) is { LimbsHealed: DamageFlags.CrippledRightLeg, Experience: 100 }
                && world.PlayerCrippled == DamageFlags.None && world.MedicalUsesLeft(CharacterSkill.Doctor) == 0, "Leg treated.");
            Assert(world.CanUseMedicalSkill(CharacterSkill.Doctor, false) == MedicalStatus.Tired, "Three uses.");
            var whole = Load(saved);
            Assert(whole.UseMedicalSkill(CharacterSkill.Doctor, new CombatTests.Scripted(16), false) is { Healing: false, Minutes: 60, Experience: 0 }, "16 > 15.");
        });
        check("Healer adds 4-10 per rank to both skills' healing; Medic and Living Anatomy raise the skills (perkGetSkillModifier)", () =>
        {
            Assert(MedicalRules.Healing(CharacterSkill.FirstAid, 0) == (1, 5) && MedicalRules.Healing(CharacterSkill.FirstAid, 2) == (9, 25)
                && MedicalRules.Healing(CharacterSkill.Doctor, 0) == (4, 10) && MedicalRules.Healing(CharacterSkill.Doctor, 1) == (8, 20), "Healer.");
            PlayerCharacter sheet = Hurt().Sheet, medic = sheet.WithPerks(new PerkRanks(new[] { new ChosenPerk(Perk.Medic, 12), new ChosenPerk(Perk.LivingAnatomy, 12) }));
            Assert(medic.Skill(CharacterSkill.FirstAid) == sheet.Skill(CharacterSkill.FirstAid) + 10 && medic.Skill(CharacterSkill.Doctor) == sheet.Skill(CharacterSkill.Doctor) + 20, "Medic, Living Anatomy.");
        });
        check("a First Aid Kit adds 20 to the roll and the critical chance, runs out on 1 in 10 and is refused in combat or for Doctor", () =>
        {
            var world = KitWorld(); CarriedItem kit = world.MedicalKitsCarried(CharacterSkill.FirstAid).Single();
            Assert(world.MedicalKitsCarried(CharacterSkill.Doctor).Length == 0 && world.CanUseMedicalSkill(CharacterSkill.Doctor, false, kit) == MedicalStatus.NoItem, "Not a Doctor kit.");
            Assert(world.UseMedicalSkill(CharacterSkill.FirstAid, new CombatTests.Scripted(45, 3, 2), false, kit) is { Healing: true, Healed: 3, KitUsedUp: false }
                && world.CarriedCount == 1 && world.HitPoints == 29, "20 + 5 + 20 = 45; the kit stays (2).");
            Assert(world.UseMedicalSkill(CharacterSkill.FirstAid, new CombatTests.Scripted(46, 1), false, kit) is { Healing: false, KitUsedUp: true }
                && world.CarriedCount == 0 && world.Current.Items.Balance("owner", "kit") is { Consumed: 1 }, "A failure uses it up too (1).");
            Assert(world.CanUseMedicalSkill(CharacterSkill.FirstAid, false, kit) == MedicalStatus.NoItem, "Gone.");
            var restored = WorldSession.Restore(_ => world.Current.Map, new[] { 3, 126 }, Saved(world), "catalog");
            Assert(restored.Current.Items.Balance("owner", "kit") is { Consumed: 1 } && restored.MedicalExperience == 25, "A used-up kit restores.");
            var fighting = Hurt(); fighting.BeginCombat(new CombatEncounter(fighting.Location, new[] { "map3:other-ant" }, "player"), fighting.PlayerWeapon(), new CombatTests.Scripted());
            Assert(fighting.CanUseMedicalSkill(CharacterSkill.FirstAid, false) == MedicalStatus.InCombat && fighting.CanUseMedicalSkill(CharacterSkill.Doctor, false) == MedicalStatus.InCombat, "Combat.");
        });
        check("checkpoint keeps each skill's stamps and the XP they earned, and refuses stamps out of order, in the future or with gaps", () =>
        {
            var world = Hurt();
            world.UseMedicalSkill(CharacterSkill.FirstAid, new CombatTests.Scripted(1, 1), false);
            world.UseMedicalSkill(CharacterSkill.FirstAid, new CombatTests.Scripted(1, 1), false);
            WorldCheckpoint saved = Saved(world);
            Assert(saved.Skills!.FirstAidUses!.Length == 3 && saved.Skills.DoctorUses == null && saved.Skills.MedicalExperience == 50, "Captured.");
            var restored = Load(saved);
            Assert(restored.MedicalUses(CharacterSkill.FirstAid).SequenceEqual(world.MedicalUses(CharacterSkill.FirstAid)) && restored.Experience == world.Experience
                && restored.MedicalUsesLeft(CharacterSkill.FirstAid) == 1, "Restored.");
            long[] uses = saved.Skills.FirstAidUses;
            WorldCheckpoint With(long[]? firstAid, int? xp = 50) => saved with { Skills = saved.Skills with { FirstAidUses = firstAid, MedicalExperience = xp } };
            foreach (WorldCheckpoint bad in new[]
            {
                With(new[] { uses[1], uses[0], 0L }), With(new[] { 0L, uses[0], 0L }), With(new[] { uses[0], uses[1] }), With(new[] { 0L, 0L, 0L }),
                With(new[] { uses[0], saved.Combat!.GameTime!.Value + 1, 0L }), With(new[] { 1L, uses[1], 0L }), With(uses, 30), With(uses, 0)
            })
                Reject(() => Load(bad), "Forged medical state accepted.");
        });
    }
}
