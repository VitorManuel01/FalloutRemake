using System;
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
    /// Criticals in the preview: the clock starts at 302400 ticks; past the first door it is run to game day 1 (exploration
    /// time). A seed whose fight against the nearest creature brings a critical hit by the player is found on throwaway
    /// sessions, then the same fight is played through the combat HUD: the critical shows in the log with its effect, and
    /// the clock survives save, reset and load.
    /// </summary>
    private async Task CheckCriticals()
    {
        bool checking = _checkingNavigation, encounters = _combatEncounters, walking = _walkMode; _checkingNavigation = true; _combatEncounters = false;
        byte[]? checkpointBefore = File.Exists(CheckpointPath) ? File.ReadAllBytes(CheckpointPath) : null;
        try
        {
            ResetWalker(); if (!_walkMode) ToggleWalkMode();
            if (_campaign!.GameTime != CriticalRules.GameStartTicks) throw new InvalidDataException("The clock does not start at 302400 ticks.");
            RequestWalk(24508); CompleteReadabilityWalk(); UseDoor(_layout!.Objects.Single(o => o.Source.Offset == 244040).Source.Key);
            ApplyGameTime((int)(CriticalRules.TicksPerDay - _campaign.GameTime));
            if (_campaign.GameTime != CriticalRules.TicksPerDay) throw new InvalidDataException("Exploration time did not reach game day 1.");
            PreviewObject creature = _layout.Objects.Where(o => WorldSession.EncounterPids.Contains(o.Source.Header.Pid) && o.Source.Header.Elevation == _campaign.Location.Elevation
                    && Enumerable.Range(0, 6).Any(d => FalloutGrid.Neighbor(o.Source.Header.Tile, d) is int n && _reachable!.Distance(n) >= 0))
                .OrderBy(o => FalloutGrid.Distance(o.Source.Header.Tile, _campaign.Location.Hex)).ThenBy(o => o.Source.Offset).First();
            string key = creature.Source.Key;
            var encounter = new CombatEncounter(_campaign.Location, new[] { key }, key);
            CombatWeapon weapon = NamedWeapon(_campaign.PlayerWeapon());

            // The player's policy, shared by the search and the replay: strike when possible, close in, otherwise end the turn.
            CombatActionProposal Decide(CombatSession fight)
            {
                CombatantState player = fight.Get("player"), foe = fight.Get(key);
                CombatActionPreview attack = fight.Preview(CombatActionProposal.Attack("player", key));
                if (attack.Status == CombatActionStatus.Ready) return CombatActionProposal.Attack("player", key);
                if (attack.Status == CombatActionStatus.OutOfRange && player.StepsAffordable(1) > 0
                    && fight.Movement.Path(player, h => FalloutGrid.Distance(h, foe.Hex) <= player.Weapon.Range) is { Length: > 0 } path)
                    return CombatActionProposal.Move("player", path[player.StepsAffordable(path.Length) - 1]);
                return CombatActionProposal.EndTurn("player");
            }
            bool PlayerCritical(CombatSession fight) => fight.Log.OfType<CriticalHit>().Any(c => c.AttackerId == "player");
            bool Search(ulong candidate)
            {
                CombatSession fight = _campaign.BeginCombat(encounter, weapon, new SeededCombatRandom(candidate));
                try
                {
                    for (int guard = 0; guard < 400 && !fight.Turn.Over && !PlayerCritical(fight); guard++)
                        if (fight.Active!.IsPlayer) fight.Execute(Decide(fight)); else ProvisionalMeleeBehavior.TakeTurn(fight);
                    // A critical that ends the fight would close the HUD before its log line can be read.
                    return PlayerCritical(fight) && !fight.Turn.Over;
                }
                finally { _campaign.AbandonCombat(); }
            }
            ulong seed = Enumerable.Range(1, 2000).Select(i => (ulong)i).FirstOrDefault(Search);
            if (seed == 0) throw new InvalidDataException("No seed among 2000 brings a critical hit by the player on day 1.");

            _combatSeed = seed;
            StartCombat(encounter);
            if (_combat!.GameTime != CriticalRules.TicksPerDay) throw new InvalidDataException("The fight did not open on the world's clock.");
            string log = "";
            for (int guard = 0; guard < 400 && _combat != null && !PlayerCritical(_combat); guard++)
            {
                if (_combat.Active is { IsPlayer: false }) { UpdateCombat(EnemyTurnDelay); continue; }
                ApplyCombatResult(_combat.Execute(Decide(_combat)));
                if (_combat != null && PlayerCritical(_combat)) log = _hud.CombatLogText;
            }
            if (log.Length == 0) throw new InvalidDataException("The replayed fight lost its critical hit.");
            CriticalHit critical = _combat!.Log.OfType<CriticalHit>().First(c => c.AttackerId == "player");
            if (!log.Contains("Crítico") || critical.Roll > critical.Needed || critical.Multiplier < 3)
                throw new InvalidDataException("The combat log does not show the critical hit: " + log);
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "critical.png"));
            CombatantState foe = _combat.Get(key);
            string outcome = $"{critical.Roll} <= {critical.Needed}, effect {critical.Effect} (roll {critical.EffectRoll}), x{critical.Multiplier}/2, flags {critical.Flags}, {key} at {foe.HitPoints} HP";
            DiscardCombat();
            SaveCheckpoint();
            if (CheckpointFile.Read(CheckpointPath).Combat?.GameTime != CriticalRules.TicksPerDay) throw new InvalidDataException("The checkpoint did not store the clock.");
            ResetWalker();
            if (_campaign.GameTime != CriticalRules.GameStartTicks) throw new InvalidDataException("Reset kept the clock.");
            LoadCheckpoint();
            if (_campaign.GameTime != CriticalRules.TicksPerDay) throw new InvalidDataException("The checkpoint lost the clock.");
            GD.Print($"CRITICAL_OK: day 1 reached after {(CriticalRules.TicksPerDay - CriticalRules.GameStartTicks) / 36000.0:0.0} h of exploration time; seed {seed} brings a critical hit by the player ({outcome}), shown in the combat log; the clock kept through save/reset/load.");
        }
        finally
        {
            _combatSeed = null; DiscardCombat(); _combatEncounters = encounters; _checkingNavigation = checking;
            if (checkpointBefore != null) File.WriteAllBytes(CheckpointPath, checkpointBefore); else if (File.Exists(CheckpointPath)) File.Delete(CheckpointPath);
            ResetWalker(); if (_walkMode != walking) ToggleWalkMode();
        }
    }
}
