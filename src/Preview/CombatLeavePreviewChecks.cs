using System;
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
    /// Leaving a fight with Enter (CE combatAttemptEnd): past the first door, a fight is opened against the farthest reachable
    /// elevation-0 creature; it closes in on its turn, and Enter on the player's turn ends the fight because the creature no
    /// longer perceives the player. The creature stays where it stopped, with its HP and facing, through save, reset and load.
    /// </summary>
    private async Task CheckCombatLeave()
    {
        bool checking = _checkingNavigation, encounters = _combatEncounters, walking = _walkMode; _checkingNavigation = true; _combatEncounters = false;
        byte[]? checkpointBefore = File.Exists(CheckpointPath) ? File.ReadAllBytes(CheckpointPath) : null;
        try
        {
            ResetWalker(); if (!_walkMode) ToggleWalkMode();
            RequestWalk(24508); CompleteReadabilityWalk(); UseDoor(_layout!.Objects.Single(o => o.Source.Offset == 244040).Source.Key);
            PreviewObject creature = _layout.Objects.Where(o => WorldSession.EncounterPids.Contains(o.Source.Header.Pid) && o.Source.Header.Elevation == _campaign!.Location.Elevation
                    && Enumerable.Range(0, 6).Any(d => FalloutGrid.Neighbor(o.Source.Header.Tile, d) is int n && _reachable!.Distance(n) >= 0))
                .OrderByDescending(o => FalloutGrid.Distance(o.Source.Header.Tile, _campaign!.Location.Hex)).ThenBy(o => o.Source.Offset).First();
            string key = creature.Source.Key;
            if (FalloutGrid.Distance(creature.Source.Header.Tile, _campaign!.Location.Hex) <= 15) throw new InvalidDataException("No reachable creature lies far enough to leave the fight.");
            _combatSeed = 2026;
            StartCombat(new CombatEncounter(_campaign.Location, new[] { key }, key));
            for (int guard = 0; guard < 20 && _combat?.Active is { IsPlayer: false }; guard++) UpdateCombat(EnemyTurnDelay);
            CombatantState foe = _combat!.Get(key), player = _combat.Get("player");
            if (foe.Hex == creature.Source.Header.Tile || _combat.AwareEnemies(player).Any())
                throw new InvalidDataException("The creature did not close in, or already perceives the player.");
            int hex = foe.Hex, rotation = foe.Rotation, hitPoints = foe.HitPoints;
            using (var enter = new InputEventKey { Pressed = true, PhysicalKeycode = Key.Enter }) _UnhandledInput(enter);
            Vector3 Standing() => Point(FalloutGrid.HexCenter(hex) - _layout!.Origin, Dimensions(PreviewKind.Critter).Y / 2);
            if (InCombat || _campaign.Survivors.SingleOrDefault() is not { } survivor || survivor.Key != key || survivor.Hex != hex || survivor.Rotation != rotation
                || survivor.HitPoints != hitPoints) throw new InvalidDataException("Enter did not leave the fight with the creature standing where it stopped.");
            if (_bodies[key].Position.DistanceTo(Standing()) > .001f) throw new InvalidDataException("The survivor is not shown where it stopped.");
            if (_campaign.FindEncounter(_campaign.Location)?.FoeKeys.Contains(key) == true) throw new InvalidDataException("The survivor saw the player right after the fight it could not perceive.");
            if (_capture != null)
            {
                SetCameraFollow(false); SelectObject(_layout!.Objects.Single(o => o.Source.Key == key)); _focus = _bodies[key].Position; _camera.Size = 14; UpdateCamera();
                await Capture(Path.ChangeExtension(_capture, "combat-left.png")); ClearSelection();
            }
            SaveCheckpoint(); ResetWalker();
            if (_campaign.Survivors.Count != 0 || _bodies[key].Position.DistanceTo(Standing()) < .001f) throw new InvalidDataException("Reset kept the survivor.");
            LoadCheckpoint();
            if (_campaign.Survivors.SingleOrDefault() != survivor || _bodies[key].Position.DistanceTo(Standing()) > .001f || CheckpointFile.Read(CheckpointPath).Combat?.Survivors?.Length != 1)
                throw new InvalidDataException("The checkpoint lost the survivor.");
            MapLayoutWriter.Verify(_map!);
            GD.Print($"COMBAT_LEAVE_OK: {key} closed in from hex {creature.Source.Header.Tile} to {hex} ({FalloutGrid.Distance(hex, _campaign.Location.Hex)} hexes away, rotation {rotation}), Enter refused beside the ants and accepted out of perception, survivor kept through save/reset/load; source bytes verified.");
        }
        finally
        {
            _combatSeed = null; DiscardCombat(); _combatEncounters = encounters; _checkingNavigation = checking;
            if (checkpointBefore != null) File.WriteAllBytes(CheckpointPath, checkpointBefore); else if (File.Exists(CheckpointPath)) File.Delete(CheckpointPath);
            ResetWalker(); if (_walkMode != walking) ToggleWalkMode();
        }
    }
}
