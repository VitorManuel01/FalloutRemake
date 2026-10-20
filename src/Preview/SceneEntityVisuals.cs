using System;
using System.Collections.Generic;
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
    private sealed record ContainerVisual(StaticBody3D Body, MeshInstance3D Base, MeshInstance3D Lid, Node3D Hinge, MeshInstance3D Contents, Label3D Label);
    private readonly Dictionary<string, ContainerVisual> _containerVisuals = new();
    private readonly Dictionary<string, Mesh> _entityMeshes = new();
    private readonly Dictionary<string, StandardMaterial3D> _entityMaterials = new();
    private readonly Dictionary<EntityVisualRole, BoxShape3D> _entityShapes = new();
    private long _containerVisualRevision = -1;
    private StandardMaterial3D EntityMaterial(string color, bool glow = false)
    {
        string key = color + (glow ? "+" : "");
        if (_entityMaterials.TryGetValue(key, out var material)) return material;
        material = _resources.Own(new StandardMaterial3D { AlbedoColor = new Color(color), Roughness = .85f,
            EmissionEnabled = glow, Emission = new Color(color), EmissionEnergyMultiplier = glow ? .65f : 0 });
        _entityMaterials.Add(key, material); return material;
    }
    private Mesh EntityBox(string name, Vector3 size)
    {
        if (!_entityMeshes.TryGetValue(name, out var mesh)) _entityMeshes.Add(name, mesh = _resources.Own(new BoxMesh { Size = size }));
        return mesh;
    }
    private MeshInstance3D EntityPart(StaticBody3D body, string name, Mesh mesh, Vector3 position, string color, bool glow = false)
    {
        var part = new MeshInstance3D { Name = name, Mesh = mesh, Position = position, MaterialOverride = EntityMaterial(color, glow) };
        body.AddChild(part); return part;
    }
    private void ConfigureEntityVisual(PreviewObject obj, StaticBody3D body)
    {
        EntityVisualRole role = EntityPresentation.Role(obj.Source); body.SetMeta("visual_role", role.ToString());
        if (role == EntityVisualRole.Original) return;
        var main = body.GetChild<MeshInstance3D>(0); float anchor = body.Position.Y;
        Vector3 size = role switch { EntityVisualRole.Barrier => new(.84f, .8f, .26f), EntityVisualRole.LightSource => new(.42f, 1.25f, .42f), _ => new(.76f, .58f, .64f) };
        if (!_entityShapes.TryGetValue(role, out var shape)) _entityShapes.Add(role, shape = _resources.Own(new BoxShape3D { Size = size }));
        var collision = body.GetChild<CollisionShape3D>(1); collision.Shape = shape; collision.Position = new(0, size.Y / 2 - anchor, 0);
        if (role == EntityVisualRole.Barrier && IsSharedBlockingProxy(obj))
        {
            // Keep source body and picking identity; represent the technical blocker at floor level.
            main.Visible = false; body.SetMeta("shared_blocking_proxy", true);
            foreach (float edge in new[] { -.43f, .43f })
            {
                EntityPart(body, "ProxyEdgeX", EntityBox("proxy-x", new(.92f, .045f, .04f)), new(0, .035f - anchor, edge), "bb6262");
                EntityPart(body, "ProxyEdgeZ", EntityBox("proxy-z", new(.04f, .045f, .9f)), new(edge, .035f - anchor, 0), "bb6262");
            }
        }
        else if (role == EntityVisualRole.Barrier && _layout!.Objects.Any(o => o.Source.Header.Tile == obj.Source.Header.Tile && EntityPresentation.Role(o.Source) == EntityVisualRole.LightSource))
        {
            // A real scenery blocker with an attached emitter remains a red obstacle pedestal.
            main.Mesh = EntityBox("lamp-obstacle", new(.84f, .16f, .7f)); main.Position = new(0, .08f - anchor, 0); main.MaterialOverride = EntityMaterial("bb6262");
            body.SetMeta("shared_light_obstacle", true);
        }
        else if (role == EntityVisualRole.Barrier)
        {
            main.Mesh = EntityBox("barrier", new(.84f, .8f, .26f)); main.Position = new(0, .4f - anchor, 0); main.MaterialOverride = EntityMaterial("bb6262");
            foreach (float side in new[] { -.14f, .14f })
                foreach (float angle in new[] { -42f, 42f })
                    EntityPart(body, "BarrierStripe", EntityBox("stripe", new(.68f, .055f, .025f)), new(0, .4f - anchor, side), "f1d6c0").RotationDegrees = new(0, 0, angle);
        }
        else if (role == EntityVisualRole.LightSource)
        {
            main.Mesh = EntityBox("lamp-post", new(.12f, .9f, .12f)); main.Position = new(0, .45f - anchor, 0); main.MaterialOverride = EntityMaterial("68534a");
            if (!_entityMeshes.TryGetValue("bulb", out var bulb)) _entityMeshes.Add("bulb", bulb = _resources.Own(new SphereMesh { Radius = .22f, Height = .44f, RadialSegments = 12, Rings = 6 }));
            EntityPart(body, "LightBulb", bulb, new(0, 1.03f - anchor, 0), "ffd166", true);
            EntityPart(body, "LightFoot", EntityBox("lamp-foot", new(.36f, .08f, .36f)), new(0, .04f - anchor, 0), EntityPresentation.Blocks(obj.Source) ? "bb6262" : "ab8643");
        }
        else
        {
            main.Mesh = EntityBox("container", new(.72f, .38f, .58f)); main.Position = new(0, .2f - anchor, 0);
            var hinge = new Node3D { Name = "ContainerHinge", Position = new(0, .43f - anchor, .31f) }; body.AddChild(hinge);
            var lid = new MeshInstance3D { Name = "ContainerLid", Mesh = EntityBox("lid", new(.76f, .07f, .62f)), Position = new(0, 0, -.31f), MaterialOverride = EntityMaterial("45c9b2") }; hinge.AddChild(lid);
            EntityPart(body, "ContainerLatch", EntityBox("latch", new(.12f, .16f, .04f)), new(0, .31f - anchor, -.31f), "263c40");
            var contents = EntityPart(body, "ContentsIndicator", EntityBox("contents", new(.19f, .13f, .19f)), new(0, .53f - anchor, 0), "ffe1a3");
            var label = new Label3D { Name = "ContainerCaption", Position = new(0, 1.8f - anchor, 0), FontSize = 36, PixelSize = .012f,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true };
            body.AddChild(label); RegisterWorldLabel(label); _containerVisuals.Add(obj.Source.Key, new(body, main, lid, hinge, contents, label));
        }
    }
    private void RefreshContainerVisuals(bool force = false)
    {
        if (_campaign == null || !force && _containerVisualRevision == _campaign.Current.Items.Revision) return;
        _containerVisualRevision = _campaign.Current.Items.Revision;
        foreach (var (key, visual) in _containerVisuals)
        {
            long remaining = _campaign.Current.Items.Contents(key).Sum(i => (long)i.Quantity);
            bool special = _campaign.Current.Items.ContainerStatus(key) != ItemTakeStatus.Ready;
            string color = special ? "b09add" : remaining > 0 ? "45c9b2" : "8a9caa";
            visual.Base.MaterialOverride = visual.Lid.MaterialOverride = EntityMaterial(color);
            visual.Contents.Visible = remaining > 0; visual.Hinge.RotationDegrees = new(remaining == 0 && !special ? 55 : 0, 0, 0);
            visual.Label.Text = special ? remaining > 0 ? $"ESPECIAL · {remaining}" : "ESPECIAL · VAZIO" : remaining > 0 ? $"ITENS · {remaining}" : "VAZIO";
            visual.Label.Modulate = new Color(color); visual.Body.SetMeta("container_remaining", remaining);
            visual.Body.SetMeta("container_special", special);
        }
        if (_selected != null && _containerVisuals.ContainsKey(_selected.Source.Key)) SelectObject(_selected);
        RefreshContainerLocator();
    }
    private string EntityDescription(PreviewObject obj) => EntityPresentation.Role(obj.Source) switch
    {
        EntityVisualRole.Barrier => IsSharedBlockingProxy(obj)
            ? "Bloqueador técnico original · contorno vermelho no chão. Mantém o bloqueio do hex compartilhado; use Próximo para voltar ao recipiente ou à luz."
            : _bodies[obj.Source.Key].HasMeta("shared_light_obstacle") ? "Obstáculo original · base vermelha sob a luz. Mantém o bloqueio; use Próximo para consultar o emissor separado."
            : "Obstáculo · placa vermelha com X. Bloqueia a passagem; a cor não indica que possa ser destruído.",
        EntityVisualRole.LightSource => $"Fonte de luz · luminária dourada. Raio original: {obj.Source.Header.LightDistance} hexes. "
            + (EntityPresentation.Blocks(obj.Source) ? "Base vermelha: também bloqueia a passagem." : ""),
        EntityVisualRole.Container => $"Recipiente · {_campaign!.Current.Items.Contents(obj.Source.Key).Sum(i => (long)i.Quantity)} unidade(s) restante(s). "
            + (_campaign.Current.Items.ContainerStatus(obj.Source.Key) == ItemTakeStatus.Ready ? "Verde: com itens; cinza: vazio. Use Recolher [J] para consultar." : "Roxo: interação especial ou acesso restrito; consulte Recolher [J]."),
        _ => ""
    };

    private bool IsSharedBlockingProxy(PreviewObject obj) => EntityPresentation.IsBlockingProxy(obj.Source)
        && _layout!.Objects.Any(o => o.Source.Header.Tile == obj.Source.Header.Tile
            && EntityPresentation.Role(o.Source) is EntityVisualRole.Container or EntityVisualRole.LightSource);

    private void CompleteReadabilityWalk()
    {
        bool checking = _checkingNavigation;
        try { _checkingNavigation = false; AdvanceWalker(1000); }
        finally { _checkingNavigation = checking; }
        if (_walker!.Moving || FindHudButton("SaveAction").Disabled || !FindHudButton("StopWalkingAction").Disabled || _routeVisual!.Visible)
            throw new InvalidDataException("Completed walk left stale movement actions.");
    }
    private Button FindHudButton(string name) => (Button)_hud.FindChild(name, true, false);

    private async Task CheckEntityReadability()
    {
        _checkingNavigation = true;
        bool grid = _showNavigationGrid;
        bool walls = _hud.Filters["wall"];
        try
        {
            ResetWalker(); ToggleWalkMode();
            SetNavigationGrid(false);
            var before = CheckpointFile.Encode(_campaign!.Capture(_mapCatalog!.Source.Sha256));
            var lamp = _layout!.Objects.First(o => EntityPresentation.Role(o.Source) == EntityVisualRole.LightSource && VisibleObject(o));
            var lampBody = _bodies[lamp.Source.Key];
            if (lampBody.GetMeta("visual_role").AsString() != "LightSource" || lampBody.GetNodeOrNull<MeshInstance3D>("LightBulb") == null)
                throw new InvalidDataException("Source light is not presented as a lamp.");
            SelectObject(lamp);
            if (!_hud.SelectionDetails.Contains("Fonte de luz")) throw new InvalidDataException("Light identification missing from inspector.");
            var pot = _layout.Objects.Single(o => o.Source.Offset == 206348);
            var container = _containerVisuals[pot.Source.Key];
            if (container.Label.Text != "ITENS · 1" || !container.Contents.Visible || container.Body.GetMeta("container_remaining").AsInt64() != 1)
                throw new InvalidDataException("Original container contents not distinguished visually.");
            if (!before.SequenceEqual(CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256)))) throw new InvalidDataException("Entity presentation changed world state.");
            RequestWalk(24508); CompleteReadabilityWalk(); UseDoor(_layout.Objects.Single(o => o.Source.Offset == 244040).Source.Key);
            RequestWalk(ContainerApproach(pot.Source)!.Value); CompleteReadabilityWalk();
            SelectObject(pot); _focus = Point(pot.Position, 0); _camera.Size = 18; UpdateCamera();
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "entity-container-full.png"));
            var item = _campaign.Current.Items.Contents(pot.Source.Key).Single();
            if (_campaign.TakeItem(pot.Source.Key, item.Source.Key, 1, false) != ItemTakeStatus.Ready) throw new InvalidDataException("Readability test collection failed.");
            RefreshContainerVisuals();
            if (container.Label.Text != "VAZIO" || container.Contents.Visible || container.Hinge.RotationDegrees.X <= 0 || !_hud.SelectionDetails.Contains("0 unidade(s)"))
                throw new InvalidDataException("Depleted container retained full presentation.");
            var locator = (OptionButton)_hud.FindChild("ContainerLocator", true, false);
            int potIndex = Enumerable.Range(1, locator.ItemCount - 1).Single(i => locator.GetItemMetadata(i).AsString() == pot.Source.Key);
            if (!locator.GetItemText(potIndex).Contains("vazio")) throw new InvalidDataException("Locator retained collected container contents.");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "entity-container-empty.png"));
            SaveCheckpoint(); ResetWalker();
            if (_containerVisuals[pot.Source.Key].Label.Text != "ITENS · 1") throw new InvalidDataException("Reset did not restore full baseline.");
            SelectObject(_layout!.Objects.Single(o => o.Source.Key == pot.Source.Key));
            LoadCheckpoint(); container = _containerVisuals[pot.Source.Key];
            if (_walkMode || _selected?.Source.Key != pot.Source.Key) throw new InvalidDataException("Load changed inspection mode or source selection.");
            if (container.Label.Text != "VAZIO" || container.Contents.Visible || container.Hinge.RotationDegrees.X <= 0
                || container.Base.MaterialOverride != EntityMaterial("8a9caa")) throw new InvalidDataException("Loaded depleted container lost its visual state.");
            if (_campaign.ReturnItem(3, pot.Source.Key, item.Source.Key, 1, false) != ItemTakeStatus.Ready) throw new InvalidDataException("Readability test return failed.");
            RefreshContainerVisuals();
            if (container.Label.Text != "ITENS · 1" || !container.Contents.Visible || container.Hinge.RotationDegrees.X != 0) throw new InvalidDataException("Returned contents did not restore presentation.");
            ToggleWalkMode(); SaveCheckpoint(); LoadCheckpoint();
            if (!_walkMode || _selected?.Source.Key != pot.Source.Key) throw new InvalidDataException("Load changed walking mode or source selection.");
            ChangeElevation(1);
            var barrier = _layout!.Objects.Single(o => o.Source.Offset == 303324);
            if (_bodies[barrier.Source.Key].GetMeta("visual_role").AsString() != "Barrier") throw new InvalidDataException("Known temple barrier was not distinguished.");
            SelectObject(barrier); _focus = Point(barrier.Position, 0); _camera.Size = 22; UpdateCamera();
            if (!_hud.SelectionDetails.Contains("Obstáculo") || _hud.FindChild("EntityLegend", true, false) == null) throw new InvalidDataException("Barrier description or legend missing.");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var actions = (Control)_hud.FindChild("SelectionActions", true, false);
            if (actions.GetGlobalRect().End.Y > GetViewport().GetVisibleRect().Size.Y - 64)
                throw new InvalidDataException($"Selection actions outside panel: {actions.GetGlobalRect()} / viewport {GetViewport().GetVisibleRect().Size}.");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "entity-barrier-light.png"));
            ChangeElevation(2);
            var special = _layout!.Objects.Single(o => o.Source.Offset == 465604);
            if (!_containerVisuals[special.Source.Key].Label.Text.Contains("ESPECIAL") || !_containerVisuals[special.Source.Key].Body.GetMeta("container_special").AsBool() || _containerVisuals[special.Source.Key].Hinge.RotationDegrees.X != 0)
                throw new InvalidDataException("Scripted container was shown as ordinary empty loot.");
            SelectObject(special); _focus = Point(special.Position, 0); _camera.Size = 18; UpdateCamera();
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "entity-container-special.png"));
            await CheckSceneReview();
            foreach (WorldArea area in _campaign.Areas) MapLayoutWriter.Verify(area.Map);
            GD.Print("ENTITY_READABILITY_OK: original light emitters, blocked scenery, container full/empty/returned/checkpoint/special states, legend and inspector; source bytes, IDs and navigation retained; presentation only.");
        }
        finally { _hud.Filters["wall"] = walls; ResetWalker(); SetNavigationGrid(grid); _checkingNavigation = false; await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    }
}
