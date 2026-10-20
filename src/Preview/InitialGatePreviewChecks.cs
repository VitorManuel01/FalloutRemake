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
    private async Task CheckInitialGatePreview()
    {
        WorldCheckpoint baseline = _campaign!.Capture(_mapCatalog!.Source.Sha256);
        byte[] State() => CheckpointFile.Encode(_campaign!.Capture(_mapCatalog!.Source.Sha256));
        byte[] before = State(); bool walking = _walkMode;
        var gate = _layout!.Objects.Single(o => o.Source.Key == _doors!.InitialTempleGateKey);
        var actor = _layout.Objects.Single(o => o.Source.Offset == 425776);
        void Walk(int target) { RequestWalk(target); CompleteReadabilityWalk(); }
        int Approach(int hex) => Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(hex, d))
            .Where(t => t.HasValue && _reachable!.Distance(t.Value) >= 0 && !_navigation!.IsExit(t.Value)).Select(t => t!.Value)
            .OrderBy(_reachable!.Distance).ThenBy(t => t).First();
        try
        {
            SelectObject(gate); int approach = Approach(gate.Source.Header.Tile); Walk(approach);
            if (!_hud.SelectionDetails.Contains("Porta final do templo") || _doors!.CanLockpick(gate.Source.Key, approach, 2, false) != DoorUseStatus.ScriptRequired)
                throw new InvalidDataException("Initial gate identification or restricted interaction missing.");
            UseDoor(gate.Source.Key);
            if (!_doors!.Get(gate.Source.Key)!.IsOpen || !_navigation!.Walkable(gate.Source.Header.Tile) || _reachable!.Count != 1321)
                throw new InvalidDataException("Initial gate did not update preview navigation.");
            _focus = Point(gate.Position, 0); _camera.Size = 18; UpdateCamera();
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "initial-gate-open.png"));
            Walk(gate.Source.Header.Tile);
            if (_doors.CanToggle(gate.Source.Key, _walker!.CurrentHex, 2, false) != DoorUseStatus.Occupied)
                throw new InvalidDataException("Occupied initial gate allowed closing.");
            CheckpointFile.Write(CheckpointPath, _campaign.Capture(_mapCatalog.Source.Sha256)); LoadCheckpoint();
            if (_walker!.CurrentHex != 19928 || !_doors!.Get(gate.Source.Key)!.IsOpen) throw new InvalidDataException("Initial gate checkpoint failed on former blocker.");
            int near = Approach(actor.Source.Header.Tile); Walk(near); SelectObject(_layout!.Objects.Single(o => o.Source.Key == actor.Source.Key));
            _focus = Point(actor.Position, 0); _camera.Size = 20; UpdateCamera();
            if (near != 13928 || _campaign.Experience != baseline.Areas.Sum(a => a.Session.Experience) || _campaign.CarriedCount != 2)
                throw new InvalidDataException("Source NPC approach changed items/XP or arrived elsewhere.");
            _hud.Status("Ao alcance do personagem · conversa e duelo ainda indisponíveis");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "npc-approach.png"));
            CheckpointFile.Write(CheckpointPath, _campaign.Capture(_mapCatalog.Source.Sha256)); LoadCheckpoint();
            if (_campaign.Location.Hex != near || !_doors!.Get(gate.Source.Key)!.IsOpen) throw new InvalidDataException("NPC approach checkpoint lost gate state.");
            GD.Print("INITIAL_GATE_PREVIEW_OK: original gate 104 open via production interaction, 974->1321 reachable, occupied-close refusal, v5 load on gate, actual walk to NPC at 13928, unchanged XP/items/source, no dialogue/lockpick/combat.");
        }
        finally
        {
            CheckpointFile.Write(CheckpointPath, baseline); LoadCheckpoint();
            if (_walkMode != walking) ToggleWalkMode();
            if (!before.SequenceEqual(State())) throw new InvalidDataException("Initial gate fixture did not restore its complete baseline.");
        }
    }
}
