using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private MeshInstance3D? _hoverLine, _hoverTarget;
    private StandardMaterial3D? _hoverTargetMaterial;
    private int? _hoverHex;
    private int[] _hoverPath = Array.Empty<int>();
    private RouteStatus? _hoverStatus;
    private void BuildRouteHover(ResourceScope resources)
    {
        _hoverTargetMaterial ??= _resources.Own(new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, NoDepthTest = true });
        _hoverTarget = new MeshInstance3D { Name = "RoutePreviewTarget", Visible = false,
            Mesh = resources.Own(new CylinderMesh { TopRadius = .45f, BottomRadius = .45f, Height = .035f, RadialSegments = 6 }),
            MaterialOverride = _hoverTargetMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _hoverLine = new MeshInstance3D { Name = "RoutePreviewLine", Visible = false,
            MaterialOverride = resources.Own(new StandardMaterial3D { AlbedoColor = new Color("65d4e2"), NoDepthTest = true,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled }),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _world.AddChild(_hoverLine); _world.AddChild(_hoverTarget);
    }
    private void ClearRouteHover(bool keepFramePlan = false)
    {
        if (!keepFramePlan) _framePreview = null;
        _pinnedHoverScreen = null;
        _hoverHex = null; _hoverPath = Array.Empty<int>(); _hoverStatus = null; _hoverDoorKey = null; _hoverContainerKey = null;
        if (_hoverLine != null) { _hoverLine.Visible = false; _hoverLine.Mesh = null; }
        if (_hoverTarget != null) _hoverTarget.Visible = false;
        _hud.RouteHint(null);
        RefreshFrameRouteAction();
    }
    private int? GroundHexAtScreen(Vector2 screen)
    {
        if (_layout == null) return null;
        Vector3 origin = _camera.ProjectRayOrigin(screen), direction = _camera.ProjectRayNormal(screen);
        if (Math.Abs(direction.Y) < .001f) return null;
        float distance = -origin.Y / direction.Y;
        if (distance < 0 || distance > _camera.Far) return null;
        Vector3 point = origin + direction * distance;
        return FalloutGrid.HexAt(new GroundPoint(point.X + _layout.Origin.X, point.Z + _layout.Origin.Z));
    }
    private void RouteHoverAtScreen(Vector2 screen)
    {
        if (!_walkMode || _walker?.Moving != false || InspectorOpen) { ClearRouteHover(); return; }
        if (_pinnedHoverScreen.HasValue)
        { if (_pinnedHoverScreen.Value.DistanceSquaredTo(screen) <= 16) return; _pinnedHoverScreen = null; }
        if (_hud.MouseOverPanelAt(screen)) { ClearRouteHover(true); return; }
        if (!GetViewport().GetVisibleRect().HasPoint(screen)) { ClearRouteHover(); return; }
        PreviewObject? picked = InteractionObjectAtScreen(screen);
        if (picked is { Kind: PreviewKind.Door } door) { PreviewDoorRoute(door); return; }
        if (picked != null && IsContainer(picked)) { PreviewContainerRoute(picked); return; }
        PreviewGroundRoute(GroundHexAtScreen(screen) ?? -1);
    }
    private void PreviewGroundRoute(int target)
    {
        if (!_walkMode || _walker?.Moving != false || _navigation == null || _reachable == null || _hoverTarget == null)
        { ClearRouteHover(); return; }
        if (_hoverDoorKey != null || _hoverContainerKey != null) ClearRouteHover();
        if (_hoverHex == target) return;
        ClearRouteHover(); _hoverHex = target;
        if (!HexNavigation.Valid(target)) { _hoverStatus = RouteStatus.OutsideGrid; _hud.RouteHint("Fora da malha do mapa"); return; }
        _hoverStatus = !_navigation.Walkable(target) ? RouteStatus.Blocked : _reachable.Distance(target) < 0 ? RouteStatus.Unreachable
            : target == _walker.CurrentHex ? RouteStatus.AlreadyThere : RouteStatus.Ready;
        bool ready = _hoverStatus is RouteStatus.Ready or RouteStatus.AlreadyThere;
        bool exit = _navigation.IsExit(target);
        _hoverTargetMaterial!.AlbedoColor = new Color(!ready ? "e4766abb" : exit ? "eacb70bb" : "64d3a9bb");
        _hoverTarget.Position = Point(FalloutGrid.HexCenter(target) - _layout!.Origin, .09f); _hoverTarget.Visible = true;
        if (ready) _hoverPath = _reachable.PathTo(target);
        if (_hoverPath.Length > 1)
        {
            using var mesh = CreateRouteMesh(_hoverPath, .18f);
            _hoverLine!.Mesh = mesh; _hoverLine.Visible = true;
        }
        string hint = _hoverStatus switch
        {
            RouteStatus.Ready => $"{(exit ? "Saída" : "Prévia")} · {_hoverPath.Length - 1} passos · clique para caminhar",
            RouteStatus.AlreadyThere => "O personagem já está neste hex",
            RouteStatus.Blocked => $"Bloqueado · {BlockerLabel(target)}",
            _ => "Sem caminho com as portas e bloqueadores atuais"
        };
        _hud.RouteHint(exit ? ExitRouteHint(target, _hoverStatus.Value, _hoverPath.Length - 1) : hint);
        RememberFramePreview();
    }
    private async Task CheckRouteHover()
    {
        _checkingNavigation = true;
        try
        {
            ResetWalker(); ToggleWalkMode(); SetNavigationGrid(false);
            var location = _campaign!.Location; var doors = _doors!.Capture(); var reach = _reachable;
            _focus = Point(FalloutGrid.HexCenter(24508) - _layout!.Origin, 0); _camera.Size = 40; UpdateCamera();
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Vector2 screen = _camera.UnprojectPosition(Point(FalloutGrid.HexCenter(24508) - _layout.Origin, 0));
            RouteHoverAtScreen(screen);
            if (_hoverHex != 24508 || _hoverStatus != RouteStatus.Ready || _hoverPath.Length != 24 || !_hoverLine!.Visible
                || !_hoverTarget!.Visible || _navigationOverlay!.Visible || _walker!.Moving || _campaign.Location != location)
                throw new InvalidDataException("Ground hover preview moved the actor or disagreed with the real 23-step route.");
            int[] preview = _hoverPath.ToArray();
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "route-preview.png"));
            RouteHoverAtScreen(new Vector2(20, 20));
            if (_hoverTarget.Visible || _hud.RouteHintText != null) throw new InvalidDataException("HUD hover retained a world preview.");
            PreviewGroundRoute(24308);
            if (_hoverStatus != RouteStatus.Blocked || _hoverLine.Visible) throw new InvalidDataException("Closed door showed a traversable route.");
            int unreachable = Enumerable.Range(0, 40000).First(t => _navigation!.Walkable(t) && _reachable!.Distance(t) < 0);
            PreviewGroundRoute(unreachable);
            if (_hoverStatus != RouteStatus.Unreachable || _hoverPath.Length != 0) throw new InvalidDataException("Disconnected destination showed a path.");
            PreviewGroundRoute(31306);
            if (_hoverStatus != RouteStatus.Ready || _hoverPath.Length != 13 || !_hud.RouteHintText!.StartsWith("Saída"))
                throw new InvalidDataException("Exit preview disagreed with its terminal route.");
            PreviewGroundRoute(-1);
            if (_hoverStatus != RouteStatus.OutsideGrid || _hoverTarget.Visible) throw new InvalidDataException("Outside-grid hover retained a marker.");
            PreviewGroundRoute(24508);
            if (!ReferenceEquals(reach, _reachable) || !_doors.Capture().Doors.SequenceEqual(doors.Doors) || _skills.LastCheck != null)
                throw new InvalidDataException("Hover recomputed reachability or altered doors/skills.");
            HexRoute clicked = RequestWalk(24508);
            if (!clicked.Tiles.SequenceEqual(preview) || _hoverTarget.Visible) throw new InvalidDataException("Clicked path differed from its preview.");
            PreviewGroundRoute(28906);
            if (_hoverTarget.Visible) throw new InvalidDataException("Hover remained active during movement.");
            StopWalking(); PreviewGroundRoute(24508); ToggleWalkMode();
            if (_hoverTarget.Visible || _hud.RouteHintText != null) throw new InvalidDataException("Inspect mode retained a path preview.");
            ChangeElevation(1);
            if (_hoverTarget != null || _hud.RouteHintText != null) throw new InvalidDataException("Other elevation retained actor route preview.");
            MapLayoutWriter.Verify(_map!); SetNavigationGrid(true); ResetWalker();
            GD.Print("ROUTE_HOVER_OK: screen projection, 23-step preview/click agreement, blocked/unreachable/exit/outside targets, cached reachability, HUD, moving/inspection guards, actor, skills, doors and source bytes verified.");
        }
        finally { _checkingNavigation = false; }
    }
}
