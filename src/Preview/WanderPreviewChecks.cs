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
    /// The creatures wander on exploration time (seeded): past the first door, two minutes of game time move them through
    /// the scene; every one stands on its projected hex, never on the actor or a blocked hex. A walk started meanwhile
    /// finishes because wanderers keep off its remaining route; the positions survive save, reset and load.
    /// </summary>
    private async Task CheckWander()
    {
        bool checking = _checkingNavigation, encounters = _combatEncounters, walking = _walkMode; ICombatRandom random = _wanderRandom;
        byte[]? checkpointBefore = File.Exists(CheckpointPath) ? File.ReadAllBytes(CheckpointPath) : null;
        _checkingNavigation = true; _combatEncounters = false; _wanderInChecks = true; _wanderRandom = new SeededCombatRandom(7);
        try
        {
            ResetWalker(); if (!_walkMode) ToggleWalkMode();
            RequestWalk(24508); CompleteReadabilityWalk(); UseDoor(_layout!.Objects.Single(o => o.Source.Offset == 244040).Source.Key);
            var sources = _campaign!.Current.Map.ObjectLayers.SelectMany(l => l.Objects).Where(o => WorldSession.EncounterPids.Contains(o.Header.Pid)).ToArray();
            int moved = 0;
            for (int second = 0; second < 120; second++)
            {
                var before = _campaign.Survivors.ToArray(); ApplyGameTime(10);
                moved += _campaign.Survivors.Count(s => !before.Contains(s));
            }
            if (moved == 0 || !_campaign.Survivors.Any(s => s.Elevation == _elevation)) throw new InvalidDataException("Two minutes of game time moved no creature on the shown elevation.");
            void Verify(string when)
            {
                foreach (SurvivingCritter s in _campaign.Survivors)
                {
                    HexNavigation navigation = _campaign.Current.Doors.Navigation(s.Elevation);
                    if (navigation.BlockersAt(s.Hex).Any(b => b.Key != s.Key) || s.Elevation == _campaign.Location.Elevation && s.Hex == _campaign.Location.Hex)
                        throw new InvalidDataException($"{when}: wanderer {s.Key} stands on an occupied hex {s.Hex}.");
                    // The scene holds the shown elevation only.
                    if (s.Elevation != _elevation) continue;
                    Vector3 expected = Point(FalloutGrid.HexCenter(s.Hex) - _layout!.Origin, Dimensions(PreviewKind.Critter).Y / 2);
                    if (_bodies[s.Key].Position.DistanceTo(expected) > .001f || _layout.Objects.Single(o => o.Source.Key == s.Key).Source.Header.Tile != s.Hex)
                        throw new InvalidDataException($"{when}: wanderer {s.Key} is not shown on its hex {s.Hex}.");
                }
            }
            Verify("after wandering");
            // A route started now finishes: the wanderers keep off it while time passes.
            RebuildWalkerNavigation();
            int target = Enumerable.Range(0, 40000).Where(t => _reachable!.Distance(t) is >= 8 and <= 12 && !_navigation!.IsExit(t)).OrderBy(t => t).First();
            if (RequestWalk(target).Status != RouteStatus.Ready) throw new InvalidDataException("No route after the creatures moved.");
            for (int guard = 0; guard < 200 && _walker!.Moving; guard++)
            {
                ApplyGameTime(3);
                _checkingNavigation = false; AdvanceWalker(.1); _checkingNavigation = true;
            }
            if (_walker!.CurrentHex != target) throw new InvalidDataException("The walk did not finish among wandering creatures.");
            Verify("after walking");
            SaveCheckpoint(); var saved = _campaign.Survivors.ToArray(); ResetWalker();
            if (_campaign.Survivors.Count != 0) throw new InvalidDataException("Reset kept the wanderers.");
            LoadCheckpoint();
            if (!_campaign.Survivors.SequenceEqual(saved)) throw new InvalidDataException("The checkpoint lost the wanderers.");
            Verify("after load");
            if (_capture != null)
            {
                SurvivingCritter shown = saved.Where(s => s.Elevation == _elevation).OrderBy(s => FalloutGrid.Distance(s.Hex, _campaign.Location.Hex)).First();
                SetCameraFollow(false); SelectObject(_layout!.Objects.Single(o => o.Source.Key == shown.Key)); _focus = _bodies[shown.Key].Position; _camera.Size = 16; UpdateCamera();
                await Capture(Path.ChangeExtension(_capture, "wander.png")); ClearSelection();
            }
            foreach (WorldArea area in _campaign.Areas) MapLayoutWriter.Verify(area.Map);
            GD.Print($"WANDER_OK: {moved} walks in 120 s of game time by {saved.Length} of {sources.Length} creatures, all on free projected hexes, a 8-12 hex walk finished among them, kept through save/reset/load; source bytes verified.");
        }
        finally
        {
            _wanderRandom = random; _wanderInChecks = false; _combatEncounters = encounters; _checkingNavigation = checking;
            if (checkpointBefore != null) File.WriteAllBytes(CheckpointPath, checkpointBefore); else if (File.Exists(CheckpointPath)) File.Delete(CheckpointPath);
            ResetWalker(); if (_walkMode != walking) ToggleWalkMode();
        }
    }
}
