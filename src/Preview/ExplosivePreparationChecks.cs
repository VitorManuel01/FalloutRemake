using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    // Called after walking to and collecting the original explosive, before the existing effect audit.
    private async Task CheckExplosivePreparation(MapObject owner, MapObject item)
    {
        WorldCheckpoint before = _campaign!.Capture(_mapCatalog!.Source.Sha256);
        ActorLocation originalLocation = _campaign.Location;
        byte[] State() => CheckpointFile.Encode(_campaign!.Capture(_mapCatalog!.Source.Sha256));
        byte[] initial = State();
        void Restore() { CheckpointFile.Write(CheckpointPath, before); LoadCheckpoint(); }
        Button PrepareButton() => (Button)_bag!.FindChild("PrepareExplosiveAction", true, false);
        void OpenDraft()
        {
            OpenBag(); if (!PrepareButton().Visible || PrepareButton().Disabled) throw new InvalidDataException("Original explosive has no available preparation action.");
            PrepareButton().EmitSignal(Button.SignalName.Pressed);
            if (_explosivePreparation?.Visible != true || _bag != null) throw new InvalidDataException("Preparation did not replace the bag modal.");
        }

        _focus = Point(FalloutGrid.HexCenter(originalLocation.Hex) - _layout!.Origin, 0); _camera.Size = 18; UpdateCamera();
        OpenDraft();
        var draft = _explosivePreparation!;
        var seconds = (SpinBox)draft.FindChild("ExplosiveSeconds", true, false);
        if (seconds.MinValue != 10 || seconds.MaxValue != 180 || seconds.Step != 10 || seconds.Value != 60 || draft.Plan?.Schedule.DelayTicks != 600)
            throw new InvalidDataException("Preparation timer profile differs from verified source behavior.");
        seconds.Value = 10; if (draft.Plan?.Schedule.DelayTicks != 100) throw new InvalidDataException("Minimum timer proposal failed.");
        seconds.Value = 180; if (draft.Plan?.Schedule.DelayTicks != 1800) throw new InvalidDataException("Maximum timer proposal failed.");
        foreach (Key code in new[] { Key.K, Key.R, Key.M, Key.I, Key.J, Key.B, Key.Q })
        { using var key = new InputEventKey { Pressed = true, PhysicalKeycode = code }; _UnhandledInput(key); }
        _Process(100);
        if (!State().SequenceEqual(initial) || _explosivePreparation != draft) throw new InvalidDataException("Draft/shortcuts changed live state.");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        CheckDialogBounds(draft);
        if (_capture != null) await Capture(Path.ChangeExtension(_capture, "explosive-preparation.png"));
        draft.GetCancelButton().EmitSignal(Button.SignalName.Pressed);
        if (_explosivePreparation != null || _bag?.Visible != true || !State().SequenceEqual(initial)) throw new InvalidDataException("Cancel removed an item or failed to return to the bag.");
        CloseBag(); OpenDraft(); _explosivePreparation!.EmitSignal(Window.SignalName.CloseRequested);
        if (_explosivePreparation != null || _bag?.Visible != true || !State().SequenceEqual(initial)) throw new InvalidDataException("Window-close cancellation changed ownership.");
        CloseBag();

        ChangeElevation(0); OpenBag();
        if (!PrepareButton().Disabled) throw new InvalidDataException("Inspection of another elevation enabled placement.");
        OpenExplosivePreparation(_campaign.Carried.Single());
        if (_explosivePreparation != null || !State().SequenceEqual(initial)) throw new InvalidDataException("Placement from another inspection elevation altered the session.");
        CloseBag(); ChangeElevation(originalLocation.Elevation);
        int neighbor = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(_walker!.CurrentHex, d))
            .Where(t => t.HasValue && _navigation!.Walkable(t.Value) && !_navigation.IsExit(t.Value)).Select(t => t!.Value).First();
        if (!_walkMode) ToggleWalkMode(); RequestWalk(neighbor); OpenBag();
        if (!PrepareButton().Disabled) throw new InvalidDataException("Moving actor enabled placement.");
        CloseBag(); Restore();

        OpenDraft(); draft = _explosivePreparation!;
        _campaign.Current.Travel.MoveTo(neighbor); byte[] changed = State();
        draft.GetOkButton().EmitSignal(Button.SignalName.Pressed);
        if (!State().SequenceEqual(changed) || !draft.GetOkButton().Disabled || !draft.StatusText.Contains("mudou"))
            throw new InvalidDataException("Stale actor placement was committed or silently replanned.");
        Restore();
        OpenDraft(); draft = _explosivePreparation!;
        var lateSubmit = draft.SubmitRequested!; var oldPlan = draft.Plan;
        Restore(); changed = State(); lateSubmit(oldPlan);
        if (InspectorOpen || !State().SequenceEqual(changed)) throw new InvalidDataException("Load retained a preparation modal or accepted its late callback.");

        OpenDraft(); draft = _explosivePreparation!;
        seconds = (SpinBox)draft.FindChild("ExplosiveSeconds", true, false);
        seconds.GetLineEdit().Text = "10"; seconds.Apply();
        ExplosiveUsePlan plan = draft.Plan!; lateSubmit = draft.SubmitRequested!;
        int roots = _lookup.Count, reach = _reachable!.Count;
        draft.GetOkButton().EmitSignal(Button.SignalName.Pressed);
        if (InspectorOpen || _campaign.CarriedCount != 0 || _campaign.ItemUses.Single().Allocation.Id != plan.Id
            || _campaign.ItemUses.Single().Allocation.RemainingTicks != 100 || _lookup.Count != roots || _reachable.Count != reach
            || _placedUseNodes.Count != 1 || _campaign.Current.Items.Balance(owner.Key, item.Key) != new ItemBalance(1, 0, 0, 1, 0))
            throw new InvalidDataException($"Confirmed placement mismatch: modal={InspectorOpen}, carried={_campaign.CarriedCount}, id={_campaign.ItemUses.SingleOrDefault()?.Allocation.Id}/{plan.Id}, ticks={_campaign.ItemUses.SingleOrDefault()?.Allocation.RemainingTicks}, roots={_lookup.Count}/{roots}, reach={_reachable.Count}/{reach}, visuals={_placedUseNodes.Count}, balance={_campaign.Current.Items.Balance(owner.Key, item.Key)}, status={draft.StatusText}.");
        changed = State(); lateSubmit(plan);
        if (!State().SequenceEqual(changed)) throw new InvalidDataException("Repeated submit changed placed ownership.");
        _campaign.AdvanceUseTime(25); RefreshPlacedUses(); SaveCheckpoint(); ResetWalker(); LoadCheckpoint();
        if (_campaign!.ItemUses.Single().Allocation.Id != plan.Id || _campaign.ItemUses.Single().Allocation.RemainingTicks != 75
            || _campaign.CarriedCount != 0 || _placedUseNodes.Count != 1) throw new InvalidDataException("Placed UI item lost its GUID or partial timer on load.");
        changed = State(); _Process(100); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!State().SequenceEqual(changed)) throw new InvalidDataException("Rehearsal advanced time automatically.");
        _focus = Point(FalloutGrid.HexCenter(plan.Placement.Hex) - _layout!.Origin, 0); _camera.Size = 18; UpdateCamera();
        if (_capture != null) await Capture(Path.ChangeExtension(_capture, "explosive-preparation-placed.png"));
        await CheckUseClock(plan.Id);
        Restore();
        if (!State().SequenceEqual(initial) || _campaign.CarriedCount != 1 || _placedUseNodes.Count != 0)
            throw new InvalidDataException("Preparation audit did not restore its prior test fixture.");
        MapLayoutWriter.Verify(_map!);
        GD.Print("EXPLOSIVE_PREPARATION_UI_OK: original collected unit, bag action, 10/60/180s draft, cancel/X, modal guards, moving/inspection refusals, stale placement/load callbacks, single atomic confirmation, duplicate refusal, GUID/75-tick checkpoint, paused clock, source IDs/navigation/bytes retained; explicit success rehearsal only.");
    }
}
