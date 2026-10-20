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

/// <summary>Conservative direct-container transfers. Original MAP records remain immutable; no stacking across source keys.</summary>
public sealed class ItemTransferSession
{
    public int MapId { get; }
    private readonly Dictionary<string, MapObject> _owners;
    private readonly Dictionary<int, Prototype> _prototypes;
    private Dictionary<string, TakenItem> _taken = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, ItemUseAllocation> _uses = new();
    public const int MaxUses = 1024;
    public long Revision { get; private set; }
    public ItemTransferSession(MapDocument map)
    {
        MapId = map.Prefix.Header.MapIndex;
        _owners = map.ObjectLayers.SelectMany(l => l.Objects).Where(o => o.Header.Pid >> 24 == 0 && o.Subtype == 1).ToDictionary(o => o.Key);
        _prototypes = map.Prototypes.ToDictionary(p => p.Pid);
        foreach (MapObject owner in _owners.Values)
            if (owner.Inventory.Any(e => e.Quantity <= 0) || owner.Inventory.Select(e => e.Object.Key).Distinct().Count() != owner.Inventory.Length)
                throw new InvalidDataException("Invalid direct container inventory.");
    }
    public IReadOnlyList<MapObject> Containers => _owners.Values.OrderBy(o => o.Offset).ToArray();
    public ContainerItem[] Contents(string ownerKey) => _owners.TryGetValue(ownerKey, out MapObject? owner)
        ? owner.Inventory.Select(e => new ContainerItem(e.Object, e.Quantity - (_taken.GetValueOrDefault(e.Object.Key)?.Quantity ?? 0))).ToArray()
        : Array.Empty<ContainerItem>();
    public TakenItem[] Capture() => _taken.Values.OrderBy(t => t.ItemKey, StringComparer.Ordinal).ToArray();
    public ItemUseAllocation[] CaptureUses() => _uses.Values.OrderBy(u => u.Id).ToArray();
    private int UsedQuantity(string itemKey) => _uses.Values.Where(u => u.ItemKey == itemKey).Sum(u => u.Quantity);
    private int CarriedQuantity(string itemKey) => (_taken.GetValueOrDefault(itemKey)?.Quantity ?? 0) - UsedQuantity(itemKey);
    public CarriedItem[] Carried => Capture().Select(t => new CarriedItem(MapId, t.OwnerKey,
        _owners[t.OwnerKey].Inventory.Single(e => e.Object.Key == t.ItemKey).Object, CarriedQuantity(t.ItemKey))).Where(i => i.Quantity > 0).ToArray();
    public ItemBalance? Balance(string ownerKey, string itemKey)
    {
        InventoryEntry? entry = _owners.GetValueOrDefault(ownerKey)?.Inventory.SingleOrDefault(e => e.Object.Key == itemKey);
        if (entry == null) return null;
        return new(entry.Quantity, entry.Quantity - (_taken.GetValueOrDefault(itemKey)?.Quantity ?? 0), CarriedQuantity(itemKey),
            _uses.Values.Where(u => u.ItemKey == itemKey && u.Phase == ItemUsePhase.Placed).Sum(u => u.Quantity),
            _uses.Values.Where(u => u.ItemKey == itemKey && u.Phase == ItemUsePhase.Consumed).Sum(u => u.Quantity));
    }
    public void Reset() { _taken.Clear(); _uses.Clear(); Revision++; }
    private bool Scripted(MapObject obj) => obj.Header.Sid >= 0 || obj.Header.ScriptIndex >= 0
        || _prototypes.TryGetValue(obj.Header.Pid, out Prototype? p) && p.ScriptId is >= 0;
    public ItemTakeStatus ContainerStatus(string ownerKey)
    {
        MapObject? owner = _owners.GetValueOrDefault(ownerKey);
        if (owner == null) return ItemTakeStatus.NoItem;
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
        return actor.Hex == owner.Header.Tile || Enumerable.Range(0, 6).Any(d => FalloutGrid.Neighbor(actor.Hex, d) == owner.Header.Tile)
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
    public void Restore(TakenItem[] saved) => Restore(saved, Array.Empty<ItemUseAllocation>());
    public void Restore(TakenItem[] saved, ItemUseAllocation[] uses)
    {
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
        _taken.Clear(); foreach (var row in candidate) _taken.Add(row.Key, row.Value);
        _uses.Clear(); foreach (var row in allocations) _uses.Add(row.Key, row.Value);
        Revision++;
    }
}
