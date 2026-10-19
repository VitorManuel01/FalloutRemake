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
    private string? _hoverContainerKey;
    private static bool IsContainer(PreviewObject obj) => obj.Source.Header.Pid >> 24 == 0 && obj.Source.Subtype == 1;
    private PreviewObject? InteractionObjectAtScreen(Vector2 screen)
    {
        PreviewObject? nearest = WorldObjectAtScreen(screen);
        // The source pot shares a hex with a larger scenery placeholder. Resolve only that same-hex overlap.
        if (nearest != null && nearest.Kind is PreviewKind.Scenery or PreviewKind.Item && _layout != null)
            return _layout.Objects.Where(o => IsContainer(o) && o.Source.Header.Tile == nearest.Source.Header.Tile && VisibleObject(o))
                .OrderBy(o => o.Source.Offset).FirstOrDefault() ?? nearest;
        return nearest;
    }
    private void ApproachContainerFromClick(PreviewObject container)
    {
        ClearRouteHover(); SelectObject(container);
        if (_walker == null) return;
        if (_walker.Moving) { NavigationStatus("Aguarde o percurso terminar ou use Parar [Esc]."); return; }
        int? target = ContainerApproach(container.Source);
        if (!target.HasValue) { NavigationStatus("Sem caminho até o recipiente com os bloqueios atuais."); return; }
        if (target.Value == _walker.CurrentHex) { NavigationStatus("Recipiente ao alcance · J para consultar e recolher"); return; }
        RequestWalk(target.Value);
    }
    private void PreviewContainerRoute(PreviewObject container)
    {
        if (!_walkMode || _walker?.Moving != false || _navigation == null || _reachable == null || !IsContainer(container))
        { ClearRouteHover(); return; }
        if (_hoverContainerKey == container.Source.Key) return;
        ClearRouteHover();
        string name = OriginalText(container.Source)?.Name ?? "Recipiente"; if (name.Length > 40) name = name[..39] + "…";
        int? target = ContainerApproach(container.Source);
        if (target.HasValue && target.Value != _walker.CurrentHex)
        {
            PreviewGroundRoute(target.Value);
            _hud.RouteHint($"{name} · aproximação em {_hoverPath.Length - 1} passos · clique para aproximar · J depois de chegar");
        }
        else
        {
            _hoverStatus = target.HasValue ? RouteStatus.AlreadyThere : RouteStatus.Unreachable;
            _hoverTargetMaterial!.AlbedoColor = new Color(target.HasValue ? "eacb70bb" : "e4766abb");
            _hoverTarget!.Position = Point(container.Position, .09f); _hoverTarget.Visible = true;
            _hud.RouteHint(target.HasValue ? $"{name} · ao alcance · J para consultar e recolher" : $"{name} · sem caminho até o recipiente");
        }
        _hoverContainerKey = container.Source.Key; RememberFramePreview();
    }
    private async Task CheckContainerRouteHover()
    {
        _checkingNavigation = true;
        try
        {
            ResetWalker(); ToggleWalkMode(); SetNavigationGrid(false);
            PreviewObject pot = _layout!.Objects.Single(o => o.Source.Offset == 206348);
            _focus = Point(pot.Position, 0); _camera.Size = 24; UpdateCamera();
            Vector3 plannedFocus = _focus;
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Vector2 screen = _camera.UnprojectPosition(Point(pot.Position, .125f));
            RouteHoverAtScreen(screen);
            if (_hoverContainerKey != pot.Source.Key || _hoverStatus != RouteStatus.Unreachable || _hoverPath.Length != 0 || _walker!.Moving || _selected != null)
                throw new InvalidDataException($"Blocked pot hover was not picked or altered actor/selection: hover={_hoverContainerKey}, status={_hoverStatus}, path={_hoverPath.Length}, selected={_selected?.Source.Header.Id}, picked={WorldObjectAtScreen(screen)?.Source.Header.Id}, screen={screen}, hud={_hud.MouseOverPanelAt(screen)}, focus={_focus}/{plannedFocus}, camera={_camera.Position}, size={_camera.Size}, follow={_followActor}.");
            RequestWalk(24508); _walker!.Advance(1000); UpdateWalkerPosition(); UseDoor(_layout.Objects.Single(o => o.Source.Offset == 244040).Source.Key);
            var before = _campaign!.Location; var doors = _doors!.Capture();
            int target = ContainerApproach(pot.Source)!.Value; int[] expected = _navigation!.FindRoute(_walker.CurrentHex, target).Tiles;
            RouteHoverAtScreen(screen);
            if (_hoverContainerKey != pot.Source.Key || !_hoverPath.SequenceEqual(expected) || _campaign.CarriedCount != 0 || _selected != null || _campaign.Location != before)
                throw new InvalidDataException("Container hover disagreed with real approach or changed world state.");
            FrameRoute();
            if (_framePreview?.TargetKey != pot.Source.Key || !FramingPointsVisible(FramingPoints(_framePreview)))
                throw new InvalidDataException("Container framing lost the target or clipped route/actor.");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "container-route.png"));
            _pinnedHoverScreen = null; RouteHoverAtScreen(new Vector2(20, 20)); FrameRoute();
            if (_hoverContainerKey != pot.Source.Key) throw new InvalidDataException("HUD frame action restored a ground route instead of the container target.");
            screen = _camera.UnprojectPosition(Point(pot.Position, .125f)); WalkAtScreen(screen);
            if (_selected?.Source.Key != pot.Source.Key || !_walker.Moving || !_committedRoute.SequenceEqual(expected) || _hoverContainerKey != null)
                throw new InvalidDataException("Container click did not select and follow its previewed approach.");
            _walker.Advance(1000); UpdateWalkerPosition(); RefreshReachability();
            PreviewContainerRoute(pot);
            if (_hoverStatus != RouteStatus.AlreadyThere || _hoverPath.Length != 0 || !_hud.RouteHintText!.Contains("J para consultar"))
                throw new InvalidDataException("Nearby container retained a stale approach.");
            OpenLoot(); RouteHoverAtScreen(screen);
            if (_hoverContainerKey != null || _hoverTarget!.Visible) throw new InvalidDataException("Modal retained a container hover.");
            CloseLoot(); ToggleWalkMode(); PreviewContainerRoute(pot);
            if (_hoverContainerKey != null) throw new InvalidDataException("Inspection retained container route preview.");
            if (!_doors.Capture().Doors.SequenceEqual(doors.Doors) || _campaign.CarriedCount != 0 || _skills.LastCheck != null)
                throw new InvalidDataException("Container approach altered doors, items or skill rolls.");
            MapLayoutWriter.Verify(_map!); SetNavigationGrid(true); ResetWalker();
            GD.Print("CONTAINER_ROUTE_OK: real pot picking, closed-door rejection, shared hover/click/modal approach, T target framing/restoration, selection without auto-loot, near/modal/inspection cleanup, unchanged doors/items/skills/source bytes verified.");
        }
        finally { _checkingNavigation = false; }
    }
}
