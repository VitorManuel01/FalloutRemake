using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Geometry;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private async Task CheckNatureAssetFallback(PreviewObject[] source)
    {
        const string name = "RockS01";
        var original = _natureAssets[name]; var exists = _natureAssetAvailable;
        byte[] before = CheckpointFile.Encode(_campaign!.Capture(_mapCatalog!.Source.Sha256));
        var affected = source.Where(o => NaturePresentation.Resolve(126,o.Source)?.Name == name).ToArray();
        try
        {
            _natureAssets.Remove(name);
            _natureAssetAvailable = path => !path.EndsWith("/RockS01.glb",StringComparison.Ordinal) && exists(path);
            BuildWorld();
            if (_natureMissingObjects != affected.Length || _natureCount != source.Length-affected.Length || affected.Length == 0)
                throw new InvalidDataException("Missing Nature model did not preserve the remaining batches and exact fallback count.");
            foreach (var obj in affected)
            {
                var body = _bodies[obj.Source.Key];
                if (!body.GetChild<MeshInstance3D>(0).Visible || body.GetChild<CollisionShape3D>(1).Disabled
                    || body.GetChildren().OfType<CollisionShape3D>().Any(c => c.Shape is ConcavePolygonShape3D)
                    || _lookup[body.GetInstanceId()].Source.Key != obj.Source.Key)
                    throw new InvalidDataException("Missing Nature model lost its provisional appearance, picking or original identity.");
            }
            if (!before.SequenceEqual(CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256))))
                throw new InvalidDataException("Missing Nature model changed gameplay state.");
            // Simulate a newly available imported file without moving or modifying any user asset.
            _natureAssetAvailable = exists; BuildWorld();
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            if (_natureMissingObjects != 0 || _natureCount != source.Length || _natureAssets[name] == null
                || !before.SequenceEqual(CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256))))
                throw new InvalidDataException("A previously absent Nature model did not recover on scene rebuild.");
            GD.Print("NATURE_ASSET_FALLBACK_OK: simulated unavailable local model keeps original geometry/picking/IDs and remaining Nature batches, counts missing objects, retries a negative cache when the model returns; rebuild recovers without asset file writes or gameplay changes.");
        }
        finally { _natureAssetAvailable = exists; _natureAssets[name] = original; }
    }
}
