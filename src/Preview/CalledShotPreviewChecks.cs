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
    /// The player's aimed attack: past the first door the player opens a fight on the nearest creature and closes in. On a
    /// turn beside it, a right click on the creature opens the called-shot window with the eight CE locations, each with the
    /// fight's own odds and the extra AP; cancelling spends nothing; choosing the eyes strikes them for the blow's AP + 1.
    /// </summary>
    private async Task CheckCalledShot()
    {
        bool checking = _checkingNavigation, encounters = _combatEncounters, walking = _walkMode; _checkingNavigation = true; _combatEncounters = false;
        byte[]? checkpointBefore = File.Exists(CheckpointPath) ? File.ReadAllBytes(CheckpointPath) : null;
        try
        {
            ResetWalker(); if (!_walkMode) ToggleWalkMode();
            RequestWalk(24508); CompleteReadabilityWalk(); UseDoor(_layout!.Objects.Single(o => o.Source.Offset == 244040).Source.Key);
            PreviewObject creature = _layout.Objects.Where(o => WorldSession.EncounterPids.Contains(o.Source.Header.Pid) && o.Source.Header.Elevation == _campaign!.Location.Elevation
                    && Enumerable.Range(0, 6).Any(d => FalloutGrid.Neighbor(o.Source.Header.Tile, d) is int n && _reachable!.Distance(n) >= 0))
                .OrderBy(o => FalloutGrid.Distance(o.Source.Header.Tile, _campaign!.Location.Hex)).ThenBy(o => o.Source.Offset).First();
            string key = creature.Source.Key;
            _combatSeed = 2026;
            StartCombat(new CombatEncounter(_campaign!.Location, new[] { key }, "player"));
            CombatantState player = _combat!.Get("player"), foe = _combat.Get(key);
            CombatActionProposal Eyes() => CombatActionProposal.Attack("player", key, HitLocation.Eyes);

            // Close in until a turn starts beside the creature with the AP for an aimed blow.
            for (int guard = 0; guard < 200; guard++)
            {
                if (_combat == null) throw new InvalidDataException("The fight ended before an aimed blow.");
                if (_combat.Active is { IsPlayer: false }) { UpdateCombat(EnemyTurnDelay); continue; }
                if (_combat.Preview(Eyes()).Status == CombatActionStatus.Ready) break;
                CombatActionPreview plain = _combat.Preview(CombatActionProposal.Attack("player", key));
                if (plain.Status == CombatActionStatus.OutOfRange && player.StepsAffordable(1) > 0
                    && _combat.Movement.Path(player, h => FalloutGrid.Distance(h, foe.Hex) <= player.Weapon.Range) is { Length: > 0 } path)
                    ApplyCombatResult(_combat.Execute(CombatActionProposal.Move("player", path[player.StepsAffordable(path.Length) - 1])));
                else ApplyCombatResult(_combat.Execute(CombatActionProposal.EndTurn("player")));
            }
            if (_combat?.Active != player || _combat.Preview(Eyes()).Status != CombatActionStatus.Ready) throw new InvalidDataException("No turn beside the creature with AP to aim.");
            int cost = player.Weapon.ActionPoints + 1, before = player.ActionPoints;

            void RightClick()
            {
                // Centre the view on the creature, as a player would, so the click lands outside the HUD panels.
                Vector3 body = _bodies[key].GlobalPosition;
                SetCameraFollow(false); _focus = new Vector3(body.X, 0, body.Z); UpdateCamera();
                Vector2 screen = _camera.UnprojectPosition(body);
                using (var press = new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Right, Position = screen }) _UnhandledInput(press);
                if (_calledShot != null) throw new InvalidDataException("The window opened on the press: a drag could not start on the creature.");
                using (var release = new InputEventMouseButton { Pressed = false, ButtonIndex = MouseButton.Right, Position = screen }) _UnhandledInput(release);
                if (_calledShot == null) throw new InvalidDataException($"A right click on the creature did not open the called-shot window (screen {screen}, panel {_hud.MouseOverPanelAt(screen)}, "
                    + $"found {EnemyAtScreen(screen)?.Id ?? "none"}, active {_combat?.Active?.Id}, inspector {InspectorOpen}).");
            }
            RightClick();
            foreach (HitLocation location in CalledShotWindow.LeftColumn.Concat(CalledShotWindow.RightColumn))
            {
                CombatActionPreview odds = _combat.Preview(CombatActionProposal.Attack("player", key, location));
                string text = _calledShot!.ButtonFor(location).Text;
                if (!text.StartsWith(CalledShotWindow.LocationName(location)) || !text.Contains($"{odds.ToHit!.Chance}%") || !text.Contains($"{cost} AP"))
                    throw new InvalidDataException($"The {location} option differs from the rules: {text}.");
            }
            int torso = _combat.Preview(CombatActionProposal.Attack("player", key, HitLocation.Torso)).ToHit!.Chance, eyes = _combat.Preview(Eyes()).ToHit!.Chance;
            if (eyes >= torso && torso > CombatResolver.MinimumChance) throw new InvalidDataException("The eyes are not harder to hit than the torso.");
            _calledShot!.EmitSignal(AcceptDialog.SignalName.Canceled);
            if (_calledShot != null || player.ActionPoints != before) throw new InvalidDataException("Cancelling the window spent AP or left it open.");

            RightClick();
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "called-shot.png"));
            _calledShot!.ButtonFor(HitLocation.Eyes).EmitSignal(Button.SignalName.Pressed);
            AttackResolved attack = _combat!.Log.OfType<AttackResolved>().Last(a => a.AttackerId == "player");
            if (_calledShot != null || attack.Location != HitLocation.Eyes || attack.ActionPointsSpent != cost || attack.Chance != eyes
                || !_hud.CombatLogText.Contains("mirando os olhos"))
                throw new InvalidDataException("Choosing the eyes did not strike them for the aimed cost: " + _hud.CombatLogText);
            GD.Print($"CALLED_SHOT_OK: a right click on {key} opened the eight CE locations with the fight's odds (torso {torso}%, eyes {eyes}%) at {cost} AP; cancelling spent nothing; the eyes were struck for {cost} AP ({(attack.Hit ? "hit" : "miss")}, roll {attack.Roll}).");
        }
        finally
        {
            _combatSeed = null; DiscardCombat(); _combatEncounters = encounters; _checkingNavigation = checking;
            if (checkpointBefore != null) File.WriteAllBytes(CheckpointPath, checkpointBefore); else if (File.Exists(CheckpointPath)) File.Delete(CheckpointPath);
            ResetWalker(); if (_walkMode != walking) ToggleWalkMode();
        }
    }
}
