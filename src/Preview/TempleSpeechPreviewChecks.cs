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
    private async Task CheckSpeechConsultation()
    {
        byte[] State() => CheckpointFile.Encode(_campaign!.Capture(_mapCatalog!.Source.Sha256));
        byte[] before = State();
        void Unchanged() { if (!before.SequenceEqual(State())) throw new InvalidDataException("Speech consultation changed the active world."); }
        var button = (Button)_hud.FindChild("SpeechAction", true, false);
        if (!button.Visible || button.Disabled) throw new InvalidDataException("Nearby NPC consultation action is unavailable.");
        var data = new GameData(_directory!);
        var file = MessageTextFile.Read(data.Read($"text/{_objectText!.Language}/dialog/actemvil.msg"));
        button.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!InspectorOpen || _speechWindow == null || !_speechWindow.ReadableLayout || _speechDraft?.StepIndex != 0
            || !_speechWindow.ReplyText.Contains(file.Entries[103].Text) || !_speechWindow.ReplyText.Contains(file.Entries[104].Text)
            || _speechWindow.ChoiceText != file.Entries[108].Text)
            throw new InvalidDataException("Speech consultation lost source text, modal isolation or readable layout.");
        CheckDialogBounds(_speechWindow);
        Vector3 camera = _camera.Position;
        using var rotate = new InputEventKey { Pressed = true, PhysicalKeycode = Key.E };
        _UnhandledInput(rotate);
        if (_camera.Position != camera) throw new InvalidDataException("Speech modal leaked camera input.");
        if (_capture != null) await Capture(Path.ChangeExtension(_capture, "speech-consultation.png"));
        _speechWindow.ChooseRequested!(); _speechWindow.ChooseRequested!(); Unchanged();
        if (_speechDraft.StepIndex != 2 || _speechState!.Revision != 0) throw new InvalidDataException("Speech draft wrote before completion.");
        _speechWindow.EmitSignal(AcceptDialog.SignalName.Canceled);
        if (InspectorOpen || _speechState != null || _speechDraft != null) throw new InvalidDataException("Speech cancellation retained a draft.");
        Unchanged(); button.EmitSignal(Button.SignalName.Pressed);
        if (_speechDraft?.StepIndex != 0) throw new InvalidDataException("Reopening resumed cancelled choices.");
        for (int i = 0; i < 5; i++) { _speechWindow!.ChooseRequested!(); Unchanged(); }
        if (!_speechDraft!.ChoicesComplete || _speechProfile!.Classify(_speechState!.Capture()) != TempleSpeechState.Accepted
            || !_speechWindow!.StatusText.Contains("mesmo lugar")) throw new InvalidDataException("Speech consultation did not finish its detached route.");
        _speechWindow.ChooseRequested!(); Unchanged();
        if (_capture != null) await Capture(Path.ChangeExtension(_capture, "speech-result.png"));
        _speechWindow.RestartRequested!();
        if (_speechDraft.StepIndex != 0 || _speechProfile.Classify(_speechState.Capture()) != TempleSpeechState.Initial)
            throw new InvalidDataException("Speech restart did not reset the detached state.");
        CloseInventory(); Unchanged();
        if (InspectorOpen || _speechWindow != null) throw new InvalidDataException("World rebuild cleanup retained the Speech modal.");
        GD.Print("TEMPLE_SPEECH_UI_OK: local original MSG, nearby source NPC, five verified choices, explicit rehearsal, readable modal, camera isolation, cancellation/reopen, restart, repeated final action, unchanged world checkpoint/items/XP verified.");
    }
}
