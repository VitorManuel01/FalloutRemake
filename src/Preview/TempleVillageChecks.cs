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
        var approach = _campaign!.InspectTempleVillageRoute(false);
        if (approach.Status != TempleWorldStatus.DoorRequired || approach.DoorKey == null || approach.TargetHex == null)
            throw new InvalidDataException("Village guidance must first respect the closed source door 100.");
        var doorAction = (Button)_hud.FindChild("TempleVillageExitAction",true,false);
        var door = _doors!.Get(approach.DoorKey)!;
        if (door.Source.Header.Id != 100 || door.Source.Header.Tile != 13528 || door.IsOpen)
            throw new InvalidDataException("Village guidance picked a foreign door.");
        doorAction.EmitSignal(Button.SignalName.Pressed);
        if (!_walker!.Moving || !_committedRoute.SequenceEqual(approach.Tiles!) || _doors.Get(approach.DoorKey)!.IsOpen)
            throw new InvalidDataException("Village approach skipped walking or opened its door automatically.");
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
        if (_campaign.CurrentMap != 4 || _campaign.Location != route.Destination || _campaign.CarriedCount != 2 || _campaign.Experience != 25
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
        GD.Print("TEMPLE_VILLAGE_ROUTE_OK: source door 100 approach and explicit U, open-door v7 restoration, source exits to map 4/0/20517, exact real walk, explicit Enter travel, busy/stale guards, Arroyo checkpoint with inactive Temple progress, original source bytes/items/25 XP preserved.");
    }
}
