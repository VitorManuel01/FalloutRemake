using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private async Task CheckArroyoElderApproach()
    {
        var world=_campaign!; var source=ArroyoElderSource.Match(world.Current.Map)
            ?? throw new InvalidDataException("Original elder source identity is unavailable.");
        var globals=world.ArroyoGlobals; int xp=world.Experience;
        string inventory=JsonSerializer.Serialize(world.Areas.Select(a=>a.Items.Capture()));
        string temple=JsonSerializer.Serialize(world.TempleProgress);
        var route=world.InspectArroyoElderApproach(false);
        if (route.Status!=TempleWorldStatus.Ready || route.TargetHex==null || route.Tiles is not {Length:>3}
            || route.ActorKey!=source.Key || route.Tiles.Contains(source.Header.Tile)
            || route.Tiles.Skip(1).Any(world.Current.Doors.Navigation(0).IsExit))
            throw new InvalidDataException($"Elder guidance needs a real nontrivial source route: {route.Status}, {JsonSerializer.Serialize(route)}.");
        var button=(Button)_hud.FindChild("ArroyoElderApproachAction",true,false);
        if (!button.Visible || button.Disabled || button.Text!="Ir até a anciã")
            throw new InvalidDataException("Elder approach is not offered after arrival in Arroyo.");
        OpenElderConsultation();
        if (_elderWindow!=null) throw new InvalidDataException("Elder consultation opened before actual approach.");
        byte[] before=CheckpointFile.Encode(world.Capture(_mapCatalog!.Source.Sha256)); var old=_hud.ArroyoElderRequested!;
        button.EmitSignal(Button.SignalName.Pressed);
        if (!_walker!.Moving || !_committedRoute.SequenceEqual(route.Tiles) || _selected?.Source.Key!=source.Key
            || !button.Disabled || !before.SequenceEqual(CheckpointFile.Encode(world.Capture(_mapCatalog.Source.Sha256))))
            throw new InvalidDataException("Elder approach skipped its actual walk or selected the duplicate ID.");
        old();
        if (!_committedRoute.SequenceEqual(route.Tiles)) throw new InvalidDataException("Busy elder action replaced the walk.");
        OpenElderConsultation();
        if (_elderWindow!=null) throw new InvalidDataException("Elder consultation opened during the approach walk.");
        FrameRoute();
        if (_capture!=null) await Capture(Path.ChangeExtension(_capture,"arroyo-elder-route.png"));
        _walker.Advance(.05); UpdateWalkerPosition(); int next=_walker.NextHex;
        StopWalking(); CompleteReadabilityWalk();
        if (world.Location.Hex!=next || world.CurrentMap!=4) throw new InvalidDataException("Elder stop failed to finish only its current step.");
        SaveCheckpoint(); LoadCheckpoint();
        byte[] restored=CheckpointFile.Encode(_campaign!.Capture(_mapCatalog.Source.Sha256)); old();
        if (_walker!.Moving || !restored.SequenceEqual(CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256))))
            throw new InvalidDataException("Elder action captured before load changed the restored world.");
        route=_campaign.InspectArroyoElderApproach(false);
        if (route.Status!=TempleWorldStatus.Ready || route.Tiles![0]!=next || route.TargetHex==null)
            throw new InvalidDataException("Elder guidance did not replan after partial walk and load.");
        button.EmitSignal(Button.SignalName.Pressed); CompleteReadabilityWalk();
        if (_campaign.Location.Hex!=route.TargetHex || FalloutGrid.Distance(_campaign.Location.Hex,source.Header.Tile)!=1
            || !button.Disabled || button.Text!="Anciã ao alcance" || _campaign.CurrentMap!=4
            || _campaign.ArroyoGlobals!=globals || _campaign.Experience!=xp
            || JsonSerializer.Serialize(_campaign.TempleProgress)!=temple
            || JsonSerializer.Serialize(_campaign.Areas.Select(a=>a.Items.Capture()))!=inventory
            || ArroyoElderSource.Match(_campaign.Current.Map)?.Header.Tile!=18915)
            throw new InvalidDataException("Elder arrival lost source identity or changed dialogue/reward/inventory state.");
        byte[] arrived=CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256));
        _hud.ArroyoElderRequested!();
        if (_walker.Moving || !arrived.SequenceEqual(CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256))))
            throw new InvalidDataException("Repeated elder action moved an already adjacent player.");
        SaveCheckpoint(); LoadCheckpoint();
        if (!arrived.SequenceEqual(CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256))))
            throw new InvalidDataException("Adjacent elder checkpoint failed restoration.");
        _focus=Point(FalloutGrid.HexCenter(source.Header.Tile)-_layout!.Origin,0); _camera.Size=20; UpdateCamera();
        if (_capture!=null) await Capture(Path.ChangeExtension(_capture,"arroyo-elder-arrival.png"));
        foreach (var area in _campaign.Areas) MapLayoutWriter.Verify(area.Map);
        GD.Print("ARROYO_ELDER_APPROACH_OK: original offset 126588/SID67108867 despite duplicate Id20, real adjacent walk, busy guard, Esc partial-step stop, K/R replan, stale callback refusal, exact reach, unchanged 625 XP/65 reputation/items/Temple/source; no automatic dialogue.");
        await CheckArroyoElderConsultation();
    }
}
