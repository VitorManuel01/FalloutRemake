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
    private readonly Dictionary<Guid, Node3D> _replacementNodes = new();
    private Mesh? _replacementMesh;
    private StandardMaterial3D? _replacementMaterial;
    private void BuildAppliedSceneryVisuals()
    {
        _replacementNodes.Clear();
        if (_campaign == null || _layout == null) return;
        foreach (SceneryReplacement replacement in _campaign.Current.Scenery.Capture())
        {
            if (replacement.Location.Elevation != _elevation) continue;
            _replacementMesh ??= _resources.Own(new BoxMesh { Size = new Vector3(.52f, .12f, .24f) });
            _replacementMaterial ??= _resources.Own(new StandardMaterial3D { AlbedoColor = new Color("8b9c9c"), Roughness = 1 });
            var node = new Node3D { Name = "Replacement_" + replacement.Id.ToString("N"), Position = Point(FalloutGrid.HexCenter(replacement.Location.Hex) - _layout.Origin, 0) };
            node.SetMeta("replacement_id", replacement.Id.ToString("D")); node.SetMeta("cause_use_id", replacement.CauseUseId.ToString("D"));
            node.SetMeta("replaces_source_key", replacement.ReplacesKey); node.SetMeta("pid", replacement.Pid); node.SetMeta("fid", replacement.Fid);
            foreach (Vector3 position in new[] { new Vector3(-.18f, .06f, -.16f), new Vector3(.18f, .06f, .16f), new Vector3(.08f, .1f, -.1f) })
                node.AddChild(new MeshInstance3D { Mesh = _replacementMesh, MaterialOverride = _replacementMaterial, Position = position,
                    RotationDegrees = new Vector3(0, position.X < 0 ? 28 : -20, 0) });
            var caption = new Label3D { Text = "PASSAGEM\nABERTA", Position = new Vector3(0, 1.8f, 0), FontSize = 32, PixelSize = .012f,
                Modulate = new Color("b6edc8"), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true };
            node.AddChild(caption); RegisterWorldLabel(caption);
            _world.AddChild(node); _replacementNodes.Add(replacement.Id, node);
        }
        ApplySceneryReplacementFilter();
    }
    private void ApplySceneryReplacementFilter()
    {
        foreach (Node3D node in _replacementNodes.Values) node.Visible = _hud.Filters["scenery"];
    }
    private BarrierCommitResult ApplyBarrierEffect(TempleBarrierEffectPlan plan, Guid replacementId)
    {
        if (_campaign == null) return new(BarrierCommitStatus.StalePlan);
        BarrierCommitResult result = _campaign.CommitBarrierEffect(plan, replacementId, _walker?.Moving == true);
        if (result.Status != BarrierCommitStatus.Ready) return result;
        bool walking = _walkMode;
        // Rebuild all navigation/hover/selection snapshots against the applied overlay.
        BuildWorld(); if (walking && _walker != null) ToggleWalkMode();
        NavigationStatus("Passagem aberta · efeito de cenário aplicado · K para salvar");
        return result;
    }
    private async Task CheckAppliedBarrierPreview(TempleBarrierEffectPlan plan, int originalRoots, string ownerKey, string itemKey)
    {
        bool grid = _showNavigationGrid, hidden = _hud.Filters["hidden"];
        try
        {
            ActorLocation actor = _campaign!.Location; int xp = _campaign.Experience; Guid id = Guid.NewGuid();
            if (ApplyBarrierEffect(plan, id).Status != BarrierCommitStatus.Ready) throw new InvalidDataException("Preview barrier effect commit failed.");
            void CheckView()
            {
                var original = _bodies[plan.TargetKey]; var node = _replacementNodes.Single().Value;
                if (_lookup.Count != originalRoots || _bodies.Count != originalRoots || original.Visible || original.CollisionLayer != 0
                    || original.GetMeta("source_id").AsInt32() != 44 || node.HasMeta("source_id") || node.HasMeta("source_key")
                    || node.GetMeta("replacement_id").AsString() != id.ToString("D") || node.GetMeta("cause_use_id").AsString() != plan.UseId.ToString("D")
                    || node.GetMeta("replaces_source_key").AsString() != plan.TargetKey || node.GetMeta("pid").AsInt32() != 33555365
                    || node.GetChildren().Any(c => c is CollisionObject3D) || node.Position.DistanceTo(Point(FalloutGrid.HexCenter(14322) - _layout!.Origin, 0)) > .001f
                    || _placedUseNodes.Count != 0 || !_navigation!.Walkable(14322) || _reachable!.Count != 1258
                    || _campaign.Current.Items.Balance(ownerKey, itemKey) != new ItemBalance(1, 0, 0, 0, 1) || _campaign.Experience != xp)
                    throw new InvalidDataException("Applied preview lost replacement identity, navigation, source roots, consumption or XP.");
            }
            void Walk(int hex)
            {
                RequestWalk(hex); _walker!.Advance(1000); UpdateWalkerPosition();
                if (_walker.CurrentHex != hex) throw new InvalidDataException("Applied preview attempted an unreachable walk.");
            }
            void TravelTo(int elevation)
            {
                int exit = _layout!.Objects.Where(o => o.Kind == PreviewKind.Exit && o.Source.State.GetValueOrDefault("destinationMap", -1) == 3
                    && o.Source.State.GetValueOrDefault("destinationElevation", -1) == elevation && _reachable!.Distance(o.Source.Header.Tile) >= 0)
                    .Select(o => o.Source.Header.Tile).OrderBy(t => _reachable!.Distance(t)).ThenBy(t => t).First();
                Walk(exit); UseExit();
                if (_campaign.Location.Elevation != elevation) throw new InvalidDataException("Applied preview exit failed.");
            }
            CheckView(); if (_campaign.Location != actor) throw new InvalidDataException("Barrier effect moved the preview actor.");
            _hud.Filters["scenery"] = false; _hud.Filters["hidden"] = true; ApplyFilters();
            if (_replacementNodes[id].Visible || _bodies[plan.TargetKey].Visible || !_navigation!.Walkable(14322)) throw new InvalidDataException("Scenery filter changed traversal or resurrected the original blocker.");
            _hud.Filters["scenery"] = true; _hud.Filters["hidden"] = hidden; ApplyFilters();
            SaveCheckpoint(); ResetWalker(); LoadCheckpoint(); CheckView();
            Walk(14322); SaveCheckpoint(); ResetWalker(); LoadCheckpoint(); CheckView();
            if (_walker!.CurrentHex != 14322) throw new InvalidDataException("Preview failed to reload the actor on the former blocker.");
            SetNavigationGrid(false); _focus = Point(FalloutGrid.HexCenter(14322) - _layout!.Origin, 0); _camera.Size = 20; UpdateCamera();
            _hud.Status("Passagem aberta · cenário substituído e checkpoint restaurado");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "barrier-applied.png"));
            TravelTo(2);
            if (_replacementNodes.Count != 0 || _campaign.SceneryReplacements.Single().Id != id || _campaign.Location.Hex != 23914 || _reachable!.Count != 974)
                throw new InvalidDataException("Applied preview lost the passage on another elevation or used an incorrect arrival.");
            MapObject chest = _campaign.Current.Items.Containers.Single(o => o.Offset == 458120 && o.Header.Id == 376);
            Walk(ContainerApproach(chest)!.Value);
            await CheckTakeAllChest(chest);
            await CheckReturnAllChest(chest);
            RefreshContainerVisuals();
            if (_campaign.CarriedCount != 2 || _campaign.CarriedWeight != 2 || !_campaign.Carried.Select(i => (i.Source.Header.Id, i.Source.Header.Pid)).ToHashSet().SetEquals(new[] { (126, 273), (100, 49) }))
                throw new InvalidDataException("Applied preview lost original upper item identities/weight.");
            var scripted = _campaign.Current.Items.Containers.Single(o => o.Offset == 465604);
            if (_campaign.Current.Items.ContainerStatus(scripted.Key) != ItemTakeStatus.ScriptRequired || _campaign.Current.Items.Contents(scripted.Key).Length != 0)
                throw new InvalidDataException("Applied preview bypassed the scripted chest.");
            SaveCheckpoint(); ResetWalker(); LoadCheckpoint();
            OpenBag(); if (_bag?.RowCount != 2) throw new InvalidDataException("Upper items did not reach the bag UI after checkpoint.");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "barrier-upper-bag.png"));
            CloseBag(); TravelTo(1); CheckView(); Walk(14322); SaveCheckpoint(); ResetWalker(); LoadCheckpoint(); CheckView();
            if (_campaign.CarriedCount != 2 || _walker!.CurrentHex != 14322) throw new InvalidDataException("Return checkpoint lost upper items or passage position.");
            GD.Print("APPLIED_BARRIER_PREVIEW_OK: atomic scenery/consumption, hidden original root 44, separate GUID/nonblocking rubble, filters, rebuilt navigation, save/load on former blocker, original elevation-2 travel and 2-item bag, script refusal, return checkpoint, unchanged XP/source bytes; effect resolution remains an audit without general combat.");
        }
        finally { CloseBag(); _hud.Filters["hidden"] = hidden; SetNavigationGrid(grid); }
    }
}
