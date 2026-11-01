using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FalloutRemake.Geometry;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public enum ItemTakeStatus { Ready, NoItem, Unsupported, ScriptRequired, Locked, Busy, TooFar, Unavailable, AtExit, InvalidQuantity, Empty, OverCapacity, WeightUnavailable }
public sealed record TakenItem(string OwnerKey, string ItemKey, int Quantity);
public sealed record ContainerItem(MapObject Source, int Quantity);
public sealed record CarriedItem(int Map, string OwnerKey, MapObject Source, int Quantity);
/// <summary>Units of a collected source item used up by the actor (a drug taken); they leave the carried quantity for good.</summary>
public sealed record ConsumedItem([property: System.Text.Json.Serialization.JsonRequired] string OwnerKey, [property: System.Text.Json.Serialization.JsonRequired] string ItemKey,
    [property: System.Text.Json.Serialization.JsonRequired] int Quantity);

/// <summary>Conservative direct-container transfers. Original MAP records remain immutable; no stacking across source keys.</summary>
public sealed class ItemTransferSession
{
    public int MapId { get; }
    private readonly Dictionary<string, MapObject> _owners;
    // Critters that carry items: their inventory can be looted once they lie dead (Cameron carries the Temple Key).
    private readonly HashSet<string> _bodies;
    /// <summary>Hex of a critter's corpse, or null while it lives; set by the world, which owns the fight outcomes.</summary>
    internal Func<string, int?>? CorpseHex { get; set; }
    private readonly Dictionary<int, Prototype> _prototypes;
    private Dictionary<string, TakenItem> _taken = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, ItemUseAllocation> _uses = new();
    private readonly Dictionary<string, ConsumedItem> _consumed = new(StringComparer.Ordinal);
    // Units held by a script container (the Temple challenge chest) until the actor takes them back.
    private readonly Dictionary<string, ConsumedItem> _custody = new(StringComparer.Ordinal);
    public const int MaxUses = 1024;
    // Every change of the area's items moves the revision; the world listens to let go of items that left the hands.
    public long Revision { get => _revision; private set { _revision = value; Changed?.Invoke(); } }
    private long _revision;
    public event Action? Changed;
    public ItemTransferSession(MapDocument map)
    {
        MapId = map.Prefix.Header.MapIndex;
        _owners = map.ObjectLayers.SelectMany(l => l.Objects).Where(o => o.Header.Pid >> 24 == 0 && o.Subtype == 1
            || o.Header.Pid >> 24 == 1 && o.Inventory.Length != 0).ToDictionary(o => o.Key);
        _bodies = _owners.Values.Where(o => o.Header.Pid >> 24 == 1).Select(o => o.Key).ToHashSet(StringComparer.Ordinal);
        _prototypes = map.Prototypes.ToDictionary(p => p.Pid);
        foreach (MapObject owner in _owners.Values)
            if (owner.Inventory.Any(e => e.Quantity <= 0) || owner.Inventory.Select(e => e.Object.Key).Distinct().Count() != owner.Inventory.Length)
                throw new InvalidDataException("Invalid direct container inventory.");
    }
    public IReadOnlyList<MapObject> Containers => _owners.Values.Where(o => !_bodies.Contains(o.Key)).OrderBy(o => o.Offset).ToArray();
    public bool IsBody(string key) => _bodies.Contains(key);
    /// <summary>A container, or a critter with items at the hex where it lies dead.</summary>
    public MapObject? Owner(string key) => !_owners.TryGetValue(key, out MapObject? owner) ? null
        : _bodies.Contains(key) && CorpseHex?.Invoke(key) is int hex ? owner with { Header = owner.Header with { Tile = hex } } : owner;
    public ContainerItem[] Contents(string ownerKey) => _owners.TryGetValue(ownerKey, out MapObject? owner)
        ? owner.Inventory.Select(e => new ContainerItem(e.Object, e.Quantity - (_taken.GetValueOrDefault(e.Object.Key)?.Quantity ?? 0))).ToArray()
        : Array.Empty<ContainerItem>();
    public TakenItem[] Capture() => _taken.Values.OrderBy(t => t.ItemKey, StringComparer.Ordinal).ToArray();
    public ItemUseAllocation[] CaptureUses() => _uses.Values.OrderBy(u => u.Id).ToArray();
    public ConsumedItem[] CaptureConsumed() => _consumed.Values.OrderBy(c => c.ItemKey, StringComparer.Ordinal).ToArray();
    public ConsumedItem[] CaptureCustody() => _custody.Values.OrderBy(c => c.ItemKey, StringComparer.Ordinal).ToArray();
    private int UsedQuantity(string itemKey) => _uses.Values.Where(u => u.ItemKey == itemKey).Sum(u => u.Quantity) + (_consumed.GetValueOrDefault(itemKey)?.Quantity ?? 0)
        + (_custody.GetValueOrDefault(itemKey)?.Quantity ?? 0);
    /// <summary>Moves every carried unit into custody (CE itemMoveAll from the actor), returning what moved.</summary>
    internal ConsumedItem[] DepositAll()
    {
        ConsumedItem[] moved = Carried.Select(c => new ConsumedItem(c.OwnerKey, c.Source.Key, c.Quantity)).ToArray();
        foreach (ConsumedItem row in moved)
            _custody[row.ItemKey] = row with { Quantity = row.Quantity + (_custody.GetValueOrDefault(row.ItemKey)?.Quantity ?? 0) };
        if (moved.Length != 0) Revision++;
        return moved;
    }
    /// <summary>Gives back every unit in custody (taking all from the container).</summary>
    internal int ReleaseCustody()
    {
        int units = _custody.Values.Sum(c => c.Quantity);
        if (_custody.Count != 0) { _custody.Clear(); Revision++; }
        return units;
    }
    internal void RestoreCustody(ConsumedItem[] rows)
    {
        var candidate = new Dictionary<string, ConsumedItem>(StringComparer.Ordinal);
        foreach (ConsumedItem row in rows ?? throw new InvalidDataException("Missing custody rows."))
        {
            if (row == null || row.OwnerKey == null || row.ItemKey == null || row.Quantity <= 0 || _taken.GetValueOrDefault(row.ItemKey)?.OwnerKey != row.OwnerKey
                || !candidate.TryAdd(row.ItemKey, row) || CarriedQuantity(row.ItemKey) < row.Quantity)
                throw new InvalidDataException("Custody item source or quantity is invalid.");
        }
        _custody.Clear(); foreach (var row in candidate) _custody.Add(row.Key, row.Value); Revision++;
    }
    private int CarriedQuantity(string itemKey) => (_taken.GetValueOrDefault(itemKey)?.Quantity ?? 0) - UsedQuantity(itemKey);
    public CarriedItem[] Carried => Capture().Select(t => new CarriedItem(MapId, t.OwnerKey,
        _owners[t.OwnerKey].Inventory.Single(e => e.Object.Key == t.ItemKey).Object, CarriedQuantity(t.ItemKey))).Where(i => i.Quantity > 0).ToArray();
    public ItemBalance? Balance(string ownerKey, string itemKey)
    {
        InventoryEntry? entry = _owners.GetValueOrDefault(ownerKey)?.Inventory.SingleOrDefault(e => e.Object.Key == itemKey);
        if (entry == null) return null;
        return new(entry.Quantity, entry.Quantity - (_taken.GetValueOrDefault(itemKey)?.Quantity ?? 0), CarriedQuantity(itemKey),
            _uses.Values.Where(u => u.ItemKey == itemKey && u.Phase == ItemUsePhase.Placed).Sum(u => u.Quantity),
            _uses.Values.Where(u => u.ItemKey == itemKey && u.Phase == ItemUsePhase.Consumed).Sum(u => u.Quantity) + (_consumed.GetValueOrDefault(itemKey)?.Quantity ?? 0),
            _custody.GetValueOrDefault(itemKey)?.Quantity ?? 0);
    }
    public void Reset() { _taken.Clear(); _uses.Clear(); _consumed.Clear(); _custody.Clear(); Revision++; }
    internal Prototype? PrototypeOf(MapObject item) => _prototypes.GetValueOrDefault(item.Header.Pid);
    /// <summary>Uses up carried units of one collected source item; the caller has already checked what the item does.</summary>
    internal ItemUseStatus ConsumeCarried(string ownerKey, string itemKey, int quantity)
    {
        if (quantity <= 0) return ItemUseStatus.InvalidState;
        if (Eligibility(ownerKey, itemKey, out _, out _) != ItemTakeStatus.Ready || _taken.GetValueOrDefault(itemKey)?.OwnerKey != ownerKey) return ItemUseStatus.NoItem;
        if (CarriedQuantity(itemKey) < quantity) return ItemUseStatus.Empty;
        _consumed[itemKey] = new(ownerKey, itemKey, quantity + (_consumed.GetValueOrDefault(itemKey)?.Quantity ?? 0)); Revision++; return ItemUseStatus.Ready;
    }
    private bool Scripted(MapObject obj) => obj.Header.Sid >= 0 || obj.Header.ScriptIndex >= 0
        || _prototypes.TryGetValue(obj.Header.Pid, out Prototype? p) && p.ScriptId is >= 0;
    public ItemTakeStatus ContainerStatus(string ownerKey)
    {
        MapObject? owner = _owners.GetValueOrDefault(ownerKey);
        if (owner == null) return ItemTakeStatus.NoItem;
        // A body is looted without its script, as the CE loot screen; a living critter keeps its inventory.
        if (_bodies.Contains(ownerKey)) return CorpseHex?.Invoke(ownerKey) is int hex && HexNavigation.Valid(hex) ? ItemTakeStatus.Ready : ItemTakeStatus.Unavailable;
        if (Scripted(owner)) return ItemTakeStatus.ScriptRequired;
        if (!HexNavigation.Valid(owner.Header.Tile) || owner.ContainerElevation != owner.Header.Elevation
            || (owner.Header.Flags & HexNavigation.Hidden) != 0) return ItemTakeStatus.Unavailable;
        if (!owner.State.TryGetValue("flags", out int flags) || flags != 0) return (flags & (DoorSession.Locked | DoorSession.Jammed)) != 0
            ? ItemTakeStatus.Locked : ItemTakeStatus.Unsupported;
        return _prototypes.TryGetValue(owner.Header.Pid, out Prototype? proto) && proto.Type == 0 && proto.Subtype == 1
            ? ItemTakeStatus.Ready : ItemTakeStatus.Unsupported;
    }
    private ItemTakeStatus Eligibility(string ownerKey, string itemKey, out MapObject? owner, out InventoryEntry? entry)
    {
        owner = _owners.GetValueOrDefault(ownerKey); entry = owner?.Inventory.SingleOrDefault(e => e.Object.Key == itemKey);
        if (owner == null || entry == null) return ItemTakeStatus.NoItem;
        ItemTakeStatus status = ContainerStatus(ownerKey);
        if (status != ItemTakeStatus.Ready) return status;
        if (Scripted(entry.Object)) return ItemTakeStatus.ScriptRequired;
        MapObject item = entry.Object;
        if (!_prototypes.TryGetValue(item.Header.Pid, out Prototype? proto) || proto.Type != 0 || proto.Subtype != item.Subtype
            || item.Subtype is null or 1 || item.Inventory.Length != 0 || (proto.ExtendedFlags & 0x08000000) != 0
            || (item.Header.Flags & 0x07000001) != 0) return ItemTakeStatus.Unsupported;
        return ItemTakeStatus.Ready;
    }
    public ItemTakeStatus CanTake(string ownerKey, string itemKey, int quantity, ActorLocation actor, HexNavigation navigation, bool moving)
    {
        ItemTakeStatus status = Eligibility(ownerKey, itemKey, out MapObject? owner, out InventoryEntry? entry);
        if (status != ItemTakeStatus.Ready) return status;
        int remaining = entry!.Quantity - (_taken.GetValueOrDefault(itemKey)?.Quantity ?? 0);
        if (remaining == 0) return ItemTakeStatus.Empty;
        if (quantity <= 0 || quantity > remaining) return ItemTakeStatus.InvalidQuantity;
        return InteractionStatus(owner!, actor, navigation, moving);
    }
    private ItemTakeStatus InteractionStatus(MapObject owner, ActorLocation actor, HexNavigation navigation, bool moving)
    {
        if (actor.Map != MapId || actor.Elevation != owner.Header.Elevation || navigation.Elevation != actor.Elevation
            || !navigation.Walkable(actor.Hex)) return ItemTakeStatus.Unavailable;
        if (moving) return ItemTakeStatus.Busy;
        if (navigation.IsExit(actor.Hex)) return ItemTakeStatus.AtExit;
        int tile = Owner(owner.Key)!.Header.Tile;
        return actor.Hex == tile || Enumerable.Range(0, 6).Any(d => FalloutGrid.Neighbor(actor.Hex, d) == tile)
            ? ItemTakeStatus.Ready : ItemTakeStatus.TooFar;
    }
    public ItemTakeStatus CanReturn(string ownerKey, string itemKey, int quantity, ActorLocation actor, HexNavigation navigation, bool moving)
    {
        ItemTakeStatus status = Eligibility(ownerKey, itemKey, out MapObject? owner, out _);
        if (status != ItemTakeStatus.Ready) return status;
        int carried = CarriedQuantity(itemKey);
        if (carried == 0) return ItemTakeStatus.Empty;
        if (quantity <= 0 || quantity > carried) return ItemTakeStatus.InvalidQuantity;
        return InteractionStatus(owner!, actor, navigation, moving);
    }
    public ItemTakeStatus Return(string ownerKey, string itemKey, int quantity, ActorLocation actor, HexNavigation navigation, bool moving)
    {
        ItemTakeStatus status = CanReturn(ownerKey, itemKey, quantity, actor, navigation, moving);
        if (status != ItemTakeStatus.Ready) return status;
        TakenItem previous = _taken[itemKey];
        if (previous.Quantity == quantity) _taken.Remove(itemKey);
        else _taken[itemKey] = previous with { Quantity = previous.Quantity - quantity };
        Revision++;
        return status;
    }
    public ItemTakeStatus Take(string ownerKey, string itemKey, int quantity, ActorLocation actor, HexNavigation navigation, bool moving)
    {
        ItemTakeStatus status = CanTake(ownerKey, itemKey, quantity, actor, navigation, moving);
        if (status == ItemTakeStatus.Ready) { _taken[itemKey] = new(ownerKey, itemKey, quantity + (_taken.GetValueOrDefault(itemKey)?.Quantity ?? 0)); Revision++; }
        return status;
    }
    public ItemTakeStatus CanTakeAll(string ownerKey, ActorLocation actor, HexNavigation navigation, bool moving)
    {
        ItemTakeStatus status = ContainerStatus(ownerKey);
        if (status != ItemTakeStatus.Ready) return status;
        ContainerItem[] remaining = Contents(ownerKey).Where(i => i.Quantity > 0).ToArray();
        if (remaining.Length == 0) return ItemTakeStatus.Empty;
        foreach (ContainerItem line in remaining)
        {
            status = Eligibility(ownerKey, line.Source.Key, out _, out _);
            if (status != ItemTakeStatus.Ready) return status;
        }
        return InteractionStatus(_owners[ownerKey], actor, navigation, moving);
    }
    public ItemTakeStatus TakeAll(string ownerKey, ActorLocation actor, HexNavigation navigation, bool moving)
    {
        ItemTakeStatus status = CanTakeAll(ownerKey, actor, navigation, moving);
        if (status != ItemTakeStatus.Ready) return status;
        // Build the complete replacement before publishing any item transfer.
        var candidate = new Dictionary<string, TakenItem>(_taken, StringComparer.Ordinal);
        foreach (ContainerItem line in Contents(ownerKey).Where(i => i.Quantity > 0))
            candidate[line.Source.Key] = new(ownerKey, line.Source.Key, checked(line.Quantity + (_taken.GetValueOrDefault(line.Source.Key)?.Quantity ?? 0)));
        _taken = candidate; Revision++; return ItemTakeStatus.Ready;
    }
    public ItemTakeStatus CanReturnAll(string ownerKey, ActorLocation actor, HexNavigation navigation, bool moving)
    {
        ItemTakeStatus status = ContainerStatus(ownerKey);
        if (status != ItemTakeStatus.Ready) return status;
        CarriedItem[] carried = Carried.Where(i => i.OwnerKey == ownerKey).ToArray();
        if (carried.Length == 0) return ItemTakeStatus.Empty;
        foreach (CarriedItem line in carried)
        {
            status = Eligibility(ownerKey, line.Source.Key, out _, out _);
            if (status != ItemTakeStatus.Ready) return status;
        }
        return InteractionStatus(_owners[ownerKey], actor, navigation, moving);
    }
    public ItemTakeStatus ReturnAll(string ownerKey, ActorLocation actor, HexNavigation navigation, bool moving)
    {
        ItemTakeStatus status = CanReturnAll(ownerKey, actor, navigation, moving);
        if (status != ItemTakeStatus.Ready) return status;
        var candidate = new Dictionary<string, TakenItem>(_taken, StringComparer.Ordinal);
        foreach (CarriedItem line in Carried.Where(i => i.OwnerKey == ownerKey))
        {
            TakenItem taken = candidate[line.Source.Key]; int retained = taken.Quantity - line.Quantity;
            if (retained == 0) candidate.Remove(line.Source.Key);
            else candidate[line.Source.Key] = taken with { Quantity = retained };
        }
        _taken = candidate; Revision++; return ItemTakeStatus.Ready;
    }
    internal ItemUseStatus Place(ItemUseAllocation use)
    {
        if (!ItemUseProfile.Valid(use) || use.Phase != ItemUsePhase.Placed || use.RemainingTicks != use.Schedule.DelayTicks) return ItemUseStatus.InvalidState;
        if (_uses.ContainsKey(use.Id)) return ItemUseStatus.Duplicate;
        if (_uses.Count >= MaxUses) return ItemUseStatus.LimitReached;
        if (Eligibility(use.OwnerKey, use.ItemKey, out _, out InventoryEntry? entry) != ItemTakeStatus.Ready) return ItemUseStatus.NoItem;
        if (!ItemUseProfile.Supported(_prototypes.GetValueOrDefault(entry!.Object.Header.Pid))) return ItemUseStatus.Unsupported;
        if (CarriedQuantity(use.ItemKey) < use.Quantity) return ItemUseStatus.Empty;
        _uses.Add(use.Id, use); Revision++; return ItemUseStatus.Ready;
    }
    internal ItemUseStatus Consume(Guid id)
    {
        if (!_uses.TryGetValue(id, out ItemUseAllocation? use)) return ItemUseStatus.NoItem;
        if (use.Phase != ItemUsePhase.Placed) return ItemUseStatus.Empty;
        _uses[id] = use with { Phase = ItemUsePhase.Consumed, RemainingTicks = 0 }; Revision++; return ItemUseStatus.Ready;
    }
    internal void AdvanceUseTime(int ticks)
    {
        if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
        if (ticks == 0) return;
        bool changed = false;
        foreach (ItemUseAllocation use in _uses.Values.Where(u => u.Phase == ItemUsePhase.Placed && u.RemainingTicks > 0).ToArray())
        {
            _uses[use.Id] = use with { RemainingTicks = ticks >= use.RemainingTicks ? 0 : use.RemainingTicks - ticks }; changed = true;
        }
        if (changed) Revision++;
    }
    /// <summary>
    /// Items taken from bodies, restored once the corpses are known (after the fight outcomes). Nothing else can hold them:
    /// they are neither placed, used up nor in custody, so the rest of the restored state stays valid.
    /// </summary>
    internal void RestoreBodyLoot(TakenItem[] rows)
    {
        var candidate = new Dictionary<string, TakenItem>(StringComparer.Ordinal);
        foreach (TakenItem row in rows ?? throw new InvalidDataException("Missing body loot rows."))
            if (row?.OwnerKey == null || row.ItemKey == null || !_bodies.Contains(row.OwnerKey) || row.Quantity <= 0 || _taken.ContainsKey(row.ItemKey)
                || Eligibility(row.OwnerKey, row.ItemKey, out _, out InventoryEntry? entry) != ItemTakeStatus.Ready || row.Quantity > entry!.Quantity
                || !candidate.TryAdd(row.ItemKey, row))
                throw new InvalidDataException("Item taken from a body that is not a corpse, or with an invalid quantity.");
        foreach (var row in candidate) _taken.Add(row.Key, row.Value);
        if (candidate.Count != 0) Revision++;
    }
    public void Restore(TakenItem[] saved) => Restore(saved, Array.Empty<ItemUseAllocation>());
    public void Restore(TakenItem[] saved, ItemUseAllocation[] uses) => Restore(saved, uses, Array.Empty<ConsumedItem>(), _ => false);
    /// <param name="consumable">Whether a source item may have been used up (checked against its prototype).</param>
    public void Restore(TakenItem[] saved, ItemUseAllocation[] uses, ConsumedItem[] consumed, Func<Prototype?, bool> consumable)
    {
        ArgumentNullException.ThrowIfNull(consumable);
        if (consumed == null || consumed.Length > (saved?.Length ?? 0)) throw new InvalidDataException("Invalid consumed item count.");
        if (saved == null || saved.Length > _owners.Values.Sum(o => o.Inventory.Length)) throw new InvalidDataException("Invalid collected item count.");
        if (uses == null || uses.Length > MaxUses) throw new InvalidDataException("Invalid item-use allocation count.");
        var candidate = new Dictionary<string, TakenItem>(StringComparer.Ordinal);
        foreach (TakenItem row in saved)
        {
            if (row == null || row.OwnerKey == null || row.ItemKey == null || row.Quantity <= 0
                || Eligibility(row.OwnerKey, row.ItemKey, out _, out InventoryEntry? entry) != ItemTakeStatus.Ready
                || row.Quantity > entry!.Quantity || !candidate.TryAdd(row.ItemKey, row))
                throw new InvalidDataException("Collected item source, eligibility or quantity is invalid.");
        }
        var allocations = new Dictionary<Guid, ItemUseAllocation>(); var allocated = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (ItemUseAllocation use in uses)
        {
            if (!ItemUseProfile.Valid(use) || !candidate.TryGetValue(use.ItemKey, out TakenItem? taken) || taken.OwnerKey != use.OwnerKey
                || Eligibility(use.OwnerKey, use.ItemKey, out _, out InventoryEntry? entry) != ItemTakeStatus.Ready
                || !ItemUseProfile.Supported(_prototypes.GetValueOrDefault(entry!.Object.Header.Pid)) || !allocations.TryAdd(use.Id, use))
                throw new InvalidDataException("Invalid item-use identity, state or source.");
            int quantity = allocated.GetValueOrDefault(use.ItemKey) + use.Quantity;
            if (quantity > taken.Quantity) throw new InvalidDataException("Item-use allocations exceed the collected source quantity.");
            allocated[use.ItemKey] = quantity;
        }
        var eaten = new Dictionary<string, ConsumedItem>(StringComparer.Ordinal);
        foreach (ConsumedItem row in consumed)
        {
            if (row == null || row.OwnerKey == null || row.ItemKey == null || row.Quantity <= 0 || !candidate.TryGetValue(row.ItemKey, out TakenItem? taken)
                || taken.OwnerKey != row.OwnerKey || Eligibility(row.OwnerKey, row.ItemKey, out _, out InventoryEntry? entry) != ItemTakeStatus.Ready
                || !consumable(_prototypes.GetValueOrDefault(entry!.Object.Header.Pid)) || !eaten.TryAdd(row.ItemKey, row)
                || allocated.GetValueOrDefault(row.ItemKey) + row.Quantity > taken.Quantity)
                throw new InvalidDataException("Consumed item source, kind or quantity is invalid.");
        }
        _taken.Clear(); foreach (var row in candidate) _taken.Add(row.Key, row.Value);
        _uses.Clear(); foreach (var row in allocations) _uses.Add(row.Key, row.Value);
        _consumed.Clear(); foreach (var row in eaten) _consumed.Add(row.Key, row.Value);
        _custody.Clear();
        Revision++;
    }
}
