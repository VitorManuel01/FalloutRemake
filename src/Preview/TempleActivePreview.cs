using System;
using System.Linq;
using FalloutRemake.Geometry;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private PreviewLayout ProjectTempleLayout(PreviewLayout layout)
    {
        if (_campaign?.TempleProgress == null || _campaign.CurrentMap != 3) return layout;
        var roots = _campaign.Current.Map.ObjectLayers.SelectMany(l => l.Objects).ToDictionary(o => o.Key);
        return layout with { Objects = layout.Objects.Select(obj =>
        {
            var source = roots[obj.Source.Key];
            var effective = _campaign.EffectiveObject(_campaign.CurrentMap,source);
            return obj with { Source = effective, Position = FalloutGrid.HexCenter(effective.Header.Tile) - layout.Origin,
                Hidden = (effective.Header.Flags & HexNavigation.Hidden) != 0 };
        }).ToArray() };
    }
    private void RefreshActiveTempleHud()
    {
        var progress = _campaign?.TempleProgress;
        bool present = progress != null; bool current = present && _campaign!.CurrentMap == 3 && _campaign.Location.Elevation == 2 && _elevation == 2;
        bool enabled = current && _walker?.Moving == false && !InspectorOpen;
        var phase = progress?.Departure.Phase;
        string text = phase switch { NpcDeparturePhase.ToGate => "Indo até a porta", NpcDeparturePhase.GateDelay => $"Junto à porta · espera {progress!.Departure.RemainingTicks / 10.0:0.0} s",
            NpcDeparturePhase.ToMiddle => "Trecho intermediário", NpcDeparturePhase.ToExit => "Indo até a saída", NpcDeparturePhase.Hidden => "Saída concluída · NPC oculto", _ => "" };
        text += current ? "\nAvanço manual · K salva a partida" : "\nSequência pausada · volte à elevação 2 do templo";
        _hud.TempleProgress(present,text,phase is NpcDeparturePhase.ToGate or NpcDeparturePhase.ToMiddle or NpcDeparturePhase.ToExit,
            phase == NpcDeparturePhase.GateDelay,enabled,current && phase != NpcDeparturePhase.Hidden && !InspectorOpen);
        var campaign = _campaign; Guid token = campaign?.TempleToken ?? Guid.Empty; long revision = campaign?.TempleRevision ?? 0;
        bool Valid() => campaign != null && _campaign == campaign && campaign.TempleToken == token && campaign.TempleRevision == revision
            && campaign.CurrentMap == 3 && campaign.Location.Elevation == 2 && _elevation == 2 && !InspectorOpen;
        void Move(bool leg)
        {
            if (!Valid()) return;
            var originalPhase = campaign!.TempleProgress?.Departure.Phase;
            long beforeRevision = campaign.TempleRevision;
            TempleWorldStatus status;
            do { status = campaign.StepTempleNpc(token,campaign.TempleRevision,_walker?.Moving == true); }
            while (leg && status == TempleWorldStatus.Ready && campaign.TempleProgress!.Departure.Phase == originalPhase);
            if (campaign.TempleRevision != beforeRevision) RefreshActiveTempleProjection();
            NavigationStatus(TempleActionText(status));
        }
        _hud.TempleStepRequested = () => Move(false); _hud.TempleLegRequested = () => Move(true);
        void Wait(bool full)
        {
            if (!Valid()) return;
            var result = campaign!.AdvanceTempleNpcTime(full ? campaign.TempleProgress?.Departure.RemainingTicks ?? 0 : 10,token,revision,_walker?.Moving == true);
            if (result.Status == TempleWorldStatus.Ready) RefreshActiveTempleProjection();
            NavigationStatus(TempleActionText(result.Status));
        }
        _hud.TempleWaitRequested = () => Wait(true); _hud.TempleTickRequested = () => Wait(false);
        _hud.TempleFocusRequested = () =>
        {
            if (!Valid() || !current || campaign!.TempleProgress!.Departure.Phase == NpcDeparturePhase.Hidden) return;
            var actor = _layout!.Objects.Single(o => o.Source.Key == progress!.Scripts.Blocks[0].Key);
            SetCameraFollow(false); _focus = Point(actor.Position,0); _camera.Size = 20; UpdateCamera();
        };
    }
    private static string TempleActionText(TempleWorldStatus status) => status switch
    {
        TempleWorldStatus.Ready => "Saída do NPC atualizada · K para salvar a partida",
        TempleWorldStatus.Occupied => "Você está no próximo hex do NPC. Afaste-se para liberar o percurso.",
        TempleWorldStatus.Busy => "Termine ou pare sua caminhada antes de avançar o NPC.",
        TempleWorldStatus.Blocked => "O próximo hex está bloqueado. O NPC permanece no lugar.",
        TempleWorldStatus.Complete => "O NPC já concluiu a saída.",
        _ => "Esta ação não está disponível na fase ou elevação atual."
    };
    private void RefreshActiveTempleProjection()
    {
        if (_campaign == null || _layout == null || _campaign.CurrentMap != 3 || _elevation != 2) return;
        bool walking = _walkMode; int playerHex = _walker!.CurrentHex; string? selected = _selected?.Source.Key;
        _layout = ProjectTempleLayout(_layout);
        foreach (var obj in _layout.Objects)
        {
            var body = _bodies[obj.Source.Key]; _lookup[body.GetInstanceId()] = obj;
            body.Position = Point(obj.Position,Dimensions(obj.Kind).Y / 2);
            ConfigureActiveTempleBody(obj,body);
        }
        _navigation = _doors!.Navigation(2); _walker = new HexWalker(_navigation,playerHex);
        ClearRouteVisual(); _committedRoute = Array.Empty<int>(); ApplyDoorVisuals(); ApplyFilters(); RefreshReachability(); UpdateWalkerPosition();
        _walkMode = walking;
        var chosen = _layout.Objects.FirstOrDefault(o => o.Source.Key == selected && VisibleObject(o));
        if (chosen != null) SelectObject(chosen); else ClearSelection();
        RefreshActiveTempleHud();
    }
    private void ConfigureActiveTempleBody(PreviewObject obj, StaticBody3D body)
    {
        if (_campaign?.CurrentMap != 3 || _campaign.TempleProgress == null || obj.Source.Offset != 425776) return;
        body.Rotation = new(0,obj.Source.Header.Rotation * Mathf.Pi / 3,0); body.SetMeta("hex",obj.Source.Header.Tile);
        if (body.FindChild("ActiveNpcCaption",false,false) == null)
            body.AddChild(new Label3D { Name = "ActiveNpcCaption", Text = "NPC · saída", Position = new(0,1.3f,0), FontSize = 32, PixelSize = .012f,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, Modulate = new Color("e8c78d") });
    }
}
