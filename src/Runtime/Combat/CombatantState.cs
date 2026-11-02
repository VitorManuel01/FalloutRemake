using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime.Combat;

/// <summary>
/// Mutable state of one participant, changed only by its CombatSession. Id is the source object key for map critters,
/// so presentation and checkpoints keep pointing at the original record. Team 0 is the player's side.
/// </summary>
public sealed class CombatantState
{
    public string Id { get; }
    public string Name { get; }
    public int? Pid { get; }
    public int Team { get; }
    public bool IsPlayer { get; }
    public int Elevation { get; }
    /// <summary>Effective stats; the player's change only when it puts on or takes off armor in the open inventory.</summary>
    public DerivedCombatStats Stats { get; internal set; }
    public CombatWeapon Weapon { get; internal set; }
    /// <summary>The other hand's blow the player can switch to (bare hands: punch and kick); null when there is none.</summary>
    public CombatWeapon? AlternateWeapon { get; internal set; }
    /// <summary>The player strikes with the right hand (FO2's second item slot) rather than the left.</summary>
    public bool RightHandActive { get; internal set; }
    public int Experience { get; }
    public int Hex { get; internal set; }
    /// <summary>Facing (0 NE … 5 NW): the MAP rotation, then the last step walked or the last target struck, as CE animations leave it.</summary>
    public int Rotation { get; internal set; }
    public int HitPoints { get; internal set; }
    public int ActionPoints { get; internal set; }
    /// <summary>
    /// CE critterGetStat(STAT_ARMOR_CLASS): whenever it is not this combatant's turn, its current AP count as AC. Every
    /// round starts by refilling everyone's AP (_combat_set_move_all), so one that has not acted yet defends with all of them.
    /// </summary>
    public int BonusArmorClass => Acting ? 0 : HtHEvade ? 2 * ActionPoints + Stats.Skill(DerivedCombatStats.Unarmed) / 12 : ActionPoints;
    /// <summary>
    /// CE critterGetStat(AC) with the HtH Evade perk: the dude with no weapon in either hand (fist weapons have no weapon
    /// animation) counts its AP twice, plus a twelfth of its Unarmed skill.
    /// </summary>
    private bool HtHEvade => IsPlayer && Stats.Perks.Has(FalloutRemake.Runtime.Perk.HtHEvade) && Weapon.AnimationCode == 0 && (AlternateWeapon?.AnimationCode ?? 0) == 0;
    /// <summary>The Bonus Move perk's free hexes for this turn (CE _combat_free_move, 2 per rank): spent on walking before AP.</summary>
    public int FreeMove { get; internal set; }
    /// <summary>It is this combatant's turn.</summary>
    public bool Acting { get; internal set; }
    /// <summary>Carried healing doses this combatant can take during the fight.</summary>
    public int HealingDoses { get; internal set; }
    /// <summary>Carried ammunition, in inventory order (reloads draw from it); the rounds left change as the fight goes.</summary>
    public IReadOnlyList<CombatAmmo> Ammunition { get; init; } = Array.Empty<CombatAmmo>();
    public LifeStatus Status => HitPoints <= 0 ? LifeStatus.Dead : LifeStatus.Active;
    public bool Alive => Status == LifeStatus.Active;
    /// <summary>Yields when its turn starts below half its maximum HP (a duel, not a fight to the death).</summary>
    public bool YieldsBelowHalf { get; init; }
    public bool Yielded { get; internal set; }
    /// <summary>A hit on the player may poison it (the radscorpion's zclscorp combat_p_proc, see <see cref="PoisonRules"/>).</summary>
    public bool PoisonsOnHit { get; init; }
    /// <summary>Poison Resistance in percent, for the player (CE poisons only the dude).</summary>
    public int PoisonResistance { get; init; }
    /// <summary>The player's poison, carried into and out of the fight; it acts on every combat round.</summary>
    public PoisonState Poison { get => _poison; init => _poison = value.Valid ? value : throw new ArgumentOutOfRangeException(nameof(value)); }
    private PoisonState _poison;
    internal void SetPoison(PoisonState poison) => _poison = poison;
    /// <summary>Still taking part: alive, not yielded and not gone (disengaged).</summary>
    public bool InFight => Alive && !Yielded && !Disengaged;
    /// <summary>The ai.txt packet the creature's script gives it (CE combat_ai); null keeps ProvisionalMeleeBehavior.</summary>
    public AiPacket? Ai { get; init; }
    /// <summary>CE combat.whoHitMe: the id of the combatant this one holds as its danger (attacker, retaliation, its own target).</summary>
    public string? WhoHitMe { get; internal set; }
    /// <summary>CE maneuvers: engaging (noticed the fight), fleeing (runs every turn), disengaging (leaves at the round's end).</summary>
    public bool Engaging { get; internal set; }
    public bool Fleeing { get; internal set; }
    public bool Disengaging { get; internal set; }
    /// <summary>Left the fight at a round's end; alive, it stays where it stands.</summary>
    public bool Disengaged { get; internal set; }
    /// <summary>A friend's corpse it saw fall without seeing the killer (CE aiInfoGetFriendlyDead): it keeps 10 hexes away.</summary>
    public string? FriendlyDead { get; internal set; }
    /// <summary>CE combat.damageLastTurn: damage taken since the fight began (only the dude's resets each turn).</summary>
    public int DamageTaken { get; internal set; }
    /// <summary>PE as critterGetStat gives it: blindness takes 5, clamped to 1.</summary>
    public int Perception => Math.Max(1, Stats.Special.Perception - ((_conditions & DamageFlags.Blind) != 0 ? 5 : 0));
    public int EffectiveArmorClass => Stats.ArmorClass + BonusArmorClass;
    /// <summary>PRO kill type, which picks the critical table of a non-player defender (0 Man … 18 Big Bad Boss).</summary>
    public int KillType { get => _killType; init => _killType = value is >= 0 and < CriticalRules.KillTypeCount ? value : throw new ArgumentOutOfRangeException(nameof(value)); }
    private readonly int _killType;
    /// <summary>
    /// Lasting results of criticals (CE combat.results): knocked out or down, a lost turn, crippled limbs. Crippled limbs
    /// outlast the fight; the rest end with it.
    /// </summary>
    public DamageFlags Conditions { get => _conditions; init => _conditions = (value & ~DamageFlags.Results) == 0 && (value & DamageFlags.Dead) == 0 ? value : throw new ArgumentOutOfRangeException(nameof(value)); }
    private DamageFlags _conditions;
    internal void SetConditions(DamageFlags conditions) => _conditions = conditions;
    /// <summary>Game time the knockout event fires (meaningful while KnockedOut).</summary>
    public long WakeAt { get; internal set; }
    /// <summary>Woke at the last round's end: outside the turn order until the end of this round (CE _combat_add_noncoms).</summary>
    public bool Rejoining { get; internal set; }
    public bool KnockedOut => (_conditions & DamageFlags.KnockedOut) != 0;
    /// <summary>Knocked down or out (CE _critter_is_prone): +40 to hit it, and standing up costs AP.</summary>
    public bool Prone => (_conditions & (DamageFlags.KnockedOut | DamageFlags.KnockedDown)) != 0;
    public int ActionPointsPerHex => CriticalRules.ActionPointsPerHex(_conditions);
    /// <summary>Hexes of a path this combatant can walk with its AP (crippled legs make each one dearer).</summary>
    public int StepsAffordable(int pathLength) => Math.Min(pathLength, (ActionPoints + FreeMove) / ActionPointsPerHex);

    public CombatantState(string id, string name, int? pid, int team, bool isPlayer, int hex, int elevation, DerivedCombatStats stats,
        CombatWeapon weapon, int hitPoints, int experience = 0, int healingDoses = 0)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Combatant id is required.", nameof(id));
        if (!FalloutRemake.Geometry.HexNavigation.Valid(hex) || elevation is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(hex));
        ArgumentNullException.ThrowIfNull(stats); ArgumentNullException.ThrowIfNull(weapon);
        if (hitPoints < 1 || hitPoints > stats.MaxHitPoints || experience < 0 || healingDoses < 0) throw new ArgumentOutOfRangeException(nameof(hitPoints));
        Id = id; Name = name; Pid = pid; Team = team; IsPlayer = isPlayer; Hex = hex; Elevation = elevation;
        Stats = stats; Weapon = weapon; HitPoints = hitPoints; Experience = experience; ActionPoints = 0; HealingDoses = healingDoses;
    }

    /// <summary>Map critter: current HP from its serialized instance ("hp"), team and XP from the PRO/instance, natural attack.</summary>
    public static CombatantState FromMapCritter(MapObject source, CritterPrototypeData prototype, string name, CombatWeapon? weapon = null, bool poisonsOnHit = false,
        AiPacket? ai = null)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(prototype);
        if (source.Header.Pid != prototype.Pid) throw new InvalidDataException("Critter instance and prototype differ.");
        var stats = DerivedCombatStats.FromCritter(prototype);
        int hp = source.State.TryGetValue("hp", out int stored) ? stored : stats.MaxHitPoints;
        int team = source.State.TryGetValue("team", out int instanceTeam) ? instanceTeam : prototype.Team;
        if (hp < 1 || hp > stats.MaxHitPoints) throw new InvalidDataException($"Critter {source.Key} has {hp} HP outside 1..{stats.MaxHitPoints}.");
        if (team == 0) throw new InvalidDataException("Team 0 is reserved for the player's side.");
        return new(source.Key, name, prototype.Pid, team, false, source.Header.Tile, source.Header.Elevation, stats,
            weapon ?? CombatWeapon.Unarmed(prototype.DamageType is int natural and >= 0 and <= 6 ? (DamageType)natural : DamageType.Normal),
            hp, prototype.Experience) { Rotation = source.Header.Rotation, PoisonsOnHit = poisonsOnHit, KillType = prototype.KillType, Ai = ai };
    }
}

/// <summary>
/// One carried stack of ammunition as reloads see it: CE keeps the top box's rounds and full boxes under it, so a stack is
/// its total rounds ((boxes - 1) x box + top); reloading takes rounds from it until the weapon is full.
/// </summary>
public sealed class CombatAmmo
{
    public string Key { get; }
    public AmmoPrototypeData Data { get; }
    public int Rounds { get; internal set; }
    public CombatAmmo(string key, AmmoPrototypeData data, int rounds)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Ammunition key is required.", nameof(key));
        Key = key; Data = data ?? throw new ArgumentNullException(nameof(data));
        Rounds = rounds >= 0 ? rounds : throw new ArgumentOutOfRangeException(nameof(rounds));
    }
}

/// <summary>CE weaponAttemptReload and weaponReload, shared by the fight's reload and the world's outside it.</summary>
public static class FirearmReload
{
    public const int SolarScorcherPid = 390;

    /// <summary>
    /// weaponAttemptReload's choice: first a stack of the ammunition loaded last, then, only into an empty weapon, any stack
    /// of its caliber (weaponCanBeReloadedWith); null when the weapon is full or nothing fits. The Solar Scorcher recharges
    /// from light, which is not modeled.
    /// </summary>
    public static CombatAmmo? Source(CombatWeapon weapon, IEnumerable<CombatAmmo> ammo)
    {
        ArgumentNullException.ThrowIfNull(weapon); ArgumentNullException.ThrowIfNull(ammo);
        if (!weapon.UsesAmmo || weapon.Pid == SolarScorcherPid || weapon.Rounds >= weapon.AmmoCapacity) return null;
        bool Fits(CombatAmmo stack) => stack.Rounds > 0 && stack.Data.Caliber == weapon.Caliber && (weapon.Rounds == 0 || stack.Data.Pid == weapon.AmmoPid);
        return ammo.FirstOrDefault(a => a.Data.Pid == weapon.AmmoPid && Fits(a)) ?? ammo.FirstOrDefault(Fits);
    }

    /// <summary>
    /// _intface_item_reload: weaponAttemptReload until the weapon is full or nothing fits. Each stack gives what the weapon
    /// still takes (weaponReload); the weapon's ammunition type becomes the stack's. The rounds taken are listed in order.
    /// </summary>
    public static CombatWeapon Fill(CombatWeapon weapon, IEnumerable<CombatAmmo> ammo, List<(CombatAmmo Stack, int Rounds)> taken)
    {
        ArgumentNullException.ThrowIfNull(taken);
        var stacks = ammo.ToArray();
        while (Source(weapon, stacks) is { } stack)
        {
            int take = Math.Min(weapon.AmmoCapacity - weapon.Rounds, stack.Rounds);
            stack.Rounds -= take;
            weapon = weapon with { Rounds = weapon.Rounds + take, AmmoPid = stack.Data.Pid, Ammo = stack.Data };
            taken.Add((stack, take));
        }
        return weapon;
    }
}
