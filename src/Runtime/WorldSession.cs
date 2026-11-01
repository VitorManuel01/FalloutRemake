using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FalloutRemake.Geometry;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public sealed record WorldArea(MapDocument Map, DoorSession Doors, TempleTravel Travel, ItemTransferSession Items, ScenerySession Scenery);
public sealed record WorldItemUse(int SourceMap, ItemUseAllocation Allocation);
public enum ExplosiveCommitStatus { Ready, InvalidPlan, StalePlan, Busy, NoItem, Unsupported, Unavailable, AtExit, Duplicate, LimitReached }
public sealed record ExplosiveCommitResult(ExplosiveCommitStatus Status, WorldItemUse? Use = null);
public sealed record UseTimeAdvance(int AppliedTicks, int UnusedTicks, WorldItemUse[] Due);

/// <summary>Cached area sessions with transactional destination validation; no general map scripts.</summary>
public sealed partial class WorldSession
{
    private readonly Func<int, MapDocument> _load;
    private readonly HashSet<int> _allowed;
    private readonly Dictionary<int, WorldArea> _areas = new();
    public Guid SessionToken { get; } = Guid.NewGuid();
    public int InitialMap { get; }
    public int CurrentMap { get; private set; }
    public WorldArea Current => _areas[CurrentMap];
    public ActorLocation Location => Current.Travel.Location;
    public int Experience => _areas.Values.Sum(a => a.Doors.TempleDoor?.Experience ?? 0) + (_arroyoReward?.Experience ?? 0) + _combatExperience;
    public IReadOnlyList<WorldArea> Areas => _areas.OrderBy(p => p.Key).Select(p => p.Value).ToArray();
    public CarriedItem[] Carried => Areas.SelectMany(a => a.Items.Carried).ToArray();
    public WorldItemUse[] ItemUses => Areas.SelectMany(a => a.Items.CaptureUses().Select(u => new WorldItemUse(a.Map.Prefix.Header.MapIndex, u))).ToArray();
    public WorldItemUse[] DueItemUses => ItemUses.Where(u => u.Allocation.Phase == ItemUsePhase.Placed && u.Allocation.RemainingTicks == 0).ToArray();
    public long CarriedCount => Carried.Sum(i => (long)i.Quantity);
    public int CarryCapacity => Sheet.CarryWeight;
    /// <summary>Who plays: a premade, a created character or the neutral test profile of earlier milestones.</summary>
    public PlayerCharacter Character { get; private set; } = PlayerCharacter.Neutral;
    private readonly Func<string, CharacterFile>? _premade;
    // data/ai.txt packets; without them creatures keep the provisional melee behavior.
    private readonly IReadOnlyDictionary<int, AiPacket>? _aiPackets;
    /// <summary>New game with this character: the whole world returns to its start. Reset keeps the character.</summary>
    public void StartNewGame(PlayerCharacter character)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (character.Kind == CharacterKind.Draft) throw new ArgumentException("An unfinished character cannot start a game.", nameof(character));
        Reset(); Character = character;
    }
    public long? UnitWeight(int sourceMap, MapObject item) => _areas.TryGetValue(sourceMap, out WorldArea? area)
        && area.Map.Prototypes.FirstOrDefault(p => p.Pid == item.Header.Pid) is Prototype prototype ? CarryWeight.UnitWeight(prototype, item) : null;
    public long? CarriedWeight => CarryWeight.Total(Carried.Select(item => (UnitWeight(item.Map, item.Source), item.Quantity)));
    public ItemTakeStatus CanTakeItem(string ownerKey, string itemKey, int quantity, bool moving)
    {
        ItemTakeStatus status = Current.Items.CanTake(ownerKey, itemKey, quantity, Location, Current.Doors.Navigation(Location.Elevation), moving);
        if (status != ItemTakeStatus.Ready) return status;
        MapObject source = Current.Items.Contents(ownerKey).Single(i => i.Source.Key == itemKey).Source;
        long? unit = UnitWeight(CurrentMap, source), carried = CarriedWeight;
        if (!unit.HasValue || !carried.HasValue) return ItemTakeStatus.WeightUnavailable;
        // Source quantities/base weights are signed int32; the product fits int64.
        return carried.Value > CarryCapacity || unit.Value * quantity > CarryCapacity - carried.Value ? ItemTakeStatus.OverCapacity : ItemTakeStatus.Ready;
    }
    public ItemTakeStatus TakeItem(string ownerKey, string itemKey, int quantity, bool moving)
    {
        ItemTakeStatus status = CanTakeItem(ownerKey, itemKey, quantity, moving);
        return status == ItemTakeStatus.Ready ? Current.Items.Take(ownerKey, itemKey, quantity, Location, Current.Doors.Navigation(Location.Elevation), moving) : status;
    }
    public ItemTakeStatus CanTakeAllItems(string ownerKey, bool moving)
    {
        ItemTakeStatus status = Current.Items.CanTakeAll(ownerKey, Location, Current.Doors.Navigation(Location.Elevation), moving);
        if (status != ItemTakeStatus.Ready) return status;
        long? carried = CarriedWeight;
        long? additional = CarryWeight.Total(Current.Items.Contents(ownerKey).Where(i => i.Quantity > 0)
            .Select(i => (UnitWeight(CurrentMap, i.Source), i.Quantity)));
        if (!carried.HasValue || !additional.HasValue) return ItemTakeStatus.WeightUnavailable;
        return carried.Value > CarryCapacity || additional.Value > CarryCapacity - carried.Value ? ItemTakeStatus.OverCapacity : ItemTakeStatus.Ready;
    }
    public ItemTakeStatus TakeAllItems(string ownerKey, bool moving)
    {
        ItemTakeStatus status = CanTakeAllItems(ownerKey, moving);
        return status == ItemTakeStatus.Ready ? Current.Items.TakeAll(ownerKey, Location, Current.Doors.Navigation(Location.Elevation), moving) : status;
    }
    public ItemTakeStatus CanReturnItem(int sourceMap, string ownerKey, string itemKey, int quantity, bool moving) => sourceMap == CurrentMap
        ? Current.Items.CanReturn(ownerKey, itemKey, quantity, Location, Current.Doors.Navigation(Location.Elevation), moving) : ItemTakeStatus.Unavailable;
    public ItemTakeStatus CanReturnAllItems(int sourceMap, string ownerKey, bool moving) => sourceMap == CurrentMap
        ? Current.Items.CanReturnAll(ownerKey, Location, Current.Doors.Navigation(Location.Elevation), moving) : ItemTakeStatus.Unavailable;
    public ItemTakeStatus ReturnAllItems(int sourceMap, string ownerKey, bool moving) => sourceMap == CurrentMap
        ? Current.Items.ReturnAll(ownerKey, Location, Current.Doors.Navigation(Location.Elevation), moving) : ItemTakeStatus.Unavailable;
    public ItemTakeStatus ReturnItem(int sourceMap, string ownerKey, string itemKey, int quantity, bool moving) => sourceMap == CurrentMap
        ? Current.Items.Return(ownerKey, itemKey, quantity, Location, Current.Doors.Navigation(Location.Elevation), moving) : ItemTakeStatus.Unavailable;
    public ExplosiveCommitResult CommitExplosiveUse(ExplosiveUsePlan? plan, bool moving)
    {
        if (plan == null || plan.Source?.Source == null || plan.Id == Guid.Empty || plan.Quantity != 1 || plan.ActivePid != ExplosiveUseProposal.ActivePid
            || plan.MinimumDamage != ExplosiveUseProposal.MinimumDamage || plan.MaximumDamage != ExplosiveUseProposal.MaximumDamage
            || plan.SceneryRadius != ExplosiveUseProposal.SceneryRadius || !ItemUseProfile.ValidSchedule(plan.Schedule))
            return new(ExplosiveCommitStatus.InvalidPlan);
        if (plan.SessionToken != SessionToken || plan.Placement != Location) return new(ExplosiveCommitStatus.StalePlan);
        if (moving) return new(ExplosiveCommitStatus.Busy);
        if (!_areas.TryGetValue(plan.Source.Map, out WorldArea? source)) return new(ExplosiveCommitStatus.NoItem);
        if (source.Items.Revision != plan.SourceRevision) return new(ExplosiveCommitStatus.StalePlan);
        if (ItemUses.Any(u => u.Allocation.Id == plan.Id)) return new(ExplosiveCommitStatus.Duplicate);
        CarriedItem? item = source.Items.Carried.SingleOrDefault(i => i.OwnerKey == plan.Source.OwnerKey && i.Source.Key == plan.Source.Source.Key);
        if (item == null) return new(ExplosiveCommitStatus.NoItem);
        if (item.Quantity != plan.Source.Quantity) return new(ExplosiveCommitStatus.StalePlan);
        if (!ItemUseProfile.Supported(source.Map.Prototypes.SingleOrDefault(p => p.Pid == item.Source.Header.Pid))) return new(ExplosiveCommitStatus.Unsupported);
        if (!Current.Travel.CanArrive(Location)) return new(ExplosiveCommitStatus.Unavailable);
        if (Current.Doors.Navigation(Location.Elevation).IsExit(Location.Hex)) return new(ExplosiveCommitStatus.AtExit);
        var allocation = new ItemUseAllocation(plan.Id, item.OwnerKey, item.Source.Key, 1, ItemUsePhase.Placed, Location, plan.Schedule, plan.Schedule.DelayTicks);
        ItemUseStatus result = source.Items.Place(allocation);
        return result switch
        {
            ItemUseStatus.Ready => new(ExplosiveCommitStatus.Ready, new(item.Map, allocation)),
            ItemUseStatus.Empty or ItemUseStatus.NoItem => new(ExplosiveCommitStatus.NoItem),
            ItemUseStatus.Unsupported => new(ExplosiveCommitStatus.Unsupported),
            ItemUseStatus.Duplicate => new(ExplosiveCommitStatus.Duplicate),
            ItemUseStatus.LimitReached => new(ExplosiveCommitStatus.LimitReached),
            _ => new(ExplosiveCommitStatus.InvalidPlan)
        };
    }
    // Explicit logical ticks only. Frame time, map departure and damage are not wired to this clock.
    public UseTimeAdvance AdvanceUseTime(int ticks)
    {
        if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
        int untilNext = ItemUses.Where(u => u.Allocation.Phase == ItemUsePhase.Placed).Select(u => u.Allocation.RemainingTicks).DefaultIfEmpty(int.MaxValue).Min();
        int applied = Math.Min(ticks, untilNext);
        foreach (WorldArea area in Areas) area.Items.AdvanceUseTime(applied);
        return new(applied, ticks - applied, DueItemUses);
    }
    internal ItemUseStatus CompleteDueUse(Guid id)
    {
        WorldItemUse? use = ItemUses.SingleOrDefault(u => u.Allocation.Id == id);
        if (use == null) return ItemUseStatus.NoItem;
        if (use.Allocation.Phase != ItemUsePhase.Placed) return ItemUseStatus.Empty;
        if (use.Allocation.RemainingTicks != 0) return ItemUseStatus.InvalidState;
        return _areas[use.SourceMap].Items.Consume(id);
    }
    public WorldSession(Func<int, MapDocument> load, IEnumerable<int> allowedMaps, int initialMap = 3, Func<int, Prototype>? runtimePrototype = null,
        Func<MapDocument, TempleNpcDepartureProfile>? templeProfile = null, Func<MapDocument,ArroyoRewardProfile>? arroyoProfile = null,
        Func<string, CharacterFile>? premade = null, IReadOnlyDictionary<int, AiPacket>? aiPackets = null)
    {
        _load = load; _premade = premade; _aiPackets = aiPackets; _runtimePrototype = runtimePrototype; _templeProfile = templeProfile; _arroyoProfile = arroyoProfile; _allowed = allowedMaps.ToHashSet(); InitialMap = initialMap;
        if (!_allowed.Contains(initialMap)) throw new ArgumentException("Initial map must belong to the supported area profile.");
        EnsureArea(initialMap); CurrentMap = initialMap;
        _enteredMaps.Add(initialMap);
    }
    private WorldArea EnsureArea(int id)
    {
        if (!_allowed.Contains(id)) throw new InvalidDataException("Destination lies outside the supported area profile.");
        if (_areas.TryGetValue(id, out WorldArea? area)) return area;
        MapDocument map = _load(id);
        if (map.Prefix.Header.MapIndex != id) throw new InvalidDataException("Loaded MAP ID does not match the requested destination.");
        DoorSession doors = DoorSession.FromMap(map, DoorSession.SupportsTempleDoorAdapter(map));
        var scenery = new ScenerySession(); doors.IsSceneryReplaced = scenery.Replaces;
        doors.ProjectObject = source => EffectiveObject(id, source); doors.ProjectDoor = key => TempleGate(id, key);
        doors.AllowTempleExitUse = key => id == 3 && CanUseTempleExit(key);
        var items = new ItemTransferSession(map) { CorpseHex = key => _defeated.TryGetValue((id, key), out DefeatedCritter? dead) ? dead.Hex : null };
        items.Changed += ReleaseHands;
        area = new WorldArea(map, doors, new TempleTravel(map, doors), items, scenery);
        _areas.Add(id, area); return area;
    }
    public ExitOption InspectExit(bool moving)
    {
        ExitOption option = Current.Travel.InspectExit(moving);
        if (option.Status != ExitStatus.ExternalMap) return option;
        try
        {
            ActorLocation destination = option.Destination!;
            WorldArea area = EnsureArea(destination.Map);
            return area.Travel.CanArrive(destination)
                ? option with { Status = ExitStatus.Ready }
                : option with { Status = ExitStatus.BlockedDestination };
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or KeyNotFoundException or ArgumentException)
        { return option with { Status = ExitStatus.UnavailableMap }; }
    }
    // Hover inspection never loads another area, enters scripts or changes any actor position.
    public ExitOption PreviewExitAt(int elevation, int hex)
    {
        ExitOption option = Current.Travel.InspectExitAt(elevation, hex);
        if (option.Status != ExitStatus.ExternalMap) return option;
        ActorLocation destination = option.Destination!;
        if (!_allowed.Contains(destination.Map)) return option with { Status = ExitStatus.UnavailableMap };
        if (!_areas.TryGetValue(destination.Map, out WorldArea? area)) return option;
        return option with { Status = area.Travel.CanArrive(destination) ? ExitStatus.Ready : ExitStatus.BlockedDestination };
    }
    public ExitOption UseExit(bool moving)
    {
        ExitOption option = InspectExit(moving);
        if (option.Status != ExitStatus.Ready) return option;
        ActorLocation destination = option.Destination!;
        if (destination.Map == CurrentMap) return Current.Travel.UseExit(moving);
        WorldArea target = _areas[destination.Map];
        (SavedArroyoReward Reward,SavedArroyoGlobals Globals)? reward;
        try { reward = PrepareEntryReward(destination,target); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or KeyNotFoundException)
        { return option with { Status = ExitStatus.UnavailableMap }; }
        target.Travel.Arrive(destination); CurrentMap = destination.Map;
        _enteredMaps.Add(destination.Map);
        if (reward is { } entry) { _arroyoReward = entry.Reward; _arroyoGlobals = entry.Globals; }
        return option;
    }
    public void Reset()
    {
        _templeProgress = null; TempleToken = Guid.NewGuid(); TempleRevision = 0;
        _enteredMaps.Clear(); _enteredMaps.Add(InitialMap); _arroyoReward = null; _arroyoGlobals = null; ResetCombat(); ResetTempleDuel(); ResetSkills();
        foreach (WorldArea area in _areas.Values) { area.Scenery.Reset(); area.Doors.Reset(); area.Travel.Reset(); area.Items.Reset(); }
        CurrentMap = InitialMap;
    }
    private static string DependenciesHash(MapDocument map) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        string.Join("\n", map.Dependencies.OrderBy(d => d.Path, StringComparer.Ordinal).Select(d => $"{d.Path}|{d.Size}|{d.Sha256}")))));
    public WorldCheckpoint Capture(string catalogHash)
    {
        ValidateScenery(Areas.Select(a => (a, a.Scenery.Capture()))); ValidateUses(); ValidateTempleExit();
        return new(13, "neutral-v13", catalogHash, InitialMap, CurrentMap,
            Areas.Select(a => new SavedArea(a.Map.Prefix.Header.MapIndex, a.Map.Source.Sha256,
                DependenciesHash(a.Map), a.Travel.Location, a.Doors.Capture(), a.Items.Capture(), a.Items.CaptureUses(), a.Scenery.Capture(), a.Items.CaptureConsumed())).ToArray(), _templeProgress?.Capture(),CaptureEntries(),
            CaptureCombat(), Character.Capture(), CaptureTempleDuel(), CaptureSkills());
    }
    public static WorldSession Restore(Func<int, MapDocument> load, IEnumerable<int> allowedMaps, WorldCheckpoint checkpoint, string catalogHash, int initialMap = 3, Func<int, Prototype>? runtimePrototype = null,
        Func<MapDocument, TempleNpcDepartureProfile>? templeProfile = null, Func<MapDocument,ArroyoRewardProfile>? arroyoProfile = null,
        Func<string, CharacterFile>? premade = null, IReadOnlyDictionary<int, AiPacket>? aiPackets = null)
    {
        if (checkpoint == null || !(checkpoint.Version == 1 && checkpoint.Profile == "neutral-v1" || checkpoint.Version == 2 && checkpoint.Profile == "neutral-v2"
            || checkpoint.Version == 3 && checkpoint.Profile == "neutral-v3" || checkpoint.Version == 4 && checkpoint.Profile == "neutral-v4"
            || checkpoint.Version == 5 && checkpoint.Profile == "neutral-v5" || checkpoint.Version == 6 && checkpoint.Profile == "neutral-v6"
            || checkpoint.Version == 7 && checkpoint.Profile == "neutral-v7" || checkpoint.Version == 8 && checkpoint.Profile == "neutral-v8"
            || checkpoint.Version == 9 && checkpoint.Profile == "neutral-v9" || checkpoint.Version == 10 && checkpoint.Profile == "neutral-v10" || checkpoint.Version == 11 && checkpoint.Profile == "neutral-v11"
            || checkpoint.Version == 12 && checkpoint.Profile == "neutral-v12" || checkpoint.Version == 13 && checkpoint.Profile == "neutral-v13")
            || checkpoint.Version < 13 && checkpoint.Skills != null || checkpoint.Version >= 13 && checkpoint.Skills == null
            || checkpoint.Version < 12 && checkpoint.Character != null || checkpoint.Version >= 12 && checkpoint.Character == null
            || checkpoint.Version < 12 && checkpoint.Duel != null || checkpoint.Version >= 12 && checkpoint.Duel == null
            || checkpoint.CatalogHash != catalogHash
            || checkpoint.Version < 10 && checkpoint.Combat != null || checkpoint.Version >= 10 && checkpoint.Combat == null
            || checkpoint.Version < 6 && checkpoint.Temple != null
            || checkpoint.Version < 8 && checkpoint.Entries != null
            || checkpoint.Version < 9 && checkpoint.Entries?.ArroyoGlobals != null
            || checkpoint.InitialMap != initialMap || checkpoint.Areas is not { Length: > 0 and <= 16 })
            throw new InvalidDataException("Checkpoint version, profile or catalog is incompatible.");
        var allowed = allowedMaps.ToHashSet();
        if (checkpoint.Areas.Length > allowed.Count || checkpoint.Areas.Any(a => a == null || !allowed.Contains(a.Map))
            || checkpoint.Areas.Select(a => a.Map).Distinct().Count() != checkpoint.Areas.Length
            || !checkpoint.Areas.Any(a => a.Map == initialMap) || !checkpoint.Areas.Any(a => a.Map == checkpoint.CurrentMap))
            throw new InvalidDataException("Checkpoint area set is invalid.");
        var candidate = new WorldSession(load, allowed, initialMap, runtimePrototype, templeProfile,arroyoProfile, premade, aiPackets);
        // The character decides the level's HP bound checked by the combat block, so it comes first.
        try { candidate.Character = checkpoint.Character == null ? PlayerCharacter.Neutral : PlayerCharacter.Restore(checkpoint.Character, premade); }
        catch (Exception error) when (error is IOException or ArgumentException or KeyNotFoundException)
        { throw new InvalidDataException("Checkpoint premade character source is unavailable.", error); }
        var bodyLoot = new List<(WorldArea Area, TakenItem[] Rows)>();
        foreach (SavedArea saved in checkpoint.Areas)
        {
            WorldArea area = candidate.EnsureArea(saved.Map);
            if (saved.SourceHash != area.Map.Source.Sha256 || saved.DependenciesHash != DependenciesHash(area.Map)
                || saved.Location == null || saved.Location.Map != saved.Map || saved.Session == null)
                throw new InvalidDataException("Checkpoint source or dependency fingerprints changed.");
            if (checkpoint.Version == 1 && saved.Taken != null || checkpoint.Version >= 2 && saved.Taken == null
                || checkpoint.Version < 3 && saved.Uses != null || checkpoint.Version >= 3 && saved.Uses == null
                || checkpoint.Version < 4 && saved.Replacements != null || checkpoint.Version >= 4 && saved.Replacements == null
                || checkpoint.Version < 11 && saved.Consumed != null || checkpoint.Version >= 11 && saved.Consumed == null)
                throw new InvalidDataException("Checkpoint item state does not match its schema version.");
            // Items taken from bodies wait for the corpses, which the combat block restores below.
            TakenItem[] taken = saved.Taken ?? Array.Empty<TakenItem>();
            bodyLoot.Add((area, taken.Where(t => t?.OwnerKey != null && area.Items.IsBody(t.OwnerKey)).ToArray()));
            area.Items.Restore(taken.Where(t => t?.OwnerKey == null || !area.Items.IsBody(t.OwnerKey)).ToArray(), saved.Uses ?? Array.Empty<ItemUseAllocation>(),
                saved.Consumed ?? Array.Empty<ConsumedItem>(), HealingDrugProfile.Supported);
            bool killed = checkpoint.Duel?.Outcome == TempleDuelOutcome.Killed;
            area.Doors.Restore(saved.Session, checkpoint.Version >= 5, checkpoint.Version >= 7 && (checkpoint.Temple != null || killed), killed);
        }
        var scenes = checkpoint.Areas.Select(a => (Area: candidate._areas[a.Map], Records: a.Replacements ?? Array.Empty<SceneryReplacement>())).ToArray();
        candidate.ValidateScenery(scenes);
        foreach (var scene in scenes) scene.Area.Scenery.ApplyPrepared(ScenerySession.Prepare(scene.Records));
        candidate.RestoreTempleProgress(checkpoint.Temple);
        candidate.RestoreEntries(checkpoint);
        candidate.RestoreCombat(checkpoint.Combat, checkpoint.Duel?.Outcome == TempleDuelOutcome.Killed);
        foreach (var (area, rows) in bodyLoot) area.Items.RestoreBodyLoot(rows);
        candidate.RestoreTempleDuel(checkpoint.Duel);
        candidate.RestoreHands(checkpoint.Combat?.Hands);
        candidate.RestoreSkills(checkpoint.Skills);
        candidate.ValidateTempleExit();
        candidate.ValidateUses();
        foreach (SavedArea saved in checkpoint.Areas) candidate._areas[saved.Map].Travel.Arrive(saved.Location);
        candidate.CurrentMap = checkpoint.CurrentMap;
        return candidate;
    }
    private void ValidateUses()
    {
        var ids = new HashSet<Guid>();
        var navigations = new Dictionary<(int Map, int Elevation), HexNavigation>();
        foreach (WorldItemUse use in ItemUses)
        {
            ActorLocation position = use.Allocation.Placement;
            if (!ids.Add(use.Allocation.Id) || !_areas.TryGetValue(position.Map, out WorldArea? placementArea)
                || !placementArea.Map.Prefix.Layers.Any(l => l.Elevation == position.Elevation))
                throw new InvalidDataException("Item-use identity or placement area is invalid.");
            var level = (position.Map, position.Elevation);
            if (!navigations.TryGetValue(level, out HexNavigation? navigation))
                navigations[level] = navigation = placementArea.Doors.Navigation(position.Elevation);
            if (navigation.IsExit(position.Hex) || navigation.BlockersAt(position.Hex).Any(b => !b.IsDoor
                || placementArea.Doors.Get(b.Key) is not DoorState door || door.Scripted && placementArea.Doors.TempleDoor?.SourceKey != b.Key
                    && placementArea.Doors.InitialTempleGateKey != b.Key))
                throw new InvalidDataException("Item-use placement is not valid on the restored geometry.");
        }
    }
}
