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
    private async Task CheckTempleVillageGuidance()
    {
        // Start beyond a single step so interruption exercises a real remaining route.
        var retreat = _navigation!.FindRoute(_walker!.CurrentHex,19728);
        if (retreat.Tiles.Length < 7) throw new InvalidDataException("Village recovery audit needs a reachable original interior segment.");
        if (!_walkMode) ToggleWalkMode();
        RequestWalk(retreat.Tiles[6]); CompleteReadabilityWalk();
        var approach = _campaign!.InspectTempleVillageRoute(false);
        if (approach.Status != TempleWorldStatus.DoorRequired || approach.DoorKey == null || approach.TargetHex == null)
            throw new InvalidDataException("Village guidance must first respect the closed source door 100.");
        var doorAction = (Button)_hud.FindChild("TempleVillageExitAction",true,false);
        var door = _doors!.Get(approach.DoorKey)!;
        if (door.Source.Header.Id != 100 || door.Source.Header.Tile != 13528 || door.IsOpen)
            throw new InvalidDataException("Village guidance picked a foreign door.");
        doorAction.EmitSignal(Button.SignalName.Pressed);
        if (!_walker!.Moving || !_committedRoute.SequenceEqual(approach.Tiles!) || _doors.Get(approach.DoorKey)!.IsOpen
            || !doorAction.Disabled || !doorAction.Text.StartsWith("Caminhando",StringComparison.Ordinal))
            throw new InvalidDataException("Village approach skipped walking or opened its door automatically.");
        var oldApproach = _hud.TempleVillageRequested!;
        _walker.Advance(.05); UpdateWalkerPosition(); int next = _walker.NextHex;
        StopWalking();
        if (!_walker.StopRequested || !_walker.Moving) throw new InvalidDataException("Village approach did not keep the current step while stopping.");
        CompleteReadabilityWalk();
        if (_campaign.Location.Hex != next || _doors.Get(approach.DoorKey)!.IsOpen || _campaign.CurrentMap != 3)
            throw new InvalidDataException("Stopped village guidance jumped hexes or opened/travelled early.");
        SaveCheckpoint(); var stopped = CheckpointFile.Encode(_campaign.Capture(_mapCatalog!.Source.Sha256)); LoadCheckpoint();
        oldApproach();
        if (_walker!.Moving || !stopped.SequenceEqual(CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256))))
            throw new InvalidDataException("Stale village approach resumed after checkpoint load.");
        var remaining = _campaign.InspectTempleVillageRoute(false);
        if (remaining.Status != TempleWorldStatus.DoorRequired || remaining.Tiles![0] != next || remaining.Tiles.Length >= approach.Tiles!.Length)
            throw new InvalidDataException("Village approach did not replan from the stopped authoritative hex.");
        doorAction.EmitSignal(Button.SignalName.Pressed);
        if (!_walker.Moving || !_committedRoute.SequenceEqual(remaining.Tiles!))
            throw new InvalidDataException($"Restored village guidance did not restart its exact remaining walk: moving {_walker.Moving}, disabled {doorAction.Disabled}, current {_campaign.Location.Hex}, expected {string.Join(',',remaining.Tiles!)}, actual {string.Join(',',_committedRoute)}.");
        CompleteReadabilityWalk();
        using (var use = new InputEventKey { Pressed = true,PhysicalKeycode = Key.U }) _UnhandledInput(use);
        if (!_doors.Get(approach.DoorKey)!.IsOpen || _campaign.Experience != 25 || _campaign.CarriedCount != 2)
            throw new InvalidDataException("Verified village door did not open with U or granted unsupported rewards.");
        SaveCheckpoint(); var openState = CheckpointFile.Encode(_campaign.Capture(_mapCatalog!.Source.Sha256)); LoadCheckpoint();
        if (!openState.SequenceEqual(CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256))) || !_doors!.Get(approach.DoorKey)!.IsOpen)
            throw new InvalidDataException("Open village door did not survive checkpoint restoration.");
        if (_capture != null) await Capture(Path.ChangeExtension(_capture,"temple-village-door.png"));
        var route = _campaign!.InspectTempleVillageRoute(false); ActorLocation start = _campaign.Location;
        string progress = JsonSerializer.Serialize(_campaign.TempleProgress);
        if (route.Status != TempleWorldStatus.Ready || route.Destination != new ActorLocation(4,0,20517,0) || route.SourceKeys is not { Length: > 0 })
        {
            var navigation = _campaign.Current.Doors.Navigation(2); var reach = navigation.ReachableFrom(start.Hex);
            var exits = _map!.ObjectLayers.SelectMany(l => l.Objects).Where(o => o.Header.Elevation == 2 && o.State.TryGetValue("destinationMap",out int id) && id == 4)
                .Select(o => new { o.Key,o.Header.Tile,o.Header.Flags,Walkable = navigation.Walkable(o.Header.Tile),Distance = reach.Distance(o.Header.Tile),
                    Status = _campaign.PreviewExitAt(2,o.Header.Tile).Status.ToString(),Blockers = navigation.BlockersAt(o.Header.Tile) });
            throw new InvalidDataException($"Original Arroyo exit guidance is unavailable after NPC departure: {route.Status}; player {start}; phase {_campaign.TempleProgress?.Departure.Phase}; exits {JsonSerializer.Serialize(exits)}.");
        }
        foreach (string key in route.SourceKeys)
            if (!_map!.AllObjects.Any(o => o.Key == key && o.Header.Elevation == 2 && o.State.TryGetValue("destinationMap",out int id) && id == 4))
                throw new InvalidDataException("Arroyo guidance lost original exit identity.");
        var action = (Button)_hud.FindChild("TempleVillageExitAction",true,false); var old = _hud.TempleVillageRequested!;
        if (!action.Visible || action.Disabled) throw new InvalidDataException("Arroyo guidance action is not available.");
        action.EmitSignal(Button.SignalName.Pressed);
        if (!_walker!.Moving || !_walkMode || !_committedRoute.SequenceEqual(route.Tiles!) || _campaign.Location != start || _campaign.CurrentMap != 3)
            throw new InvalidDataException("Arroyo guidance skipped the actual walk or travelled early.");
        var committed = _committedRoute.ToArray(); old();
        if (!_committedRoute.SequenceEqual(committed)) throw new InvalidDataException("Busy Arroyo guidance replaced its route.");
        if (_capture != null) await Capture(Path.ChangeExtension(_capture,"temple-village-route.png"));
        CompleteReadabilityWalk();
        if (_campaign.CurrentMap != 3 || _walker.CurrentHex != route.ExitHex || CurrentExit() is not { Status:ExitStatus.Ready, Destination.Map:4 }
            || _campaign.CarriedCount != 2 || _campaign.Experience != 25)
            throw new InvalidDataException("Walk to the village exit failed its native endpoint or travelled without Enter.");
        using var enter = new InputEventKey { Pressed = true,PhysicalKeycode = Key.Enter }; _UnhandledInput(enter);
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        if (_campaign.CurrentMap != 4 || _campaign.Location != route.Destination || _campaign.CarriedCount != 2 || _campaign.Experience != 625
            || _campaign.ArroyoReward?.Experience != 600 || !_campaign.EnteredMaps.Contains(4)
            || _campaign.ArroyoGlobals is not {Karma:15,ArroyoReputation:65,KarmaTierGlobal:41}
            || progress != JsonSerializer.Serialize(_campaign.TempleProgress))
            throw new InvalidDataException("Original Arroyo travel lost the completed Temple world state.");
        SaveCheckpoint(); var saved = CheckpointFile.Encode(_campaign.Capture(_mapCatalog!.Source.Sha256));
        ResetWalker(); LoadCheckpoint();
        if (!saved.SequenceEqual(CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256))) || _campaign.CurrentMap != 4)
            throw new InvalidDataException("Arroyo checkpoint failed to restore its inactive Temple progress.");
        old();
        if (_walker!.Moving || !saved.SequenceEqual(CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256))))
            throw new InvalidDataException("Old Temple guidance changed the loaded Arroyo session.");
        foreach (var area in _campaign.Areas) MapLayoutWriter.Verify(area.Map);
        if (_capture != null) await Capture(Path.ChangeExtension(_capture,"arroyo-after-speech.png"));
        await CheckArroyoReturnLoop();
        await CheckArroyoElderApproach();
        GD.Print("TEMPLE_VILLAGE_ROUTE_OK: source door 100 approach, mid-step stop and v8 load/replan, stale approach refusal, explicit U, open-door restoration, source exits to map 4/0/20517, exact real walk, explicit Enter travel, busy/stale guards, Arroyo checkpoint with inactive Temple progress, original source bytes/items preserved; verified first entry grants 600 XP (25 to 625).");
        GD.Print("ARROYO_ENTRY_REWARD_OK: first actual entry marked independently of cached destination, source-bound neutral Speech reward, exactly 600 XP, v9 restoration with source/items/progress preserved.");
        GD.Print("ARROYO_ENTRY_GLOBALS_OK: real native first entry, karma 0 to 15, Arroyo reputation 50 to 65, Wanderer G41, v9 save/load and actual return/reentry preserve globals and 625 total XP.");
    }
}
