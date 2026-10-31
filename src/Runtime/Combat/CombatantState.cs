using System;
using System.IO;
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
    public DerivedCombatStats Stats { get; }
    public CombatWeapon Weapon { get; }
    public int Experience { get; }
    public int Hex { get; internal set; }
    public int HitPoints { get; internal set; }
    public int ActionPoints { get; internal set; }
    /// <summary>Unspent AP of the last turn, added to AC until this combatant's next turn starts.</summary>
    public int BonusArmorClass { get; internal set; }
    public LifeStatus Status => HitPoints <= 0 ? LifeStatus.Dead : LifeStatus.Active;
    public bool Alive => Status == LifeStatus.Active;
    public int EffectiveArmorClass => Stats.ArmorClass + BonusArmorClass;

    public CombatantState(string id, string name, int? pid, int team, bool isPlayer, int hex, int elevation, DerivedCombatStats stats,
        CombatWeapon weapon, int hitPoints, int experience = 0)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Combatant id is required.", nameof(id));
        if (!FalloutRemake.Geometry.HexNavigation.Valid(hex) || elevation is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(hex));
        ArgumentNullException.ThrowIfNull(stats); ArgumentNullException.ThrowIfNull(weapon);
        if (hitPoints < 1 || hitPoints > stats.MaxHitPoints || experience < 0) throw new ArgumentOutOfRangeException(nameof(hitPoints));
        Id = id; Name = name; Pid = pid; Team = team; IsPlayer = isPlayer; Hex = hex; Elevation = elevation;
        Stats = stats; Weapon = weapon; HitPoints = hitPoints; Experience = experience; ActionPoints = 0;
    }

    /// <summary>Map critter: current HP from its serialized instance ("hp"), team and XP from the PRO/instance, natural attack.</summary>
    public static CombatantState FromMapCritter(MapObject source, CritterPrototypeData prototype, string name, CombatWeapon? weapon = null)
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
            hp, prototype.Experience);
    }
}
