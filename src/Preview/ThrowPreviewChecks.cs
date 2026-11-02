using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Geometry;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    /// <summary>
    /// The spear's throw in the preview: past the first door, with the Temple spear in the left hand, Z switches the hand to
    /// the throw; a fight opens on the nearest creature and the player closes in until a turn starts with the 6 AP and a free
    /// line; the throw leaves the spear on the ground (drawn with its name), out of the hand and the bag. After the fight, J
    /// beside it puts it back in the bag. The world and checkpoint are restored afterwards.
    /// </summary>
    private async Task CheckThrow()
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
            if (_campaign.Wield(_campaign.Carried.Single(c => c.Source.Key == spear.Item.Key), Hand.Left, false) != HandStatus.Ready)
                throw new InvalidDataException("The spear did not go into the left hand.");
            using (var z = new InputEventKey { Pressed = true, PhysicalKeycode = Key.Z }) _UnhandledInput(z);
            if (!_campaign.UsesSecondMode(Hand.Left) || !_campaign.PlayerWeapon().Throws || !_hud.NavigationStatusText.Contains("arremesso"))
                throw new InvalidDataException("Z did not switch the spear to its throw: " + _hud.NavigationStatusText);

            PreviewObject creature = _layout.Objects.Where(o => WorldSession.EncounterPids.Contains(o.Source.Header.Pid) && o.Source.Header.Elevation == _campaign.Location.Elevation
                    && Enumerable.Range(0, 6).Any(d => FalloutGrid.Neighbor(o.Source.Header.Tile, d) is int n && _reachable!.Distance(n) >= 0))
                .OrderBy(o => FalloutGrid.Distance(o.Source.Header.Tile, _campaign.Location.Hex)).ThenBy(o => o.Source.Offset).First();
            string key = creature.Source.Key;
            _combatSeed = 2026;
            StartCombat(new CombatEncounter(_campaign.Location, new[] { key }, "player"));
            CombatantState player = _combat!.Get("player"), foe = _combat.Get(key);
            if (!player.Weapon.Throws || !_combatWeapon.Name.Contains("arremesso")) throw new InvalidDataException("The fight did not open with the throw.");
            CombatActionProposal Throw() => CombatActionProposal.Attack("player", key);
            WeaponThrown? thrown = null;
            for (int guard = 0; guard < 200 && thrown == null; guard++)
            {
                if (_combat == null) throw new InvalidDataException("The fight ended before the throw.");
                if (_combat.Active is { IsPlayer: false }) { UpdateCombat(EnemyTurnDelay); continue; }
                CombatActionPreview attack = _combat.Preview(Throw());
                if (attack.Status == CombatActionStatus.Ready)
                {
                    CombatActionResult result = _combat.Execute(Throw());
                    thrown = result.Events.OfType<WeaponThrown>().LastOrDefault();
                    ApplyCombatResult(result);
                    break;
                }
                // Close in while keeping the throw's 6 AP for the next turn, else end the turn.
                if (attack.Status is CombatActionStatus.OutOfRange or CombatActionStatus.AimBlocked && player.ActionPoints > attack.ActionPointCost
                    && _combat.Movement.Path(player, h => FalloutGrid.Distance(h, foe.Hex) <= 2) is { Length: > 0 } path)
                    ApplyCombatResult(_combat.Execute(CombatActionProposal.Move("player", path[0])));
                else ApplyCombatResult(_combat.Execute(CombatActionProposal.EndTurn("player")));
            }
            if (thrown == null) throw new InvalidDataException("No throw within 200 steps.");
            if (_campaign.Held(Hand.Left) != null || _campaign.Carried.Any(c => c.Source.Key == spear.Item.Key)
                || _campaign.GroundItems is not { Length: 1 } grounds || grounds[0].Item is not { } row || row.Place.Hex != thrown.LandingHex
                || _combat != null && !_hud.CombatLogText.Contains("arremessou") || _groundNodes.Count != 1)
                throw new InvalidDataException("The throw did not leave the spear on the ground: " + _hud.CombatLogText);
            if (_hud.CharacterText.Contains("(arremesso) (ativa)"))
                throw new InvalidDataException("The status line still shows the thrown spear in hand.");
            // Frame the spear on the ground for the capture.
            SetCameraFollow(false); _focus = _groundNodes[0].Position; UpdateCamera();
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "throw.png"));
            DiscardCombat();

            // Walk beside the landing hex and pick the spear up.
            int beside = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(row.Place.Hex, d)).Append(row.Place.Hex)
                .Where(t => t.HasValue && _reachable!.Distance(t.Value) >= 0 && !_navigation!.IsExit(t.Value)).Select(t => t!.Value)
                .OrderBy(t => _reachable!.Distance(t)).ThenBy(t => t).FirstOrDefault(-1);
            if (beside < 0) throw new InvalidDataException($"The spear landed on hex {row.Place.Hex}, out of reach.");
            RequestWalk(beside); CompleteReadabilityWalk();
            using (var j = new InputEventKey { Pressed = true, PhysicalKeycode = Key.J }) _UnhandledInput(j);
            if (_campaign.GroundItems.Length != 0 || !_campaign.Carried.Any(c => c.Source.Key == spear.Item.Key) || _groundNodes.Count != 0
                || !_hud.NavigationStatusText.Contains("recolhido"))
                throw new InvalidDataException("J beside the spear did not pick it up: " + _hud.NavigationStatusText);
            GD.Print($"THROW_OK: Z switched the left hand's spear to its throw (6 AP, up to 8 hexes); the throw at {key} {(thrown.LandingHex == foe.Hex ? "struck it" : "missed")} and left the spear on hex {thrown.LandingHex}, out of the hand and the bag; J beside it put it back in the bag.");
        }
        finally
        {
            _combatSeed = null; DiscardCombat(); _combatEncounters = encounters; _checkingNavigation = checking;
            if (checkpointBefore != null) File.WriteAllBytes(CheckpointPath, checkpointBefore); else if (File.Exists(CheckpointPath)) File.Delete(CheckpointPath);
            ResetWalker(); if (_walkMode != walking) ToggleWalkMode();
        }
    }
}
