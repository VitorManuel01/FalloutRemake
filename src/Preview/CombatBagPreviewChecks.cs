using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    /// <summary>
    /// The bag in a fight is CE's inventory: past the first door, with the spear carried but not wielded, a fight opens;
    /// on the player's turn B costs 4 AP, the bag's "Mão esquerda" puts the spear in the active left hand at no further
    /// cost and the fight strikes with it at once; closing the bag closes the inventory. The world is restored afterwards.
    /// </summary>
    private async Task CheckCombatBag()
    {
        bool checking = _checkingNavigation, encounters = _combatEncounters, walking = _walkMode; _checkingNavigation = true; _combatEncounters = false;
        byte[]? checkpointBefore = File.Exists(CheckpointPath) ? File.ReadAllBytes(CheckpointPath) : null;
        try
        {
            ResetWalker(); if (!_walkMode) ToggleWalkMode();
            RequestWalk(24508); CompleteReadabilityWalk(); UseDoor(_layout!.Objects.Single(o => o.Source.Offset == 244040).Source.Key);
            var spear = _campaign!.Current.Map.ObjectLayers.SelectMany(l => l.Objects)
                .SelectMany(o => o.Inventory.Select(e => (Owner: o, Item: e.Object))).Single(p => p.Item.Header.Pid == 7);
            _campaign.Current.Items.Restore(new[] { new TakenItem(spear.Owner.Key, spear.Item.Key, 1) });
            PreviewObject creature = _layout.Objects.Where(o => WorldSession.EncounterPids.Contains(o.Source.Header.Pid) && o.Source.Header.Elevation == _campaign.Location.Elevation)
                .OrderByDescending(o => FalloutGrid.Distance(o.Source.Header.Tile, _campaign.Location.Hex)).ThenBy(o => o.Source.Offset).First();
            _combatSeed = 2026;
            StartCombat(new CombatEncounter(_campaign.Location, new[] { creature.Source.Key }, "player"));
            CombatantState player = _combat!.Get("player");
            if (_combat.Active != player || player.Weapon.Pid != null) throw new InvalidDataException("The fight did not open on the player's bare-handed turn.");
            int before = player.ActionPoints;
            using (var b = new InputEventKey { Pressed = true, PhysicalKeycode = Key.B }) _UnhandledInput(b);
            if (_bag == null || !_combat.InventoryOpen || player.ActionPoints != before - CombatSession.InventoryActionPoints || !_hud.CombatLogText.Contains("abre a bolsa"))
                throw new InvalidDataException("B did not open the inventory for 4 AP.");
            if (_bag.Select(_campaign.Carried.Single(c => c.Source.Key == spear.Item.Key)) != true) throw new InvalidDataException("The spear is missing from the bag.");
            var wield = (Button)_bag.FindChild("WieldLeftAction", true, false);
            if (!wield.Visible || wield.Disabled) throw new InvalidDataException("The bag does not offer the left hand in the fight.");
            wield.EmitSignal(Button.SignalName.Pressed);
            if (player.Weapon.Pid != 7 || _combatWeapon.Pid != 7 || player.ActionPoints != before - CombatSession.InventoryActionPoints || !_hud.CombatLogText.Contains("Nas mãos"))
                throw new InvalidDataException("Wielding inside the open inventory did not arm the fight at no further cost.");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "combat-bag.png"));
            using (var b = new InputEventKey { Pressed = true, PhysicalKeycode = Key.B }) _UnhandledInput(b);
            if (_bag != null || _combat.InventoryOpen) throw new InvalidDataException("Closing the bag did not close the inventory.");
            GD.Print($"COMBAT_BAG_OK: B on the player's turn opened the inventory for {CombatSession.InventoryActionPoints} AP, the bag's left hand wielded the spear at no further cost and the fight strikes with it, closing the bag closed the inventory.");
        }
        finally
        {
            _combatSeed = null; CloseBag(); DiscardCombat(); _combatEncounters = encounters; _checkingNavigation = checking;
            if (checkpointBefore != null) File.WriteAllBytes(CheckpointPath, checkpointBefore); else if (File.Exists(CheckpointPath)) File.Delete(CheckpointPath);
            ResetWalker(); if (_walkMode != walking) ToggleWalkMode();
        }
    }
}
