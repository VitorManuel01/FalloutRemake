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
        if (_campaign.TempleProgress != null) return "Conversa já aceita nesta partida. Use Saída do NPC para acompanhar a sequência.";
        if (_walker.Moving) return "Termine ou pare a caminhada antes de consultar a conversa.";
        if (FalloutGrid.Distance(_walker.CurrentHex, _selected.Source.Header.Tile) > 1) return "Aproxime o personagem a um hex do NPC para consultar a conversa.";
        if (_directory == null || _objectText == null) return "Os textos originais da instalação não estão disponíveis.";
        if (!_campaign.Character.QualifiesForTempleSpeech)
            return $"{_campaign.Character.Name} não tem Speech marcado (ou INT abaixo de 4). No original, o caminho é o desafio desarmado de Cameron (botão Desafio de Cameron).";
        return null;
    }
    private int SpeechIntelligence => _campaign?.Character.Effective.Intelligence ?? 5;
    private void RefreshSpeechAction()
    {
        RefreshActiveTempleHud();
        _hud.SpeechRequested = OpenSpeechConsultation;
        _hud.NpcApproachRequested = ApproachTempleNpc; _hud.DuelRequested = StartTempleDuel; _hud.TempleKeyRequested = UseTempleKeyOnSelection;
        bool actor = _selected?.Source.Offset == 425776 && _map?.Source.Sha256 == TempleBarrierEffectProposal.MapHash;
        string? blocked = SpeechBlockedReason();
        _hud.SpeechAction(actor, blocked == null && !InspectorOpen, blocked ?? "Consultar cinco escolhas originais em um ensaio separado da partida.");
        RefreshDuelAction(actor); RefreshTempleKeyAction();
        int? target = TempleNpcApproachTarget();
        _hud.NpcApproachAction(actor, target.HasValue && target != _walker?.CurrentHex,
            target.HasValue ? "Caminhar até um hex ao lado do NPC. A conversa é aberta separadamente."
                : "Requer a elevação 2, caminho livre, nenhuma caminhada ou janela aberta. Abra a porta final antes de se aproximar.");
    }
    private void CloseSpeechConsultation()
    {
        CloseNpcDeparture();
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
            bool Active() => _speechWindow == dialog && _campaign?.SessionToken == worldToken && _departureWindow == null;
            dialog.DepartureRequested = () => { if (_speechWindow == dialog && _campaign?.SessionToken == worldToken) OpenNpcDeparture(); };
            void Refresh()
            {
                var state = profile.Classify(_speechState!.Capture());
                dialog.Refresh(_speechDraft, state);
                dialog.PersistenceActions(state == TempleSpeechState.Accepted, File.Exists(SpeechCheckpointPath));
                dialog.ApplyAction(state == TempleSpeechState.Accepted && _campaign?.TempleProgress == null);
            }
            void Restart()
            {
                if (!Active()) return;
                _speechDraft?.Cancel(); _speechState = profile.CreateInitial();
                _speechDraft = TempleSpeechDraft.Begin(profile, _speechState, SpeechIntelligence, _campaign!.Character.TempleSpeechTag)!;
                Refresh();
            }
            dialog.RestartRequested = Restart;
            dialog.ApplyRequested = () =>
            {
                if (!Active() || _speechState == null || SpeechBlockedReason() != null) return;
                try
                {
                    var departureProfile = TempleNpcDepartureProfile.Create(_map!,data.Read);
                    var proposal = _campaign!.PrepareTempleSpeech(departureProfile,_speechState.Capture(),_walker?.Moving == true,SpeechIntelligence,_campaign.Character.TempleSpeechTag);
                    if (proposal.Status != TempleWorldStatus.Ready || _campaign.CommitTempleSpeech(proposal.Plan,_walker?.Moving == true) != TempleWorldStatus.Ready)
                    { dialog.Notice("A situação mudou. Feche e consulte novamente antes de aplicar a conversa."); return; }
                    CloseSpeechConsultation(); RefreshActiveTempleProjection();
                    NavigationStatus("Speech aceito na partida · saída manual disponível · K para salvar");
                }
                catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
                { dialog.Notice("Os dados locais não estão disponíveis para aplicar a conversa. O resultado do ensaio foi preservado."); GD.Print("SPEECH_APPLY_UNAVAILABLE: " + error.Message); }
            };
            dialog.ChooseRequested = () =>
            {
                if (!Active() || _speechDraft == null || _speechState == null) return;
                if (_speechDraft.ChoicesComplete) return;
                if (SpeechBlockedReason() != null) { dialog.Message("A sessão mudou. Feche e abra o ensaio novamente."); return; }
                var result = _speechDraft.Choose(profile.Step(_speechDraft.StepIndex).ChoiceOffset);
                if (result == TempleSpeechStatus.Ready && _speechDraft.ChoicesComplete) result = _speechDraft.CommitDetached();
                if (result != TempleSpeechStatus.Ready) { dialog.Message("Ensaio desatualizado. Feche e abra novamente."); return; }
                Refresh();
            };
            dialog.SaveRequested = () =>
            {
                if (!Active() || _speechState == null
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
                if (!Active()) return;
                try
                {
                    var candidate = TempleSpeechStateFile.Read(SpeechCheckpointPath, profile);
                    _speechDraft?.Cancel(); _speechState = candidate;
                    _speechDraft = TempleSpeechDraft.Begin(profile, candidate, SpeechIntelligence, _campaign!.Character.TempleSpeechTag);
                    Refresh(); dialog.Notice("Ensaio carregado. As escolhas anteriores foram substituídas; a partida permanece no mesmo estado.");
                }
                catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
                { dialog.Notice("Arquivo de ensaio indisponível ou incompatível. As escolhas atuais e a partida foram preservadas."); GD.Print("SPEECH_LOAD_REJECTED: " + error.Message); }
            };
            void Close() { if (_speechWindow == dialog) CloseSpeechConsultation(); }
            dialog.Confirmed += Close; dialog.Canceled += Close; dialog.CloseRequested += Close;
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
