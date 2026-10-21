using FalloutRemake.Runtime;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private void RefreshTempleVillageGuidance()
    {
        if (_campaign == null) return;
        bool returning = _campaign.CurrentMap == 4;
        var route = returning ? _campaign.InspectArroyoReturnRoute(_walker?.Moving == true) : _campaign.InspectTempleVillageRoute(_walker?.Moving == true);
        bool complete = _campaign.TempleProgress?.Departure.Phase == NpcDeparturePhase.Hidden;
        bool visible = returning ? _campaign.TempleProgress != null && _elevation == 0 && _campaign.Location.Elevation == 0
            : complete && _campaign.CurrentMap == 3 && _elevation == 2 && _campaign.Location.Elevation == 2;
        bool arrived = route.TargetHex == _campaign.Location.Hex;
        bool door = route.Status == TempleWorldStatus.DoorRequired;
        string label = route.Status switch {
            TempleWorldStatus.DoorRequired => arrived ? "Selecionar porta · U para abrir" : "Ir à porta de Arroyo",
            TempleWorldStatus.Ready => arrived ? "Na saída · Enter para passar" : returning ? "Voltar ao templo" : "Ir à saída de Arroyo",
            TempleWorldStatus.Busy => "Caminhando · Esc para parar",
            TempleWorldStatus.Blocked => "Passagem sem caminho disponível",
            _ => returning ? "Volta ao templo indisponível" : "Saída de Arroyo indisponível" };
        _hud.TempleVillageAction(visible,(route.Status == TempleWorldStatus.Ready && !arrived || door) && !InspectorOpen,
            label,
            returning ? "Caminhe até a passagem original de volta ao templo e confirme com Enter. A recompensa não se repete."
                : "Caminhe até a porta e abra com U. Depois siga até a passagem e confirme com Enter.");
        var campaign = _campaign; var token = campaign.TempleToken;
        int expectedElevation = returning ? 0 : 2;
        _hud.TempleVillageRequested = () =>
        {
            if (_campaign != campaign || campaign.TempleToken != token || _elevation != expectedElevation || InspectorOpen) return;
            var current = returning ? campaign.InspectArroyoReturnRoute(_walker?.Moving == true) : campaign.InspectTempleVillageRoute(_walker?.Moving == true);
            if (current.Status is not (TempleWorldStatus.Ready or TempleWorldStatus.DoorRequired) || current.TargetHex == null) return;
            if (current.DoorKey != null && _layout != null)
                SelectObject(System.Linq.Enumerable.Single(_layout.Objects,o => o.Source.Key == current.DoorKey));
            if (current.TargetHex == campaign.Location.Hex) { NavigationStatus("Porta ao alcance · U para abrir a passagem de Arroyo"); return; }
            if (!_walkMode) ToggleWalkMode();
            RequestWalk(current.TargetHex.Value);
            FrameRoute();
            NavigationStatus(current.DoorKey != null ? "Caminhando até a porta de Arroyo · U abre ao chegar · Esc para parar"
                : returning ? "Caminhando até a volta ao templo · Enter atravessa ao chegar · Esc para parar"
                    : "Caminhando até a saída original de Arroyo · Enter atravessa ao chegar · Esc para parar");
        };
    }
}
