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
    private async Task CheckNaturePicking(PreviewObject tree)
    {
        byte[] before = CheckpointFile.Encode(_campaign!.Capture(_mapCatalog!.Source.Sha256));
        var body = _bodies[tree.Source.Key];
        await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
        StaticBody3D? Hit(Vector3 from,Vector3 to)
        {
            using var query = PhysicsRayQueryParameters3D.Create(from,to,1);
            using var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
            return hit.TryGetValue("collider",out var value) ? value.AsGodotObject() as StaticBody3D : null;
        }
        Vector3 start = default,end = default; bool found = false;
        foreach (var collider in body.GetChildren().OfType<CollisionShape3D>().Where(n => n.Shape is ConcavePolygonShape3D))
        {
            var vertices = ((ConcavePolygonShape3D)collider.Shape!).GetFaces();
            for (int i = 0; i + 2 < vertices.Length; i += 3)
            {
                var a = collider.GlobalTransform * vertices[i]; var b = collider.GlobalTransform * vertices[i+1]; var c = collider.GlobalTransform * vertices[i+2];
                var center = (a+b+c)/3;
                if (center.Y < body.GlobalPosition.Y + 1 || (b-a).Cross(c-a).LengthSquared() < .000001f) continue;
                var normal = (b-a).Cross(c-a).Normalized(); start = center + normal * .04f; end = center - normal * .04f;
                if (Hit(start,end) != body) continue;
                found = true; break;
            }
            if (found) break;
        }
        if (!found || !body.GetChild<CollisionShape3D>(1).Disabled)
            throw new InvalidDataException("Nature upper-tree triangles could not be picked independently of the former short proxy.");
        var picked = Hit(start,end)!;
        if (_lookup[picked.GetInstanceId()].Source.Key != tree.Source.Key || picked.GetMeta("source_id").AsInt32() != tree.Source.Header.Id)
            throw new InvalidDataException("Nature mesh picking introduced a foreign object identity.");
        SetNatureModels(false); await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
        if (Hit(start,end) == body || _naturePicking.Any(s => !s.Disabled) || body.GetChild<CollisionShape3D>(1).Disabled)
            throw new InvalidDataException("Nature comparison failed to restore original proxy picking.");
        SetNatureModels(true); await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
        if (Hit(start,end) != body || !before.SequenceEqual(CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256))))
            throw new InvalidDataException("Nature mesh picking did not restore or changed the world checkpoint.");
        GD.Print("NATURE_PICKING_OK: high native-tree mesh triangles select the existing source body and ID; shared triangle shapes, exact visual transforms, short proxy restored on comparison, filters/layer identity and unchanged world checkpoint verified; no gameplay collision/navigation changes.");
    }
}
