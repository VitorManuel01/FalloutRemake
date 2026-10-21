using System;
using System.Collections.Generic;
using System.IO;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

/// <summary>A separate 3D world owned by the rehearsal dialog; never duplicates or modifies active scene nodes.</summary>
public partial class TempleNpcDepartureScene : SubViewportContainer
{
    private readonly SubViewport _viewport = new() { Name = "DepartureViewport", OwnWorld3D = true, Size = new(640, 220),
        GuiDisableInput = true, PhysicsObjectPicking = false, RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible };
    private readonly Camera3D _camera = new() { Projection = Camera3D.ProjectionType.Orthogonal, Size = 9, Near = .1f, Far = 200, Current = true };
    private MeshInstance3D? _actor, _gate;
    private GroundPoint _origin;
    private TempleNpcDepartureProfile? _profile;
    public override void _Ready()
    {
        Stretch = true; MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_viewport); _viewport.AddChild(_camera);
    }
    public void Configure(MapDocument map, TempleNpcDepartureProfile profile)
    {
        if (_profile != null) throw new InvalidOperationException("Departure scene is already configured.");
        var layout = PreviewLayout.Create(map, 2); _origin = layout.Origin;
        using var resources = new ResourceScope();
        var floorMesh = resources.Own(new BoxMesh { Size = new((float)FalloutGrid.FloorWidth * .98f, .08f, 1.96f) });
        var floors = resources.Own(new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true,
            Mesh = floorMesh, InstanceCount = layout.Floors.Length });
        for (int i = 0; i < layout.Floors.Length; i++)
        {
            var floor = layout.Floors[i];
            floors.SetInstanceTransform(i, new(Basis.Identity, Point(floor.Position, -.05f)));
            float shade = .30f + ((floor.Raw & 0xFFF) % 7) * .012f;
            floors.SetInstanceColor(i, new(shade, shade * .94f, shade * .82f));
        }
        _viewport.AddChild(new MultiMeshInstance3D { Multimesh = floors,
            MaterialOverride = resources.Own(new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1 }) });
        var meshes = new Dictionary<PreviewKind, Mesh>(); var materials = new Dictionary<PreviewKind, StandardMaterial3D>();
        foreach (var obj in layout.Objects)
        {
            bool actor = obj.Source.Key == profile.ActorKey, gate = obj.Source.Key == profile.GateKey;
            if (obj.Hidden && !actor || obj.Kind == PreviewKind.Marker) continue;
            if (!meshes.TryGetValue(obj.Kind, out var mesh))
            {
                mesh = resources.Own<Mesh>(obj.Kind == PreviewKind.Critter ? new CapsuleMesh { Radius = .3f, Height = 1.1f }
                    : new BoxMesh { Size = Dimensions(obj.Kind) }); meshes.Add(obj.Kind, mesh);
                materials.Add(obj.Kind, resources.Own(new StandardMaterial3D { AlbedoColor = new Color(obj.Kind switch
                    { PreviewKind.Wall => "ae9c80", PreviewKind.Door => "cfad62", PreviewKind.Critter => "c87761", PreviewKind.Exit => "59b49e", _ => "887954" }), Roughness = .9f }));
            }
            var node = new MeshInstance3D { Mesh = mesh, MaterialOverride = materials[obj.Kind], Position = Point(obj.Position, Dimensions(obj.Kind).Y / 2),
                Name = $"DepartureObject_{obj.Source.Offset}" };
            node.SetMeta("source_key", obj.Source.Key); node.SetMeta("source_id", obj.Source.Header.Id);
            _viewport.AddChild(node);
            if (actor)
            {
                _actor = node;
                node.MaterialOverride = resources.Own(new StandardMaterial3D { AlbedoColor = new Color("9ddfff"),
                    EmissionEnabled = true, Emission = new Color("447791"), NoDepthTest = true });
                node.AddChild(new Label3D { Text = "NPC · ensaio", Position = new(0, 1.15f, 0), FontSize = 36, PixelSize = .014f,
                    Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, Modulate = new Color("c5edff") });
            }
            if (gate) _gate = node;
        }
        if (_actor == null || _gate == null) throw new InvalidDataException("Departure scene is missing its original NPC or gate.");
        _viewport.AddChild(new WorldEnvironment { Environment = resources.Own(new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color("111e25"), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color("c2c5b8"), AmbientLightEnergy = .65f }) });
        _viewport.AddChild(new DirectionalLight3D { RotationDegrees = new(-60, -30, 0), LightEnergy = 1.1f });
        _profile = profile;
    }
    public void Inspect(NpcDepartureSnapshot snapshot)
    {
        if (_profile == null) return;
        var actor = _profile.Actor(snapshot); var gate = _profile.Gate(snapshot);
        GroundPoint point = FalloutGrid.HexCenter(actor.Header.Tile) - _origin;
        _actor!.Position = Point(point, .55f); _actor.Rotation = new(0, actor.Header.Rotation * Mathf.Pi / 3, 0);
        _actor.Visible = (actor.Header.Flags & HexNavigation.Hidden) == 0; _actor.SetMeta("hex", actor.Header.Tile);
        _gate!.Scale = new(1, gate.IsOpen ? .1f : 1, 1);
        _gate.Position = Point(FalloutGrid.HexCenter(gate.Source.Header.Tile) - _origin, gate.IsOpen ? .065f : .65f);
        _gate.SetMeta("open", gate.IsOpen);
        Vector3 target = Point(point, 0);
        _camera.Position = target + new Vector3(15, 22, 15); _camera.LookAt(target, Vector3.Up);
    }
    public bool Matches(NpcDepartureSnapshot snapshot)
    {
        if (_profile == null || _actor == null || _gate == null) return false;
        var actor = _profile.Actor(snapshot); var gate = _profile.Gate(snapshot);
        return _viewport.OwnWorld3D && _viewport.FindWorld3D().GetInstanceId() != GetViewport().FindWorld3D().GetInstanceId()
            && _actor.Position.IsEqualApprox(Point(FalloutGrid.HexCenter(actor.Header.Tile) - _origin, .55f))
            && _actor.Visible == ((actor.Header.Flags & HexNavigation.Hidden) == 0)
            && _actor.GetMeta("source_key").AsString() == actor.Key && _actor.GetMeta("source_id").AsInt32() == actor.Header.Id
            && _gate.GetMeta("open").AsBool() == gate.IsOpen && _gate.Scale.Y == (gate.IsOpen ? .1f : 1);
    }
    private static Vector3 Point(GroundPoint point, float y) => new((float)point.X, y, (float)point.Z);
    private static Vector3 Dimensions(PreviewKind kind) => kind switch { PreviewKind.Wall => new(.88f, .9f, .88f),
        PreviewKind.Door => new(.85f, 1.3f, .3f), PreviewKind.Critter => new(.6f, 1.1f, .6f),
        PreviewKind.Exit => new(1, .04f, 1), PreviewKind.Item => new(.5f, .4f, .5f), _ => new(.6f, .5f, .6f) };
}
