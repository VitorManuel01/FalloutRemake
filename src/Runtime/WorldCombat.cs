using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime.Combat;

namespace FalloutRemake.Runtime;

public enum CombatCommitStatus { Ready, NotOver, PlayerDefeated, InvalidFight, StaleSource, AlreadyDefeated }
/// <summary>Source critters that notice the player at its current location; the nearest one starts the fight.</summary>
public sealed record CombatEncounter(ActorLocation Player, string[] FoeKeys, string InitiatorKey);
/// <summary>A source critter killed in a won fight: its record stays intact; the world projects the corpse.</summary>
public sealed record DefeatedCritter([property: JsonRequired] int Map, [property: JsonRequired] string Key, [property: JsonRequired] int Pid,
    [property: JsonRequired] int Elevation, [property: JsonRequired] int Hex);

/// <summary>
/// Outcome of won fights in the live world (roadmap COM-004), saved in checkpoint v10 (<see cref="SavedCombat"/>).
/// </summary>
public sealed partial class WorldSession
{
    private readonly Dictionary<(int Map, string Key), DefeatedCritter> _defeated = new();
    private int _combatExperience;
    public int CombatExperience => _combatExperience;
    /// <summary>Player HP after the last fight; null means unhurt (no world HP model exists outside combat yet).</summary>
    public int? PlayerHitPoints { get; private set; }
    public bool HasCombatOutcome => _defeated.Count != 0 || PlayerHitPoints != null;
    public IReadOnlyList<DefeatedCritter> Defeated => _defeated.Values.OrderBy(d => d.Map).ThenBy(d => d.Key, StringComparer.Ordinal).ToArray();
    public bool IsDefeated(int map, string key) => _defeated.ContainsKey((map, key));

    // A corpse keeps its source identity at the hex where it fell and no longer blocks movement.
    private MapObject DefeatedObject(int map, MapObject source) => _defeated.TryGetValue((map, source.Key), out DefeatedCritter? dead)
        ? source with { Header = source.Header with { Tile = dead.Hex, Flags = source.Header.Flags | HexNavigation.NoBlock } } : source;

    public CombatCommitStatus CommitCombat(CombatSession fight)
    {
        ArgumentNullException.ThrowIfNull(fight);
        if (!fight.Turn.Over) return CombatCommitStatus.NotOver;
        CombatantState[] players = fight.Combatants.Where(c => c.IsPlayer).ToArray();
        if (players.Length != 1) return CombatCommitStatus.InvalidFight;
        if (!players[0].Alive) return CombatCommitStatus.PlayerDefeated;
        var roots = Current.Map.ObjectLayers.SelectMany(l => l.Objects).ToDictionary(o => o.Key, StringComparer.Ordinal);
        var dead = new List<DefeatedCritter>();
        foreach (CombatantState foe in fight.Combatants.Where(c => !c.IsPlayer))
        {
            if (!roots.TryGetValue(foe.Id, out MapObject? source) || source.Header.Pid != foe.Pid || source.Header.Elevation != foe.Elevation
                || (source.Header.Pid >> 24) != 1) return CombatCommitStatus.StaleSource;
            if (IsDefeated(CurrentMap, foe.Id)) return CombatCommitStatus.AlreadyDefeated;
            if (!foe.Alive) dead.Add(new DefeatedCritter(CurrentMap, foe.Id, source.Header.Pid, foe.Elevation, foe.Hex));
        }
        int awarded = fight.Log.OfType<CombatantDied>().Sum(d => d.Experience);
        if (awarded != fight.Turn.ExperienceAwarded || awarded < 0) return CombatCommitStatus.InvalidFight;
        foreach (DefeatedCritter critter in dead) _defeated.Add((critter.Map, critter.Key), critter);
        _combatExperience += awarded;
        PlayerHitPoints = players[0].HitPoints == players[0].Stats.MaxHitPoints ? null : players[0].HitPoints;
        return CombatCommitStatus.Ready;
    }

    private void ResetCombat() { _defeated.Clear(); _combatExperience = 0; PlayerHitPoints = null; }

    private SavedCombat CaptureCombat() => new(Defeated.ToArray(), _combatExperience, PlayerHitPoints);

    // Atomic with the rest of Restore (runs on the candidate session). Every corpse must be an encounter critter of a
    // restored area lying on geometry it could reach, and the XP must be exactly what those kills award.
    private void RestoreCombat(SavedCombat? saved)
    {
        ResetCombat();
        if (saved == null) return;
        int maxHitPoints = DerivedCombatStats.ForCharacter(Special.Neutral).MaxHitPoints;
        if (saved.Defeated is not { Length: <= 64 } defeated || saved.Experience < 0
            || saved.PlayerHitPoints is int hp && (hp < 1 || hp >= maxHitPoints))
            throw new InvalidDataException("Checkpoint combat outcome is malformed.");
        int experience = 0;
        foreach (DefeatedCritter dead in defeated)
        {
            if (dead?.Key == null || _defeated.ContainsKey((dead.Map, dead.Key)) || !_areas.TryGetValue(dead.Map, out WorldArea? area))
                throw new InvalidDataException("Checkpoint corpse is duplicated or outside the restored areas.");
            MapObject? source = area.Map.ObjectLayers.SelectMany(l => l.Objects).FirstOrDefault(o => o.Key == dead.Key);
            Prototype? prototype = area.Map.Prototypes.FirstOrDefault(p => p.Pid == dead.Pid);
            if (source == null || prototype == null || source.Header.Pid != dead.Pid || !EncounterPids.Contains(dead.Pid)
                || source.Header.Elevation != dead.Elevation || !HexNavigation.Valid(dead.Hex) || (source.Header.Flags & HexNavigation.Hidden) != 0)
                throw new InvalidDataException("Checkpoint corpse does not match an encounter critter of its MAP.");
            HexNavigation navigation = area.Doors.Navigation(dead.Elevation);
            if (navigation.IsExit(dead.Hex) || navigation.BlockersAt(dead.Hex).Any(b => b.FidType != 1 && !b.IsDoor))
                throw new InvalidDataException("Checkpoint corpse lies on geometry no critter can stand on.");
            experience += CritterPrototypeData.Read(prototype).Experience;
            _defeated.Add((dead.Map, dead.Key), dead);
        }
        if (experience != saved.Experience) throw new InvalidDataException("Checkpoint combat experience differs from its defeated critters.");
        _combatExperience = experience; PlayerHitPoints = saved.PlayerHitPoints;
    }

    // Hostile temple creatures: Giant Ant (PID 382) and Radscorpion (PID 5). Dumar keeps his Speech role.
    public static readonly IReadOnlySet<int> EncounterPids = new HashSet<int> { 0x0100017E, 0x01000005 };

    /// <summary>
    /// Weapon the player fights with: the carried hand weapon (melee or unarmed primary attack) with the highest average
    /// damage, ties by PID then source key; bare hands when none is carried. Ranged weapons wait for ammunition tracking.
    /// There is no equip slot yet, so carrying is wielding.
    /// </summary>
    public CombatWeapon PlayerWeapon()
    {
        CombatWeapon best = CombatWeapon.Unarmed();
        foreach (CarriedItem item in Carried.OrderBy(i => i.Source.Header.Pid).ThenBy(i => i.Source.Key, StringComparer.Ordinal))
        {
            if (!_areas.TryGetValue(item.Map, out WorldArea? area)
                || area.Map.Prototypes.FirstOrDefault(p => p.Pid == item.Source.Header.Pid) is not { Type: 0, Subtype: WeaponPrototypeData.Subtype } prototype) continue;
            CombatWeapon weapon = CombatWeapon.Primary($"PID {prototype.Pid}", WeaponPrototypeData.Read(prototype));
            if (!weapon.IsRanged && weapon.MinDamage + weapon.MaxDamage > best.MinDamage + best.MaxDamage) best = weapon;
        }
        return best;
    }
    public const int EncounterDistance = 5, EncounterApproach = 10;

    /// <summary>
    /// Hostile encounter critters within <see cref="EncounterDistance"/> hexes that can reach the player in at most
    /// <see cref="EncounterApproach"/> steps (a critter behind a wall in another room never starts a fight).
    /// </summary>
    public CombatEncounter? FindEncounter(ActorLocation player)
    {
        if (player.Map != CurrentMap) return null;
        ObjectLayer? layer = Current.Map.ObjectLayers.FirstOrDefault(l => l.Elevation == player.Elevation);
        if (layer == null) return null;
        HexNavigation navigation = Current.Doors.Navigation(player.Elevation);
        var noticing = new List<string>();
        foreach (MapObject foe in layer.Objects.Where(o => EncounterPids.Contains(o.Header.Pid) && HexNavigation.Valid(o.Header.Tile)
                     && (o.Header.Flags & HexNavigation.Hidden) == 0 && !IsDefeated(CurrentMap, o.Key)
                     && o.State.GetValueOrDefault("team", 1) != 0 && FalloutGrid.Distance(player.Hex, o.Header.Tile) <= EncounterDistance)
                     .OrderBy(o => FalloutGrid.Distance(player.Hex, o.Header.Tile)).ThenBy(o => o.Key, StringComparer.Ordinal))
        {
            var probe = new[]
            {
                new CombatantState("player", "player", null, 0, true, player.Hex, player.Elevation, DerivedCombatStats.ForCharacter(Special.Neutral), CombatWeapon.Unarmed(), 1),
                new CombatantState(foe.Key, foe.Key, foe.Header.Pid, 1, false, foe.Header.Tile, player.Elevation, DerivedCombatStats.ForCharacter(Special.Neutral), CombatWeapon.Unarmed(), 1)
            };
            if (new CombatMovement(navigation, probe).Path(probe[0], h => FalloutGrid.Distance(h, foe.Header.Tile) <= 1) is { } path && path.Length <= EncounterApproach)
                noticing.Add(foe.Key);
        }
        return noticing.Count == 0 ? null : new CombatEncounter(player, noticing.ToArray(), noticing[0]);
    }

    /// <summary>Opens the fight for an encounter found at the actor's current location; the noticing critter acts first (CE).</summary>
    public CombatSession BeginCombat(CombatEncounter encounter, CombatWeapon playerWeapon, ICombatRandom random)
    {
        ArgumentNullException.ThrowIfNull(encounter); ArgumentNullException.ThrowIfNull(playerWeapon);
        if (encounter.Player != Location) throw new InvalidOperationException("The encounter belongs to another actor location.");
        var roots = Current.Map.ObjectLayers.SelectMany(l => l.Objects).ToDictionary(o => o.Key, StringComparer.Ordinal);
        var stats = DerivedCombatStats.ForCharacter(Special.Neutral);
        var combatants = new List<CombatantState> { new("player", "Escolhido", null, 0, true, Location.Hex, Location.Elevation, stats,
            playerWeapon, PlayerHitPoints ?? stats.MaxHitPoints) };
        foreach (string key in encounter.FoeKeys)
        {
            if (!roots.TryGetValue(key, out MapObject? source) || IsDefeated(CurrentMap, key)) throw new InvalidOperationException("Encounter critter is no longer available.");
            Prototype prototype = Current.Map.Prototypes.FirstOrDefault(p => p.Pid == source.Header.Pid)
                ?? throw new System.IO.InvalidDataException($"Critter prototype {source.Header.Pid:X8} is not part of the map dependencies.");
            combatants.Add(CombatantState.FromMapCritter(source, CritterPrototypeData.Read(prototype), $"PID {source.Header.Pid:X8}"));
        }
        return new CombatSession(combatants, Current.Doors.Navigation(Location.Elevation), random, encounter.InitiatorKey);
    }
}
