using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private async Task CheckArroyoReturnLoop()
    {
        _ = _campaign!.Capture(_mapCatalog!.Source.Sha256);
        string temple = JsonSerializer.Serialize(_campaign.TempleProgress);
        string inventory = JsonSerializer.Serialize(_campaign.Areas.Select(a => a.Items.Capture()));
        int[] entries = _campaign.EnteredMaps;
        if (_campaign.CurrentMap != 4 || _campaign.Experience != 625) throw new InvalidDataException("Arroyo return audit requires the first rewarded arrival.");
        var guidance = _campaign.InspectArroyoReturnRoute(false);
        if (guidance.Status != TempleWorldStatus.Ready || guidance.ExitHex == null)
            throw new InvalidDataException("Arroyo return guidance could not find an original reachable exit.");
        int target = guidance.ExitHex.Value;
        var option = _campaign.PreviewExitAt(0,target);
        if (option.Status != ExitStatus.Ready || option.Destination != new ActorLocation(3,2,14128,0))
            throw new InvalidDataException("Original Arroyo return exit differs from its native destination.");
        var keys = option.SourceKeys!.ToArray();
        foreach (string key in keys)
            if (!_map!.AllObjects.Any(o => o.Key == key && o.Header.Tile == target && o.State["destinationMap"] == 3))
                throw new InvalidDataException("Arroyo return guidance lost its original exit identity.");
        var action = (Button)_hud.FindChild("TempleVillageExitAction",true,false);
        if (!action.Visible || action.Disabled || action.Text != "Voltar ao templo")
            throw new InvalidDataException("Arroyo return action is not visible and readable.");
        action.EmitSignal(Button.SignalName.Pressed);
        if (!_walker!.Moving || !_committedRoute.SequenceEqual(guidance.Tiles!) || _campaign.CurrentMap != 4 || _campaign.Experience != 625)
            throw new InvalidDataException("Arroyo return skipped its actual walk or changed the reward early.");
        FrameRoute();
        if (_capture != null) await Capture(Path.ChangeExtension(_capture,"arroyo-return-route.png"));
        CompleteReadabilityWalk(); UseExit();
        if (_campaign.CurrentMap != 3 || _campaign.Location != option.Destination || _campaign.Experience != 625
            || JsonSerializer.Serialize(_campaign.TempleProgress) != temple || _campaign.TempleProgress?.Departure.Phase != NpcDeparturePhase.Hidden)
            throw new InvalidDataException("Returning to the Temple lost inactive NPC state or duplicated XP.");
        var route = _campaign.InspectTempleVillageRoute(false);
        if (route.Status != TempleWorldStatus.Ready || route.ExitHex == null)
            throw new InvalidDataException("The previously opened source door did not allow a second walk to Arroyo.");
        SaveCheckpoint(); var returned = CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256));
        LoadCheckpoint();
        if (!returned.SequenceEqual(CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256))))
            throw new InvalidDataException("Temple checkpoint lost the already claimed Arroyo reward.");
        RequestWalk(route.ExitHex.Value); CompleteReadabilityWalk(); UseExit();
        if (_campaign.CurrentMap != 4 || _campaign.Experience != 625 || _campaign.CarriedCount != 2
            || !_campaign.EnteredMaps.SequenceEqual(entries) || JsonSerializer.Serialize(_campaign.TempleProgress) != temple
            || JsonSerializer.Serialize(_campaign.Areas.Select(a => a.Items.Capture())) != inventory)
            throw new InvalidDataException("A second native Arroyo entry repeated its reward or changed inventory/Temple state.");
        SaveCheckpoint(); byte[] valid = CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256));
        var invalid = _campaign.Capture(_mapCatalog.Source.Sha256);
        invalid = invalid with { Entries = invalid.Entries! with { ArroyoReward = invalid.Entries.ArroyoReward! with { Experience = 601 } } };
        CheckpointFile.Write(CheckpointPath,invalid); var live = _campaign; LoadCheckpoint();
        if (_campaign != live || !valid.SequenceEqual(CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256))))
            throw new InvalidDataException("Forged Arroyo XP replaced the live world during a repeated-entry audit.");
        SaveCheckpoint(); LoadCheckpoint();
        if (_campaign.Experience != 625) throw new InvalidDataException("Repeated-entry checkpoint failed final XP persistence.");
        foreach (var area in _campaign.Areas) MapLayoutWriter.Verify(area.Map);
        if (_capture != null) await Capture(Path.ChangeExtension(_capture,"arroyo-return-no-duplicate.png"));
        GD.Print("ARROYO_RETURN_LOOP_OK: real native village-to-Temple walk and Enter to 3/2/14128, saved open-door return, actual second walk/Enter to Arroyo, XP stays 625, 2 items and NPC progress preserved, v8 return checkpoint, forged 601 XP rejected without replacing the live world.");
    }
}
