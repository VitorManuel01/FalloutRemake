using System.IO;
using System.Threading.Tasks;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    /// <summary>
    /// Poison on exploration time: a checkpoint carrying level 3 due in 5 ticks loses 1 HP after 0.5 s of frame time
    /// (one tick per 100 ms), keeps level 1 through save, reset and load, is cured by its next event, and a lethal event ends
    /// the game like a lost fight. The stings themselves are covered by the synthetic tests and the run audit.
    /// </summary>
    private Task CheckPoison()
    {
        bool checking = _checkingNavigation; _checkingNavigation = true;
        byte[]? checkpointBefore = File.Exists(CheckpointPath) ? File.ReadAllBytes(CheckpointPath) : null;
        try
        {
            ResetWalker();
            string catalog = _mapCatalog!.Source.Sha256;
            WorldCheckpoint clean = _campaign!.Capture(catalog);
            SavedCombat combat = clean.Combat ?? new SavedCombat(System.Array.Empty<DefeatedCritter>(), 0, null);
            void Load(SavedCombat state) { CheckpointFile.Write(CheckpointPath, clean with { Combat = state }); LoadCheckpoint(); }
            Load(combat with { Poison = new SavedPoison(3, 5) });
            int hp = _campaign.HitPoints;
            if (_campaign.Poison != new PoisonState(3, 5)) throw new InvalidDataException("The checkpoint did not load the poison.");
            _checkingNavigation = false; AdvanceGameTime(.55); _checkingNavigation = true;
            if (_campaign.HitPoints != hp - 1 || _campaign.Poison != new PoisonState(1, PoisonRules.DelayTicks(1)))
                throw new InvalidDataException($"Half a second of exploration did not run the due poison event ({_campaign.Poison}).");
            SaveCheckpoint(); ResetWalker();
            if (_campaign.Poison.Active) throw new InvalidDataException("Reset kept the poison.");
            LoadCheckpoint();
            if (_campaign.Poison != new PoisonState(1, PoisonRules.DelayTicks(1)) || _campaign.HitPoints != hp - 1) throw new InvalidDataException("The checkpoint lost the poison.");
            ApplyGameTime(PoisonRules.DelayTicks(1));
            if (_campaign.Poison.Active || _campaign.HitPoints != hp - 2) throw new InvalidDataException("The last poison event did not cure the player.");
            Load(combat with { PlayerHitPoints = 1, Poison = new SavedPoison(2, 1) });
            ApplyGameTime(1);
            if (_campaign.Poison.Active || _campaign.HitPoints != _campaign.MaxHitPoints) throw new InvalidDataException("A lethal poison event did not end the game.");
            GD.Print($"POISON_OK: level 3 due in 5 ticks cost 1 HP after 0.5 s of exploration, level 1 kept through save/reset/load, cured by its next event, a lethal event reset the game.");
        }
        finally
        {
            _checkingNavigation = checking;
            if (checkpointBefore != null) File.WriteAllBytes(CheckpointPath, checkpointBefore); else if (File.Exists(CheckpointPath)) File.Delete(CheckpointPath);
            ResetWalker();
        }
        return Task.CompletedTask;
    }
}
