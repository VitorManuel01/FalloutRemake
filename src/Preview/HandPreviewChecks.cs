using System.IO;
using System.Threading.Tasks;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    /// <summary>
    /// The bare-handed hand outside a fight: X picks the right hand's kick, the status names the blow, and the choice
    /// survives save, reset (back to the left hand) and load, as FO2 keeps the hand in its save.
    /// </summary>
    private Task CheckHand()
    {
        bool checking = _checkingNavigation; _checkingNavigation = true;
        byte[]? checkpointBefore = File.Exists(CheckpointPath) ? File.ReadAllBytes(CheckpointPath) : null;
        try
        {
            ResetWalker();
            if (_campaign!.ActiveHand != Hand.Left) throw new InvalidDataException("A new game does not start on the left hand.");
            using (var x = new InputEventKey { Pressed = true, PhysicalKeycode = Key.X }) _UnhandledInput(x);
            if (_campaign.ActiveHand != Hand.Right || !_hud.NavigationStatusText.Contains("Chute")) throw new InvalidDataException("X did not pick the kick: " + _hud.NavigationStatusText);
            string blow = _campaign.PlayerWeapon().Name;
            SaveCheckpoint(); ResetWalker();
            if (_campaign.ActiveHand != Hand.Left) throw new InvalidDataException("Reset kept the right hand.");
            LoadCheckpoint();
            if (_campaign.ActiveHand != Hand.Right || CheckpointFile.Read(CheckpointPath).Combat?.Hands?.RightActive != true) throw new InvalidDataException("The checkpoint lost the right hand.");
            using (var x = new InputEventKey { Pressed = true, PhysicalKeycode = Key.X }) _UnhandledInput(x);
            if (_campaign.ActiveHand != Hand.Left) throw new InvalidDataException("X did not go back to the punch.");
            GD.Print($"HAND_OK: X picked the right hand ({blow}) outside a fight, kept through save/reset/load, and went back to the punch.");
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
