using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private async Task CheckBarrierEffectReview(Guid useId)
    {
        WorldCheckpoint pending = _campaign!.Capture(_mapCatalog!.Source.Sha256);
        byte[] State() => CheckpointFile.Encode(_campaign!.Capture(_mapCatalog!.Source.Sha256));
        byte[] before = State(); int roots = _lookup.Count, xp = _campaign.Experience;
        string key = _layout!.Objects.Single(o => o.Source.Offset == 303324).Source.Key;
        void Restore() { CheckpointFile.Write(CheckpointPath, pending); LoadCheckpoint(); }
        ConfirmationDialog Review()
        {
            if (_useClock == null) OpenUseClock();
            var action = (Button)_useClock!.FindChild("ReviewPendingEffect", true, false);
            if (action.Disabled) throw new InvalidDataException("Pending event has no effect review action.");
            action.EmitSignal(Button.SignalName.Pressed);
            return _barrierEffectReview ?? throw new InvalidDataException("Effect review did not open.");
        }
        ReviewPendingEffect(Guid.NewGuid());
        if (_barrierEffectReview != null || !State().SequenceEqual(before)) throw new InvalidDataException("Unknown event altered the scene.");
        var dialog = Review();
        _Process(100);
        if (!State().SequenceEqual(before) || !_bodies[key].Visible || _navigation!.Walkable(14322)) throw new InvalidDataException("Effect review applied a change before confirmation.");
        foreach (Key code in new[] { Key.K, Key.R, Key.M, Key.B, Key.I, Key.J })
        { using var input = new InputEventKey { Pressed = true, PhysicalKeycode = code }; _UnhandledInput(input); }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); CheckDialogBounds(dialog);
        if (_capture != null) await Capture(Path.ChangeExtension(_capture, "barrier-effect-review.png"));
        dialog.GetCancelButton().EmitSignal(Button.SignalName.Pressed);
        if (_barrierEffectReview != null || _useClock?.Visible != true || !State().SequenceEqual(before)) throw new InvalidDataException("Cancel changed a pending event.");
        dialog = Review(); dialog.EmitSignal(Window.SignalName.CloseRequested);
        if (_barrierEffectReview != null || !State().SequenceEqual(before)) throw new InvalidDataException("Closing effect review changed the event.");
        dialog = Review(); Restore(); dialog.EmitSignal(AcceptDialog.SignalName.Confirmed);
        if (_barrierEffectReview != null || !State().SequenceEqual(before)) throw new InvalidDataException("Old review callback changed a reloaded session.");
        dialog = Review(); dialog.GetOkButton().EmitSignal(Button.SignalName.Pressed);
        if (InspectorOpen || _campaign.SceneryReplacements.Length != 1 || _campaign.ItemUses.Single(u => u.Allocation.Id == useId).Allocation.Phase != ItemUsePhase.Consumed
            || _campaign.CarriedCount != 0 || _campaign.Experience != xp || _lookup.Count != roots || _bodies[key].Visible || !_navigation!.Walkable(14322)
            || _placedUseNodes.Count != 0 || _replacementNodes.Count != 1 || !FindHudButton("UseClockAction").Disabled)
            throw new InvalidDataException("Confirmed effect lost atomic ownership, scenery, source identity or navigation.");
        byte[] applied = State(); dialog.EmitSignal(AcceptDialog.SignalName.Confirmed);
        if (!State().SequenceEqual(applied)) throw new InvalidDataException("Repeated effect callback changed the applied world.");
        SaveCheckpoint(); ResetWalker(); LoadCheckpoint();
        if (!State().SequenceEqual(applied) || _bodies[key].Visible || !_navigation!.Walkable(14322) || _replacementNodes.Count != 1)
            throw new InvalidDataException("Confirmed effect did not survive checkpoint reset/load.");
        var target = _layout!.Objects.Single(o => o.Source.Key == key); _focus = Point(target.Position, 0); _camera.Size = 22; UpdateCamera();
        if (_capture != null) await Capture(Path.ChangeExtension(_capture, "barrier-effect-confirmed.png"));
        Restore();
        if (!State().SequenceEqual(before) || !_bodies[key].Visible || _navigation!.Walkable(14322)) throw new InvalidDataException("Effect UI audit failed to restore pending fixture.");
        OpenUseClock();
        GD.Print("BARRIER_EFFECT_UI_OK: pending event review, source-bound projection, unknown event refusal, no mutation before confirmation, cancel/X/shortcut guards, closed-load callback and duplicate refusal, one consumed use and one replacement, walkable former blocker, unchanged source roots/items/XP, exact checkpoint reset/load.");
    }
}
