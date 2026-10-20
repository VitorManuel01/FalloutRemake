using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Geometry;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private async Task CheckUseClock(Guid id)
    {
        WorldCheckpoint original = _campaign!.Capture(_mapCatalog!.Source.Sha256);
        byte[] State() => CheckpointFile.Encode(_campaign!.Capture(_mapCatalog!.Source.Sha256));
        void Restore() { CheckpointFile.Write(CheckpointPath, original); LoadCheckpoint(); }
        void CheckTicks(int ticks)
        {
            var expected = original with { Areas = original.Areas.Select(a => a with {
                Uses = a.Uses!.Select(u => u.Id == id ? u with { RemainingTicks = ticks } : u).ToArray() }).ToArray() };
            if (!State().SequenceEqual(CheckpointFile.Encode(expected))) throw new InvalidDataException("Clock changed more than the expected remaining ticks.");
        }
        var action = FindHudButton("UseClockAction");
        if (action.Disabled) throw new InvalidDataException("Placed item did not enable clock inspection.");
        action.EmitSignal(Button.SignalName.Pressed);
        var window = _useClock ?? throw new InvalidDataException("Clock action did not open a modal.");
        var one = (Button)window.FindChild("AdvanceUseOneSecond", true, false);
        var ten = (Button)window.FindChild("AdvanceUseTenSeconds", true, false);
        var row = ((Tree)window.FindChild("UseClockEvents", true, false)).GetRoot().GetFirstChild();
        if (window.EventCount != 1 || row.GetText(1) != "7,5 s" || one.Disabled || ten.Disabled) throw new InvalidDataException("Partial clock display or actions are incorrect.");
        CheckTicks(75);
        foreach (Key code in new[] { Key.K, Key.R, Key.M, Key.B, Key.I, Key.J, Key.Q })
        { using var key = new InputEventKey { Pressed = true, PhysicalKeycode = code }; _UnhandledInput(key); }
        _Process(100); CheckTicks(75);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); CheckDialogBounds(window);
        if (_capture != null) await Capture(Path.ChangeExtension(_capture, "use-clock-paused.png"));
        var callback = window.AdvanceRequested!;
        callback(123); CheckTicks(75);
        int neighbor = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(_walker!.CurrentHex, d))
            .Where(t => t.HasValue && _navigation!.Walkable(t.Value) && !_navigation.IsExit(t.Value)).Select(t => t!.Value).First();
        if (!_walkMode) ToggleWalkMode(); RequestWalk(neighbor); callback(10); CheckTicks(75);
        if (!one.Disabled) throw new InvalidDataException("Moving actor retained an enabled clock action.");
        CloseUseClock(); CompleteReadabilityWalk(); Restore();
        callback(10); CheckTicks(75);
        ChangeElevation(0); OpenUseClock();
        if (_useClock == null || !((Button)_useClock.FindChild("AdvanceUseOneSecond", true, false)).Disabled)
            throw new InvalidDataException("Inspection clock did not remain read-only on another elevation.");
        _useClock.AdvanceRequested!(10); CheckTicks(75); CloseUseClock(); Restore();
        OpenUseClock(); window = _useClock!; callback = window.AdvanceRequested!;
        OpenBag(); callback(10); CheckTicks(75);
        if (_useClock != null || _bag?.Visible != true) throw new InvalidDataException("Bag did not replace clock modal.");
        CloseBag(); OpenUseClock(); window = _useClock!;
        one = (Button)window.FindChild("AdvanceUseOneSecond", true, false); ten = (Button)window.FindChild("AdvanceUseTenSeconds", true, false);
        one.EmitSignal(Button.SignalName.Pressed); CheckTicks(65);
        if (!window.StatusText.Contains("1 s") || _placedUseNodes[id].GetMeta("remaining_ticks").AsInt32() != 65) throw new InvalidDataException("One-second advance did not reach the scene.");
        ten.EmitSignal(Button.SignalName.Pressed); CheckTicks(0);
        if (!one.Disabled || !ten.Disabled || !window.StatusText.Contains("6,5 s") || !action.Text.Contains("pendente")
            || _campaign.DueItemUses.Single().Allocation.Id != id || _campaign.SceneryReplacements.Length != 0
            || !_placedUseNodes[id].GetNode<Label3D>("UseLabel").Text.StartsWith("PENDENTE"))
            throw new InvalidDataException("Deadline did not stop at the pending event without applying an effect.");
        callback = window.AdvanceRequested!; callback(100); CheckTicks(0);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (_capture != null) await Capture(Path.ChangeExtension(_capture, "use-clock-pending.png"));
        CloseUseClock(); SaveCheckpoint(); ResetWalker(); LoadCheckpoint();
        if (_campaign!.DueItemUses.Single().Allocation.Id != id || !action.Text.Contains("pendente")) throw new InvalidDataException("Pending checkpoint did not restore the same stopped event.");
        callback(100); CheckTicks(0); OpenUseClock();
        if (!((Button)_useClock!.FindChild("AdvanceUseTenSeconds", true, false)).Disabled) throw new InvalidDataException("Reload unblocked a pending event.");
        CloseUseClock(); Restore(); CheckTicks(75);
        GD.Print("USE_CLOCK_UI_OK: real placed GUID, 7.5s display, explicit +1/+10s, exact 6.5s deadline clamp, pending freeze, no effect/consumption, checkpoint restoration, invalid step/moving/inspection/modal/late-callback guards, unchanged actor/items/doors/source bytes.");
    }
}
