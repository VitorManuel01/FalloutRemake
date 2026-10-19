using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FalloutRemake.Geometry;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public enum ItemTakeStatus { Ready, NoItem, Unsupported, ScriptRequired, Locked, Busy, TooFar, Unavailable, AtExit, InvalidQuantity, Empty }
public sealed record TakenItem(string OwnerKey, string ItemKey, int Quantity);
public sealed record ContainerItem(MapObject Source, int Quantity);
public sealed record CarriedItem(int Map, string OwnerKey, MapObject Source, int Quantity);

/// <summary>Conservative direct-container transfers. Original MAP records remain immutable; no stacking across source keys.</summary>
public sealed class ItemTransferSession
{
    public int MapId { get; }
    private readonly Dictionary<string, MapObject> _owners;
    private readonly Dictionary<int, Prototype> _prototypes;
    private readonly Dictionary<string, TakenItem> _taken = new(StringComparer.Ordinal);
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
    public CarriedItem[] Carried => Capture().Select(t => new CarriedItem(MapId, t.OwnerKey,
        _owners[t.OwnerKey].Inventory.Single(e => e.Object.Key == t.ItemKey).Object, t.Quantity)).ToArray();
    public void Reset() => _taken.Clear();
    private bool Scripted(MapObject obj) => obj.Header.Sid >= 0 || obj.Header.ScriptIndex >= 0
        || _prototypes.TryGetValue(obj.Header.Pid, out Prototype? p) && p.ScriptId is >= 0;
    private ItemTakeStatus Eligibility(string ownerKey, string itemKey, out MapObject? owner, out InventoryEntry? entry)
    {
        owner = _owners.GetValueOrDefault(ownerKey); entry = owner?.Inventory.SingleOrDefault(e => e.Object.Key == itemKey);
        if (owner == null || entry == null) return ItemTakeStatus.NoItem;
        if (Scripted(owner) || Scripted(entry.Object)) return ItemTakeStatus.ScriptRequired;
        if (!HexNavigation.Valid(owner.Header.Tile) || owner.ContainerElevation != owner.Header.Elevation
            || (owner.Header.Flags & HexNavigation.Hidden) != 0) return ItemTakeStatus.Unavailable;
        if (!owner.State.TryGetValue("flags", out int flags) || flags != 0) return (flags & (DoorSession.Locked | DoorSession.Jammed)) != 0
            ? ItemTakeStatus.Locked : ItemTakeStatus.Unsupported;
        MapObject item = entry.Object;
        if (!_prototypes.TryGetValue(owner.Header.Pid, out Prototype? ownerProto) || ownerProto.Type != 0 || ownerProto.Subtype != 1
            || !_prototypes.TryGetValue(item.Header.Pid, out Prototype? proto) || proto.Type != 0 || proto.Subtype != item.Subtype
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
        int carried = _taken.GetValueOrDefault(itemKey)?.Quantity ?? 0;
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
        return status;
    }
    public ItemTakeStatus Take(string ownerKey, string itemKey, int quantity, ActorLocation actor, HexNavigation navigation, bool moving)
    {
        ItemTakeStatus status = CanTake(ownerKey, itemKey, quantity, actor, navigation, moving);
        if (status == ItemTakeStatus.Ready) _taken[itemKey] = new(ownerKey, itemKey, quantity + (_taken.GetValueOrDefault(itemKey)?.Quantity ?? 0));
        return status;
    }
    public void Restore(TakenItem[] saved)
    {
        if (saved == null || saved.Length > _owners.Values.Sum(o => o.Inventory.Length)) throw new InvalidDataException("Invalid collected item count.");
        var candidate = new Dictionary<string, TakenItem>(StringComparer.Ordinal);
        foreach (TakenItem row in saved)
        {
            if (row == null || row.OwnerKey == null || row.ItemKey == null || row.Quantity <= 0
                || Eligibility(row.OwnerKey, row.ItemKey, out _, out InventoryEntry? entry) != ItemTakeStatus.Ready
                || row.Quantity > entry!.Quantity || !candidate.TryAdd(row.ItemKey, row))
                throw new InvalidDataException("Collected item source, eligibility or quantity is invalid.");
        }
        _taken.Clear(); foreach (var row in candidate) _taken.Add(row.Key, row.Value);
    }
}
