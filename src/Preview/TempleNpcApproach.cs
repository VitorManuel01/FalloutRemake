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
    private int? TempleNpcApproachTarget()
    {
        if (_selected?.Source.Offset != 425776 || _map?.Source.Sha256 != TempleBarrierEffectProposal.MapHash
            || _campaign?.CurrentMap != 3 || _campaign.Location.Elevation != 2 || _elevation != 2
            || _walker?.Moving != false || _navigation == null || _reachable == null || InspectorOpen || !VisibleObject(_selected)) return null;
        int actorHex = _selected.Source.Header.Tile;
        if (FalloutGrid.Distance(_walker.CurrentHex, actorHex) <= 1) return _walker.CurrentHex;
        return Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(actorHex, d))
            .Where(t => t.HasValue && _reachable.Distance(t.Value) >= 0 && !_navigation.IsExit(t.Value))
            .OrderBy(t => _reachable.Distance(t!.Value)).ThenBy(t => t).FirstOrDefault();
    }
    private void ApproachTempleNpc()
    {
        int? target = TempleNpcApproachTarget();
        if (!target.HasValue) { NavigationStatus("Sem caminho disponível até o NPC. Abra a porta final e use a elevação do personagem."); return; }
        if (target == _walker!.CurrentHex) { NavigationStatus("NPC ao alcance · use Conversa · ensaio para consultar o texto original."); return; }
        if (!_walkMode) ToggleWalkMode();
        RequestWalk(target.Value);
    }
    private async Task CheckTempleNpcApproach(int expected)
    {
        var actor = _layout!.Objects.Single(o => o.Source.Offset == 425776); SelectObject(actor);
        var button = (Button)_hud.FindChild("NpcApproachAction", true, false);
        byte[] before = CheckpointFile.Encode(_campaign!.Capture(_mapCatalog!.Source.Sha256));
        int sourceHex = actor.Source.Header.Tile;
        if (button.Disabled || TempleNpcApproachTarget() != expected) throw new InvalidDataException("NPC approach unavailable with open gate.");
        int[] route = _navigation!.FindRoute(_walker!.CurrentHex, expected).Tiles;
        button.EmitSignal(Button.SignalName.Pressed);
        if (!_walker.Moving || !_walkMode || !_committedRoute.SequenceEqual(route) || !button.Disabled
            || route.Contains(sourceHex) || route.Any(_navigation.IsExit)
            || !before.SequenceEqual(CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256))))
            throw new InvalidDataException("NPC approach skipped movement or changed state before walking.");
        _hud.NpcApproachRequested!();
        if (!_committedRoute.SequenceEqual(route)) throw new InvalidDataException("Busy approach replaced the route.");
        FrameRoute();
        if (_capture != null) await Capture(Path.ChangeExtension(_capture, "npc-route.png"));
        CompleteReadabilityWalk(); RefreshDoorAction();
        if (_walker.CurrentHex != expected || !button.Disabled || ((Button)_hud.FindChild("SpeechAction", true, false)).Disabled
            || _speechWindow != null || _campaign.CarriedCount != 2 || actor.Source.Header.Tile != sourceHex)
            throw new InvalidDataException("NPC arrival failed to stop adjacent or opened conversation automatically.");
        byte[] arrived = CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256));
        ApproachTempleNpc();
        if (_walker.Moving || !arrived.SequenceEqual(CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256))))
            throw new InvalidDataException("Nearby NPC approach changed the active world.");
        GD.Print("TEMPLE_NPC_APPROACH_OK: closed-gate refusal, selected source NPC, reachable adjacent target, exact route, no teleport, busy/repeated refusal, stop within one hex, no automatic conversation/items/XP/source changes verified.");
    }
}
