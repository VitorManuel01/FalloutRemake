using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private HexNavigation? _navigation;
    private HexReachability? _reachable;
    private HexWalker? _walker;
    private Node3D? _walkerNode;
    private MultiMeshInstance3D? _navigationOverlay;
    private MeshInstance3D? _routeVisual;
    private bool _walkMode, _checkingNavigation;

    private void BuildNavigation()
    {
        using var resources = new ResourceScope();
        _committedRoute = Array.Empty<int>();
        ClearRouteHover(); _hoverLine = null; _hoverTarget = null;
        _walker = null; _navigation = null; _reachable = null; _walkerNode = null;
        _navigationOverlay = null; _routeVisual = null; _walkMode = false;
        ApplyDoorVisuals();
        if (_map == null || _layout == null || _travel == null || _elevation != _travel.Location.Elevation)
        {
            RefreshDoorAction(); NavigationStatus($"Inspeção · o personagem permanece na elevação {_travel?.Location.Elevation ?? 0}."); return;
        }
        _navigation = _doors!.Navigation(_elevation);
        int start = _travel.Location.Hex;
        if (!_navigation.Walkable(start)) { NavigationStatus("Entrada bloqueada nos dados; marcador não criado."); return; }
        _walker = new HexWalker(_navigation, start);
        _walkerNode = new Node3D { Name = "NavigationMarker" }; _world.AddChild(_walkerNode);
        _walkerNode.AddChild(new MeshInstance3D
        {
            Position = new Vector3(0, .6f, 0), Mesh = resources.Own(new CapsuleMesh { Radius = .27f, Height = 1.1f }),
            MaterialOverride = resources.Own(new StandardMaterial3D { AlbedoColor = new Color("50d7ea"), Roughness = .6f,
                EmissionEnabled = true, Emission = new Color("167889"), EmissionEnergyMultiplier = .6f })
        });
        _walkerNode.AddChild(new MeshInstance3D { Position = new Vector3(0, .02f, 0),
            Mesh = resources.Own(new CylinderMesh { TopRadius = .43f, BottomRadius = .43f, Height = .025f, RadialSegments = 32 }),
            MaterialOverride = resources.Own(new StandardMaterial3D { AlbedoColor = new Color("c2f7ff"), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded }) });
        _routeVisual = new MeshInstance3D { Name = "PlannedRoute", Visible = false,
            MaterialOverride = resources.Own(new StandardMaterial3D { AlbedoColor = new Color("c47920"), NoDepthTest = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded }) }; _world.AddChild(_routeVisual);
        BuildRouteHover(resources);
        RefreshReachability(); UpdateWalkerPosition();
        ApplyDoorVisuals(); RefreshDoorAction();
        NavigationStatus($"Personagem no hex {start} · M para caminhar · {_reachable!.Count} hexes alcançáveis");
    }

    private void NavigationStatus(string text)
    {
        _hud.Navigation(_walker != null, _walkMode, text);
        _hud.StopAction(_walker?.Moving == true, _walker?.StopRequested == true);
        RefreshTravelAction(); RefreshCheckpointActions();
        RefreshFrameRouteAction();
        RefreshUseClock();
        RefreshSpeechAction();
    }

    private void RefreshReachability()
    {
        if (_navigation == null || _walker == null || _layout == null) return;
        ClearRouteHover();
        using var resources = new ResourceScope();
        _reachable = _navigation.ReachableFrom(_walker.CurrentHex);
        if (_navigationOverlay != null) { _world.RemoveChild(_navigationOverlay); _navigationOverlay.QueueFree(); }
        var cells = new List<(int Hex, Color Color)>();
        for (int tile = 0; tile < 40000; tile++)
        {
            if (!_navigation.Walkable(tile)) cells.Add((tile, new Color("c76a5a")));
            else if (_reachable.Distance(tile) >= 0)
                cells.Add((tile, _navigation.IsExit(tile) ? new Color("eacb70") : new Color("5bbb96")));
        }
        var instances = resources.Own(new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true,
            Mesh = resources.Own(new CylinderMesh { TopRadius = .19f, BottomRadius = .19f, Height = .01f, RadialSegments = 6 }), InstanceCount = cells.Count });
        for (int i = 0; i < cells.Count; i++)
        {
            instances.SetInstanceTransform(i, new Transform3D(Basis.Identity, Point(FalloutGrid.HexCenter(cells[i].Hex) - _layout.Origin, .035f)));
            instances.SetInstanceColor(i, cells[i].Color);
        }
        _navigationOverlay = new MultiMeshInstance3D { Name = "HexPassability", Multimesh = instances, Visible = _walkMode && _showNavigationGrid,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = resources.Own(new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, NoDepthTest = true,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded }) };
        _world.AddChild(_navigationOverlay);
    }

    private void ToggleWalkMode()
    {
        if (CombatRefuses("trocar de modo")) return;
        if (_walker == null) return;
        ClearRouteHover();
        _walkMode = !_walkMode;
        _navigationOverlay!.Visible = _walkMode && _showNavigationGrid;
        if (_routeVisual != null) _routeVisual.Visible = _walkMode && _routeVisual.Mesh != null;
        NavigationStatus(_walkMode ? "Clique no chão para caminhar ou em uma porta para se aproximar" : "Inspeção · clique em um objeto para consultar os dados");
    }

    private void ResetWalker()
    {
        if (_campaign == null) return;
        DiscardCombat();
        SetCameraFollow(false);
        _campaign.Reset(); _skills.Reset(); BindCurrentArea();
        ChangeElevation(_travel!.Location.Elevation); FocusEntry();
        NavigationStatus($"Marcador reiniciado · {_reachable!.Count} hexes alcançáveis");
    }

    private void WalkAtScreen(Vector2 screen)
    {
        if (_layout == null || _walker == null) return;
        PreviewObject? picked = InteractionObjectAtScreen(screen);
        if (picked is { Kind: PreviewKind.Door } door) { ApproachDoor(door); return; }
        if (picked != null && IsContainer(picked)) { ApproachContainerFromClick(picked); return; }
        RequestWalk(GroundHexAtScreen(screen) ?? -1);
    }

    private HexRoute RequestWalk(int target)
    {
        if (_walker == null || _navigation == null) return new(RouteStatus.OutsideGrid, Array.Empty<int>());
        if (CombatRefuses("caminhar livremente")) return new(RouteStatus.Busy, Array.Empty<int>());
        ClearRouteHover();
        HexRoute route = _walker.WalkTo(target);
        string message = route.Status switch
        {
            RouteStatus.Ready => $"Caminhando até hex {target} · {route.Tiles.Length - 1} passos",
            RouteStatus.AlreadyThere => $"O marcador já está no hex {target}.",
            RouteStatus.Blocked => $"Hex {target} bloqueado · {BlockerLabel(target)}",
            RouteStatus.Unreachable => "Sem caminho com as portas e bloqueadores atuais.",
            RouteStatus.Busy => "Aguarde o percurso terminar ou use Parar [Esc].",
            _ => "Esse ponto fica fora da malha do mapa."
        };
        NavigationStatus(message);
        if (route.Status == RouteStatus.Ready) DrawRoute(route.Tiles);
        RefreshDoorAction();
        return route;
    }

    private string BlockerLabel(int hex)
    {
        HexBlocker? blocker = _navigation?.BlockersAt(hex).FirstOrDefault();
        if (blocker == null) return "objeto de origem";
        string kind = blocker.IsDoor ? "porta" : blocker.FidType switch { 1 => "criatura", 3 => "parede", _ => "cenário" };
        return $"{kind}, ID {blocker.Id}";
    }

    private void DrawRoute(int[] tiles)
    {
        if (_layout == null || _routeVisual == null) return;
        _committedRoute = (int[])tiles.Clone();
        using var mesh = CreateRouteMesh(tiles, .16f);
        _routeVisual.Mesh = mesh; _routeVisual.Visible = _walkMode;
        RefreshFrameRouteAction();
    }
    private ImmediateMesh CreateRouteMesh(int[] tiles, float height)
    {
        var mesh = new ImmediateMesh(); mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        for (int i = 1; i < tiles.Length; i++)
        {
            Vector3 start = Point(FalloutGrid.HexCenter(tiles[i - 1]) - _layout!.Origin, height);
            Vector3 end = Point(FalloutGrid.HexCenter(tiles[i]) - _layout.Origin, height);
            Vector3 direction = (end - start).Normalized(), side = new Vector3(-direction.Z, 0, direction.X) * .06f;
            mesh.SurfaceAddVertex(start - side); mesh.SurfaceAddVertex(start + side); mesh.SurfaceAddVertex(end + side);
            mesh.SurfaceAddVertex(start - side); mesh.SurfaceAddVertex(end + side); mesh.SurfaceAddVertex(end - side);
        }
        mesh.SurfaceEnd(); return mesh;
    }

    private void AdvanceWalker(double delta)
    {
        if (_walker == null || _checkingNavigation || !_walker.Moving) return;
        bool stopping = _walker.StopRequested;
        _walker.Advance(delta); UpdateWalkerPosition();
        if (!_walker.Moving)
        {
            ClearRouteVisual();
            RefreshReachability();
            if (_selected?.Kind == PreviewKind.Door) SelectObject(_selected);
            RefreshDoorAction();
            NavigationStatus(_navigation!.IsExit(_walker.CurrentHex)
                ? ExitHint(CurrentExit())
                : stopping ? $"Caminhada interrompida no hex {_walker.CurrentHex} · escolha outro destino"
                : $"Chegou ao hex {_walker.CurrentHex} · clique em outro destino");
        }
    }
    private void UpdateWalkerPosition()
    {
        if (_walker != null && _travel != null && _elevation == _travel.Location.Elevation) _travel.MoveTo(_walker.CurrentHex);
        if (_walkerNode != null && _walker != null && _layout != null) _walkerNode.Position = Point(_walker.Position - _layout.Origin, 0);
        RefreshNpcRouteView();
        RefreshTempleVillageGuidance();
    }

    private string NavigationDetails(PreviewObject obj)
    {
        if (_navigation == null) return "";
        bool blocks = _navigation.BlockersAt(obj.Source.Header.Tile).Any(b => b.Key == obj.Source.Key);
        string text = $"\n\nBloqueio por este objeto: {(blocks ? "sim" : "não")}\nTipo FID: {(obj.Source.Header.Fid >> 24) & 15}";
        if (obj.Kind == PreviewKind.Door && _doors?.Get(obj.Source.Key) is { } door)
            text = $"\n\nPorta na prévia: {(door.IsOpen ? "aberta" : "fechada")}\nTrancada: {(door.IsLocked ? "sim" : "não")} · Emperrada: {(door.IsJammed ? "sim" : "não")}\n{DoorHint(DoorStatus(obj.Source.Key))}\nFonte preservada: {((obj.Source.State["openFlags"] & 1) != 0 ? "aberta" : "fechada")}" + text;
        return text;
    }

    private async Task CheckNavigation()
    {
        _checkingNavigation = true;
        try
        {
            if (_navigation == null || _walker == null || _layout == null || _reachable?.Count != 540
                || _navigation.BlockedHexCount != 1206 || _navigation.DoorFlagDisagreements != 0)
                throw new InvalidDataException("Original elevation 0 navigation snapshot mismatch.");
            using var modeKey = new InputEventKey { Pressed = true, PhysicalKeycode = Key.M };
            _UnhandledInput(modeKey);
            if (!_walkMode || !_navigationOverlay!.Visible) throw new InvalidDataException("Walk mode input failed.");
            if (RequestWalk(24308).Status != RouteStatus.Blocked) throw new InvalidDataException("Closed door was traversable.");
            // Display filters must not change movement rules.
            _hud.Filters["wall"] = false; _hud.Filters["critter"] = false; ApplyFilters();
            if (_navigation.ReachableFrom(28906).Count != 540) throw new InvalidDataException("Visual filters changed passability.");
            _hud.Filters["wall"] = true; _hud.Filters["critter"] = true; ApplyFilters();
            Vector2 target = _camera.UnprojectPosition(Point(FalloutGrid.HexCenter(24508) - _layout.Origin, 0));
            WalkAtScreen(target);
            if (!_walker.Moving) throw new InvalidDataException("Projected floor click did not start walking.");
            int steps = 0, previous = _walker.CurrentHex;
            while (_walker.Moving)
            {
                _walker.Advance(.2); UpdateWalkerPosition();
                int next = _walker.CurrentHex;
                if (!_navigation.Walkable(next) || !Enumerable.Range(0, 6).Any(d => FalloutGrid.Neighbor(previous, d) == next))
                    throw new InvalidDataException("Marker skipped a hex or crossed a blocker.");
                previous = next;
                if (++steps > 100) throw new InvalidDataException("Marker failed to finish the route.");
            }
            if (steps != 23 || _walker.CurrentHex != 24508 || _walkerNode!.Position.DistanceTo(Point(FalloutGrid.HexCenter(24508) - _layout.Origin, 0)) > .0001f)
                throw new InvalidDataException("Door approach route or final marker position mismatch.");
            _focus = _walkerNode.Position / 2; _camera.Size = 36; UpdateCamera();
            NavigationStatus("Percurso validado: 23 passos · porta fechada preservada");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "navigation.png"));
            ResetWalker();
            HexRoute exitRoute = RequestWalk(31306);
            if (exitRoute.Status != RouteStatus.Ready || exitRoute.Tiles.Length != 13) throw new InvalidDataException("Exit route mismatch.");
            _walker!.Advance(10); UpdateWalkerPosition();
            if (!_navigation.IsExit(_walker.CurrentHex) || _walker.CurrentHex != 31306
                || _walker.WalkTo(28906).Status != RouteStatus.Unreachable) throw new InvalidDataException("Exit was not a terminal endpoint.");
            ChangeElevation(1);
            if (_walker != null || _walkMode) throw new InvalidDataException("Movement leaked into an unsupported elevation.");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GoToEntry();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_walker!.CurrentHex != 31306) throw new InvalidDataException("Inspection moved the actor away from its exit.");
            ResetWalker();
            int frameTarget = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(28906, d)!.Value)
                .First(t => _navigation!.Walkable(t) && !_navigation.IsExit(t));
            RequestWalk(frameTarget);
            _checkingNavigation = false;
            using var timer = GetTree().CreateTimer(.4);
            await ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
            _checkingNavigation = true;
            if (_walker!.Moving || _walker.CurrentHex != frameTarget) throw new InvalidDataException("Frame-driven animation did not finish.");
            ResetWalker();
            MapLayoutWriter.Verify(_map!);
            GD.Print("NAVIGATION_TEST_OK: 1206 blocked hexes; 540 reachable; 23-step door approach; 12-step exit; camera picking, frame-driven movement, marker positions, filters, resets and source bytes verified.");
        }
        finally { _checkingNavigation = false; }
    }
}
