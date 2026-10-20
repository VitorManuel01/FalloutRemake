using System;
using System.IO;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private TempleSpeechWindow? _speechWindow;
    private TempleSpeechDraft? _speechDraft;
    private BoundedScriptState? _speechState;
    private TempleSpeechProfile? _speechProfile;
    private string? _speechCheckpointOverride;
    private string SpeechCheckpointPath => _speechCheckpointOverride ?? ProjectSettings.GlobalizePath(_test
        ? "res://.local/checkpoints/temple-speech-test.json" : "res://.local/checkpoints/temple-speech.json");
    private string? SpeechBlockedReason()
    {
        if (_selected?.Source.Offset != 425776 || _map?.Source.Sha256 != TempleBarrierEffectProposal.MapHash || _campaign?.CurrentMap != 3)
            return "Selecione o personagem do templo.";
        if (_walker == null || _elevation != 2 || _campaign.Location.Elevation != 2) return "Volte à elevação do personagem.";
        if (_walker.Moving) return "Termine ou pare a caminhada antes de consultar a conversa.";
        if (FalloutGrid.Distance(_walker.CurrentHex, _selected.Source.Header.Tile) > 1) return "Aproxime o personagem a um hex do NPC para consultar a conversa.";
        if (_directory == null || _objectText == null) return "Os textos originais da instalação não estão disponíveis.";
        return null;
    }
    private void RefreshSpeechAction()
    {
        _hud.SpeechRequested = OpenSpeechConsultation;
        bool actor = _selected?.Source.Offset == 425776 && _map?.Source.Sha256 == TempleBarrierEffectProposal.MapHash;
        string? blocked = SpeechBlockedReason();
        _hud.SpeechAction(actor, blocked == null && !InspectorOpen, blocked ?? "Consultar cinco escolhas originais em um ensaio separado da partida.");
    }
    private void CloseSpeechConsultation()
    {
        _speechDraft?.Cancel(); _speechDraft = null; _speechState = null; _speechProfile = null;
        if (_speechWindow != null) { _speechWindow.Hide(); _speechWindow.QueueFree(); _speechWindow = null; }
        RefreshSpeechAction();
    }
    private void OpenSpeechConsultation()
    {
        if (_speechWindow?.Visible == true) { CloseSpeechConsultation(); return; }
        if (InspectorOpen) return;
        string? blocked = SpeechBlockedReason();
        if (blocked != null) { NavigationStatus(blocked); return; }
        try
        {
            var data = new GameData(_directory!); var profile = TempleSpeechProfile.Create(_map!, data.Read);
            var text = new TempleSpeechText(profile, MessageTextFile.Read(data.Read($"text/{_objectText!.Language}/dialog/actemvil.msg")), _objectText.Language);
            ClearRouteHover(); GetTree().Root.GuiEmbedSubwindows = true;
            Guid worldToken = _campaign!.SessionToken;
            var dialog = new TempleSpeechWindow { Size = new(800, 570), Theme = _hud.GetChild<Control>(0).Theme };
            _speechWindow = dialog; _speechProfile = profile;
            void Refresh()
            {
                var state = profile.Classify(_speechState!.Capture());
                dialog.Refresh(_speechDraft, state);
                dialog.PersistenceActions(state == TempleSpeechState.Accepted, File.Exists(SpeechCheckpointPath));
            }
            void Restart()
            {
                if (_speechWindow != dialog || _campaign?.SessionToken != worldToken) return;
                _speechDraft?.Cancel(); _speechState = profile.CreateInitial();
                _speechDraft = TempleSpeechDraft.Begin(profile, _speechState, 5, true)!;
                Refresh();
            }
            dialog.RestartRequested = Restart;
            dialog.ChooseRequested = () =>
            {
                if (_speechWindow != dialog || _campaign?.SessionToken != worldToken || _speechDraft == null || _speechState == null) return;
                if (_speechDraft.ChoicesComplete) return;
                if (SpeechBlockedReason() != null) { dialog.Message("A sessão mudou. Feche e abra o ensaio novamente."); return; }
                var result = _speechDraft.Choose(profile.Step(_speechDraft.StepIndex).ChoiceOffset);
                if (result == TempleSpeechStatus.Ready && _speechDraft.ChoicesComplete) result = _speechDraft.CommitDetached();
                if (result != TempleSpeechStatus.Ready) { dialog.Message("Ensaio desatualizado. Feche e abra novamente."); return; }
                Refresh();
            };
            dialog.SaveRequested = () =>
            {
                if (_speechWindow != dialog || _campaign?.SessionToken != worldToken || _speechState == null
                    || profile.Classify(_speechState.Capture()) != TempleSpeechState.Accepted) return;
                try
                {
                    TempleSpeechStateFile.Write(SpeechCheckpointPath, profile, _speechState);
                    Refresh(); dialog.Notice("Resultado do ensaio salvo em arquivo separado. O checkpoint da partida não foi alterado.");
                }
                catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
                { dialog.Notice("Não foi possível salvar o ensaio. O arquivo anterior e a partida foram preservados."); GD.Print("SPEECH_SAVE_FAILED: " + error.Message); }
            };
            dialog.LoadRequested = () =>
            {
                if (_speechWindow != dialog || _campaign?.SessionToken != worldToken) return;
                try
                {
                    var candidate = TempleSpeechStateFile.Read(SpeechCheckpointPath, profile);
                    _speechDraft?.Cancel(); _speechState = candidate;
                    _speechDraft = TempleSpeechDraft.Begin(profile, candidate, 5, true);
                    Refresh(); dialog.Notice("Ensaio carregado. As escolhas anteriores foram substituídas; a partida permanece no mesmo estado.");
                }
                catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
                { dialog.Notice("Arquivo de ensaio indisponível ou incompatível. As escolhas atuais e a partida foram preservadas."); GD.Print("SPEECH_LOAD_REJECTED: " + error.Message); }
            };
            dialog.Confirmed += CloseSpeechConsultation; dialog.Canceled += CloseSpeechConsultation; dialog.CloseRequested += CloseSpeechConsultation;
            _hud.AddChild(dialog); dialog.Inspect(profile, text); Restart();
            dialog.PopupCenteredClamped(new Vector2I(800, 570), .92f); RefreshSpeechAction();
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            CloseSpeechConsultation(); NavigationStatus("Conversa indisponível: os dados locais não correspondem ao perfil verificado.");
            GD.Print("SPEECH_CONSULTATION_UNAVAILABLE: " + error.Message);
        }
    }
}
