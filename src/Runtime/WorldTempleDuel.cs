using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime.Combat;

namespace FalloutRemake.Runtime;

/// <summary>State of Cameron's unarmed challenge. InProgress exists only while the fight is open and is never saved.</summary>
public enum TempleDuelOutcome { None, InProgress, Yielded, Killed }
/// <summary>Units of a source item held in the challenge chest, by the area that owns the source.</summary>
public sealed record CustodyItem([property: JsonRequired] int Map, [property: JsonRequired] string OwnerKey, [property: JsonRequired] string ItemKey,
    [property: JsonRequired] int Quantity);
/// <summary>
/// Checkpoint v12: outcome of the challenge and the equipment still in the chest. KeyUnlocked (absent in older files) lists
/// the doors opened to use with the Temple Key after Cameron's death (aitemdor L7 = 1).
/// </summary>
public sealed record SavedTempleDuel([property: JsonRequired] TempleDuelOutcome Outcome, [property: JsonRequired] CustodyItem[] Custody,
    string[]? KeyUnlocked = null);
public enum TempleChestStatus { Ready, Empty, InCombat, Busy, TooFar, Unavailable }

/// <summary>
/// Cameron's unarmed challenge (scripts/actemvil.int), the path of every character without a tagged Speech:
/// Node009 moves the whole inventory into the challenge chest (itemMoveAll to temple_chest_ptr, the chest at offset
/// 465604, hex 20132) and closes and locks the gate at 19928; critter_p_proc then attacks while L13=1. On his turn below
/// half his maximum HP, combat_p_proc ends the fight: Cameron yields and departs as after Speech (L13=2, G10=1).
/// If he dies instead, destroy_p_proc also passes the trial (M20=2, G10=1) but writes G369=1: the exit door and the gate
/// stay locked. Cameron carries the Temple Key; taken from his body and used on either door, it unlocks it for good
/// (aitemdor use_obj_on_p_proc, <see cref="TempleExitDoorProfile"/>).
/// The equipment comes back by taking it from the chest, as CE looting does.
/// </summary>
public sealed partial class WorldSession
{
    public const int TempleChestOffset = 465604, TempleChestPid = 245;
    private TempleDuelOutcome _duelOutcome;
    private TempleNpcDepartureProfile? _duelProfile;
    private CombatSession? _duelFight;
    private CustodyItem[] _duelDeposit = Array.Empty<CustodyItem>();
    private readonly SortedSet<string> _keyUnlocked = new(StringComparer.Ordinal);
    public TempleDuelOutcome TempleDuel => _duelOutcome;
    public bool TempleKeyCarried => Carried.Any(c => c.Source.Header.Pid == TempleExitDoorProfile.TempleKeyPid);
    public bool KeyUnlocked(string doorKey) => _keyUnlocked.Contains(doorKey);

    /// <summary>The Temple Key on the gate or the exit door, from a neighbouring hex (CE use item on object).</summary>
    public TempleWorldStatus CanUseTempleKey(string doorKey, bool moving)
    {
        if (InCombat || moving) return TempleWorldStatus.Busy;
        // Only Cameron's body yields the key, and he dies only on this path.
        if (_duelOutcome != TempleDuelOutcome.Killed || _duelProfile == null || CurrentMap != 3) return TempleWorldStatus.Unavailable;
        if (doorKey != _duelProfile.GateKey && doorKey != _duelProfile.ExitDoor?.Key) return TempleWorldStatus.Unsupported;
        if (_keyUnlocked.Contains(doorKey)) return TempleWorldStatus.AlreadyAccepted;
        if (!TempleKeyCarried) return TempleWorldStatus.DoorRequired;
        MapObject door = Current.Map.ObjectLayers.SelectMany(l => l.Objects).Single(o => o.Key == doorKey);
        if (Location.Elevation != door.Header.Elevation || FalloutGrid.Distance(Location.Hex, door.Header.Tile) != 1) return TempleWorldStatus.TooFar;
        return TempleWorldStatus.Ready;
    }

    public TempleWorldStatus UseTempleKey(string doorKey, bool moving)
    {
        TempleWorldStatus status = CanUseTempleKey(doorKey, moving);
        if (status != TempleWorldStatus.Ready) return status;
        // The gate leaves the locked projection as the closed, unlocked door the player can now open.
        if (doorKey == _duelProfile!.GateKey)
        {
            DoorState locked = _duelProfile.LockedGate();
            Current.Doors.SetScriptedState(doorKey, locked with { OpenFlags = locked.OpenFlags & ~DoorSession.Locked });
        }
        _keyUnlocked.Add(doorKey); TempleRevision++;
        return status;
    }
    public CustodyItem[] TempleChestCustody => Areas.SelectMany(a => a.Items.CaptureCustody()
        .Select(c => new CustodyItem(a.Map.Prefix.Header.MapIndex, c.OwnerKey, c.ItemKey, c.Quantity))).ToArray();

    public TempleWorldStatus PrepareTempleDuel(TempleNpcDepartureProfile? profile, bool moving)
    {
        if (_templeProgress != null || _duelOutcome != TempleDuelOutcome.None) return TempleWorldStatus.AlreadyAccepted;
        if (InCombat) return TempleWorldStatus.Busy;
        if (moving) return TempleWorldStatus.Busy;
        if (profile == null) return TempleWorldStatus.Unsupported;
        if (CurrentMap != 3 || Location.Elevation != 2 || !profile.Matches(Current.Map) || TempleChest() == null) return TempleWorldStatus.Unavailable;
        var initial = profile.Actor(new(1, profile.Fingerprint, NpcDeparturePhase.ToGate, 0, 0));
        if (IsDefeated(3, initial.Key)) return TempleWorldStatus.Unavailable;
        if (FalloutGrid.Distance(Location.Hex, initial.Header.Tile) != 1) return TempleWorldStatus.TooFar;
        if (!Current.Travel.CanArrive(Location) || Current.Doors.Navigation(2).IsExit(Location.Hex)) return TempleWorldStatus.Unavailable;
        return TempleWorldStatus.Ready;
    }

    /// <summary>Node009 then the first attack: equipment into the chest, gate locked, Cameron acts first.</summary>
    public CombatSession BeginTempleDuel(TempleNpcDepartureProfile profile, ICombatRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (PrepareTempleDuel(profile, false) != TempleWorldStatus.Ready) throw new InvalidOperationException("The challenge cannot start here.");
        MapObject source = Current.Map.ObjectLayers.SelectMany(l => l.Objects).Single(o => o.Key == profile.ActorKey);
        Prototype prototype = Current.Map.Prototypes.FirstOrDefault(p => p.Pid == source.Header.Pid)
            ?? throw new InvalidDataException("Cameron's prototype is not part of the map dependencies.");
        var deposit = new List<CustodyItem>();
        foreach (WorldArea area in Areas)
            deposit.AddRange(area.Items.DepositAll().Select(c => new CustodyItem(area.Map.Prefix.Header.MapIndex, c.OwnerKey, c.ItemKey, c.Quantity)));
        _duelDeposit = deposit.ToArray(); _duelProfile = profile; _duelOutcome = TempleDuelOutcome.InProgress;
        // Bare hands and no healing dose: the inventory now lies in the chest.
        var player = new CombatantState("player", Character.Name, null, 0, true, Location.Hex, 2, DerivedCombatStats.ForPlayer(Sheet, Level),
            CombatWeapon.Unarmed(), HitPoints, healingDoses: HealingDoses) { PoisonResistance = Sheet.PoisonResistance, Poison = _poison };
        CombatantState cameron = CombatantState.FromMapCritter(source, CritterPrototypeData.Read(prototype), "Cameron");
        cameron = new CombatantState(cameron.Id, cameron.Name, cameron.Pid, cameron.Team, false, cameron.Hex, cameron.Elevation, cameron.Stats,
            cameron.Weapon, cameron.HitPoints, cameron.Experience) { YieldsBelowHalf = true };
        var session = new CombatSession(new[] { player, cameron }, Current.Doors.Navigation(2), random, cameron.Id);
        _activeFight = session; _activeFightMap = 3; _duelFight = session;
        return session;
    }

    // Abandoning the open challenge (load, reset) puts the equipment back where it was and forgets the fight.
    private void AbandonTempleDuel()
    {
        if (_duelOutcome != TempleDuelOutcome.InProgress) return;
        foreach (WorldArea area in Areas) area.Items.ReleaseCustody();
        _duelOutcome = TempleDuelOutcome.None; _duelProfile = null; _duelFight = null; _duelDeposit = Array.Empty<CustodyItem>();
    }

    // Called by CommitCombat for the challenge fight once it is over and the player stands.
    private CombatCommitStatus CommitTempleDuel(CombatSession fight, CombatantState player)
    {
        CombatantState cameron = fight.Combatants.Single(c => !c.IsPlayer);
        TempleNpcDepartureProfile profile = _duelProfile!;
        if (cameron.Yielded)
        {
            _playerDamage = player.Stats.MaxHitPoints - player.HitPoints;
            // Node009 closed and locked the gate; Cameron unlocks and opens it on his way out.
            _templeProgress = new TempleWorldProgress(profile, profile.BeginAfterDuel().Capture(), false, TempleTrialPath.Duel);
            _duelOutcome = TempleDuelOutcome.Yielded; TempleRevision++;
        }
        else
        {
            if (cameron.Alive) return CombatCommitStatus.InvalidFight;
            _defeated.Add((3, cameron.Id), new DefeatedCritter(3, cameron.Id, cameron.Pid!.Value, 2, cameron.Hex));
            _combatExperience += fight.Turn.ExperienceAwarded;
            _playerDamage = player.Stats.MaxHitPoints - player.HitPoints;
            _duelOutcome = TempleDuelOutcome.Killed;
        }
        _duelFight = null; _duelDeposit = Array.Empty<CustodyItem>();
        return CombatCommitStatus.Ready;
    }

    private MapObject? TempleChest() => _areas.TryGetValue(3, out WorldArea? area) && area.Map.Source.Sha256 == TempleBarrierEffectProposal.MapHash
        ? area.Items.Containers.SingleOrDefault(o => o.Offset == TempleChestOffset && o.Header.Pid == TempleChestPid && o.Header.Elevation == 2) : null;

    public TempleChestStatus CanRetrieveTempleChest(bool moving)
    {
        if (TempleChestCustody.Length == 0) return TempleChestStatus.Empty;
        if (InCombat) return TempleChestStatus.InCombat;
        if (moving) return TempleChestStatus.Busy;
        MapObject? chest = TempleChest();
        if (chest == null || CurrentMap != 3 || Location.Elevation != 2) return TempleChestStatus.Unavailable;
        return Location.Hex == chest.Header.Tile || Enumerable.Range(0, 6).Any(d => FalloutGrid.Neighbor(Location.Hex, d) == chest.Header.Tile)
            ? TempleChestStatus.Ready : TempleChestStatus.TooFar;
    }
    public bool IsTempleChest(string key) => TempleChest()?.Key == key;

    /// <summary>Takes everything back from the challenge chest (CE inventory looting, take all).</summary>
    public TempleChestStatus RetrieveTempleChest(bool moving)
    {
        TempleChestStatus status = CanRetrieveTempleChest(moving);
        if (status != TempleChestStatus.Ready) return status;
        foreach (WorldArea area in Areas) area.Items.ReleaseCustody();
        return TempleChestStatus.Ready;
    }

    private void ResetTempleDuel() { _duelOutcome = TempleDuelOutcome.None; _duelProfile = null; _duelFight = null; _duelDeposit = Array.Empty<CustodyItem>(); _keyUnlocked.Clear(); }

    private SavedTempleDuel CaptureTempleDuel() => new(_duelOutcome == TempleDuelOutcome.InProgress ? TempleDuelOutcome.None : _duelOutcome, TempleChestCustody,
        _keyUnlocked.Count == 0 ? null : _keyUnlocked.ToArray());

    // After areas, Temple progress and combat: the outcome must agree with them, and custody needs a finished challenge.
    private void RestoreTempleDuel(SavedTempleDuel? saved)
    {
        ResetTempleDuel();
        if (saved == null) return;
        if (saved.Custody == null || !Enum.IsDefined(saved.Outcome) || saved.Outcome == TempleDuelOutcome.InProgress)
            throw new InvalidDataException("Checkpoint challenge state is malformed.");
        TempleNpcDepartureProfile? profile = null;
        if (saved.Outcome != TempleDuelOutcome.None)
        {
            if (!_areas.TryGetValue(3, out WorldArea? temple) || _templeProfile == null || TempleChest() == null)
                throw new InvalidDataException("Checkpoint challenge requires the Temple area and its profile.");
            profile = _templeProfile(temple.Map);
            bool cameronDead = IsDefeated(3, profile.ActorKey);
            bool consistent = saved.Outcome switch
            {
                TempleDuelOutcome.Yielded => _templeProgress?.Path == TempleTrialPath.Duel && !cameronDead,
                // The gate stays as Node009 left it until the Temple Key unlocks it.
                TempleDuelOutcome.Killed => _templeProgress == null && cameronDead && temple.Doors.Get(profile.GateKey) is { } gate
                    && (saved.KeyUnlocked?.Contains(profile.GateKey) == true ? !gate.IsLocked
                        : profile.LockedGate() is var locked && gate.Flags == locked.Flags && gate.OpenFlags == locked.OpenFlags),
                _ => false
            };
            if (!consistent) throw new InvalidDataException("Checkpoint challenge outcome disagrees with Cameron and the Temple progress.");
        }
        else if (_templeProgress?.Path == TempleTrialPath.Duel || saved.Custody.Length != 0)
            throw new InvalidDataException("Checkpoint holds duel results without a finished challenge.");
        string[] unlocked = saved.KeyUnlocked ?? Array.Empty<string>();
        if (unlocked.Length != 0 && (saved.Outcome != TempleDuelOutcome.Killed || unlocked.Distinct().Count() != unlocked.Length
            || unlocked.Any(k => k != profile!.GateKey && k != profile.ExitDoor?.Key)))
            throw new InvalidDataException("Checkpoint uses the Temple Key without Cameron's death or on another door.");
        foreach (var group in saved.Custody.GroupBy(c => c.Map))
        {
            if (!_areas.TryGetValue(group.Key, out WorldArea? area)) throw new InvalidDataException("Custody item outside the restored areas.");
            area.Items.RestoreCustody(group.Select(c => new ConsumedItem(c.OwnerKey, c.ItemKey, c.Quantity)).ToArray());
        }
        _duelOutcome = saved.Outcome; _duelProfile = profile;
        foreach (string key in unlocked) _keyUnlocked.Add(key);
    }
}
