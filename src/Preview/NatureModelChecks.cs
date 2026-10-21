using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private async Task CheckNatureModels()
    {
        var baseline = _campaign!; bool enabled = _natureEnabled;
        try
        {
            if (_natureCount != 0) throw new InvalidDataException("Temple benches were incorrectly classified as natural rocks.");
            var data = new GameData(_directory!);
            foreach (int id in new[] {126,4})
            {
                _campaign = new WorldSession(map => _mapCatalog!.Load(data,map),new[] {3,4,126},initialMap:id);
                BindCurrentArea(); ChangeElevation(0);
                byte[] before = CheckpointFile.Encode(_campaign.Capture(_mapCatalog!.Source.Sha256));
                var source = _layout!.Objects.Where(o => NaturePresentation.Resolve(id,o.Source) != null).ToArray();
                if (_natureCount != source.Length || _natureCount == 0 || _natureBatches.Count is < 1 or > 4
                    || _natureTreeCount != (id == 126 ? 88 : 0))
                    throw new InvalidDataException($"Nature batch/instance counts disagree with original map {id}: {_natureCount}/{source.Length} in {_natureBatches.Count} batches.");
                foreach (var asset in _natureAssets.Where(a => a.Value != null))
                    if (asset.Key is not ("RockS01" or "RiverRock1" or "BlastedForestBurntTreeStump01")) throw new InvalidDataException("An unselected Nature model or another category was loaded.");
                SetNatureModels(true);
                if (_natureOriginals.Keys.Any(n => n.Visible) || !_natureBatches.Any(b => b.Node.Visible))
                    throw new InvalidDataException("Nature models overlapped provisional rock geometry.");
                foreach (var obj in source)
                {
                    var body = _bodies[obj.Source.Key];
                    if (body.GetMeta("source_id").AsInt32() != obj.Source.Header.Id || _lookup[body.GetInstanceId()].Source.Key != obj.Source.Key
                        || body.CollisionLayer != (VisibleObject(obj) ? 1u : 0u)) throw new InvalidDataException("Nature rendering changed original picking identity.");
                }
                var selected = source.First(o => VisibleObject(o)); SelectObject(selected);
                if (!_hud.SelectionDetails.Contains($"PID  {selected.Source.Header.Pid}")) throw new InvalidDataException("Nature source selection lost original PID.");
                _hud.Filters["scenery"] = false; ApplyFilters();
                if (_natureBatches.Any(b => b.Node.Visible)) throw new InvalidDataException("Hidden scenery left Nature batches visible.");
                _hud.Filters["scenery"] = true; ApplyFilters();
                SetNatureModels(false);
                if (_natureBatches.Any(b => b.Node.Visible) || _natureOriginals.Any(p => p.Key.Visible != p.Value))
                    throw new InvalidDataException("Nature comparison did not restore the provisional meshes exactly.");
                SetNatureModels(true);
                if (!before.SequenceEqual(CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256))))
                    throw new InvalidDataException("Nature/filter comparison changed world state.");
                MapLayoutWriter.Verify(_map!);
                _focus = Point(selected.Position,0); _camera.Size = 14; UpdateCamera();
                if (_capture != null) await Capture(Path.ChangeExtension(_capture,$"nature-map-{id}.png"));
                if (id == 126)
                {
                    var tree = source.First(o => NaturePresentation.Resolve(id,o.Source)!.Tree && VisibleObject(o));
                    SelectObject(tree);
                    if (_selected?.Source.Key != tree.Source.Key || !_hud.SelectionHeading.Contains("Tree")
                        || !_hud.SelectionDetails.Contains("Árvore queimada") || _hud.SelectionDetails.Contains("placa vermelha"))
                        throw new InvalidDataException("Nature tree selection lost original name and identity.");
                    SetNatureModels(false);
                    if (!_hud.SelectionDetails.Contains("placa vermelha")) throw new InvalidDataException("Nature comparison left stale inspector appearance.");
                    SetNatureModels(true);
                    if (!_hud.SelectionDetails.Contains("Árvore queimada")) throw new InvalidDataException("Nature inspector did not follow the visible tree model.");
                    _focus = Point(tree.Position,0); _camera.Size = 14; UpdateCamera();
                    if (_capture != null) await Capture(Path.ChangeExtension(_capture,"nature-exterior-trees.png"));
                    GD.Print($"NATURE_TREES_OK: 88 original exterior trees, one shared Nature burnt-tree mesh batch, original rotations/IDs/blockers/picking, comparison/filter and unchanged checkpoint verified.");
                }
                GD.Print($"NATURE_MAP_OK: map {id}; {_natureCount-_natureTreeCount} rocks and {_natureTreeCount} trees in {_natureBatches.Count} shared mesh batches; source IDs, picking, filters, comparison and checkpoint preserved.");
            }
            GD.Print("NATURE_MODELS_OK: local Nature-only meshes and PBR materials, shared batched rendering, original footprint anchors/rotations, no proprietary assets copied, no navigation or state changes; missing models keep provisional geometry.");
        }
        finally { _campaign = baseline; _natureEnabled = enabled; BindCurrentArea(); ChangeElevation(baseline.Location.Elevation); }
    }
}
