using System;
using System.IO;
using System.Threading.Tasks;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private RestWindow? _restWindow;
    // CE's pipboy_available_at_game_start, for this session only (a game option, not part of a save).
    private bool _pipBoyFromStart;

    private void CloseRestWindow() { if (_restWindow != null) { _restWindow.Hide(); _restWindow.QueueFree(); _restWindow = null; } }

    // The rules a rest reads from outside the world: the catalog's can_rest_here and the Pip-Boy option.
    private void PrepareRest()
    {
        if (_campaign == null) return;
        _campaign.PipBoyFromStart = _pipBoyFromStart;
        _campaign.CanRestOn = (map, elevation) => _mapCatalog?.Get(map).CanRest(elevation) ?? true;
    }

    private void OpenRestWindow()
    {
        if (_restWindow?.Visible == true) { CloseRestWindow(); return; }
        if (_campaign == null || CombatRefuses("descansar")) return;
        CloseBarrierEffectReview(); CloseUseClock(); CloseExplosivePreparation(); CloseInventory(); CloseLoot(); CloseBag(); ClearRouteHover();
        GetTree().Root.GuiEmbedSubwindows = true;
        var dialog = new RestWindow { Size = new Vector2I(640, 360), Theme = _hud.GetChild<Control>(0).Theme };
        _restWindow = dialog; _hud.AddChild(dialog);
        dialog.Confirmed += CloseRestWindow; dialog.Canceled += CloseRestWindow; dialog.CloseRequested += CloseRestWindow;
        dialog.RestRequested = (hours, minutes) => { if (_restWindow == dialog) RestFor(hours, minutes); };
        dialog.RestUntilHealedRequested = () => { if (_restWindow == dialog) RestFor(null, 0); };
        dialog.PipBoyToggled = value => { if (_restWindow == dialog) { _pipBoyFromStart = value; RefreshRestWindow(); } };
        // Opening the Pip-Boy starts its rest count over (pipboyOpen).
        PrepareRest(); _campaign.OpenPipBoy();
        RefreshRestWindow();
        dialog.PopupCentered(new Vector2I(640, 360));
    }

    private string RestClock()
    {
        long ticks = _campaign!.GameTime;
        long day = ticks / CriticalRules.TicksPerDay, minute = ticks % CriticalRules.TicksPerDay / 600;
        return $"Dia {day} · {minute / 60:00}:{minute % 60:00} · HP {_campaign.HitPoints}/{_campaign.MaxHitPoints} · cura {_campaign.Sheet.HealingRate} a cada 3 horas";
    }

    private void RefreshRestWindow(string? message = null)
    {
        if (_restWindow == null || _campaign == null) return;
        PrepareRest();
        _restWindow.Refresh(_campaign.CanRest(_walker?.Moving == true), _pipBoyFromStart, RestClock(), message);
    }

    private void RestFor(int? hours, int minutes)
    {
        if (_campaign == null) return;
        PrepareRest();
        RestResult result = hours is int h ? _campaign.Rest(h, minutes, _walker?.Moving == true) : _campaign.RestUntilHealed(_walker?.Moving == true);
        if (result.Died) { CloseRestWindow(); ResetWalker(); NavigationStatus("Você morreu envenenado durante o descanso · a prévia voltou ao início do Templo."); return; }
        string done = result.Status != RestStatus.Ready ? RestWindow.Hint(result.Status)
            : $"Descansou {result.Minutes / 60} h {result.Minutes % 60:00} min: +{result.Healed} HP{(result.PoisonLost > 0 ? $", o veneno tirou {result.PoisonLost}" : "")}.";
        NavigationStatus(done);
        RefreshRestWindow(done); RefreshDoorAction();
    }

    /// <summary>
    /// The rest window in the preview: inside the Temple the Pip-Boy is missing (the original gives it with the vault suit);
    /// with CE's option the caves still refuse, since maps.txt forbids rest there while creatures live.
    /// </summary>
    private async Task CheckRestWindow()
    {
        bool checking = _checkingNavigation, option = _pipBoyFromStart; _checkingNavigation = true;
        try
        {
            ResetWalker(); _pipBoyFromStart = false;
            OpenRestWindow();
            if (_restWindow == null || _restWindow.RestEnabled || !_restWindow.StatusText.Contains("Pip-Boy")) throw new InvalidDataException("Without the Pip-Boy the rest is not refused: " + _restWindow?.StatusText);
            _restWindow.TogglePipBoy(true);
            if (!_pipBoyFromStart || _restWindow.RestEnabled || !_restWindow.StatusText.Contains("maps.txt"))
                throw new InvalidDataException("The Temple's caves do not refuse the rest: " + _restWindow.StatusText);
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "rest.png"));
            CloseRestWindow();
            GD.Print("REST_OK: without the vault suit the Pip-Boy is missing; with CE's option the Temple's caves still refuse rest (maps.txt can_rest_here=No with creatures alive).");
        }
        finally { CloseRestWindow(); _pipBoyFromStart = option; _checkingNavigation = checking; }
    }
}
