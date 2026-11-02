using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using FalloutRemake.Import;
using FalloutRemake.Runtime.Combat;

namespace FalloutRemake.Runtime;

/// <summary>A weapon's ammunition once the game changed it: its ammoTypePid and the rounds it holds.</summary>
public sealed record SavedWeaponLoad([property: JsonRequired] int Map, [property: JsonRequired] string ItemKey, [property: JsonRequired] int AmmoPid,
    [property: JsonRequired] int Rounds);
/// <summary>
/// One ammunition source item's partial boxes: the rounds missing from the top box of the player's units and of the units
/// left where it came from (the original's top box starts the second).
/// </summary>
public sealed record SavedAmmoPool([property: JsonRequired] int Map, [property: JsonRequired] string ItemKey, [property: JsonRequired] int PlayerDeficit,
    [property: JsonRequired] int ContainerDeficit);
/// <summary>Firearms (optional, inside v13): the weapons' loads and the ammunition's partial boxes; absent while nothing changed.</summary>
public sealed record SavedFirearms(SavedWeaponLoad[]? Weapons = null, SavedAmmoPool[]? Ammo = null);

public enum ReloadStatus { Ready, NoWeapon, AlreadyLoaded, NoAmmo, InCombat, Busy }

/// <summary>
/// Firearms in the world. A weapon that takes ammunition holds the rounds and type of its MAP record (ammoQuantity,
/// ammoTypePid) until the game changes them. Ammunition follows CE stacks: a stack is one partial box on top of full ones
/// (itemRemove refills the rest), so each source item keeps the rounds missing from the top box on the player's side and
/// on the side it came from. Taking units hands over the partial top box (itemRemove) and merges it into the player's
/// (itemAdd adds the tops; two tops that fit one box leave one box fewer); returning does the same the other way; reloading
/// takes rounds from the top box and drops an emptied box (weaponReload, _obj_destroy). A stack of identical firearms
/// shares one load in CE, so only a firearm whose source is a single unit can be wielded.
/// </summary>
public sealed partial class WorldSession
{
    private readonly Dictionary<(int Map, string Key), (int AmmoPid, int Rounds)> _loads = new();
    // The open fight's copy: the player's weapons write into it until the fight is applied or dropped.
    private Dictionary<(int Map, string Key), (int AmmoPid, int Rounds)>? _fightLoads;
    private readonly Dictionary<(int Map, string Key), (int Player, int Container)> _ammoDeficits = new();

    private Prototype? RuntimePrototype(int pid)
    {
        try { return _runtimePrototype?.Invoke(pid); }
        catch (Exception error) when (error is KeyNotFoundException or InvalidDataException or IOException or ArgumentException) { return null; }
    }
    // An item PRO: the map's own dependencies first, else the game data (an ammunition type no object of the map uses).
    private Prototype? ItemPrototype(int map, int pid) =>
        (_areas.TryGetValue(map, out WorldArea? area) ? area.Map.Prototypes.FirstOrDefault(p => p.Pid == pid) : null) ?? (pid > 0 ? RuntimePrototype(pid) : null);

    /// <summary>An ammunition PRO, or null when the pid names none the game can read.</summary>
    public AmmoPrototypeData? AmmoData(int map, int pid) =>
        pid > 0 && ItemPrototype(map, pid) is { Type: 0, Subtype: AmmoPrototypeData.Subtype } prototype ? AmmoPrototypeData.Read(prototype) : null;

    /// <summary>A weapon's ammunition type and rounds: the world's record once they changed, else its MAP record.</summary>
    public (int AmmoPid, int Rounds) WeaponLoad(int map, MapObject source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if ((_fightLoads ?? _loads).TryGetValue((map, source.Key), out var load)) return load;
        return (source.State.GetValueOrDefault("ammoTypePid", -1), Math.Max(0, source.State.GetValueOrDefault("ammoQuantity", 0)));
    }

    // The weapon a carried source fires, with its load; null when the load names ammunition the game cannot read.
    private CombatWeapon? LoadedWeapon(CarriedItem item, WeaponPrototypeData data, bool second)
    {
        string name = $"PID {data.Pid}";
        if (data.AmmoCapacity <= 0)
            return (second ? CombatWeapon.Secondary(name, data) : null) ?? CombatWeapon.Primary(name, data);
        var (ammoPid, rounds) = WeaponLoad(item.Map, item.Source);
        rounds = Math.Min(rounds, data.AmmoCapacity);
        AmmoPrototypeData? ammo = ammoPid > 0 ? AmmoData(item.Map, ammoPid) : null;
        if (ammo != null && ammo.Caliber != data.Caliber || rounds > 0 && ammo == null) return null;
        CombatWeapon weapon = (second ? CombatWeapon.Secondary(name, data, ammo, rounds) : null) ?? CombatWeapon.Primary(name, data, ammo, rounds);
        return weapon with { AmmoPid = ammoPid };
    }

    private static bool IsAmmo(Prototype? prototype) => prototype is { Type: 0, Subtype: AmmoPrototypeData.Subtype };

    // The box size of an ammunition source; 0 when its PRO is not ammunition.
    private int Box(int map, MapObject source) => _areas.TryGetValue(map, out WorldArea? area) && IsAmmo(area.Items.PrototypeOf(source))
        ? Math.Max(1, AmmoPrototypeData.Read(area.Items.PrototypeOf(source)!).Quantity) : 0;

    private (int Player, int Container) Deficits(int map, MapObject source, int box) => _ammoDeficits.TryGetValue((map, source.Key), out var d) ? d
        : (0, Math.Clamp(box - source.State.GetValueOrDefault("ammoQuantity", box), 0, box));

    /// <summary>The rounds of a carried ammunition source: its boxes less what its top box misses.</summary>
    public int CarriedRounds(CarriedItem ammo)
    {
        ArgumentNullException.ThrowIfNull(ammo);
        int box = Box(ammo.Map, ammo.Source);
        return box == 0 ? 0 : Math.Max(0, ammo.Quantity * box - Deficits(ammo.Map, ammo.Source, box).Player);
    }

    /// <summary>The carried ammunition as reloads see it, in inventory order; the key names the area, owner and item.</summary>
    public CombatAmmo[] CarriedAmmunition() => Carried.Where(c => Box(c.Map, c.Source) > 0 && CarriedRounds(c) > 0)
        .Select(c => new CombatAmmo($"{c.Map}|{c.OwnerKey}|{c.Source.Key}", AmmoPrototypeData.Read(_areas[c.Map].Items.PrototypeOf(c.Source)!), CarriedRounds(c))).ToArray();

    private CarriedItem? AmmoByKey(string key)
    {
        string[] parts = key.Split('|');
        return parts.Length == 3 && int.TryParse(parts[0], out int map) ? Carried.FirstOrDefault(c => c.Map == map && c.OwnerKey == parts[1] && c.Source.Key == parts[2]) : null;
    }

    // Units of a source were taken: the container's partial top box joins the player's (itemAdd); tops that fit one box
    // merge into it, so a box object goes away.
    private void AmmoTaken(int map, string ownerKey, MapObject source)
    {
        int box = Box(map, source);
        if (box == 0) return;
        var (player, container) = Deficits(map, source, box);
        player += container; container = 0;
        if (player >= box)
        {
            if (_areas[map].Items.ConsumeCarried(ownerKey, source.Key, 1) != ItemUseStatus.Ready) throw new InvalidOperationException("A merged ammunition box could not be dropped.");
            player -= box;
        }
        _ammoDeficits[(map, source.Key)] = (player, container);
    }

    // Before units go back: the player's partial top box goes with them and joins the container's; when both tops fit one
    // box, one of the units leaves no box behind (it is used up first). Returns how many units still go back.
    private int AmmoReturning(int map, string ownerKey, MapObject source, int quantity)
    {
        int box = Box(map, source);
        if (box == 0 || quantity <= 0) return quantity;
        var (player, container) = Deficits(map, source, box);
        int remaining = _areas[map].Items.Balance(ownerKey, source.Key)?.Remaining ?? 0;
        container = remaining == 0 ? player : container + player; player = 0;
        if (container >= box)
        {
            if (_areas[map].Items.ConsumeCarried(ownerKey, source.Key, 1) != ItemUseStatus.Ready) throw new InvalidOperationException("A merged ammunition box could not be dropped.");
            container -= box; quantity--;
        }
        _ammoDeficits[(map, source.Key)] = (player, container);
        return quantity;
    }

    // Rounds a reload took from the player's units: the top box empties first, and an empty box is dropped (_obj_destroy).
    private void SpendRounds(CarriedItem ammo, int rounds)
    {
        int box = Box(ammo.Map, ammo.Source);
        var (player, container) = Deficits(ammo.Map, ammo.Source, box);
        player += rounds;
        int boxes = ammo.Quantity;
        while (player >= box && boxes > 0)
        {
            if (_areas[ammo.Map].Items.ConsumeCarried(ammo.OwnerKey, ammo.Source.Key, 1) != ItemUseStatus.Ready) throw new InvalidOperationException("An empty ammunition box could not be dropped.");
            player -= box; boxes--;
        }
        _ammoDeficits[(ammo.Map, ammo.Source.Key)] = (boxes == 0 ? 0 : player, container);
    }

    /// <summary>
    /// The active hand's reload outside a fight (CE _intface_item_reload, no AP then): from the carried ammunition until the
    /// weapon is full or nothing fits.
    /// </summary>
    public ReloadStatus CanReload(Hand hand, bool moving)
    {
        if (InCombat) return ReloadStatus.InCombat;
        if (moving) return ReloadStatus.Busy;
        if (Held(hand) == null || HandWeapon(hand) is not { UsesAmmo: true } weapon) return ReloadStatus.NoWeapon;
        if (weapon.Rounds >= weapon.AmmoCapacity) return ReloadStatus.AlreadyLoaded;
        return FirearmReload.Source(weapon, CarriedAmmunition()) == null ? ReloadStatus.NoAmmo : ReloadStatus.Ready;
    }

    public ReloadStatus Reload(Hand hand, bool moving)
    {
        ReloadStatus status = CanReload(hand, moving);
        if (status != ReloadStatus.Ready) return status;
        var taken = new List<(CombatAmmo Stack, int Rounds)>();
        CombatWeapon loaded = FirearmReload.Fill(HandWeapon(hand), CarriedAmmunition(), taken);
        foreach (var (stack, rounds) in taken) SpendRounds(AmmoByKey(stack.Key)!, rounds);
        HeldItem held = Held(hand)!;
        _loads[(held.Map, held.ItemKey)] = (loaded.AmmoPid, loaded.Rounds);
        return ReloadStatus.Ready;
    }

    // The player's weapons in a fight write their rounds back to the held items (the hand the fight strikes with first).
    private void SyncFightLoads(CombatSession fight, Dictionary<(int Map, string Key), (int AmmoPid, int Rounds)> loads)
    {
        if (fight.Combatants.FirstOrDefault(c => c.IsPlayer) is not { } player) return;
        Hand active = player.RightHandActive ? Hand.Right : Hand.Left;
        foreach (var (hand, weapon) in new[] { (active, player.Weapon), (Other(active), player.AlternateWeapon) })
            if (weapon is { UsesAmmo: true } && Held(hand) is { } held && CarriedHeld(held) is { } item && item.Source.Header.Pid == weapon.Pid)
                loads[(held.Map, held.ItemKey)] = (weapon.AmmoPid, weapon.Rounds);
    }

    // Before a fight is applied: every stack it reloaded from is still carried with the rounds it gave.
    private bool FightAmmoValid(CombatantState player) => player.Ammunition.All(a => AmmoByKey(a.Key) is { } carried && CarriedRounds(carried) >= a.Rounds);

    // Applying a fight: its reloads empty the carried stacks, its shots and reloads become the weapons' loads.
    private void CommitFightFirearms(CombatSession fight, CombatantState player, Dictionary<(int Map, string Key), (int AmmoPid, int Rounds)>? fightLoads)
    {
        foreach (CombatAmmo stack in player.Ammunition)
            if (AmmoByKey(stack.Key) is { } carried && CarriedRounds(carried) - stack.Rounds is int used and > 0) SpendRounds(carried, used);
        var loads = fightLoads ?? new Dictionary<(int Map, string Key), (int AmmoPid, int Rounds)>(_loads);
        SyncFightLoads(fight, loads);
        _loads.Clear();
        foreach (var pair in loads) _loads[pair.Key] = pair.Value;
    }

    private void ResetFirearms() { _loads.Clear(); _fightLoads = null; _ammoDeficits.Clear(); }

    private SavedFirearms? CaptureFirearms()
    {
        var weapons = _loads.OrderBy(p => p.Key.Map).ThenBy(p => p.Key.Key, StringComparer.Ordinal)
            .Select(p => new SavedWeaponLoad(p.Key.Map, p.Key.Key, p.Value.AmmoPid, p.Value.Rounds)).ToArray();
        var ammo = _ammoDeficits.OrderBy(p => p.Key.Map).ThenBy(p => p.Key.Key, StringComparer.Ordinal)
            .Select(p => new SavedAmmoPool(p.Key.Map, p.Key.Key, p.Value.Player, p.Value.Container)).ToArray();
        return weapons.Length == 0 && ammo.Length == 0 ? null : new SavedFirearms(weapons.Length == 0 ? null : weapons, ammo.Length == 0 ? null : ammo);
    }

    // A source item of an area's containers or bodies, by key.
    private (string Owner, MapObject Source)? SourceItem(int map, string key)
    {
        if (!_areas.TryGetValue(map, out WorldArea? area)) return null;
        foreach (MapObject owner in area.Map.ObjectLayers.SelectMany(l => l.Objects))
            if (owner.Inventory.FirstOrDefault(e => e.Object.Key == key) is { } entry) return (owner.Key, entry.Object);
        return null;
    }

    // After the items are restored: each load is a weapon that takes ammunition, of a single unit, holding at most its
    // magazine of readable ammunition of its caliber; each pool is ammunition with partial tops inside one box.
    private void RestoreFirearms(SavedFirearms? saved)
    {
        ResetFirearms();
        if (saved == null) return;
        if (saved.Weapons is { Length: 0 } || saved.Ammo is { Length: 0 } || saved.Weapons == null && saved.Ammo == null)
            throw new InvalidDataException("Checkpoint firearms store their defaults explicitly.");
        foreach (SavedWeaponLoad load in saved.Weapons ?? Array.Empty<SavedWeaponLoad>())
        {
            if (load?.ItemKey == null || SourceItem(load.Map, load.ItemKey) is not { } found) throw new InvalidDataException("Checkpoint weapon load names no item.");
            var (owner, source) = found;
            bool possible = _areas[load.Map].Items.PrototypeOf(source) is { Type: 0, Subtype: WeaponPrototypeData.Subtype } prototype
                && WeaponPrototypeData.Read(prototype) is { AmmoCapacity: > 0 } data && _areas[load.Map].Items.Balance(owner, load.ItemKey) is { Original: 1 }
                && load.Rounds >= 0 && load.Rounds <= data.AmmoCapacity && (load.Rounds == 0 || AmmoData(load.Map, load.AmmoPid)?.Caliber == data.Caliber);
            if (!possible || !_loads.TryAdd((load.Map, load.ItemKey), (load.AmmoPid, load.Rounds))) throw new InvalidDataException("Checkpoint weapon load is not possible.");
        }
        foreach (SavedAmmoPool pool in saved.Ammo ?? Array.Empty<SavedAmmoPool>())
        {
            if (pool?.ItemKey == null || SourceItem(pool.Map, pool.ItemKey) is not { } found) throw new InvalidDataException("Checkpoint ammunition pool names no item.");
            var (owner, source) = found;
            int box = Box(pool.Map, source);
            bool possible = box > 0 && _areas[pool.Map].Items.Balance(owner, pool.ItemKey) is { } balance
                && pool.PlayerDeficit >= 0 && pool.PlayerDeficit < box && (balance.Carried > 0 || pool.PlayerDeficit == 0)
                && pool.ContainerDeficit >= 0 && pool.ContainerDeficit <= box && (balance.Remaining > 0 || pool.ContainerDeficit == 0);
            if (!possible || !_ammoDeficits.TryAdd((pool.Map, pool.ItemKey), (pool.PlayerDeficit, pool.ContainerDeficit)))
                throw new InvalidDataException("Checkpoint ammunition pool is not possible.");
        }
    }
}
