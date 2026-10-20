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
        await CheckSpeechPersistence(Unchanged);
        _speechWindow.RestartRequested!();
        if (_speechDraft.StepIndex != 0 || _speechProfile.Classify(_speechState.Capture()) != TempleSpeechState.Initial)
            throw new InvalidDataException("Speech restart did not reset the detached state.");
        CloseInventory(); Unchanged();
        if (InspectorOpen || _speechWindow != null) throw new InvalidDataException("World rebuild cleanup retained the Speech modal.");
        GD.Print("TEMPLE_SPEECH_UI_OK: local original MSG, nearby source NPC, five verified choices, explicit rehearsal, readable modal, camera isolation, cancellation/reopen, restart, repeated final action, unchanged world checkpoint/items/XP verified.");
    }
    private async Task CheckSpeechPersistence(Action unchanged)
    {
        string? previous = _speechCheckpointOverride;
        string path = ProjectSettings.GlobalizePath($"res://.local/checkpoints/speech-fixture-{Guid.NewGuid():N}.json");
        _speechCheckpointOverride = path;
        try
        {
            var save = (Button)_speechWindow!.FindChild("SpeechSave", true, false);
            var load = (Button)_speechWindow.FindChild("SpeechLoad", true, false);
            save.EmitSignal(Button.SignalName.Pressed);
            byte[] saved = File.ReadAllBytes(path); unchanged();
            if (_speechProfile!.Classify(TempleSpeechStateFile.Read(path, _speechProfile).Capture()) != TempleSpeechState.Accepted
                || !_speechWindow.StatusText.Contains("arquivo separado") || load.Disabled)
                throw new InvalidDataException("Speech result save did not use the separate validated file.");
            _speechWindow.RestartRequested!(); _speechWindow.ChooseRequested!();
            var draft = _speechDraft!; var token = _speechState!.SessionToken;
            if (!save.Disabled || draft.StepIndex != 1) throw new InvalidDataException("Partial choices were offered as a saved result.");
            _speechWindow.SaveRequested!();
            if (!saved.SequenceEqual(File.ReadAllBytes(path))) throw new InvalidDataException("Partial rehearsal overwrote the saved result.");
            File.WriteAllText(path, "{}"); load.EmitSignal(Button.SignalName.Pressed); unchanged();
            if (_speechState.SessionToken != token || _speechDraft != draft || draft.StepIndex != 1 || !_speechWindow.StatusText.Contains("incompatível"))
                throw new InvalidDataException("Rejected rehearsal file changed the live draft.");
            File.WriteAllBytes(path, saved); load.EmitSignal(Button.SignalName.Pressed); unchanged();
            if (_speechState.SessionToken == token || _speechDraft != null || _speechProfile.Classify(_speechState.Capture()) != TempleSpeechState.Accepted
                || draft.CommitDetached() != TempleSpeechStatus.Cancelled || !_speechWindow.StatusText.Contains("Ensaio carregado"))
                throw new InvalidDataException("Rehearsal load failed to replace the draft independently of the world.");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "speech-loaded.png"));
            var oldSave = _speechWindow.SaveRequested!; var oldLoad = _speechWindow.LoadRequested!;
            CloseSpeechConsultation(); OpenSpeechConsultation();
            token = _speechState!.SessionToken;
            oldSave(); oldLoad(); unchanged();
            if (_speechState.SessionToken != token || _speechDraft!.StepIndex != 0 || !saved.SequenceEqual(File.ReadAllBytes(path)))
                throw new InvalidDataException("Closed Speech window callbacks changed the new rehearsal or file.");
            _speechWindow!.LoadRequested!(); unchanged();
            if (_speechProfile.Classify(_speechState.Capture()) != TempleSpeechState.Accepted) throw new InvalidDataException("Reopened consultation could not load saved result.");
            GD.Print("TEMPLE_SPEECH_SAVE_UI_OK: separate accepted-result file, no partial overwrite, rejected-file draft preservation, fresh token, cancellation of old draft, explicit reopen/load, stale window callbacks, unchanged active world verified.");
        }
        finally
        {
            _speechCheckpointOverride = previous;
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
