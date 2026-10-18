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
    private bool _revealActor = true, _occlusionDirty = true;
    private PreviewObject[] _walls = Array.Empty<PreviewObject>();
    private readonly HashSet<string> _cutWalls = new();
    private Vector3 _lastSightFrom, _lastSightTo;
    private static SightPoint Sight(Vector3 p) => new(p.X, p.Y, p.Z);
    private static bool WallCovers(Vector3 from, Vector3 to, PreviewObject wall)
    {
        Vector3 size = Dimensions(PreviewKind.Wall);
        return SightOcclusion.Intersects(Sight(from), Sight(to), Sight(Point(wall.Position, size.Y / 2)),
            Sight(size / 2 + new Vector3(.15f, .12f, .15f)));
    }
    private void SetRevealActor(bool enabled)
    {
        _revealActor = enabled; _occlusionDirty = true; _hud.RevealActor(enabled); UpdateWallOcclusion();
    }
    private void UpdateWallOcclusion()
    {
        Vector3 to = (_walkerNode?.Position ?? Vector3.Zero) + Vector3.Up * .2f;
        Vector3 from = _camera.ProjectRayOrigin(_camera.UnprojectPosition(to));
        bool active = _revealActor && _walkerNode != null && _elevation == _travel?.Location.Elevation;
        if (!_occlusionDirty && from.IsEqualApprox(_lastSightFrom) && to.IsEqualApprox(_lastSightTo)) return;
        _lastSightFrom = from; _lastSightTo = to; _occlusionDirty = false;
        foreach (PreviewObject wall in _walls)
        {
            bool cut = active && VisibleObject(wall) && WallCovers(from, to, wall);
            _bodies[wall.Source.Key].GetChild<MeshInstance3D>(0).Visible = !cut;
            if (cut) _cutWalls.Add(wall.Source.Key); else _cutWalls.Remove(wall.Source.Key);
        }
    }
    private async Task CheckWallOcclusion()
    {
        _checkingNavigation = true;
        try
        {
            ResetWalker(); SetRevealActor(true); FocusCharacter();
            var doors = _doors!.Capture(); int reachCount = _reachable!.Count;
            // Find a real reachable approach where the normal orthographic view covers the actor's feet.
            int approach = -1;
            foreach (PreviewObject wall in _walls)
            {
                for (int d = 0; d < 6; d++)
                {
                    int? hex = FalloutGrid.Neighbor(wall.Source.Header.Tile, d);
                    if (!hex.HasValue || _reachable.Distance(hex.Value) < 0 || _navigation!.IsExit(hex.Value)) continue;
                    Vector3 target = Point(FalloutGrid.HexCenter(hex.Value) - _layout!.Origin, .2f);
                    if (WallCovers(_camera.ProjectRayOrigin(_camera.UnprojectPosition(target)), target, wall))
                    { approach = hex.Value; break; }
                }
                if (approach >= 0) break;
            }
            if (approach < 0) throw new InvalidDataException("No real wall occlusion approach found.");
            RequestWalk(approach); _walker!.Advance(1000); UpdateWalkerPosition(); FocusCharacter(); UpdateWallOcclusion();
            if (_cutWalls.Count == 0) throw new InvalidDataException("Intervening walls did not reveal the actor.");
            foreach (string key in _cutWalls)
                if (!_bodies[key].Visible || _bodies[key].CollisionLayer != 1 || _bodies[key].GetChild<MeshInstance3D>(0).Visible)
                    throw new InvalidDataException("Wall occlusion changed the source body/picking layer.");
            if (_navigation!.ReachableFrom(_walker.CurrentHex).Count != reachCount || !_doors.Capture().Doors.SequenceEqual(doors.Doors))
                throw new InvalidDataException("Wall occlusion changed doors or walkability.");
            using var toggle = new InputEventKey { Pressed = true, PhysicalKeycode = Key.V };
            _UnhandledInput(toggle);
            if (_cutWalls.Count != 0 || !_walls.All(w => _bodies[w.Source.Key].GetChild<MeshInstance3D>(0).Visible))
                throw new InvalidDataException("Disabling wall occlusion failed to restore meshes.");
            var button = _hud.FindChild("WallOcclusionAction", true, false) as Button ?? throw new InvalidDataException("Wall reveal button missing.");
            button.EmitSignal(BaseButton.SignalName.Toggled, true);
            if (_cutWalls.Count == 0) throw new InvalidDataException("Wall reveal button failed.");
            _hud.Filters["wall"] = false; ApplyFilters();
            if (_cutWalls.Count != 0) throw new InvalidDataException("Filtered walls retained cutaway state.");
            _hud.Filters["wall"] = true; ApplyFilters();
            ChangeElevation(1); UpdateWallOcclusion();
            if (_cutWalls.Count != 0) throw new InvalidDataException("Inspection without actor retained cutaway walls.");
            FocusCharacter(); UpdateWallOcclusion();
            NavigationStatus($"Personagem revelado · {_cutWalls.Count} paredes ocultadas apenas na vista");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "occlusion.png"));
            MapLayoutWriter.Verify(_map!); ResetWalker();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print("WALL_OCCLUSION_OK: real reachable approach, orthographic sight, V/button, restoration, filters, elevation rebuild, actor, picking layers, navigation, doors and source bytes verified.");
        }
        finally { _checkingNavigation = false; }
    }
}
