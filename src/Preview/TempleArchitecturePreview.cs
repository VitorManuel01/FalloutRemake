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
    private const string KitDirectory = "res://Assets/TempleOfTrials/Godot_Modular_glTF/";
    private const string KitFloor = "Tile_Piso_1.73x2.0";
    // Source "Wall" scenery (pro_scen.msg 160000/160100) and wall torches stand on the wall lines.
    private static readonly int[] SceneryWallPids = { 0x02000640, 0x02000641 };
    private static readonly int[] TorchPids = { 0x0200055D, 0x0200055E, 0x0200055F };
    private static string KitMesh(ArchitectureModule module, bool vines) => (module, vines) switch
    {
        (ArchitectureModule.Wall1, false) => "Parede_Reta_1m", (ArchitectureModule.Wall2, false) => "Parede_Reta_2m",
        (ArchitectureModule.Wall4, false) => "Parede_Reta_4m", (ArchitectureModule.Pillar, false) => "Pilar_Pedra",
        (ArchitectureModule.Wall1, true) => "Vinhas_Parede_1m", (ArchitectureModule.Wall2, true) => "Vinhas_Parede",
        (ArchitectureModule.Wall4, true) => "Vinhas_Parede_4m", _ => "Vinhas_Pilar"
    };
    private static int KitMetres(ArchitectureModule module) => module switch { ArchitectureModule.Wall2 => 2, ArchitectureModule.Wall4 => 4, _ => 1 };

    private sealed record ArchitecturePart(GeometryInstance3D Node, string[] Keys);
    private Dictionary<string, Mesh>? _kit;
    private string _kitStatus = "Kit do templo não carregado.";
    private bool _architectureEnabled = true;
    private bool ArchitectureActive => _architectureEnabled && _kit != null;
    private ResourceCatalog? _artLists;
    private Node3D? _architecture;
    private MultiMeshInstance3D[] _architectureFloor = Array.Empty<MultiMeshInstance3D>();
    private readonly List<ArchitecturePart> _architectureParts = new();
    private WallPlan? _wallPlan;
    private int _architectureLights, _architectureTorches, _architectureFloorTiles;
    private Godot.Environment? _environment;
    private DirectionalLight3D? _sun;

    private void LoadKit()
    {
        var meshes = new Dictionary<string, Mesh>();
        var names = new[] { ArchitectureModule.Wall1, ArchitectureModule.Wall2, ArchitectureModule.Wall4, ArchitectureModule.Pillar }
            .SelectMany(m => new[] { KitMesh(m, false), KitMesh(m, true) }).Append(KitFloor);
        foreach (string name in names)
        {
            string path = KitDirectory + name + ".gltf";
            if (!ResourceLoader.Exists(path)) { _kitStatus = $"Kit do templo ausente ({name}.gltf); formas provisórias mantidas."; GD.Print($"TEMPLE_KIT_UNAVAILABLE: {path}"); return; }
            var scene = _resources.Own(ResourceLoader.Load<PackedScene>(path));
            Node root = scene.Instantiate();
            try
            {
                Mesh? mesh = (root as MeshInstance3D ?? root.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>().FirstOrDefault())?.Mesh;
                if (mesh == null) { _kitStatus = $"Kit do templo sem malha em {name}.gltf; formas provisórias mantidas."; return; }
                // Same fix as addons/temple_kit_import, in case the scene was imported without the plugin:
                // Godot 4.7.x drops COLOR_0 (baked tint and cavity AO) on the first primitive (godot#120625).
                for (int i = 0; i < mesh.GetSurfaceCount(); i++)
                    if (mesh.SurfaceGetMaterial(i) is BaseMaterial3D material
                        && (material.ResourceName.StartsWith("M_Temple_", StringComparison.Ordinal) || material.ResourceName.StartsWith("M_Dry_Vines", StringComparison.Ordinal)))
                        material.VertexColorUseAsAlbedo = true;
                meshes.Add(name, mesh);
            }
            finally { root.Free(); }
        }
        _kit = meshes; _kitStatus = $"Kit do templo · {meshes.Count} módulos";
        GD.Print($"TEMPLE_KIT_READY: {meshes.Count} modules from {KitDirectory}");
    }

    // Visual wall hexes: source walls with art (block.frm is an invisible blocker) and "Wall" scenery.
    private bool IsArchitectureWall(PreviewObject obj)
    {
        if (obj.Hidden) return false;
        if (obj.Kind == PreviewKind.Scenery) return SceneryWallPids.Contains(obj.Source.Header.Pid);
        if (obj.Kind != PreviewKind.Wall) return false;
        return !ArtName("art/walls/walls.lst", obj.Source.Header.Fid & 0xFFF).StartsWith("block", StringComparison.OrdinalIgnoreCase);
    }
    // Invisible blocking block.frm walls/scenery: drawn as stone only where they continue a wall (TempleArchitecture).
    private bool IsWallFiller(PreviewObject obj)
    {
        if ((obj.Source.Header.Flags & (HexNavigation.Hidden | HexNavigation.NoBlock)) != 0) return false;
        string list = obj.Kind switch { PreviewKind.Wall => "art/walls/walls.lst", PreviewKind.Scenery => "art/scenery/scenery.lst", _ => "" };
        return list.Length != 0 && ArtName(list, obj.Source.Header.Fid & 0xFFF).StartsWith("block", StringComparison.OrdinalIgnoreCase);
    }
    private string ArtName(string list, int index)
    {
        try { return _artLists?.ListEntry(list, index, ".frm") ?? ""; }
        catch (InvalidDataException) { return ""; }
    }
    private bool IsTempleFloor(PreviewFloor floor) => ArtName("art/tiles/tiles.lst", (int)(floor.Raw & 0xFFF)).StartsWith("tepflr", StringComparison.OrdinalIgnoreCase);

    private void BuildArchitecture()
    {
        _architectureParts.Clear(); _architectureFloor = Array.Empty<MultiMeshInstance3D>(); _wallPlan = null;
        _architectureLights = _architectureTorches = _architectureFloorTiles = 0; _architecture = null;
        ApplyLightingMode();
        if (!ArchitectureActive || _layout == null) return;
        using var resources = new ResourceScope();
        _architecture = new Node3D { Name = "TempleArchitecture" }; _world.AddChild(_architecture);
        var walls = _layout.Objects.Where(IsArchitectureWall).ToArray();
        var fillers = _layout.Objects.Where(IsWallFiller).ToArray();
        _wallPlan = TempleArchitecture.PlanWalls(walls.Select(o => o.Source.Header.Tile), _layout.Origin, fillers.Select(o => o.Source.Header.Tile));
        var filled = _wallPlan.Filled.ToHashSet();
        var keysAt = walls.Concat(fillers.Where(o => filled.Contains(o.Source.Header.Tile))).GroupBy(o => o.Source.Header.Tile)
            .ToDictionary(g => g.Key, g => g.Select(o => o.Source.Key).ToArray());
        // A red obstacle plate that the wall now encloses would only poke through the stone; its body stays selectable.
        foreach (PreviewObject filler in fillers.Where(o => o.Kind == PreviewKind.Scenery && filled.Contains(o.Source.Header.Tile)))
            foreach (MeshInstance3D part in _bodies[filler.Source.Key].GetChildren().OfType<MeshInstance3D>()) part.Visible = false;
        float k = (float)TempleArchitecture.KitScale;
        foreach (ArchitecturePiece piece in _wallPlan.Pieces)
        {
            string[] keys = piece.Tiles.SelectMany(t => keysAt.GetValueOrDefault(t, Array.Empty<string>())).ToArray();
            Transform3D transform;
            bool vines = piece.Vines;
            if (piece.Module == ArchitectureModule.Pillar)
            {
                int turn = (int)((uint)piece.Tiles[0] * 2654435761u >> 30);
                vines = turn == 0 && piece.Tiles[0] % 3 == 0;
                transform = new Transform3D(new Basis(Vector3.Up, turn * Mathf.Pi / 2).Scaled(Vector3.One * k), Point(piece.Origin, 0));
            }
            else
            {
                float yaw = Mathf.Atan2(-(float)piece.Direction.Z, (float)piece.Direction.X);
                var scale = new Vector3((float)piece.Length / KitMetres(piece.Module), k, k * (float)piece.ThicknessScale);
                transform = new Transform3D(new Basis(Vector3.Up, yaw) * Basis.FromScale(scale), Point(piece.Origin, 0));
            }
            AddArchitecturePart($"{piece.Module}_{piece.Tiles[0]}", KitMesh(piece.Module, false), transform, keys);
            if (vines) AddArchitecturePart($"Vines_{piece.Tiles[0]}", KitMesh(piece.Module, true), transform, keys);
        }
        foreach (PreviewObject wall in walls.Concat(_layout.Objects.Where(o => o.Kind == PreviewKind.Wall)))
            _bodies[wall.Source.Key].GetChild<MeshInstance3D>(0).Mesh = null; // The kit draws walls; block.frm stays invisible as in CE.
        BuildArchitectureFloor(resources);
        BuildTorches(resources);
        OrientDoors();
    }
    private void AddArchitecturePart(string name, string mesh, Transform3D transform, string[] keys)
    {
        var node = new MeshInstance3D { Name = name, Mesh = _kit![mesh], Transform = transform };
        _architecture!.AddChild(node); _architectureParts.Add(new(node, keys));
    }

    private void BuildArchitectureFloor(ResourceScope resources)
    {
        // Tile_Piso_1.73x2.0 spans one original floor square: x 0..1.734, z -2..0, walking surface at y≈0.
        var cells = _layout!.Floors.Where(IsTempleFloor).ToArray();
        var chunks = new List<MultiMeshInstance3D>();
        foreach (var chunk in cells.GroupBy(f => (f.Square % 100 / 8, f.Square / 100 / 8)))
        {
            PreviewFloor[] squares = chunk.ToArray();
            var instances = resources.Own(new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = _kit![KitFloor], InstanceCount = squares.Length });
            for (int i = 0; i < squares.Length; i++)
            {
                bool turned = ((uint)squares[i].Square * 2246822519u >> 31) != 0;
                Vector3 corner = turned ? new Vector3(.867f, 0, -1) : new Vector3(-.867f, 0, 1);
                instances.SetInstanceTransform(i, new Transform3D(new Basis(Vector3.Up, turned ? Mathf.Pi : 0), Point(squares[i].Position, 0) + corner));
            }
            var node = new MultiMeshInstance3D { Name = $"TempleFloor_{chunk.Key.Item1}_{chunk.Key.Item2}", Multimesh = instances,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            node.SetMeta("source_squares", squares.Select(f => f.Square).ToArray());
            _architecture!.AddChild(node); chunks.Add(node);
        }
        _architectureFloor = chunks.ToArray(); _architectureFloorTiles = cells.Length;
        // Reference cells stay for counts and filters: under kit tiles they sink below the dirt bed (the kit's open
        // joints would show them), and the surrounding cave fill (cav4009/cav4010) reads as the void around the temple.
        if (_floor?.Multimesh is { } cellsMesh)
            for (int i = 0; i < _layout.Floors.Length; i++)
            {
                bool temple = IsTempleFloor(_layout.Floors[i]);
                if (temple) cellsMesh.SetInstanceTransform(i, new Transform3D(Basis.Identity, Point(_layout.Floors[i].Position, -.35f)));
                cellsMesh.SetInstanceColor(i, temple ? new Color(.02f, .018f, .016f) : new Color(.012f, .014f, .016f));
            }
    }

    private void BuildTorches(ResourceScope resources)
    {
        var torches = _layout!.Objects.Where(o => o.Kind == PreviewKind.Scenery && TorchPids.Contains(o.Source.Header.Pid)).ToArray();
        var lights = _layout.Objects.Where(o => EntityPresentation.Role(o.Source) == EntityVisualRole.LightSource).ToArray();
        var iron = resources.Own(new StandardMaterial3D { AlbedoColor = new Color("2b2622"), Metallic = .6f, Roughness = .55f });
        var flameMaterial = resources.Own(new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(1, .62f, .25f),
            EmissionEnabled = true, Emission = new Color(1, .5f, .16f), EmissionEnergyMultiplier = 5 });
        var bracket = resources.Own(new BoxMesh { Size = new Vector3(.07f, .07f, .3f) });
        var cup = resources.Own(new CylinderMesh { TopRadius = .09f, BottomRadius = .05f, Height = .16f, RadialSegments = 10 });
        var flame = resources.Own(new SphereMesh { Radius = .08f, Height = .28f, RadialSegments = 10, Rings = 5 });
        foreach (PreviewObject torch in torches)
        {
            // The original light emitter sits on the walkable hex beside the wall torch; the torch faces it.
            GroundPoint at = torch.Position;
            PreviewObject? emitter = lights.Where(l => FalloutGrid.Distance(l.Source.Header.Tile, torch.Source.Header.Tile) <= 2)
                .MinBy(l => FalloutGrid.Distance(l.Source.Header.Tile, torch.Source.Header.Tile));
            GroundPoint toward = emitter != null ? emitter.Position - at : new GroundPoint(0, 1);
            double length = Math.Sqrt(toward.X * toward.X + toward.Z * toward.Z);
            if (length < 1e-6) { toward = new GroundPoint(0, 1); length = 1; }
            var face = new Vector3((float)(toward.X / length), 0, (float)(toward.Z / length));
            var root = new Node3D { Name = $"Torch_{torch.Source.Header.Id}", Basis = Basis.LookingAt(face, Vector3.Up, true),
                Position = Point(at, 1.55f) + face * (float)(TempleArchitecture.WallHalfThickness + .05) };
            root.AddChild(new MeshInstance3D { Mesh = bracket, MaterialOverride = iron, Position = new Vector3(0, 0, -.1f) });
            root.AddChild(new MeshInstance3D { Mesh = cup, MaterialOverride = iron, Position = new Vector3(0, .08f, .06f) });
            root.AddChild(new MeshInstance3D { Mesh = flame, MaterialOverride = flameMaterial, Position = new Vector3(0, .25f, .06f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            _architecture!.AddChild(root);
            foreach (MeshInstance3D part in root.GetChildren().OfType<MeshInstance3D>()) _architectureParts.Add(new(part, new[] { torch.Source.Key }));
            _bodies[torch.Source.Key].GetChild<MeshInstance3D>(0).Mesh = null;
            _architectureTorches++;
        }
        foreach (PreviewObject light in lights)
        {
            // Original radius in hexes; intensity 65536 is full strength in CE.
            float strength = Math.Clamp(light.Source.Header.LightIntensity / 65536f, .2f, 1f);
            PreviewObject? torch = torches.Where(t => FalloutGrid.Distance(t.Source.Header.Tile, light.Source.Header.Tile) <= 2)
                .MinBy(t => FalloutGrid.Distance(t.Source.Header.Tile, light.Source.Header.Tile));
            Vector3 position = torch != null ? (Point(torch.Position, 0) + Point(light.Position, 0)) / 2 + Vector3.Up * 1.9f : Point(light.Position, 1.9f);
            _architecture!.AddChild(new OmniLight3D { Name = $"Light_{light.Source.Header.Id}", Position = position,
                LightColor = new Color(1, .64f, .36f), LightEnergy = 2.4f * strength, LightSpecular = .35f,
                OmniRange = light.Source.Header.LightDistance + 3.5f, OmniAttenuation = .85f, ShadowEnabled = true, ShadowBias = .06f });
            // The lamp stand stays selectable; the torch and its light replace the provisional bulb.
            StaticBody3D body = _bodies[light.Source.Key];
            body.GetChild<MeshInstance3D>(0).Visible = false;
            if (body.GetNodeOrNull<MeshInstance3D>("LightBulb") is { } bulb) bulb.Visible = false;
            if (body.GetNodeOrNull<MeshInstance3D>("LightFoot") is { } foot) foot.Visible = EntityPresentation.Blocks(light.Source);
            _architectureLights++;
        }
    }

    private void OrientDoors()
    {
        var walls = _layout!.Objects.Where(o => o.Kind == PreviewKind.Wall).Select(o => o.Source.Header.Tile).ToHashSet();
        foreach (PreviewObject door in _layout.Objects.Where(o => o.Kind == PreviewKind.Door))
        {
            // Rotations 2 and 5 stay in the same hex column: a wall along world Z, so the door turns across it.
            bool alongZ = new[] { 2, 5 }.Any(d => FalloutGrid.Neighbor(door.Source.Header.Tile, d) is int n && walls.Contains(n));
            _bodies[door.Source.Key].Rotation = new Vector3(0, alongZ ? Mathf.Pi / 2 : 0, 0);
        }
    }

    private void ApplyArchitectureVisibility()
    {
        bool walls = _hud.Filters["wall"];
        foreach (ArchitecturePart part in _architectureParts)
            part.Node.Visible = walls && !part.Keys.Any(_cutWalls.Contains);
        bool floor = _hud.Filters["floor"] && !_useOriginalFloor;
        foreach (MultiMeshInstance3D chunk in _architectureFloor) chunk.Visible = floor;
    }

    private void ApplyLightingMode()
    {
        if (_environment == null || _sun == null) return;
        bool kit = ArchitectureActive;
        // Kit: dark stone over a black void, warm torches and a cool fill kept bright enough to read every corridor.
        _environment.BackgroundColor = new Color(kit ? "040506" : "101918");
        _environment.AmbientLightColor = new Color(kit ? "8a97a6" : "b2c4b7");
        _environment.AmbientLightEnergy = kit ? .55f : .65f;
        _environment.TonemapMode = kit ? Godot.Environment.ToneMapper.Agx : Godot.Environment.ToneMapper.Filmic;
        _environment.SsaoEnabled = kit; _environment.SsaoRadius = .7f; _environment.SsaoIntensity = 1.6f;
        _environment.GlowEnabled = kit; _environment.GlowIntensity = .45f; _environment.GlowBloom = .05f;
        _sun.LightColor = new Color(kit ? "a9b8cc" : "f0d8ac");
        _sun.LightEnergy = kit ? .45f : 1.3f;
    }

    private void SetArchitecture(bool enabled)
    {
        if (enabled == _architectureEnabled) { _hud.Architecture(ArchitectureActive, _kit != null, _kitStatus); return; }
        if (_walker?.Moving == true) { _hud.Architecture(ArchitectureActive, _kit != null, _kitStatus); NavigationStatus("Termine ou pare a caminhada [Esc] antes de trocar a arquitetura."); return; }
        _architectureEnabled = enabled;
        // Sizes of walls/doors differ between modes: rebuild shared shapes and meshes.
        _shapes.Clear(); _meshes.Clear(); _materials.Clear();
        if (_map != null) { PreviewObject? selected = _selected; BuildWorld(); if (selected != null && _bodies.ContainsKey(selected.Source.Key)) SelectObject(selected); }
        BindStatus();
        _hud.Architecture(ArchitectureActive, _kit != null, _kitStatus);
    }

    private async Task CheckTempleArchitecture()
    {
        if (_kit == null)
        {
            // Without the local kit the scene must stay fully usable with provisional shapes.
            if (_architectureParts.Count != 0 || _layout!.Objects.Where(o => o.Kind == PreviewKind.Wall).Any(w => _bodies[w.Source.Key].GetChild<MeshInstance3D>(0).Mesh == null))
                throw new InvalidDataException("Missing kit left walls without provisional meshes.");
            GD.Print($"TEMPLE_ARCHITECTURE_UNAVAILABLE: {_kitStatus}");
            return;
        }
        foreach (int elevation in new[] { 0, 1, 2 })
        {
            ChangeElevation(elevation);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var walls = _layout!.Objects.Where(IsArchitectureWall).ToArray();
            if (walls.Length == 0 || _wallPlan == null) throw new InvalidDataException("Temple elevation without kit walls.");
            foreach (PreviewObject wall in walls)
                if (!_wallPlan.Covers(wall.Source.Header.Tile) || !_architectureParts.Any(p => p.Keys.Contains(wall.Source.Key)))
                    throw new InvalidDataException($"Source wall {wall.Source.Key} has no kit module.");
            // No open stretch between two touching wall hexes, including the blockers drawn as wall.
            var stone = walls.Select(w => w.Source.Header.Tile).Concat(_wallPlan.Filled).ToArray();
            var gaps = TempleArchitecture.UncoveredLinks(_wallPlan, stone, _layout.Origin).ToArray();
            if (gaps.Length != 0) throw new InvalidDataException($"Open gap between wall hexes {gaps[0].A} and {gaps[0].B}.");
            if (_wallPlan.Filled.Any(t => !_layout.Objects.Any(o => o.Source.Header.Tile == t && IsWallFiller(o))))
                throw new InvalidDataException("Stone drawn on a hex without an original blocker.");
            // Doors stay passable: every hex the source data leaves walkable (doors open) is clear of stone.
            var open = new HexNavigation(_map!.ObjectLayers.Single(l => l.Elevation == elevation).Objects, elevation,
                o => new ObjectTraversalState(PreviewLayout.Kind(o) == PreviewKind.Door ? o.Header.Flags | HexNavigation.NoBlock : o.Header.Flags, null));
            for (int tile = 0; tile < 40000; tile++)
            {
                if (!open.Walkable(tile)) continue;
                GroundPoint center = FalloutGrid.HexCenter(tile) - _layout.Origin;
                if (_wallPlan.Pieces.FirstOrDefault(p => p.Contains(center, .1)) is { } blocking)
                    throw new InvalidDataException($"Kit {blocking.Module} covers walkable hex {tile}.");
            }
            int temple = _layout.Floors.Count(IsTempleFloor);
            if (temple == 0 || _architectureFloorTiles != temple || _architectureFloor.Sum(c => c.Multimesh.InstanceCount) != temple)
                throw new InvalidDataException("Temple floor squares and kit tiles differ.");
            int lights = _layout.Objects.Count(o => EntityPresentation.Role(o.Source) == EntityVisualRole.LightSource);
            if (_architectureLights != lights || _architecture!.GetChildren().OfType<OmniLight3D>().Count() != lights) throw new InvalidDataException("Original light emitters and torch lights differ.");
            if (_layout.Objects.Where(o => o.Kind == PreviewKind.Wall).Any(w => _bodies[w.Source.Key].GetChild<MeshInstance3D>(0).Mesh != null))
                throw new InvalidDataException("A provisional wall box remains under the kit.");
            GD.Print($"TEMPLE_ARCHITECTURE_LAYER_OK: elevation {elevation}; {walls.Length} source wall objects; {_wallPlan.Filled.Length} blocker hexes closed; no open wall links; {_wallPlan.Runs.Length} runs; {_wallPlan.Pieces.Length} modules; {temple} floor tiles; {lights} torch lights; walkable hexes clear.");
        }
        GoToEntry(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _hud.Filters["wall"] = false; ApplyFilters();
        if (_architectureParts.Any(p => p.Node.Visible)) throw new InvalidDataException("Wall filter left kit modules visible.");
        _hud.Filters["wall"] = true; ApplyFilters();
        if (_architectureParts.Any(p => !p.Node.Visible)) throw new InvalidDataException("Kit modules hidden without a cutaway.");
        SetOriginalFloor(true);
        if (_architectureFloor.Any(c => c.Visible)) throw new InvalidDataException("Kit floor overlaps the original floor reference.");
        SetOriginalFloor(false);
        int reach = _reachable!.Count, objects = _lookup.Count;
        using var key = new InputEventKey { Pressed = true, PhysicalKeycode = Key.P };
        _UnhandledInput(key);
        if (ArchitectureActive || _architectureParts.Count != 0 || _layout!.Objects.Where(o => o.Kind == PreviewKind.Wall).Any(w => _bodies[w.Source.Key].GetChild<MeshInstance3D>(0).Mesh == null))
            throw new InvalidDataException("Provisional comparison did not restore the reference shapes.");
        if (_reachable!.Count != reach || _lookup.Count != objects) throw new InvalidDataException("Architecture switch changed navigation or source objects.");
        _UnhandledInput(key);
        if (!ArchitectureActive || _architectureParts.Count == 0 || _reachable!.Count != reach) throw new InvalidDataException("Kit architecture did not return.");
        MapLayoutWriter.Verify(_map!);
        if (_capture != null)
        {
            FocusEntry(); await Capture(Path.ChangeExtension(_capture, "architecture.png"));
            _camera.Size = 14; UpdateCamera(); await Capture(Path.ChangeExtension(_capture, "architecture-close.png"));
            // Review shots where original blockers now continue the masonry.
            int[] closed = _wallPlan!.Filled.OrderBy(t => t).ToArray();
            for (int i = 0; i < 3 && closed.Length > 0; i++)
            {
                _focus = Point(FalloutGrid.HexCenter(closed[closed.Length * (2 * i + 1) / 6]) - _layout!.Origin, 0); _camera.Size = 16; UpdateCamera();
                await Capture(Path.ChangeExtension(_capture, $"architecture-joint{i + 1}.png"));
            }
            foreach (int elevation in new[] { 1, 2 })
            {
                ChangeElevation(elevation); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await Capture(Path.ChangeExtension(_capture, $"architecture-elevation{elevation}.png"));
            }
            GoToEntry(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        GD.Print($"TEMPLE_ARCHITECTURE_OK: kit walls, pillars, floor and torch lights on 3 elevations; filters, original floor, P switch, navigation and source bytes verified; {_kitStatus}.");
    }
}

public partial class PreviewHud
{
    private readonly CheckButton _architecture = new() { Name = "ArchitectureAction", Text = "Arquitetura 3D [P]", ButtonPressed = true, ClipText = true };
    public Action<bool>? ArchitectureRequested;
    private void AddArchitectureControls(VBoxContainer controls)
    {
        controls.AddChild(_architecture); _architecture.Toggled += enabled => ArchitectureRequested?.Invoke(enabled);
        _architecture.TooltipText = "Alternar entre o kit modular do Temple e as formas provisórias. IDs, bloqueios e caminhos permanecem iguais.";
    }
    public void Architecture(bool active, bool available, string status)
    {
        _architecture.SetPressedNoSignal(active); _architecture.Disabled = !available;
        _architecture.TooltipText = status + ". Alterna com as formas provisórias; IDs, bloqueios e caminhos permanecem iguais.";
    }
}
