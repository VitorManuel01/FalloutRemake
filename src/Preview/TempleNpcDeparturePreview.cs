using System;
using System.IO;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private TempleNpcDepartureWindow? _departureWindow;
    private TempleNpcDeparture? _departure;
    private string? _departureCheckpointOverride;
    private string DepartureCheckpointPath => _departureCheckpointOverride ?? ProjectSettings.GlobalizePath(_test
        ? "res://.local/checkpoints/temple-departure-test.json" : "res://.local/checkpoints/temple-departure.json");
    private void CloseNpcDeparture(bool returnToSpeech = false)
    {
        _departure = null;
        if (_departureWindow != null) { _departureWindow.Hide(); _departureWindow.QueueFree(); _departureWindow = null; }
        if (returnToSpeech && _speechWindow != null) _speechWindow.PopupCenteredClamped(new(800, 570), .92f);
    }
    private void OpenNpcDeparture()
    {
        if (_departureWindow != null || _speechWindow?.Visible != true || _speechState == null || _speechProfile == null
            || _speechProfile.Classify(_speechState.Capture()) != TempleSpeechState.Accepted || SpeechBlockedReason() != null) return;
        try
        {
            var data = new GameData(_directory!); var profile = TempleNpcDepartureProfile.Create(_map!, data.Read);
            var session = profile.Begin(_speechState.Capture());
            var parent = _speechWindow; Guid worldToken = _campaign!.SessionToken;
            var dialog = new TempleNpcDepartureWindow { Size = new(780, 620), Theme = _hud.GetChild<Control>(0).Theme };
            _departure = session; _departureWindow = dialog;
            bool Valid() => _departureWindow == dialog && _speechWindow == parent && _campaign?.SessionToken == worldToken && _departure != null;
            void Refresh()
            {
                if (!Valid()) return;
                var current = _departure!; Guid token = current.SessionToken; long revision = current.Revision;
                bool Current() => Valid() && _departure == current && current.SessionToken == token && current.Revision == revision;
                void Move(bool wholeLeg)
                {
                    if (!Current()) return;
                    var phase = current.Capture().Phase; NpcDepartureStatus result;
                    // This isolated copy has no active player; runtime occupancy guards are tested independently.
                    do { result = current.Step(token, current.Revision); }
                    while (wholeLeg && result == NpcDepartureStatus.Ready && current.Walking && current.Capture().Phase == phase);
                    Refresh();
                    if (result != NpcDepartureStatus.Ready) dialog.Notice("O percurso não avançou. Recomece ou carregue o ensaio.");
                }
                void Wait(bool untilEvent)
                {
                    if (!Current()) return;
                    var result = current.AdvanceDelay(untilEvent ? current.Capture().RemainingTicks : 10, token, revision);
                    Refresh();
                    if (result.Status != NpcDepartureStatus.Ready) dialog.Notice("A espera não avançou. Este evento só fica disponível junto à porta.");
                }
                dialog.StepRequested = () => Move(false); dialog.LegRequested = () => Move(true);
                dialog.TickRequested = () => Wait(false); dialog.EventRequested = () => Wait(true);
                dialog.Refresh(current, File.Exists(DepartureCheckpointPath));
            }
            dialog.RestartRequested = () =>
            {
                if (!Valid()) return;
                _departure = profile.Begin(_speechState!.Capture()); Refresh();
            };
            dialog.SaveRequested = () =>
            {
                if (!Valid()) return;
                try { TempleNpcDepartureFile.Write(DepartureCheckpointPath, _departure!); Refresh(); dialog.Notice("Saída salva em arquivo separado da partida e do resultado Speech."); }
                catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
                { dialog.Notice("Não foi possível salvar a saída. O arquivo anterior foi preservado."); GD.Print("DEPARTURE_SAVE_FAILED: " + error.Message); }
            };
            dialog.LoadRequested = () =>
            {
                if (!Valid()) return;
                try
                {
                    var candidate = TempleNpcDepartureFile.Read(DepartureCheckpointPath, profile);
                    _departure = candidate; Refresh(); dialog.Notice("Saída carregada. Posição, porta, bloqueio e espera foram restaurados juntos.");
                }
                catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
                { dialog.Notice("Arquivo de saída indisponível ou incompatível. O ensaio atual foi preservado."); GD.Print("DEPARTURE_LOAD_REJECTED: " + error.Message); }
            };
            void Return() { if (Valid()) CloseNpcDeparture(true); }
            dialog.Confirmed += Return; dialog.Canceled += Return; dialog.CloseRequested += Return;
            parent.Hide(); _hud.AddChild(dialog); dialog.PopupCenteredClamped(new(780, 620), .92f); Refresh();
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            CloseNpcDeparture(true); _speechWindow?.Notice("Saída indisponível: os dados locais não correspondem ao perfil verificado.");
            GD.Print("DEPARTURE_UNAVAILABLE: " + error.Message);
        }
    }
}
