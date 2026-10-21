using System.Linq;
using FalloutRemake.Runtime;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private void RefreshArroyoElderGuidance()
    {
        var campaign=_campaign;
        var token=campaign?.TempleToken;
        bool visible=campaign?.CurrentMap==4 && campaign.Location.Elevation==0 && _elevation==0;
        RefreshElderConsultationAction(visible);
        var route=visible ? campaign!.InspectArroyoElderApproach(_walker?.Moving==true) : new(TempleWorldStatus.Unavailable);
        bool arrived=route.TargetHex==campaign?.Location.Hex;
        string label=route.Status switch {
            TempleWorldStatus.Ready=>arrived ? "Anciã ao alcance" : "Ir até a anciã",
            TempleWorldStatus.Busy=>"Caminhando · Esc para parar",
            TempleWorldStatus.Blocked=>"Anciã sem caminho disponível",_=>"Anciã indisponível"};
        _hud.ArroyoElderAction(visible,route.Status==TempleWorldStatus.Ready && !arrived && !InspectorOpen,label);
        _hud.ArroyoElderRequested=()=>
        {
            if (!visible || campaign==null || _campaign!=campaign || campaign.TempleToken!=token || _elevation!=0 || InspectorOpen) return;
            var current=campaign.InspectArroyoElderApproach(_walker?.Moving==true);
            if (current.Status!=TempleWorldStatus.Ready || current.ActorKey==null || current.TargetHex==null
                || current.TargetHex==campaign.Location.Hex || _layout==null) return;
            var actor=_layout.Objects.Single(o=>o.Source.Key==current.ActorKey);
            if (!VisibleObject(actor)) { NavigationStatus("Mostre os personagens para localizar a anciã."); return; }
            SelectObject(actor);
            if (!_walkMode) ToggleWalkMode();
            RequestWalk(current.TargetHex.Value); FrameRoute();
            NavigationStatus("Caminhando até a anciã · Esc para parar");
        };
    }
}
