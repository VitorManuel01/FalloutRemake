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
    private FloorArtCatalog? _floorArt;
    private bool _useOriginalFloor;
    private Node3D? _originalFloor;
    private ArrayMesh? _floorArtworkMesh;
    private readonly Dictionary<string, StandardMaterial3D> _floorArtworkMaterials = new();

    private void SetOriginalFloor(bool enabled)
    {
        _useOriginalFloor = enabled;
        if (enabled && _layout != null && _originalFloor == null)
        {
            Node3D? candidate = null;
            try
            {
                using var resources = new ResourceScope();
                candidate = new Node3D { Name = "OriginalFloorArtwork" };
                foreach (var group in _layout.Floors.GroupBy(f => (int)(f.Raw & 0xFFF)))
                {
                    FloorArt tile = _floorArt!.Get(group.Key);
                    StandardMaterial3D material = FloorArtworkMaterial(tile);
                    PreviewFloor[] cells = group.ToArray();
                    var instances = resources.Own(new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                        Mesh = FloorArtworkMesh(), InstanceCount = cells.Length });
                    for (int i = 0; i < cells.Length; i++)
                        instances.SetInstanceTransform(i, new Transform3D(Basis.Identity, Point(cells[i].Position, -.02f)));
                    var node = new MultiMeshInstance3D { Name = $"Floor_{tile.Id}", Multimesh = instances,
                        MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                    node.SetMeta("floor_id", tile.Id); node.SetMeta("source_path", tile.Source.Path); node.SetMeta("source_hash", tile.Source.Sha256);
                    node.SetMeta("source_squares", cells.Select(f => f.Square).ToArray());
                    candidate.AddChild(node);
                }
                _world.AddChild(candidate); _originalFloor = candidate; candidate = null;
            }
            catch (Exception error) when (error is IOException or InvalidDataException)
            {
                _useOriginalFloor = false;
                NavigationStatus("Piso original indisponível para estes dados; referência em volumes mantida");
                GD.Print($"FLOOR_ART_UNAVAILABLE: {error.Message}");
            }
            finally { candidate?.Free(); }
        }
        _hud.OriginalFloor(_useOriginalFloor);
        ApplyFloorVisibility();
    }
    private void ApplyFloorVisibility()
    {
        if (_floor != null) _floor.Visible = _hud.Filters["floor"] && !_useOriginalFloor;
        if (_originalFloor != null) _originalFloor.Visible = _hud.Filters["floor"] && _useOriginalFloor;
    }
    private ArrayMesh FloorArtworkMesh()
    {
        if (_floorArtworkMesh != null) return _floorArtworkMesh;
        Vector2[] uv = { new(0, 0), new(1, 0), new(1, 1), new(0, 1) };
        using var arrays = new Godot.Collections.Array(); arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = uv.Select(p => Point(FloorArtwork.Corner(p.X, p.Y), 0)).ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = new[] { Vector3.Up, Vector3.Up, Vector3.Up, Vector3.Up };
        arrays[(int)Mesh.ArrayType.TexUV] = uv;
        arrays[(int)Mesh.ArrayType.Index] = new[] { 0, 1, 2, 0, 2, 3 };
        _floorArtworkMesh = _resources.Own(new ArrayMesh());
        _floorArtworkMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return _floorArtworkMesh;
    }
    private StandardMaterial3D FloorArtworkMaterial(FloorArt tile)
    {
        string palette = _floorArt!.Dependencies.Single(d => d.Path == "color.pal").Sha256;
        string key = palette + tile.Source.Sha256;
        if (_floorArtworkMaterials.TryGetValue(key, out StandardMaterial3D? material)) return material;
        if (_floorArtworkMaterials.Count >= 512) throw new InvalidDataException("Floor artwork cache limit reached.");
        using var image = Image.CreateFromData(FloorArt.Width, FloorArt.Height, false, Image.Format.Rgba8, tile.Rgba.ToArray());
        var texture = _resources.Own(ImageTexture.CreateFromImage(image));
        material = _resources.Own(new StandardMaterial3D { AlbedoTexture = texture, Roughness = 1,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor, AlphaScissorThreshold = .5f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled });
        _floorArtworkMaterials.Add(key, material); return material;
    }

    private async Task CheckOriginalFloor()
    {
        ResetWalker();
        using var toggle = new InputEventKey { Pressed = true, PhysicalKeycode = Key.O };
        _UnhandledInput(toggle);
        if (!_useOriginalFloor) throw new InvalidDataException("Original floor toggle failed.");
        foreach (int elevation in new[] { 0, 1, 2 })
        {
            ChangeElevation(elevation);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            CheckOriginalFloorInstances();
            MapLayoutWriter.Verify(_map!);
        }
        GoToEntry();
        var location = _campaign!.Location;
        int reachable = _reachable!.Count;
        _hud.Filters["floor"] = false; ApplyFilters();
        if (_floor!.Visible || _originalFloor!.Visible) throw new InvalidDataException("Hidden original floor remained visible.");
        _hud.Filters["floor"] = true; ApplyFilters();
        if (_floor.Visible || !_originalFloor.Visible) throw new InvalidDataException("Original floor filter lost mode.");
        var button = _hud.FindChild("OriginalFloorAction", true, false) as Button ?? throw new InvalidDataException("Floor reference button missing.");
        button.EmitSignal(BaseButton.SignalName.Toggled, false);
        if (_useOriginalFloor || !_floor.Visible || _originalFloor.Visible) throw new InvalidDataException("Floor reference button failed.");
        _UnhandledInput(toggle);
        if (_campaign.Location != location || _reachable.Count != reachable) throw new InvalidDataException("Floor art changed navigation or actor.");
        _hud.Select("Piso original · referência", "Imagens da instalação local, lidas em memória.\n\nIDs e células originais preservados. Paredes e objetos ainda usam formas provisórias.");
        if (_capture != null) await Capture(Path.ChangeExtension(_capture, "floor.png"));
        // Reuse the normal walking/door/exit path with artwork enabled on both sides.
        await CheckWorldTravel();
        if (!_useOriginalFloor) throw new InvalidDataException("Area travel lost original floor mode.");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        CheckOriginalFloorInstances();
        // The third configured area is checked structurally, without claiming its scripts run.
        MapDocument village = _mapCatalog!.Load(new GameData(_directory!), 4);
        foreach (var cell in PreviewLayout.Create(village, 0).Floors) _floorArt!.Get((int)(cell.Raw & 0xFFF));
        foreach (var source in _floorArt!.Dependencies)
            GD.Print($"FLOOR_RESOURCE: {source.Path}; {source.Size} bytes; {source.Sha256}");
        SetOriginalFloor(false); ClearSelection();
        FloorArtCatalog original = _floorArt;
        try
        {
            _floorArt = new FloorArtCatalog(_ => throw new FileNotFoundException("Synthetic missing floor resource."));
            ChangeElevation(0); var before = _campaign.Location; int beforeReach = _reachable!.Count;
            SetOriginalFloor(true);
            if (_useOriginalFloor || !_floor!.Visible || _originalFloor != null || _campaign.Location != before || _reachable.Count != beforeReach)
                throw new InvalidDataException("Missing artwork failed to retain the placeholder scene and actor.");
        }
        finally { _floorArt = original; }
        GD.Print($"FLOOR_ART_PREVIEW_OK: 3 temple elevations, exterior travel, village floor resources, IDs, square transforms, palette, filters, key/button, navigation and source bytes verified; {_floorArt!.Dependencies.Length} dependencies.");
    }
    private void CheckOriginalFloorInstances()
    {
        if (!_useOriginalFloor || _originalFloor == null || !_originalFloor.Visible || _floor!.Visible)
            throw new InvalidDataException("Original floor unavailable.");
        int total = 0;
        foreach (MultiMeshInstance3D node in _originalFloor.GetChildren())
        {
            int id = node.GetMeta("floor_id").AsInt32();
            PreviewFloor[] cells = _layout!.Floors.Where(f => (f.Raw & 0xFFF) == id).ToArray();
            using var instances = node.Multimesh;
            if (instances.InstanceCount != cells.Length || !node.GetMeta("source_squares").AsInt32Array().SequenceEqual(cells.Select(f => f.Square)))
                throw new InvalidDataException("Original floor IDs/cells changed.");
            // The dummy headless renderer returns zero transforms; verify GPU storage in the graphical run.
            for (int i = 0; DisplayServer.GetName() != "headless" && i < cells.Length; i++)
                if (instances.GetInstanceTransform(i).Origin.DistanceTo(Point(cells[i].Position, -.02f)) > .0001f)
                    throw new InvalidDataException($"Original floor {id} square {cells[i].Square} transform {instances.GetInstanceTransform(i).Origin} != {Point(cells[i].Position, -.02f)}.");
            total += instances.InstanceCount;
        }
        if (total != _layout!.Floors.Length) throw new InvalidDataException("Original floor instance count mismatch.");
    }
}
