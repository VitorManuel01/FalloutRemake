using System.Collections.Generic;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private readonly Dictionary<ulong,ConcavePolygonShape3D> _naturePickingMeshes = new();
    private readonly Dictionary<CollisionShape3D,bool> _natureProxies = new();
    private readonly List<CollisionShape3D> _naturePicking = new();
    private void ResetNaturePicking() { _natureProxies.Clear(); _naturePicking.Clear(); }
    private void ConfigureNaturePicking(StaticBody3D body,NatureAsset asset,Transform3D placement)
    {
        var proxy = body.GetChild<CollisionShape3D>(1); _natureProxies.Add(proxy,proxy.Disabled);
        foreach (var part in asset.Parts)
        {
            ulong id = part.Mesh.GetInstanceId();
            if (!_naturePickingMeshes.TryGetValue(id,out var shape))
            {
                shape = _resources.Own(part.Mesh.CreateTrimeshShape()); shape.BackfaceCollision = true;
                _naturePickingMeshes.Add(id,shape);
            }
            // Picking-only: the existing source body still owns metadata and layer 1, with mask zero.
            // Gameplay navigation continues to read the original hex graph, never these triangles.
            var collider = new CollisionShape3D {Name = "NaturePicking",Shape = shape,
                Transform = body.Transform.AffineInverse() * placement * part.Transform,Disabled = !_natureEnabled};
            body.AddChild(collider); _naturePicking.Add(collider);
        }
    }
    private void ApplyNaturePicking()
    {
        foreach (var pair in _natureProxies) pair.Key.Disabled = _natureEnabled || pair.Value;
        foreach (var shape in _naturePicking) shape.Disabled = !_natureEnabled;
    }
}
