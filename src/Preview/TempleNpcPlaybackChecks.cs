using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private async Task CheckTemplePlayback(byte[] start, Action checkPose)
    {
        void Press(string name) => ((Button)_hud.FindChild(name,true,false)).EmitSignal(Button.SignalName.Pressed);
        void Toggle(bool enabled)
        {
            ((CheckButton)_hud.FindChild("TempleWorldAutomatic",true,false)).ButtonPressed = enabled;
            if (enabled) Tick(0); // No time before the enabling click belongs to playback.
        }
        void Tick(double seconds)
        {
            bool checking = _checkingNavigation;
            try { _checkingNavigation = false; AdvanceTemplePlayback(seconds); }
            finally { _checkingNavigation = checking; }
        }
        byte[] State() => CheckpointFile.Encode(_campaign!.Capture(_mapCatalog!.Source.Sha256));
        CheckpointFile.Write(CheckpointPath,CheckpointFile.Decode(start)); LoadCheckpoint(); checkPose();
        if (_templeAutomatic) throw new InvalidDataException("Loading unexpectedly enabled NPC playback.");
        Toggle(true); byte[] before = State(); Tick(.199);
        if (!_templeAutomatic || !before.SequenceEqual(State())) throw new InvalidDataException("Automatic NPC moved before one complete step interval.");
        Tick(.001); checkPose();
        if (before.SequenceEqual(State())) throw new InvalidDataException("Automatic NPC failed to commit its first complete step.");
        var progress = _campaign!.TempleProgress!; string actorKey = progress.Scripts.Blocks[0].Key;
        SelectObject(_layout!.Objects.Single(o => o.Source.Key == actorKey)); OpenInventory(); before = State(); Tick(10);
        if (!InspectorOpen || !before.SequenceEqual(State())) throw new InvalidDataException("NPC playback advanced through an inventory modal.");
        CloseInventory();
        var nav = _doors!.Navigation(2); var reach = nav.ReachableFrom(_walker!.CurrentHex);
        int safe = Enumerable.Range(0,40000).Where(t => reach.Distance(t) == 1 && !nav.IsExit(t)).First();
        RequestWalk(safe); _walker.Advance(.05); before = State(); Tick(3);
        if (!before.SequenceEqual(State())) throw new InvalidDataException("Automatic NPC advanced during player walking.");
        CompleteReadabilityWalk();
        TryInspectionElevation(1); before = State(); Tick(10);
        if (!before.SequenceEqual(State())) throw new InvalidDataException("NPC playback advanced during inspection of another elevation.");
        TryInspectionElevation(2); long revision = _campaign.TempleRevision; Tick(100); checkPose();
        if (_campaign.TempleRevision <= revision || _campaign.TempleRevision - revision > 16)
            throw new InvalidDataException("NPC playback did not bound catch-up work per frame.");
        Toggle(false); Toggle(true);
        for (int guard = 0; _campaign.TempleProgress!.Departure.Phase != NpcDeparturePhase.GateDelay && guard < 100; guard++) Tick(.2);
        if (_campaign.TempleProgress!.Departure.Phase != NpcDeparturePhase.GateDelay || _campaign.TempleProgress.Departure.RemainingTicks != 20)
            throw new InvalidDataException("NPC playback failed to reach the gate before starting its timer.");
        Tick(.7); checkPose();
        if (_campaign.TempleProgress.Departure.RemainingTicks != 13) throw new InvalidDataException("NPC playback lost native timer granularity.");
        Press("TempleWorldFocus"); Press("TempleProgressAction");
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        if (_capture != null) await Capture(Path.ChangeExtension(_capture,"active-npc-automatic.png"));
        SaveCheckpoint(); byte[] saved = State();
        if (_templeAutomatic || ((CheckButton)_hud.FindChild("TempleWorldAutomatic",true,false)).ButtonPressed)
            throw new InvalidDataException("Saving did not pause automatic NPC playback.");
        Toggle(true); Tick(1); LoadCheckpoint(); checkPose();
        if (_templeAutomatic || !saved.SequenceEqual(State()) || _campaign.TempleProgress!.Departure.RemainingTicks != 13)
            throw new InvalidDataException("Load did not restore the partial wait into paused playback.");
        Toggle(true); Tick(1.3);
        if (_campaign.TempleProgress.Departure.Phase != NpcDeparturePhase.ToMiddle)
            throw new InvalidDataException("Automatic native event did not release the second leg.");
        for (int guard = 0; _campaign.TempleProgress.Departure.Phase != NpcDeparturePhase.Hidden && guard < 100; guard++) Tick(.2);
        checkPose();
        if (_campaign.TempleProgress.Departure.Phase != NpcDeparturePhase.Hidden || _templeAutomatic)
            throw new InvalidDataException("Automatic playback did not stop after the NPC became hidden.");
        GD.Print("TEMPLE_NPC_AUTOMATIC_OK: explicit opt-in, fractional frame clock, native timer ticks, modal/walking/inspection pauses, bounded catch-up, save/load pause and partial wait, final hidden stop, source pose/items/25 XP preserved.");
    }
}
