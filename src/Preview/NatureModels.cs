using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FalloutRemake.Geometry;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private sealed record NaturePart(Mesh Mesh, Material? Material, Transform3D Transform);
    private sealed record NatureAsset(PackedScene Scene, NaturePart[] Parts, Aabb Bounds);
    private sealed record NatureInstance(string Key, Transform3D Transform);
    private sealed record NatureBatch(MultiMeshInstance3D Node, NatureInstance[] Instances);
    private readonly Dictionary<string,NatureAsset?> _natureAssets = new();
    private readonly List<NatureBatch> _natureBatches = new();
    private readonly Dictionary<Node3D,bool> _natureOriginals = new();
    private bool _natureEnabled = true, _natureTest;
    private int _natureCount, _natureTreeCount;
    private static string NaturePath(string name) => name switch {
        "RiverRock1" or "RockS01" or "BlastedForestBoulder01" or "BlastedForestCliff01" or "BlastedForestBurntTreeStump01"
            => $"res://Assets/Nature/{name}.glb",
        _ => throw new InvalidDataException("Only the local Nature category is supported.") };
    private NatureAsset? LoadNatureAsset(string name)
    {
        if (_natureAssets.TryGetValue(name,out var existing)) return existing;
        string path = NaturePath(name);
        if (!ResourceLoader.Exists(path)) { _natureAssets.Add(name,null); GD.Print($"NATURE_ASSET_UNAVAILABLE: {name}; provisional geometry retained."); return null; }
        var scene = _resources.Own(ResourceLoader.Load<PackedScene>(path));
        Node root = scene.Instantiate(); var parts = new List<NaturePart>();
        Vector3 min = new(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity), max = -min;
        try
        {
            void Visit(Node node, Transform3D parent)
            {
                Transform3D transform = node is Node3D n ? parent * n.Transform : parent;
                if (node is MeshInstance3D { Mesh: { } mesh } instance)
                {
                    parts.Add(new(mesh,instance.MaterialOverride,transform));
                    var bounds = mesh.GetAabb();
                    for (int i = 0; i < 8; i++) { var point = transform * bounds.GetEndpoint(i); min = min.Min(point); max = max.Max(point); }
                }
                foreach (Node child in node.GetChildren()) Visit(child,transform);
            }
            Visit(root,Transform3D.Identity);
            if (parts.Count == 0 || !min.IsFinite() || !max.IsFinite() || (max-min).Length() <= 0)
                throw new InvalidDataException("Nature scene has no bounded mesh geometry.");
        }
        finally { root.Free(); }
        var asset = new NatureAsset(scene,parts.ToArray(),new(min,max-min)); _natureAssets.Add(name,asset);
        GD.Print($"NATURE_ASSET_READY: {name}; {parts.Count} shared mesh parts; bounds {asset.Bounds.Size}.");
        return asset;
    }
    private void BuildNatureModels()
    {
        _natureBatches.Clear(); _natureOriginals.Clear(); ResetNaturePicking(); _natureCount = _natureTreeCount = 0;
        if (_layout == null || _campaign == null) return;
        using var resources = new ResourceScope();
        var groups = _layout.Objects.Select(o => (Object:o,Model:NaturePresentation.Resolve(_campaign.CurrentMap,o.Source)))
            .Where(pair => pair.Model != null).GroupBy(pair => pair.Model!.Name);
        foreach (var group in groups)
        {
            var asset = LoadNatureAsset(group.Key); if (asset == null) continue;
            var placements = new List<NatureInstance>();
            foreach (var pair in group)
            {
                var obj = pair.Object; var body = _bodies[obj.Source.Key];
                foreach (Node child in body.GetChildren())
                    if (child is VisualInstance3D visual) _natureOriginals.Add(visual,visual.Visible);
                float scale = pair.Model!.Width / Math.Max(asset.Bounds.Size.X,asset.Bounds.Size.Z);
                var basis = new Basis(Vector3.Up,obj.Source.Header.Rotation * Mathf.Pi / 3).Scaled(Vector3.One * scale);
                var anchor = asset.Bounds.Position + new Vector3(asset.Bounds.Size.X / 2,0,asset.Bounds.Size.Z / 2);
                var placement = new Transform3D(basis,Point(obj.Position,0) - basis * anchor);
                placements.Add(new(obj.Source.Key,placement));
                ConfigureNaturePicking(body,asset,placement);
                _natureCount++;
                if (pair.Model.Tree) _natureTreeCount++;
            }
            foreach (var part in asset.Parts)
            {
                var mesh = resources.Own(new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                    Mesh = part.Mesh, InstanceCount = placements.Count, VisibleInstanceCount = 0 });
                var node = new MultiMeshInstance3D { Name = "Nature_" + group.Key,Multimesh = mesh,MaterialOverride = part.Material,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.On, LodBias = .75f };
                _world.AddChild(node);
                _natureBatches.Add(new(node,placements.Select(p => p with { Transform = p.Transform * part.Transform }).ToArray()));
            }
        }
        ApplyNatureModels();
    }
    private void SetNatureModels(bool enabled)
    { _natureEnabled = enabled; ApplyNatureModels(); if (_selected != null) SelectObject(_selected); }
    private string? NatureDescription(PreviewObject obj)
    {
        var model = _campaign == null ? null : NaturePresentation.Resolve(_campaign.CurrentMap,obj.Source);
        if (!_natureEnabled || model == null || !_natureAssets.TryGetValue(model.Name,out var asset) || asset == null) return null;
        return (model.Tree ? "Árvore queimada" : "Rocha") + " · modelo Nature. "
            + (EntityPresentation.Blocks(obj.Source) ? "Mantém o bloqueio original da passagem." : "Não bloqueia a passagem no mapa original.");
    }
    private void ApplyNatureModels()
    {
        ApplyNaturePicking();
        foreach (var pair in _natureOriginals) pair.Key.Visible = !_natureEnabled && pair.Value;
        foreach (var batch in _natureBatches)
        {
            int index = 0;
            foreach (var instance in batch.Instances)
                if (_natureEnabled && _bodies[instance.Key].Visible)
                    batch.Node.Multimesh.SetInstanceTransform(index++,instance.Transform);
            batch.Node.Multimesh.VisibleInstanceCount = index;
            batch.Node.Visible = index > 0;
        }
        _hud.NatureModelsState(_natureEnabled,_natureCount,_natureTreeCount);
    }
}

public partial class PreviewHud
{
    private readonly CheckButton _nature = new() { Name = "NatureModelsAction", Text = "Nature 3D",ButtonPressed = true,ClipText = true };
    public Action<bool>? NatureModelsRequested;
    private void AddNatureControls(VBoxContainer controls)
    {
        controls.AddChild(_nature); _nature.Toggled += enabled => NatureModelsRequested?.Invoke(enabled);
        _nature.TooltipText = "Comparar os modelos Nature com as formas provisórias. IDs e caminhos permanecem iguais.";
    }
    public void NatureModelsState(bool enabled,int count,int trees)
    {
        _nature.SetPressedNoSignal(enabled); _nature.Disabled = count == 0;
        _nature.Text = count == 0 ? "Nature 3D" : $"Nature 3D · {count} objetos";
        _nature.TooltipText = $"{count-trees} rochas e {trees} árvores queimadas. Compare com as formas provisórias; IDs e caminhos permanecem iguais.";
    }
}
