using System;
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
    private string CheckpointPath => ProjectSettings.GlobalizePath(_test
        ? "res://.local/checkpoints/preview-test.json" : "res://.local/checkpoints/preview.json");
    private void RefreshCheckpointActions() => _hud.CheckpointActions(_campaign != null && _walker?.Moving != true, File.Exists(CheckpointPath));
    private void SaveCheckpoint()
    {
        if (_campaign == null || _mapCatalog == null) return;
        if (_walker?.Moving == true) { NavigationStatus("Aguarde terminar o percurso para salvar."); return; }
        if (CombatRefuses("salvar")) return;
        SetTemplePlayback(false);
        try
        {
            CheckpointFile.Write(CheckpointPath, _campaign.Capture(_mapCatalog.Source.Sha256));
            NavigationStatus("Checkpoint salvo · R para continuar desta posição");
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        { NavigationStatus("Não foi possível salvar o checkpoint."); GD.Print("CHECKPOINT_SAVE_FAILED: " + error.Message); }
    }
    private void LoadCheckpoint()
    {
        if (_campaign == null || _directory == null) return;
        DiscardCombat();
        _skipTempleFrame = true;
        try
        {
            WorldCheckpoint saved = CheckpointFile.Read(CheckpointPath);
            var data = new GameData(_directory);
            var catalog = new MapCatalog(data.Read("data/maps.txt"));
            var runtimePrototypes = new PrototypeCatalog(new ResourceCatalog(data.Read));
            WorldSession restored = WorldSession.Restore(id => catalog.Load(data, id), new[] { 3, 4, 126 }, saved, catalog.Source.Sha256, runtimePrototype: runtimePrototypes.Get,
                templeProfile: map => TempleNpcDepartureProfile.Create(map,data.Read),arroyoProfile: map => ArroyoRewardProfile.Create(map,data.Read),
                premade: path => CharacterFile.Read(data.Read(path)));
            bool walking = _walkMode; string? selectedKey = _selected?.Source.Key;
            _campaign = restored; _mapCatalog = catalog; BindCharacter(); BindCurrentArea();
            ChangeElevation(_travel!.Location.Elevation); if (walking != _walkMode) ToggleWalkMode();
            var selected = _layout!.Objects.FirstOrDefault(o => o.Source.Key == selectedKey && VisibleObject(o));
            if (selected != null) SelectObject(selected);
            _focus = Point(FalloutGrid.HexCenter(_travel.Location.Hex) - _layout!.Origin, 0); _camera.Size = 32; UpdateCamera();
            NavigationStatus($"Checkpoint carregado · mapa {_campaign.CurrentMap} · XP {_campaign.Experience}");
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        { NavigationStatus("Checkpoint indisponível ou incompatível · sessão atual preservada"); GD.Print("CHECKPOINT_REJECTED: " + error.Message); }
    }
    private async Task CheckCheckpoint()
    {
        _checkingNavigation = true;
        try
        {
            ResetWalker(); ToggleWalkMode();
            string first = _layout!.Objects.Single(o => o.Source.Offset == 244040).Source.Key;
            string second = _doors!.TempleDoor!.SourceKey;
            RequestWalk(24508); _walker!.Advance(100); UpdateWalkerPosition(); UseDoor(first);
            HexReachability reach = _navigation!.ReachableFrom(_walker.CurrentHex);
            int approach = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(11108, d))
                .Where(t => t.HasValue && reach.Distance(t.Value) >= 0 && !_navigation.IsExit(t.Value))
                .Select(t => t!.Value).OrderBy(reach.Distance).First();
            RequestWalk(approach); _walker.Advance(100); UpdateWalkerPosition();
            SelectObject(_layout.Objects.Single(o => o.Source.Key == second)); UseSelectedLockpick(); UseDoor(second);
            RequestWalk(ClosestExitTo(126)); _walker.Advance(1000); UpdateWalkerPosition(); UseExit();
            ActorLocation savedLocation = _campaign!.Location;
            using var save = new InputEventKey { Pressed = true, PhysicalKeycode = Key.K };
            using var load = new InputEventKey { Pressed = true, PhysicalKeycode = Key.R };
            _UnhandledInput(save);
            WorldCheckpoint checkpoint = CheckpointFile.Read(CheckpointPath);
            if (checkpoint.CurrentMap != 126 || checkpoint.Areas.Length != 2) throw new InvalidDataException("Checkpoint failed to capture both areas.");
            ResetWalker();
            WorldSession beforeLegacyKey = _campaign;
            using var legacyLoad = new InputEventKey { Pressed = true, PhysicalKeycode = Key.F9 };
            _UnhandledInput(legacyLoad);
            if (!ReferenceEquals(beforeLegacyKey, _campaign) || _campaign.CurrentMap != 3 || _campaign.Experience != 0)
                throw new InvalidDataException("Reserved editor key still loads a checkpoint.");
            _UnhandledInput(load);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_campaign!.Location != savedLocation || _campaign.Experience != 25 || _campaign.CurrentMap != 126)
                throw new InvalidDataException("Checkpoint did not restore actor, area and XP.");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "checkpoint.png"));
            RequestWalk(ClosestExitTo(3)); _walker!.Advance(1000); UpdateWalkerPosition(); UseExit();
            if (!_doors!.Get(first)!.IsOpen || !_doors.Get(second)!.IsOpen || _campaign.Experience != 25)
                throw new InvalidDataException("Checkpoint lost the inactive temple state.");
            WorldSession live = _campaign;
            CheckpointFile.Write(CheckpointPath, checkpoint with { CatalogHash = "incompatible" });
            _UnhandledInput(load);
            if (!ReferenceEquals(live, _campaign) || _campaign.Experience != 25)
                throw new InvalidDataException("Rejected checkpoint changed the live session.");
            var saveButton = _hud.FindChild("SaveAction", true, false) as Button ?? throw new InvalidDataException("Save button missing.");
            saveButton.EmitSignal(Button.SignalName.Pressed);
            ResetWalker();
            var loadButton = _hud.FindChild("LoadAction", true, false) as Button ?? throw new InvalidDataException("Load button missing.");
            loadButton.EmitSignal(Button.SignalName.Pressed);
            if (_campaign.Experience != 25 || !_doors!.Get(second)!.IsOpen) throw new InvalidDataException("Checkpoint HUD actions failed.");
            foreach (WorldArea area in _campaign.Areas) MapLayoutWriter.Verify(area.Map);
            ResetWalker();
            GD.Print("CHECKPOINT_PREVIEW_OK: K/R and HUD; F9 ignored by preview; exterior actor, 2 areas, doors, XP 25, incompatible-source rejection and byte reconstruction verified; production slot untouched.");
        }
        finally { _checkingNavigation = false; }
    }
}
