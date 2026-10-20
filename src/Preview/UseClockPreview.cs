using System;
using System.Linq;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private UseClockWindow? _useClock;
    private WorldItemUse[] PlacedClockUses => _campaign?.ItemUses.Where(u => u.Allocation.Phase == ItemUsePhase.Placed).ToArray() ?? Array.Empty<WorldItemUse>();
    private void CloseUseClock()
    {
        if (_useClock == null) return;
        _useClock.Hide(); _useClock.QueueFree(); _useClock = null;
    }
    private string? ClockBlockedReason()
    {
        if (_campaign == null || PlacedClockUses.Length == 0) return "Posicione um explosivo para consultar seu prazo.";
        if (_campaign.DueItemUses.Length != 0) return "Prazo vencido. Resolva os eventos pendentes antes de avançar novamente.";
        if (_walker?.Moving == true) return "Termine ou pare a caminhada antes de avançar o tempo.";
        if (_elevation != _campaign.Location.Elevation || _walker == null) return "Volte à elevação do personagem para avançar o tempo.";
        if (InspectorOpen && _useClock?.Visible != true) return "Feche a outra janela antes de avançar o tempo.";
        return null;
    }
    private void RefreshUseClock()
    {
        WorldItemUse[] uses = PlacedClockUses;
        _hud.UseClockAction(uses.Length > 0 && _walker?.Moving != true, uses.Any(u => u.Allocation.RemainingTicks == 0));
        _useClock?.Refresh(uses, MapDisplayName, ClockBlockedReason());
    }
    private void OpenUseClock()
    {
        if (_useClock?.Visible == true) { CloseUseClock(); return; }
        if (InspectorOpen || _campaign == null || PlacedClockUses.Length == 0 || _walker?.Moving == true) return;
        ClearRouteHover(); GetTree().Root.GuiEmbedSubwindows = true;
        Guid token = _campaign.SessionToken;
        var dialog = new UseClockWindow { Size = new(720, 500), Theme = _hud.GetChild<Control>(0).Theme };
        _useClock = dialog;
        dialog.ReviewRequested = id => { if (_useClock == dialog && _campaign?.SessionToken == token) ReviewPendingEffect(id); };
        dialog.AdvanceRequested = ticks =>
        {
            if (_useClock != dialog || _campaign?.SessionToken != token || ticks is not (10 or 100)) return;
            string? blocked = ClockBlockedReason();
            if (blocked != null) { RefreshUseClock(); return; }
            UseTimeAdvance step = _campaign.AdvanceUseTime(ticks);
            ClearRouteHover(); RefreshPlacedUses(); UpdateCamera();
            string message = step.Due.Length > 0
                ? $"Avançou {UseClockWindow.Duration(step.AppliedTicks)} dos {UseClockWindow.Duration(ticks)} solicitados. Prazo vencido; avanço bloqueado."
                : $"Avançou {UseClockWindow.Duration(step.AppliedTicks)}. Os prazos estão pausados novamente.";
            NavigationStatus(message);
            dialog.Refresh(PlacedClockUses, MapDisplayName, ClockBlockedReason(), message);
        };
        dialog.Confirmed += CloseUseClock; dialog.Canceled += CloseUseClock; dialog.CloseRequested += CloseUseClock;
        _hud.AddChild(dialog); RefreshUseClock(); dialog.PopupCenteredClamped(new Vector2I(720, 500), .92f);
    }
}
