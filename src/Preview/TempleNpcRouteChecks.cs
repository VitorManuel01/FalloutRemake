using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private async Task CheckNpcRouteView()
    {
        byte[] before = CheckpointFile.Encode(_campaign!.Capture(_mapCatalog!.Source.Sha256));
        var route = _campaign.InspectTempleNpcRoute();
        var button = (Button)_hud.FindChild("TempleNpcRouteAction",true,false);
        if (!button.Visible || button.Disabled || route.Status != TempleWorldStatus.Occupied || route.BlockedHex != _walker!.CurrentHex)
            throw new InvalidDataException("NPC route did not expose the original player's blocking hex.");
        button.ButtonPressed = true;
        var plan = new RouteFramePlan(_navigation!,route.Tiles[0],route.Tiles,route.TargetKey);
        if (_npcRouteNode?.Visible != true || _npcRouteBlock?.Visible != true || !FramingPointsVisible(FramingPoints(plan))
            || _npcRouteNode.GetChildren().Any(c => c is CollisionObject3D)) throw new InvalidDataException("NPC path overlay lost framing, blocked marker or picking isolation.");
        float yaw = _yaw;
        foreach (float angle in new[] { yaw - Mathf.Pi / 3,yaw + Mathf.Pi / 2 })
        { _yaw = angle; UpdateCamera(); FrameNpcRoute(); if (!FramingPointsVisible(FramingPoints(plan))) throw new InvalidDataException("NPC framing clipped a rotated path."); }
        _yaw = yaw; UpdateCamera(); FrameNpcRoute();
        _hud.Filters["critter"] = false; ApplyFilters();
        if (_npcRouteNode.Visible || _npcRouteBlock.Visible) throw new InvalidDataException("Hidden creatures retained a visible NPC route.");
        _hud.Filters["critter"] = true; ApplyFilters();
        if (!_npcRouteNode.Visible || !_npcRouteBlock.Visible || !before.SequenceEqual(CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256))))
            throw new InvalidDataException("NPC route/filter inspection changed the active world or failed to restore its view.");
        if (_capture != null) await Capture(Path.ChangeExtension(_capture,"active-npc-route.png"));
        GD.Print("TEMPLE_NPC_ROUTE_OK: owned read-only leg, red player obstruction, distinct noncolliding overlay, full safe framing at three rotations, creature-filter hide/restore, unchanged checkpoint/items/XP/source verified.");
    }
}
