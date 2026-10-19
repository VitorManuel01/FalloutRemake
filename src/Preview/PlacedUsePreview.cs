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
    private Node3D? _placedUseLayer;
    private readonly Dictionary<Guid, Node3D> _placedUseNodes = new();
    private BoxMesh? _placedUseMesh;
    private StandardMaterial3D? _placedUseMaterial;

    // Runtime GUIDs have their own layer: never register them as imported MAP roots or navigation bodies.
    private void RefreshPlacedUses()
    {
        if (_placedUseLayer != null) { _world.RemoveChild(_placedUseLayer); _placedUseLayer.QueueFree(); }
        _placedUseLayer = new Node3D { Name = "PlacedUses" }; _world.AddChild(_placedUseLayer);
        _placedUseNodes.Clear();
        if (_campaign == null || _layout == null) return;
        _placedUseMesh ??= _resources.Own(new BoxMesh { Size = new Vector3(.46f, .16f, .34f) });
        _placedUseMaterial ??= _resources.Own(new StandardMaterial3D { AlbedoColor = new Color("efb75c"), Roughness = .85f });
        foreach (WorldItemUse worldUse in _campaign.ItemUses.Where(u => u.Allocation.Phase == ItemUsePhase.Placed
            && u.Allocation.Placement.Map == _campaign.CurrentMap && u.Allocation.Placement.Elevation == _elevation))
        {
            ItemUseAllocation use = worldUse.Allocation;
            var node = new Node3D { Name = "Use_" + use.Id.ToString("N"), Position = Point(FalloutGrid.HexCenter(use.Placement.Hex) - _layout.Origin, .08f) };
            node.SetMeta("use_id", use.Id.ToString("D")); node.SetMeta("origin_map", worldUse.SourceMap);
            node.SetMeta("owner_key", use.OwnerKey); node.SetMeta("item_key", use.ItemKey);
            node.SetMeta("active_pid", ExplosiveUseProposal.ActivePid); node.SetMeta("remaining_ticks", use.RemainingTicks);
            node.AddChild(new MeshInstance3D { Mesh = _placedUseMesh, MaterialOverride = _placedUseMaterial });
            int seconds = (use.RemainingTicks + ExplosiveTimer.TicksPerSecond - 1) / ExplosiveTimer.TicksPerSecond;
            node.AddChild(new Label3D { Name = "UseLabel", Text = use.RemainingTicks == 0 ? "PENDENTE\naguarda resolução" : $"POSICIONADO\n{seconds} s · tempo lógico",
                Position = new Vector3(0, 1.1f, 0), FontSize = 32, PixelSize = .012f, Modulate = new Color("ffd496"),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true });
            _placedUseLayer.AddChild(node); _placedUseNodes.Add(use.Id, node);
        }
        ApplyPlacedUseFilter();
    }

    private void ApplyPlacedUseFilter()
    {
        foreach (Node3D node in _placedUseNodes.Values) node.Visible = _hud.Filters["scenery"];
    }

    private async Task CheckPlacedUsePreview()
    {
        _checkingNavigation = true;
        bool scenery = _hud.Filters["scenery"];
        try
        {
            ResetWalker(); ToggleWalkMode();
            void Walk(int hex) { RequestWalk(hex); _walker!.Advance(1000); UpdateWalkerPosition(); if (_walker.CurrentHex != hex) throw new InvalidDataException("Explosive preview audit walk failed."); }
            int Approach(MapObject obj) => Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(obj.Header.Tile, d)).Append(obj.Header.Tile)
                .Where(t => t.HasValue && _reachable!.Distance(t.Value) >= 0 && !_navigation!.IsExit(t.Value)).Select(t => t!.Value)
                .OrderBy(t => _reachable!.Distance(t)).ThenBy(t => t).First();
            foreach (int offset in new[] { 244040, 137396 })
            {
                var door = _layout!.Objects.Single(o => o.Source.Offset == offset).Source; Walk(Approach(door));
                if (offset == 137396 && _doors!.UseLockpick(door.Key, _walker!.CurrentHex, 0, false, _ => SkillRollOutcome.Success).ExperienceAwarded != 25)
                    throw new InvalidDataException("Explosive preview audit controlled lockpick failed.");
                if (UseDoor(door.Key) != DoorUseStatus.Ready) throw new InvalidDataException("Explosive preview audit door failed.");
            }
            int exit = _layout!.Objects.Where(o => o.Kind == PreviewKind.Exit && o.Source.State.GetValueOrDefault("destinationMap", -1) == 3
                && o.Source.State.GetValueOrDefault("destinationElevation", -1) == 1 && _reachable!.Distance(o.Source.Header.Tile) >= 0)
                .Select(o => o.Source.Header.Tile).OrderBy(t => _reachable!.Distance(t)).ThenBy(t => t).First();
            Walk(exit); UseExit();
            if (_campaign!.Location.Elevation != 1) throw new InvalidDataException("Explosive preview audit elevation failed.");
            MapObject owner = _campaign!.Current.Items.Containers.Single(o => o.Offset == 337852 && o.Header.Id == 358);
            MapObject item = _campaign.Current.Items.Contents(owner.Key).Single(i => i.Source.Header.Id == 364 && i.Source.Header.Pid == 85).Source;
            Walk(Approach(owner));
            if (_campaign.TakeItem(owner.Key, item.Key, 1, false) != ItemTakeStatus.Ready) throw new InvalidDataException("Explosive preview audit original collection failed.");
            int placement = Enumerable.Range(1, 39999).Where(t => _reachable!.Distance(t) >= 0 && !_navigation!.IsExit(t) && FalloutGrid.Distance(t, 14322) <= 3)
                .OrderBy(t => FalloutGrid.Distance(t, 14322)).ThenBy(t => _reachable!.Distance(t)).ThenBy(t => t).First();
            Walk(placement);
            var plan = ExplosiveUseProposal.Create(_campaign, _campaign.Carried.Single(), 60, SkillRollOutcome.Success, false).Plan!;
            int roots = _lookup.Count, reach = _reachable!.Count; ActorLocation actor = _campaign.Location;
            if (_campaign.CommitExplosiveUse(plan, false).Status != ExplosiveCommitStatus.Ready) throw new InvalidDataException("Placed-use fixture commit failed.");
            RefreshPlacedUses();
            Node3D node = _placedUseNodes.Single().Value;
            Vector3 expected = Point(FalloutGrid.HexCenter(actor.Hex) - _layout!.Origin, .08f);
            if (_placedUseNodes.Single().Key != plan.Id || node.Position.DistanceTo(expected) > .001f || node.HasMeta("source_id") || node.HasMeta("source_key")
                || node.GetMeta("use_id").AsString() != plan.Id.ToString("D") || node.GetMeta("item_key").AsString() != item.Key
                || node.GetMeta("origin_map").AsInt32() != 3 || node.GetChildren().Any(c => c is CollisionObject3D)
                || _lookup.Count != roots || _bodies.Count != roots || _reachable.Count != reach)
                throw new InvalidDataException("Placed-use visual changed original identities, navigation or transforms.");
            _hud.Filters["scenery"] = false; ApplyFilters();
            if (node.Visible) throw new InvalidDataException("Placed-use scenery filter failed.");
            _hud.Filters["scenery"] = true; ApplyFilters();
            if (!node.Visible) throw new InvalidDataException("Placed-use filter did not restore visibility.");
            _campaign.AdvanceUseTime(175); RefreshPlacedUses();
            if (_placedUseNodes.Single().Value.GetNode<Label3D>("UseLabel").Text != "POSICIONADO\n43 s · tempo lógico")
                throw new InvalidDataException("Placed-use partial timer display failed.");
            SaveCheckpoint(); ResetWalker();
            if (_placedUseNodes.Count != 0) throw new InvalidDataException("Reset retained placed visuals.");
            LoadCheckpoint();
            if (_placedUseNodes.Count != 1 || !_placedUseNodes.ContainsKey(plan.Id) || _campaign.ItemUses.Single().Allocation.RemainingTicks != 425)
                throw new InvalidDataException("Checkpoint did not recreate the same placed GUID and partial timer.");
            ChangeElevation(0);
            if (_placedUseNodes.Count != 0 || _campaign.Location != actor) throw new InvalidDataException("Placed visual crossed inspection elevations or moved the actor.");
            ChangeElevation(1);
            if (_placedUseNodes.Count != 1) throw new InvalidDataException("Placed visual duplicated after elevation round trip.");
            _focus = expected; _camera.Size = 18; UpdateCamera();
            _hud.Status("Ensaio com coleta real · forma provisória · sem explosão");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "placed-use.png"));
            if (_campaign.AdvanceUseTime(425).Due.Single().Allocation.Id != plan.Id) throw new InvalidDataException("Placed fixture deadline failed.");
            RefreshPlacedUses();
            if (_placedUseNodes.Single().Value.GetNode<Label3D>("UseLabel").Text != "PENDENTE\naguarda resolução")
                throw new InvalidDataException("Due use disappeared before explicit resolution.");
            var replacement = new PrototypeCatalog(new ResourceCatalog(new GameData(_directory!).Read)).Get(TempleBarrierEffectProposal.ReplacementPid);
            var effect = TempleBarrierEffectProposal.Create(_campaign, plan.Id, replacement);
            var projected = TempleBarrierProjection.Create(_campaign, effect.Plan, replacement, Guid.NewGuid());
            if (effect.Status != BarrierEffectStatus.Ready || projected.Status != BarrierProjectionStatus.Ready
                || projected.Projection!.Navigation.ReachableFrom(29097).Count != 1258 || _navigation!.Walkable(14322)
                || _campaign.Current.Items.Balance(owner.Key, item.Key) != new ItemBalance(1, 0, 0, 1, 0))
                throw new InvalidDataException("Explosive preview audit lost source ownership or changed the live barrier.");
            SelectObject(_layout!.Objects.Single(o => o.Source.Key == effect.Plan!.TargetKey));
            NavigationStatus("Barreira no alcance · alternativa: 1.258 hexes · nenhum efeito aplicado");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "barrier-proposal.png"));
            if (_campaign.CompleteDueUse(plan.Id) != ItemUseStatus.Ready) throw new InvalidDataException("Logical fixture resolution failed.");
            RefreshPlacedUses();
            if (_placedUseNodes.Count != 0 || _campaign.CarriedCount != 0 || _lookup.Count != roots || _reachable!.Count != reach || _campaign.Location != actor)
                throw new InvalidDataException("Consumed fixture retained a visual or changed original navigation/actor.");
            foreach (WorldArea area in _campaign.Areas) MapLayoutWriter.Verify(area.Map);
            GD.Print("TEMPLE_EXPLOSIVE_PREVIEW_AUDIT_OK: actual walk, known doors, controlled successful lockpick, elevation exit, real pot 358/item 364 collection, placement near barrier, partial timer checkpoint, due effect target and detached 1258-hex projection; original barrier unchanged, no runtime effects.");
            GD.Print("PLACED_USE_PREVIEW_OK: real collected pot 358/item 364 fixture; runtime GUID/transform, source identity separation, nonblocking geometry, scenery filter, partial timer, checkpoint, elevation isolation, due label and consumed removal verified; no activation UI, real-time clock or damage.");
        }
        finally
        {
            _hud.Filters["scenery"] = scenery; ResetWalker(); _checkingNavigation = false;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }
}
