using System;
using System.IO;
using System.Linq;
using FalloutRemake.Import;
using FalloutRemake.Runtime.Combat;

namespace FalloutRemake.Runtime;

public enum MedicalStatus { Ready, UnknownSkill, InCombat, Busy, Tired, NoItem }

/// <summary>
/// What one First Aid or Doctor use did on the player: whether the HP roll succeeded and what it healed, the limbs Doctor
/// treated or failed, the minutes it took, the XP it earned, whether the kit ran out, and the poison the time cost.
/// </summary>
public sealed record MedicalResult(MedicalStatus Status, bool AlreadyHealthy = false, bool Healing = false, int Rolled = 0, int Healed = 0,
    DamageFlags LimbsHealed = DamageFlags.None, DamageFlags LimbsFailed = DamageFlags.None, int Minutes = 0, int Experience = 0,
    bool KitUsedUp = false, int PoisonLost = 0, bool Died = false);

/// <summary>A medical kit (CE _protinst_use_item_on): the skill it uses, its bonus and the proto.msg line when it runs out.</summary>
public sealed record MedicalKit(int Pid, int Skill, int Bonus, int Message);

public static class MedicalKits
{
    public static readonly MedicalKit[] All =
    {
        new(47, CharacterSkill.FirstAid, 20, 901),   // First Aid Kit
        new(91, CharacterSkill.Doctor, 20, 900),     // Doctor's Bag
        new(408, CharacterSkill.FirstAid, 40, 911),  // Field Medic First Aid Kit
        new(409, CharacterSkill.Doctor, 40, 910),    // Paramedic's Bag
    };
    public static MedicalKit? For(int pid) => All.FirstOrDefault(k => k.Pid == pid);
    /// <summary>CE recognizes the kits by PID alone, before any script runs.</summary>
    public static bool Supported(Prototype? prototype) => prototype is { Type: 0 } && For(prototype.Pid) != null;
}

/// <summary>CE skill.cc rules of First Aid and Doctor.</summary>
public static class MedicalRules
{
    public const int UsesPerDay = 3, FirstAidExperience = 25, DoctorExperience = 50, FirstAidMinutes = 30, DoctorMinutes = 60;
    private const long TicksPerHour = 36000;

    /// <summary>gHealableDamageFlags, in the order Doctor treats them.</summary>
    public static readonly DamageFlags[] Healable =
        { DamageFlags.Blind, DamageFlags.CrippledLeftArm, DamageFlags.CrippledRightArm, DamageFlags.CrippledRightLeg, DamageFlags.CrippledLeftLeg };

    /// <summary>The HP a success heals: First Aid 1-5, Doctor 4-10, Healer adding 4-10 per rank to both ends.</summary>
    public static (int Min, int Max) Healing(int skill, int healerRank) => skill == CharacterSkill.FirstAid
        ? (4 * healerRank + 1, 10 * healerRank + 5) : (4 * healerRank + 4, 10 * healerRank + 10);

    /// <summary>
    /// skillGetFreeUsageSlot: the first empty slot; with all three used, the last one once more than 24 whole hours passed
    /// since the oldest; otherwise none (-1). An empty slot holds 0.
    /// </summary>
    public static int FreeSlot(long[] uses, long now)
    {
        for (int slot = 0; slot < UsesPerDay; slot++) if (uses[slot] == 0) return slot;
        return (now - uses[0]) / TicksPerHour <= 24 ? -1 : UsesPerDay - 1;
    }

    /// <summary>skillUpdateLastUse: stamps the free slot, shifting the older ones when the slots were full.</summary>
    public static void Record(long[] uses, long now)
    {
        int slot = FreeSlot(uses, now);
        if (slot == -1) return;
        if (uses[slot] != 0) for (int i = 0; i < slot; i++) uses[i] = uses[i + 1];
        uses[slot] = now;
    }

    /// <summary>Uses available now: the empty slots plus the stamps already more than 24 hours old.</summary>
    public static int UsesLeft(long[] uses, long now) => uses.Count(u => u == 0 || (now - u) / TicksPerHour > 24);
}

/// <summary>
/// First Aid and Doctor on the player outside combat (CE actionUseSkill → skillUse; kits through _protinst_use_item_on).
/// Each skill allows three successful uses per day: a fourth within 24 hours of the oldest is refused before anything
/// happens. First Aid rolls skill + critical chance (+ the kit's bonus) and, on a success, heals 1-5 HP (Healer adds 4-10
/// per rank) for 25 XP; it takes 30 minutes, even on someone already healthy. Doctor first rolls each crippled limb or
/// damaged eye in turn with the same sum, then rolls the bare skill (the kit only raises the critical chance) to heal 4-10
/// HP; each treatment takes an hour more. Its XP follows CE: 50 per limb roll once any limb succeeded, and 50 more for
/// the HP. A kit runs out on 1 in 10 uses that went through. The time passes after everything, so poison acts on it.
/// </summary>
public sealed partial class WorldSession
{
    private long[] _firstAidUses = new long[MedicalRules.UsesPerDay], _doctorUses = new long[MedicalRules.UsesPerDay];
    private int _medicalExperience;

    /// <summary>The XP First Aid and Doctor earned.</summary>
    public int MedicalExperience => _medicalExperience;

    private long[] UsesOf(int skill) => skill == CharacterSkill.FirstAid ? _firstAidUses : _doctorUses;

    public int MedicalUsesLeft(int skill) => skill is CharacterSkill.FirstAid or CharacterSkill.Doctor ? MedicalRules.UsesLeft(UsesOf(skill), _gameTime) : 0;

    /// <summary>The time each slot was stamped (0 when empty), oldest first.</summary>
    public long[] MedicalUses(int skill) => skill is CharacterSkill.FirstAid or CharacterSkill.Doctor ? UsesOf(skill).ToArray() : Array.Empty<long>();

    /// <summary>The carried kits for a skill (none of them is on the Temple's or Arroyo's maps).</summary>
    public CarriedItem[] MedicalKitsCarried(int skill) => Carried.Where(c => _areas[c.Map].Items.PrototypeOf(c.Source) is var p && MedicalKits.Supported(p)
        && MedicalKits.For(p!.Pid)!.Skill == skill).ToArray();

    public MedicalStatus CanUseMedicalSkill(int skill, bool moving, CarriedItem? kit = null)
    {
        if (skill is not (CharacterSkill.FirstAid or CharacterSkill.Doctor)) return MedicalStatus.UnknownSkill;
        if (kit != null && KitOf(kit)?.Skill != skill) return MedicalStatus.NoItem;
        if (InCombat) return MedicalStatus.InCombat;
        if (moving) return MedicalStatus.Busy;
        return MedicalRules.FreeSlot(UsesOf(skill), _gameTime) == -1 ? MedicalStatus.Tired : MedicalStatus.Ready;
    }

    private MedicalKit? KitOf(CarriedItem kit)
    {
        if (!_areas.TryGetValue(kit.Map, out WorldArea? area)
            || area.Items.Carried.SingleOrDefault(c => c.OwnerKey == kit.OwnerKey && c.Source.Key == kit.Source.Key) is not { Quantity: > 0 } carried)
            return null;
        Prototype? prototype = area.Items.PrototypeOf(carried.Source);
        return MedicalKits.Supported(prototype) ? MedicalKits.For(prototype!.Pid) : null;
    }

    /// <summary>Uses First Aid or Doctor on the player, bare-handed or with a carried kit of that skill.</summary>
    public MedicalResult UseMedicalSkill(int skill, ICombatRandom random, bool moving, CarriedItem? kit = null)
    {
        ArgumentNullException.ThrowIfNull(random);
        MedicalStatus status = CanUseMedicalSkill(skill, moving, kit);
        if (status != MedicalStatus.Ready) return new(status);
        PlayerCharacter sheet = Sheet;
        long[] uses = UsesOf(skill);
        int bonus = kit == null ? 0 : KitOf(kit)!.Bonus, value = sheet.Skill(skill), critical = sheet.CriticalChance + bonus;
        var (min, max) = MedicalRules.Healing(skill, _perks.Rank(Perk.Healer));
        bool criticals = _gameTime / CriticalRules.TicksPerDay >= 1;
        // randomRoll: a d100 against the difficulty, then the die that may make it critical (from day 1), whose result
        // does not matter here.
        bool Roll(int difficulty)
        {
            int margin = difficulty - random.Between(1, 100);
            if (criticals) random.Between(1, 100);
            return margin >= 0;
        }
        int attempts = 1, xp = 0, rolled = 0, healed = 0; bool success = false, healing = false, healthy = false;
        DamageFlags treated = DamageFlags.None, failed = DamageFlags.None;
        if (skill == CharacterSkill.FirstAid)
        {
            if (HitPoints < MaxHitPoints)
            {
                // skillRoll(obj, skill, criticalChance): the critical chance (with the kit) joins the skill.
                if (Roll(value + critical))
                {
                    rolled = random.Between(min, max); healed = Math.Min(rolled, _playerDamage); _playerDamage -= healed;
                    MedicalRules.Record(uses, _gameTime); healing = success = true; xp += MedicalRules.FirstAidExperience;
                }
            }
            else healthy = true;
        }
        else if (HitPoints < MaxHitPoints || (_playerCrippled & DamageFlags.Crippled) != 0)
        {
            bool stamped = false;
            foreach (DamageFlags limb in MedicalRules.Healable.Where(f => (_playerCrippled & f) != 0))
            {
                attempts++;
                if (Roll(value + critical))
                {
                    _playerCrippled &= ~limb; treated |= limb;
                    MedicalRules.Record(uses, _gameTime); success = stamped = true;
                }
                else failed |= limb;
                // _show_skill_use_messages with the success count, which stays 1 after the first treated limb.
                if (success) xp += MedicalRules.DoctorExperience;
            }
            // The HP roll is the bare skill (randomRoll(skillValue, criticalChance)).
            if (Roll(value))
            {
                rolled = random.Between(min, max); healed = Math.Min(rolled, _playerDamage); _playerDamage -= healed;
                if (!stamped) MedicalRules.Record(uses, _gameTime);
                healing = true; xp += MedicalRules.DoctorExperience;
            }
        }
        else healthy = true;
        if (xp > 0) { _medicalExperience += xp; GainExperience(xp); }
        // _protinst_use_item_on: once skillUse went through, the kit runs out on a 1 in 10.
        bool usedUp = kit != null && random.Between(1, 10) == 1
            && _areas[kit.Map].Items.ConsumeCarried(kit.OwnerKey, kit.Source.Key, 1) == ItemUseStatus.Ready;
        int minutes = (skill == CharacterSkill.FirstAid ? MedicalRules.FirstAidMinutes : MedicalRules.DoctorMinutes) * attempts;
        WorldTimeAdvance step = AdvanceTime(minutes * 600);
        return new(MedicalStatus.Ready, healthy, healing, rolled, healed, treated, failed, minutes, xp, usedUp, step.HitPointsLost, step.Died);
    }

    private void ResetMedicine() { _firstAidUses = new long[MedicalRules.UsesPerDay]; _doctorUses = new long[MedicalRules.UsesPerDay]; _medicalExperience = 0; }

    // With the perks, before the level is needed: the XP the skills earned (25 or 50 a time).
    private void RestoreMedicalExperience(int? saved)
    {
        _medicalExperience = 0;
        if (saved == null) return;
        if (saved <= 0 || saved % MedicalRules.FirstAidExperience != 0) throw new InvalidDataException("Checkpoint medical XP is malformed.");
        _medicalExperience = saved.Value;
    }

    // Once the clock is restored: each skill's stamps, oldest first, none in the future, the empty slots last.
    private void RestoreMedicalUses(long[]? firstAid, long[]? doctor)
    {
        long[] Read(long[]? saved)
        {
            if (saved == null) return new long[MedicalRules.UsesPerDay];
            int used = saved.TakeWhile(u => u != 0).Count();
            if (saved.Length != MedicalRules.UsesPerDay || used == 0 || saved.Skip(used).Any(u => u != 0)
                || saved.Take(used).Any(u => u < CriticalRules.GameStartTicks || u > _gameTime) || saved.Take(used).Zip(saved.Skip(1).Take(used - 1)).Any(p => p.First > p.Second))
                throw new InvalidDataException("Checkpoint medical skill uses are malformed.");
            return saved.ToArray();
        }
        _firstAidUses = Read(firstAid); _doctorUses = Read(doctor);
    }
}
