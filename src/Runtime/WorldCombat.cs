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
/// <summary>What a stretch of exploration time did: poison HP lost (or a lethal event, not applied) and the creatures that wandered.</summary>
public sealed record WorldTimeAdvance(int HitPointsLost, bool Died, PoisonState Poison, string[] Wandered);
/// <summary>A source critter killed in a won fight: its record stays intact; the world projects the corpse.</summary>
public sealed record DefeatedCritter([property: JsonRequired] int Map, [property: JsonRequired] string Key, [property: JsonRequired] int Pid,
    [property: JsonRequired] int Elevation, [property: JsonRequired] int Hex);
/// <summary>
/// A living source critter standing away from its MAP record (left behind by a fight the player left, or wandered off):
/// the world projects its hex, HP and facing.
/// </summary>
public sealed record SurvivingCritter([property: JsonRequired] int Map, [property: JsonRequired] string Key, [property: JsonRequired] int Pid,
    [property: JsonRequired] int Elevation, [property: JsonRequired] int Hex, [property: JsonRequired] int HitPoints, [property: JsonRequired] int Rotation,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] DamageFlags Crippled = DamageFlags.None);

/// <summary>
/// Outcome of won fights in the live world (roadmap COM-004), saved in checkpoint v10 (<see cref="SavedCombat"/>).
/// </summary>
public sealed partial class WorldSession
{
    private readonly Dictionary<(int Map, string Key), DefeatedCritter> _defeated = new();
    private readonly Dictionary<(int Map, string Key), SurvivingCritter> _survivors = new();
    private int _combatExperience;
    public IReadOnlyList<SurvivingCritter> Survivors => _survivors.Values.OrderBy(s => s.Map).ThenBy(s => s.Key, StringComparer.Ordinal).ToArray();
    public int CombatExperience => _combatExperience;
    // Damage is what persists between fights: a level-up raises max HP and, as CE critterAdjustHitPoints, current HP alike.
    private int _playerDamage;
    public int Level => CharacterProgression.Level(Experience);
    public int MaxHitPoints => CharacterProgression.MaxHitPoints(Character.WithPerks(_perks), Level);
    public int HitPoints => MaxHitPoints - _playerDamage;

    /// <summary>Player HP when hurt; null means unhurt. Healing comes from level-ups and drugs (<see cref="UseDrug"/>).</summary>
    public int? PlayerHitPoints => _playerDamage == 0 ? null : HitPoints;
    public bool HasCombatOutcome => _defeated.Count != 0 || _survivors.Count != 0 || _playerDamage != 0 || _poison.Active || _playerCrippled != DamageFlags.None;
    // The dude's poison (radscorpion stings): it runs on game time, in combat rounds and in exploration (AdvanceTime).
    private PoisonState _poison;
    public PoisonState Poison => _poison;
    // The game clock (CE gGameTime): it starts at 302400 ticks on day 0; exploration adds a tick per 100 ms, fights 5 s a round.
    private long _gameTime = CriticalRules.GameStartTicks;
    public long GameTime => _gameTime;
    // Crippled limbs outlast fights (CE keeps them in combat.results until a doctor treats them).
    private DamageFlags _playerCrippled;
    public DamageFlags PlayerCrippled => _playerCrippled;

    /// <summary>
    /// Exploration game time (CE: one tick per 100 ms outside combat). Due poison events cost HP; a lethal one is reported
    /// (Died) without applying anything, so the caller ends the game as a lost fight does. With <paramref name="random"/>,
    /// the creatures of the current map wander (WorldWander); <paramref name="reserved"/> are hexes the walking player is
    /// about to enter, which wanderers treat like the player's own hex.
    /// </summary>
    public WorldTimeAdvance AdvanceTime(int ticks, ICombatRandom? random = null, IReadOnlyCollection<int>? reserved = null)
    {
        if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
        if (InCombat || ticks == 0) return new(0, false, _poison, Array.Empty<string>());
        PoisonState next = _poison.Advance(ticks, out int lost);
        if (lost > 0 && lost >= HitPoints) return new(lost, true, next, Array.Empty<string>());
        _poison = next; _playerDamage += lost;
        _gameTime = Math.Min(_gameTime + ticks, CriticalRules.GameEndTicks - 1);
        string[] moved = random == null ? Array.Empty<string>() : Wander(ticks, random, reserved);
        return new(lost, false, _poison, moved);
    }
    public IReadOnlyList<DefeatedCritter> Defeated => _defeated.Values.OrderBy(d => d.Map).ThenBy(d => d.Key, StringComparer.Ordinal).ToArray();
    public bool IsDefeated(int map, string key) => _defeated.ContainsKey((map, key));

    // The open fight (if any): its creatures block where they stand, not on their source hex.
    private CombatSession? _activeFight;
    private int _activeFightMap;
    public bool InCombat => _activeFight is { Turn.Over: false };

    // A corpse keeps its source identity at the hex where it fell and no longer blocks movement;
    // a creature of the open fight is projected to its current hex, so exploration routes agree with the fight.
    private MapObject DefeatedObject(int map, MapObject source)
    {
        if (_defeated.TryGetValue((map, source.Key), out DefeatedCritter? dead))
            return source with { Header = source.Header with { Tile = dead.Hex, Flags = source.Header.Flags | HexNavigation.NoBlock } };
        if (_activeFight != null && map == _activeFightMap && _activeFight.Combatants.FirstOrDefault(c => !c.IsPlayer && c.Id == source.Key) is { } live)
            return source with { Header = source.Header with { Tile = live.Hex, Flags = live.Alive ? source.Header.Flags : source.Header.Flags | HexNavigation.NoBlock } };
        if (_survivors.TryGetValue((map, source.Key), out SurvivingCritter? standing))
            return source with { Header = source.Header with { Tile = standing.Hex, Rotation = standing.Rotation },
                State = new Dictionary<string, int>(source.State) { ["hp"] = standing.HitPoints } };
        return source;
    }

    /// <summary>Drops the open fight without applying it (load, reset or a refused outcome).</summary>
    public void AbandonCombat() { _activeFight = null; AbandonTempleDuel(); }

    public CombatCommitStatus CommitCombat(CombatSession fight)
    {
        ArgumentNullException.ThrowIfNull(fight);
        if (!fight.Turn.Over) return CombatCommitStatus.NotOver;
        if (ReferenceEquals(fight, _activeFight)) _activeFight = null;
        CombatantState[] players = fight.Combatants.Where(c => c.IsPlayer).ToArray();
        if (players.Length != 1) return CombatCommitStatus.InvalidFight;
        if (!players[0].Alive) return CombatCommitStatus.PlayerDefeated;
        // Cameron's challenge is fought out: the gate is locked behind both duelists.
        if (!players[0].Poison.Valid || fight.GameTime < _gameTime || (players[0].Conditions & ~DamageFlags.Crippled) != 0) return CombatCommitStatus.InvalidFight;
        if (ReferenceEquals(fight, _duelFight))
        {
            CombatCommitStatus duel = fight.Left ? CombatCommitStatus.InvalidFight : CommitTempleDuel(fight, players[0]);
            if (duel == CombatCommitStatus.Ready) { _poison = players[0].Poison; _gameTime = fight.GameTime; _playerCrippled = players[0].Conditions; KeepHand(players[0]); }
            return duel;
        }
        var roots = Current.Map.ObjectLayers.SelectMany(l => l.Objects).ToDictionary(o => o.Key, StringComparer.Ordinal);
        var dead = new List<DefeatedCritter>();
        var standing = new List<SurvivingCritter>();
        foreach (CombatantState foe in fight.Combatants.Where(c => !c.IsPlayer))
        {
            if (!roots.TryGetValue(foe.Id, out MapObject? source) || source.Header.Pid != foe.Pid || source.Header.Elevation != foe.Elevation
                || (source.Header.Pid >> 24) != 1) return CombatCommitStatus.StaleSource;
            if (IsDefeated(CurrentMap, foe.Id)) return CombatCommitStatus.AlreadyDefeated;
            if (!foe.Alive) dead.Add(new DefeatedCritter(CurrentMap, foe.Id, source.Header.Pid, foe.Elevation, foe.Hex));
            // Alive after the fight: left behind by the player's Enter, or gone from the fight on its own (disengaged).
            else if (fight.Left || foe.Disengaged) standing.Add(new SurvivingCritter(CurrentMap, foe.Id, source.Header.Pid, foe.Elevation, foe.Hex, foe.HitPoints, foe.Rotation,
                foe.Conditions & DamageFlags.Crippled));
            else return CombatCommitStatus.InvalidFight;
        }
        int awarded = fight.Log.OfType<CombatantDied>().Sum(d => d.Experience);
        if (awarded != fight.Turn.ExperienceAwarded || awarded < 0) return CombatCommitStatus.InvalidFight;
        // Doses taken in the fight use up carried Healing Powder units, checked before anything is applied.
        int doses = fight.Log.OfType<HealingUsed>().Count(h => h.CombatantId == players[0].Id);
        CarriedItem[] powders = HealingCarried();
        if (doses > powders.Sum(p => p.Quantity)) return CombatCommitStatus.InvalidFight;
        foreach (CarriedItem powder in powders)
        {
            int take = Math.Min(doses, powder.Quantity);
            if (take > 0 && _areas[powder.Map].Items.ConsumeCarried(powder.OwnerKey, powder.Source.Key, take) != ItemUseStatus.Ready)
                throw new InvalidOperationException("Checked Healing Powder could not be used up.");
            doses -= take;
        }
        foreach (DefeatedCritter critter in dead) { _defeated.Add((critter.Map, critter.Key), critter); _survivors.Remove((critter.Map, critter.Key)); }
        foreach (SurvivingCritter critter in standing) _survivors[(critter.Map, critter.Key)] = critter;
        _combatExperience += awarded;
        _playerDamage = players[0].Stats.MaxHitPoints - players[0].HitPoints;
        _poison = players[0].Poison;
        _gameTime = fight.GameTime; _playerCrippled = players[0].Conditions; KeepHand(players[0]);
        return CombatCommitStatus.Ready;
    }


    /// <summary>Carried Healing Powder units in a fixed order (map, owner, source key): the order fights use them up.</summary>
    private CarriedItem[] HealingCarried() => Carried.Where(c => _areas[c.Map].Items.PrototypeOf(c.Source) is var p && HealingDrugProfile.Supported(p))
        .OrderBy(c => c.Map).ThenBy(c => c.OwnerKey, StringComparer.Ordinal).ThenBy(c => c.Source.Key, StringComparer.Ordinal).ToArray();
    public int HealingDoses => HealingCarried().Sum(c => c.Quantity);

    private void ResetCombat()
    {
        _defeated.Clear(); _survivors.Clear(); _combatExperience = 0; _playerDamage = 0; _poison = PoisonState.None; _activeFight = null; ResetWandering();
        _gameTime = CriticalRules.GameStartTicks; _playerCrippled = DamageFlags.None; ResetHands();
    }

    private SavedCombat CaptureCombat() => new(Defeated.ToArray(), _combatExperience, PlayerHitPoints, _survivors.Count == 0 ? null : Survivors.ToArray(),
        _poison.Active ? new SavedPoison(_poison.Level, _poison.TicksLeft) : null, _gameTime == CriticalRules.GameStartTicks ? null : _gameTime,
        _playerCrippled == DamageFlags.None ? null : (int)_playerCrippled, CaptureHands());

    // Atomic with the rest of Restore (runs on the candidate session). Every corpse must be an encounter critter of a
    // restored area lying on geometry it could reach, and the XP must be exactly what those kills award.
    // A killed Cameron (the challenge's lethal outcome) may lie among the corpses besides the encounter creatures.
    private void RestoreCombat(SavedCombat? saved, bool cameronKilled = false)
    {
        ResetCombat();
        if (saved == null) return;
        if (saved.Defeated is not { Length: <= 64 } defeated || saved.Experience < 0 || saved.PlayerHitPoints is < 1)
            throw new InvalidDataException("Checkpoint combat outcome is malformed.");
        int experience = 0;
        foreach (DefeatedCritter dead in defeated)
        {
            if (dead?.Key == null || _defeated.ContainsKey((dead.Map, dead.Key)) || !_areas.TryGetValue(dead.Map, out WorldArea? area))
                throw new InvalidDataException("Checkpoint corpse is duplicated or outside the restored areas.");
            MapObject? source = area.Map.ObjectLayers.SelectMany(l => l.Objects).FirstOrDefault(o => o.Key == dead.Key);
            Prototype? prototype = area.Map.Prototypes.FirstOrDefault(p => p.Pid == dead.Pid);
            bool cameron = cameronKilled && dead.Map == 3 && source?.Offset == 425776;
            if (source == null || prototype == null || source.Header.Pid != dead.Pid || !(EncounterPids.Contains(dead.Pid) || cameron)
                || source.Header.Elevation != dead.Elevation || !HexNavigation.Valid(dead.Hex) || (source.Header.Flags & HexNavigation.Hidden) != 0)
                throw new InvalidDataException("Checkpoint corpse does not match an encounter critter of its MAP.");
            HexNavigation navigation = area.Doors.Navigation(dead.Elevation);
            if (navigation.IsExit(dead.Hex) || navigation.BlockersAt(dead.Hex).Any(b => b.FidType != 1 && !b.IsDoor))
                throw new InvalidDataException("Checkpoint corpse lies on geometry no critter can stand on.");
            experience += CritterPrototypeData.Read(prototype).Experience;
            _defeated.Add((dead.Map, dead.Key), dead);
        }
        if (experience != saved.Experience) throw new InvalidDataException("Checkpoint combat experience differs from its defeated critters.");
        _combatExperience = experience;
        // Survivors: living encounter critters of a restored area, standing where a critter can stand, one per hex.
        if (saved.Survivors is { Length: > 64 }) throw new InvalidDataException("Checkpoint survivors are malformed.");
        foreach (SurvivingCritter alive in saved.Survivors ?? Array.Empty<SurvivingCritter>())
        {
            if (alive?.Key == null || _survivors.ContainsKey((alive.Map, alive.Key)) || _defeated.ContainsKey((alive.Map, alive.Key))
                || !_areas.TryGetValue(alive.Map, out WorldArea? area) || alive.Rotation is < 0 or > 5 || !HexNavigation.Valid(alive.Hex)
                || (alive.Crippled & ~DamageFlags.Crippled) != 0
                || _survivors.Values.Any(s => s.Map == alive.Map && s.Elevation == alive.Elevation && s.Hex == alive.Hex))
                throw new InvalidDataException("Checkpoint survivor is duplicated, already dead or outside the restored areas.");
            MapObject? source = area.Map.ObjectLayers.SelectMany(l => l.Objects).FirstOrDefault(o => o.Key == alive.Key);
            Prototype? prototype = area.Map.Prototypes.FirstOrDefault(p => p.Pid == alive.Pid);
            if (source == null || prototype == null || source.Header.Pid != alive.Pid || !EncounterPids.Contains(alive.Pid)
                || source.Header.Elevation != alive.Elevation || (source.Header.Flags & HexNavigation.Hidden) != 0
                || alive.HitPoints < 1 || alive.HitPoints > DerivedCombatStats.FromCritter(CritterPrototypeData.Read(prototype)).MaxHitPoints)
                throw new InvalidDataException("Checkpoint survivor does not match an encounter critter of its MAP.");
            HexNavigation navigation = area.Doors.Navigation(alive.Elevation);
            if (navigation.IsExit(alive.Hex) || navigation.BlockersAt(alive.Hex).Any(b => b.FidType != 1 && !b.IsDoor))
                throw new InvalidDataException("Checkpoint survivor stands on geometry no critter can stand on.");
            _survivors.Add((alive.Map, alive.Key), alive);
        }
        // The level, and so the max HP, comes from the whole restored XP; full HP is stored as null.
        if (saved.PlayerHitPoints is int hp && hp >= MaxHitPoints) throw new InvalidDataException("Checkpoint player HP is not below the level's maximum.");
        _playerDamage = saved.PlayerHitPoints is int hurt ? MaxHitPoints - hurt : 0;
        if (saved.Poison is { } poison)
        {
            var state = new PoisonState(poison.Level, poison.TicksLeft);
            if (!state.Active || !state.Valid) throw new InvalidDataException("Checkpoint poison level or timer is out of range.");
            _poison = state;
        }
        // Absent means the start time; a stored clock is past it (or it would have been left out) and before CE's year-13 end.
        if (saved.GameTime is long time && (time <= CriticalRules.GameStartTicks || time >= CriticalRules.GameEndTicks))
            throw new InvalidDataException("Checkpoint game time is out of range.");
        _gameTime = saved.GameTime ?? CriticalRules.GameStartTicks;
        if (saved.PlayerCrippled is int crippled && (crippled == 0 || (crippled & ~(int)DamageFlags.Crippled) != 0))
            throw new InvalidDataException("Checkpoint crippled limbs are malformed.");
        _playerCrippled = (DamageFlags)(saved.PlayerCrippled ?? 0);
    }

    // Hostile temple creatures: Giant Ant (PID 382) and Radscorpion (PID 5). Dumar keeps his Speech role.
    public static readonly IReadOnlySet<int> EncounterPids = new HashSet<int> { 0x0100017E, 0x01000005 };
    /// <summary>The radscorpion's zclscorp combat_p_proc may poison the dude on each hit (PoisonRules).</summary>
    public const int RadscorpionPid = 0x01000005;
    /// <summary>
    /// AI packets the creatures' map_enter_p_proc sets (critter_add_trait OBJECT_AI_PACKET): zclrat Rat 7, zclscorp Scorpion 8,
    /// and actemvil gives Cameron Dumar 143 (--combat-ai-report checks the bytecode).
    /// </summary>
    public static readonly IReadOnlyDictionary<int, int> ScriptPackets = new Dictionary<int, int> { [0x0100017E] = 7, [0x01000005] = 8 };
    public const int CameronPacket = 143;
    private AiPacket? Packet(int number) => number >= 0 && _aiPackets?.TryGetValue(number, out AiPacket? packet) == true ? packet
        : _aiPackets == null || number < 0 ? null : throw new InvalidDataException($"ai.txt has no packet {number}.");

    /// <summary>
    /// Hostile encounter critters that see the player (<see cref="CritterSight.Sees"/>, the obj_can_see_obj test their
    /// critter_p_proc makes before attacking), nearest first. CE starts the fight even when the critter has no way to the
    /// player; here a critter must also reach a hex beside the player, because leaving combat out of sight is not modelled
    /// and such a fight could never end.
    /// </summary>
    public CombatEncounter? FindEncounter(ActorLocation player)
    {
        if (player.Map != CurrentMap) return null;
        ObjectLayer? layer = Current.Map.ObjectLayers.FirstOrDefault(l => l.Elevation == player.Elevation);
        if (layer == null) return null;
        HexNavigation navigation = Current.Doors.Navigation(player.Elevation);
        var noticing = new List<string>();
        // Survivors of a fight the player left look out from where they stand, facing where they last turned.
        foreach (MapObject foe in layer.Objects.Select(o => EffectiveObject(CurrentMap, o)).Where(o => EncounterPids.Contains(o.Header.Pid) && HexNavigation.Valid(o.Header.Tile)
                     && (o.Header.Flags & HexNavigation.Hidden) == 0 && !IsDefeated(CurrentMap, o.Key) && o.State.GetValueOrDefault("team", 1) != 0)
                     .OrderBy(o => FalloutGrid.Distance(player.Hex, o.Header.Tile)).ThenBy(o => o.Key, StringComparer.Ordinal))
        {
            Prototype? prototype = Current.Map.Prototypes.FirstOrDefault(p => p.Pid == foe.Header.Pid);
            if (prototype == null || !CritterSight.Sees(foe, CritterSight.Perception(CritterPrototypeData.Read(prototype)), player.Hex, navigation)) continue;
            var probe = new[]
            {
                new CombatantState("player", "player", null, 0, true, player.Hex, player.Elevation, DerivedCombatStats.ForCharacter(Special.Neutral), CombatWeapon.Unarmed(), 1),
                new CombatantState(foe.Key, foe.Key, foe.Header.Pid, 1, false, foe.Header.Tile, player.Elevation, DerivedCombatStats.ForCharacter(Special.Neutral), CombatWeapon.Unarmed(), 1)
            };
            if (new CombatMovement(navigation, probe).Path(probe[0], h => FalloutGrid.Distance(h, foe.Header.Tile) <= 1) != null) noticing.Add(foe.Key);
        }
        return noticing.Count == 0 ? null : new CombatEncounter(player, noticing.ToArray(), noticing[0]);
    }

    /// <summary>Opens the fight for an encounter found at the actor's current location; the noticing critter acts first (CE).</summary>
    public CombatSession BeginCombat(CombatEncounter encounter, CombatWeapon playerWeapon, ICombatRandom random)
    {
        ArgumentNullException.ThrowIfNull(encounter); ArgumentNullException.ThrowIfNull(playerWeapon);
        if (encounter.Player != Location) throw new InvalidOperationException("The encounter belongs to another actor location.");
        var roots = Current.Map.ObjectLayers.SelectMany(l => l.Objects).ToDictionary(o => o.Key, StringComparer.Ordinal);
        var stats = PlayerStats();
        var combatants = new List<CombatantState> { new("player", Character.Name, null, 0, true, Location.Hex, Location.Elevation, stats,
            playerWeapon, HitPoints, healingDoses: HealingDoses) { PoisonResistance = Sheet.PoisonResistance, Poison = _poison, Conditions = _playerCrippled,
            AlternateWeapon = HandWeapon(Other(ActiveHand)), RightHandActive = ActiveHand == Hand.Right } };
        foreach (string key in encounter.FoeKeys)
        {
            if (!roots.TryGetValue(key, out MapObject? source) || IsDefeated(CurrentMap, key)) throw new InvalidOperationException("Encounter critter is no longer available.");
            Prototype prototype = Current.Map.Prototypes.FirstOrDefault(p => p.Pid == source.Header.Pid)
                ?? throw new System.IO.InvalidDataException($"Critter prototype {source.Header.Pid:X8} is not part of the map dependencies.");
            CombatantState foe = CombatantState.FromMapCritter(EffectiveObject(CurrentMap, source), CritterPrototypeData.Read(prototype), $"PID {source.Header.Pid:X8}",
                poisonsOnHit: source.Header.Pid == RadscorpionPid, ai: Packet(ScriptPackets.GetValueOrDefault(source.Header.Pid, -1)));
            if (_survivors.TryGetValue((CurrentMap, key), out SurvivingCritter? standing)) foe.SetConditions(standing.Crippled);
            combatants.Add(foe);
        }
        if (InCombat) throw new InvalidOperationException("A fight is already open.");
        var session = new CombatSession(combatants, Current.Doors.Navigation(Location.Elevation), random, encounter.InitiatorKey, _gameTime);
        _activeFight = session; _activeFightMap = CurrentMap; session.Thrown = OnThrown;
        return session;
    }
}
