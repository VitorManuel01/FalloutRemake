using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private async Task CheckNpcDepartureUi(Action unchanged)
    {
        string? previous = _departureCheckpointOverride;
        string path = ProjectSettings.GlobalizePath($"res://.local/checkpoints/departure-fixture-{Guid.NewGuid():N}.json");
        _departureCheckpointOverride = path;
        string speech = JsonSerializer.Serialize(_speechState!.Capture());
        void Preserved()
        {
            unchanged();
            if (speech != JsonSerializer.Serialize(_speechState!.Capture())) throw new InvalidDataException("Departure changed the accepted Speech rehearsal.");
        }
        void Press(string name) => ((Button)_departureWindow!.FindChild(name, true, false)).EmitSignal(Button.SignalName.Pressed);
        try
        {
            var speechWindow = _speechWindow!;
            var entry = (Button)speechWindow.FindChild("SpeechDeparture", true, false);
            if (!entry.Visible || entry.Disabled) throw new InvalidDataException("Accepted Speech did not offer departure rehearsal.");
            entry.EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_departureWindow == null || _departure == null || !InspectorOpen || speechWindow.Visible || !_departureWindow.ReadableLayout)
                throw new InvalidDataException("Departure modal failed to replace consultation or provide a readable diagram.");
            CheckDialogBounds(_departureWindow); Preserved();
            var speechToken = _speechState.SessionToken;
            speechWindow.RestartRequested!(); speechWindow.ChooseRequested!(); speechWindow.SaveRequested!(); speechWindow.LoadRequested!();
            if (_speechState.SessionToken != speechToken) throw new InvalidDataException("Hidden conversation callbacks changed the accepted state during departure.");
            Preserved();
            Vector3 camera = _camera.Position;
            using var rotate = new InputEventKey { Pressed = true, PhysicalKeycode = Key.E };
            _UnhandledInput(rotate);
            if (_camera.Position != camera) throw new InvalidDataException("Departure modal leaked camera input.");
            var oldStep = _departureWindow.StepRequested!;
            Press("DepartureStep"); var stepped = _departure.Capture(); oldStep();
            if (stepped.RouteIndex != 1 || _departure.Capture() != stepped) throw new InvalidDataException("Departure action replay advanced more than one hex.");
            Preserved(); Press("DepartureLeg");
            if (_departure.Capture().Phase != NpcDeparturePhase.GateDelay || _departure.Capture().RemainingTicks != 20
                || !_departureWindow.StateText.Contains("Porta aberta") || !((Button)_departureWindow.FindChild("DepartureStep", true, false)).Disabled)
                throw new InvalidDataException("Departure gate arrival did not stop at the timer.");
            Press("DepartureTick"); Preserved();
            if (_departure.Capture().RemainingTicks != 10) throw new InvalidDataException("Departure one-second action used the wrong clock.");
            Press("DepartureSave"); byte[] saved = File.ReadAllBytes(path);
            if (!_departureWindow.NoticeText.Contains("arquivo separado")) throw new InvalidDataException("Departure save did not report its separate file.");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "departure-wait.png"));
            var oldEvent = _departureWindow.EventRequested!;
            Press("DepartureEvent"); var fired = _departure.Capture(); oldEvent();
            if (fired.Phase != NpcDeparturePhase.ToMiddle || _departure.Capture() != fired)
                throw new InvalidDataException("Departure timer event was replayed or moved the actor.");
            Press("DepartureLeg");
            if (_departure.Capture().Phase != NpcDeparturePhase.ToExit || _departure.Location.Hex != 22712)
                throw new InvalidDataException("Departure skipped its intermediate stop.");
            Press("DepartureLeg"); Preserved();
            if (_departure.Capture().Phase != NpcDeparturePhase.Hidden || _departure.Location.Hex != 28715
                || !_departureWindow.StateText.Contains("sem bloqueio")) throw new InvalidDataException("Departure final visibility is inconsistent.");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "departure-hidden.png"));
            var live = _departure; File.WriteAllText(path, "{}"); Press("DepartureLoad");
            if (_departure != live || !_departureWindow.NoticeText.Contains("incompatível")) throw new InvalidDataException("Rejected departure file changed the live rehearsal.");
            File.WriteAllBytes(path, saved); Press("DepartureLoad"); Preserved();
            if (_departure.SessionToken == live.SessionToken || _departure.Capture().Phase != NpcDeparturePhase.GateDelay
                || _departure.Capture().RemainingTicks != 10 || _departure.Location.Hex != 19728)
                throw new InvalidDataException("Departure load did not restore pose and partial wait together.");
            var loaded = _departure.Capture(); oldStep(); oldEvent();
            if (_departure.Capture() != loaded) throw new InvalidDataException("Old departure actions mutated the loaded session.");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "departure-loaded.png"));
            Press("DepartureRestart"); Preserved();
            if (_departure.Capture().Phase != NpcDeparturePhase.ToGate || _departure.Location.Hex != 13728)
                throw new InvalidDataException("Departure restart did not restore the initial copied pose.");
            var staleSave = _departureWindow.SaveRequested!; var staleLoad = _departureWindow.LoadRequested!;
            var staleRestart = _departureWindow.RestartRequested!;
            _departureWindow.EmitSignal(AcceptDialog.SignalName.Confirmed);
            if (_departureWindow != null || _departure != null || !speechWindow.Visible || !InspectorOpen)
                throw new InvalidDataException("Departure return did not restore the accepted consultation.");
            entry.EmitSignal(Button.SignalName.Pressed);
            var reopened = _departure!; staleSave(); staleLoad(); staleRestart();
            if (_departure != reopened || _departure.Revision != 0 || !saved.SequenceEqual(File.ReadAllBytes(path)))
                throw new InvalidDataException("Closed departure callbacks changed a reopened rehearsal or saved file.");
            CloseNpcDeparture(true); Preserved();
            GD.Print("TEMPLE_NPC_DEPARTURE_UI_OK: accepted Speech entry, diagram/modal bounds, isolated input, adjacent step and leg stops, one-shot timer, separate save/load with partial wait, rejected-file rollback, fresh tokens, old callbacks, return/reopen, unchanged Speech and world verified.");
        }
        finally
        {
            CloseNpcDeparture(true); _departureCheckpointOverride = previous;
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
