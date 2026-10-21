using System;
using System.Linq;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private bool _showNpcRoute;
    private MeshInstance3D? _npcRouteNode, _npcRouteBlock;
    private (Guid Token,long Revision,ActorLocation Player,int Elevation,bool Show,bool Critters)? _npcRouteStamp;
    private void ResetNpcRouteView() { _npcRouteNode = null; _npcRouteBlock = null; _npcRouteStamp = null; }
    private void RefreshNpcRouteView()
    {
        if (_campaign == null) return;
        var progress = _campaign.TempleProgress;
        if (progress == null || progress.Departure.Phase == NpcDeparturePhase.Hidden) _showNpcRoute = false;
        var campaign = _campaign; Guid token = campaign.TempleToken;
        _hud.TempleRouteRequested = enabled =>
        {
            if (_campaign != campaign || campaign.TempleToken != token || InspectorOpen) return;
            _showNpcRoute = enabled; _npcRouteStamp = null; RefreshNpcRouteView();
            if (enabled) FrameNpcRoute();
        };
        var stamp = (campaign.TempleToken,campaign.TempleRevision,campaign.Location,_elevation,_showNpcRoute,_hud.Filters["critter"]);
        if (_npcRouteStamp == stamp) return;
        _npcRouteStamp = stamp;
        var route = campaign.InspectTempleNpcRoute();
        bool available = _elevation == 2 && campaign.CurrentMap == 3 && route.Tiles.Length > 1 && _hud.Filters["critter"];
        string hint = route.BlockedHex is int blocked ? $"Percurso do NPC · bloqueado no hex {blocked}. Libere esse ponto antes de continuar."
            : available ? $"Percurso do NPC · {route.Tiles.Length - 1} passos até a próxima parada."
            : "O percurso aparece quando o NPC estiver em um trecho de caminhada, na elevação 2.";
        _hud.TempleRouteAction(progress != null,available || _showNpcRoute,_showNpcRoute,hint);
        bool visible = _showNpcRoute && available;
        if (_npcRouteNode != null) _npcRouteNode.Visible = visible;
        if (_npcRouteBlock != null) _npcRouteBlock.Visible = visible && route.BlockedHex.HasValue;
        if (!visible || _layout == null) return;
        if (_npcRouteNode == null)
        {
            using var resources = new ResourceScope();
            _npcRouteNode = new MeshInstance3D { Name = "NpcDepartureRoute", CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                MaterialOverride = resources.Own(new StandardMaterial3D { AlbedoColor = new Color("d8a0f2"), NoDepthTest = true,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled }) };
            _npcRouteBlock = new MeshInstance3D { Name = "NpcRouteBlockedHex", CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Mesh = resources.Own(new CylinderMesh { TopRadius = .4f, BottomRadius = .4f, Height = .03f, RadialSegments = 6 }),
                MaterialOverride = resources.Own(new StandardMaterial3D { AlbedoColor = new Color("fa7772"), NoDepthTest = true, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded }) };
            _world.AddChild(_npcRouteNode); _world.AddChild(_npcRouteBlock);
        }
        using var mesh = CreateRouteMesh(route.Tiles,.22f); _npcRouteNode.Mesh = mesh; _npcRouteNode.Visible = true;
        _npcRouteBlock!.Visible = route.BlockedHex.HasValue;
        if (route.BlockedHex is int hex) _npcRouteBlock.Position = Point(Geometry.FalloutGrid.HexCenter(hex) - _layout.Origin,.25f);
    }
    private void FrameNpcRoute()
    {
        if (_campaign == null || _layout == null || InspectorOpen || _elevation != 2) return;
        var route = _campaign.InspectTempleNpcRoute(); if (route.Tiles.Length < 2) return;
        Vector3 oldFocus = _focus; float oldSize = _camera.Size;
        try
        {
            FitVisibleRoutePoints(FramingPoints(new(_navigation!,route.Tiles[0],route.Tiles,route.TargetKey)));
            NavigationStatus(route.BlockedHex is int blocked ? $"Percurso lilás do NPC · hex {blocked} bloqueado · libere o ponto vermelho"
                : $"Percurso lilás do NPC · {route.Tiles.Length - 1} passos até a próxima parada");
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        { _focus = oldFocus; _camera.Size = oldSize; UpdateCamera(); NavigationStatus("O percurso do NPC não cabe nesta vista. Use Ver NPC para aproximar."); }
    }
}
