using FalloutRemake.Runtime;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private void RefreshTempleVillageGuidance()
    {
        if (_campaign == null) return;
        var route = _campaign.InspectTempleVillageRoute(_walker?.Moving == true);
        bool complete = _campaign.TempleProgress?.Departure.Phase == NpcDeparturePhase.Hidden;
        bool visible = complete && _campaign.CurrentMap == 3 && _elevation == 2 && _campaign.Location.Elevation == 2;
        bool arrived = route.TargetHex == _campaign.Location.Hex;
        bool door = route.Status == TempleWorldStatus.DoorRequired;
        _hud.TempleVillageAction(visible,(route.Status == TempleWorldStatus.Ready && !arrived || door) && !InspectorOpen,
            door ? arrived ? "Selecionar porta · U para abrir" : "Ir à porta de Arroyo" : arrived ? "Na saída · Enter para passar" : "Ir à saída de Arroyo",
            "Caminhe até a porta e abra com U. Depois siga até a passagem e confirme com Enter.");
        var campaign = _campaign; var token = campaign.TempleToken;
        _hud.TempleVillageRequested = () =>
        {
            if (_campaign != campaign || campaign.TempleToken != token || _elevation != 2 || InspectorOpen) return;
            var current = campaign.InspectTempleVillageRoute(_walker?.Moving == true);
            if (current.Status is not (TempleWorldStatus.Ready or TempleWorldStatus.DoorRequired) || current.TargetHex == null) return;
            if (current.DoorKey != null && _layout != null)
                SelectObject(System.Linq.Enumerable.Single(_layout.Objects,o => o.Source.Key == current.DoorKey));
            if (current.TargetHex == campaign.Location.Hex) { NavigationStatus("Porta ao alcance · U para abrir a passagem de Arroyo"); return; }
            if (!_walkMode) ToggleWalkMode();
            RequestWalk(current.TargetHex.Value);
            FrameRoute();
            NavigationStatus("Caminhando até a saída original de Arroyo · Enter atravessa ao chegar");
        };
    }
}
