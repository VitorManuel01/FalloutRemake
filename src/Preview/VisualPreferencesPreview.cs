using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private VisualPreferences _visualPreferences = VisualPreferences.Defaults;
    private bool _checkingPreferences;
    private bool _preferencesStartupTest;
    private string VisualPreferencesPath => ProjectSettings.GlobalizePath(_test
        ? "res://.local/visual-preferences-test.json" : "res://.local/visual-preferences.json");
    private string? RestoreVisualPreferences()
    {
        string? notice = null;
        try { _visualPreferences = _test && !_checkingPreferences && !_preferencesStartupTest ? VisualPreferences.Defaults : VisualPreferencesFile.Read(VisualPreferencesPath); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _visualPreferences = VisualPreferences.Defaults;
            notice = "Preferências visuais indisponíveis · usando as opções padrão";
            GD.Print("VISUAL_PREFERENCES_UNAVAILABLE: " + error.Message);
        }
        SetNavigationGrid(_visualPreferences.ShowNavigationGrid);
        SetOriginalFloor(_visualPreferences.UseOriginalFloor);
        SetRevealActor(_visualPreferences.RevealActor);
        return notice;
    }
    private void SaveVisualPreferences(VisualPreferences preferences)
    {
        // Integration tests must never read or overwrite the user's preferences.
        if (_test && !_checkingPreferences) return;
        _visualPreferences = preferences;
        try { VisualPreferencesFile.Write(VisualPreferencesPath, preferences); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            NavigationStatus("Opção aplicada nesta sessão · não foi possível salvar as preferências visuais");
            GD.Print("VISUAL_PREFERENCES_SAVE_FAILED: " + error.Message);
        }
    }
    private void ChangeNavigationGrid(bool enabled)
    { SetNavigationGrid(enabled); SaveVisualPreferences(_visualPreferences with { ShowNavigationGrid = enabled }); }
    private void ChangeOriginalFloor(bool enabled)
    { SetOriginalFloor(enabled); SaveVisualPreferences(_visualPreferences with { UseOriginalFloor = enabled }); }
    private void ChangeRevealActor(bool enabled)
    { SetRevealActor(enabled); SaveVisualPreferences(_visualPreferences with { RevealActor = enabled }); }

    private async Task CheckVisualPreferences()
    {
        _checkingNavigation = true; _checkingPreferences = true;
        try
        {
            ResetWalker(); ToggleWalkMode();
            var location = _campaign!.Location; var doors = _doors!.Capture(); int reach = _reachable!.Count;
            _visualPreferences = VisualPreferences.Defaults;
            SetNavigationGrid(true); SetOriginalFloor(false); SetRevealActor(true);
            foreach (Key code in new[] { Key.G, Key.O, Key.V })
            { using var key = new InputEventKey { Pressed = true, PhysicalKeycode = code }; _UnhandledInput(key); }
            var expected = new VisualPreferences(false, true, false);
            if (VisualPreferencesFile.Read(VisualPreferencesPath) != expected) throw new InvalidDataException("Keys did not save all visual choices.");
            byte[] saved = File.ReadAllBytes(VisualPreferencesPath);
            SetNavigationGrid(true); SetOriginalFloor(false); SetRevealActor(true);
            if (RestoreVisualPreferences() != null || _showNavigationGrid || !_useOriginalFloor || _revealActor
                || _navigationOverlay!.Visible || !_originalFloor!.Visible)
                throw new InvalidDataException("Restored preferences did not reach scene visuals.");
            var grid = (CheckButton)_hud.FindChild("NavigationGridAction", true, false);
            var floor = (Button)_hud.FindChild("OriginalFloorAction", true, false);
            var reveal = (Button)_hud.FindChild("WallOcclusionAction", true, false);
            if (grid.ButtonPressed || !floor.ButtonPressed || reveal.ButtonPressed) throw new InvalidDataException("Restored HUD differs from saved preferences.");
            if (_campaign.Location != location || _walker!.Moving || _reachable.Count != reach || !_doors.Capture().Doors.SequenceEqual(doors.Doors))
                throw new InvalidDataException("Visual preferences changed world or navigation state.");
            SaveCheckpoint(); LoadCheckpoint();
            if (_showNavigationGrid || !_useOriginalFloor || _revealActor || !saved.SequenceEqual(File.ReadAllBytes(VisualPreferencesPath)))
                throw new InvalidDataException("Checkpoint restoration replaced visual preferences.");
            ChangeElevation(1); ResetWalker();
            if (_showNavigationGrid || !_useOriginalFloor || _revealActor || !saved.SequenceEqual(File.ReadAllBytes(VisualPreferencesPath)))
                throw new InvalidDataException("Rebuild/reset changed preferences or rewrote their file.");
            grid.EmitSignal(BaseButton.SignalName.Toggled, true);
            floor.EmitSignal(BaseButton.SignalName.Toggled, false);
            reveal.EmitSignal(BaseButton.SignalName.Toggled, true);
            if (VisualPreferencesFile.Read(VisualPreferencesPath) != VisualPreferences.Defaults) throw new InvalidDataException("HUD toggles did not save visual choices.");
            using (var locked = new FileStream(VisualPreferencesPath, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read))
            {
                ChangeNavigationGrid(false);
                if (_showNavigationGrid || _navigationOverlay?.Visible == true || VisualPreferencesFile.Read(VisualPreferencesPath) != VisualPreferences.Defaults)
                    throw new InvalidDataException("Failed save reverted the session choice or damaged the prior file.");
            }
            ChangeNavigationGrid(true);
            File.WriteAllText(VisualPreferencesPath, "{incomplete");
            if (RestoreVisualPreferences() == null || _visualPreferences != VisualPreferences.Defaults
                || File.ReadAllText(VisualPreferencesPath) != "{incomplete") throw new InvalidDataException("Malformed preferences were not preserved with a safe fallback.");
            // The next explicit choice repairs the file; no automatic rewrite occurs on load/rebuild.
            ChangeNavigationGrid(false);
            if (VisualPreferencesFile.Read(VisualPreferencesPath).ShowNavigationGrid) throw new InvalidDataException("Explicit choice failed to repair preferences.");
            ChangeNavigationGrid(true); MapLayoutWriter.Verify(_map!);
            // Seed non-default choices for a subsequent fresh-process startup check, only in the test slot.
            VisualPreferencesFile.Write(VisualPreferencesPath, expected);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print("VISUAL_PREFERENCES_OK: G/O/V and buttons, disk reload, HUD and visuals, rebuild/reset/checkpoint without preference writes, save failure keeps session and prior file, invalid file fallback/repair, unchanged actor/doors/navigation/source bytes, isolated test file verified.");
        }
        finally { _checkingPreferences = false; _checkingNavigation = false; }
    }
    private async Task CheckVisualPreferencesStartup()
    {
        var expected = new VisualPreferences(false, true, false);
        if (_visualPreferences != expected || _showNavigationGrid || !_useOriginalFloor || _revealActor
            || _originalFloor?.Visible != true || _floor?.Visible != false
            || ((CheckButton)_hud.FindChild("NavigationGridAction", true, false)).ButtonPressed
            || !((Button)_hud.FindChild("OriginalFloorAction", true, false)).ButtonPressed
            || ((Button)_hud.FindChild("WallOcclusionAction", true, false)).ButtonPressed)
            throw new InvalidDataException("Fresh process did not restore the non-default visual preferences fixture.");
        byte[] before = File.ReadAllBytes(VisualPreferencesPath);
        ToggleWalkMode();
        if (_navigationOverlay?.Visible != false || _walker!.Moving || !before.SequenceEqual(File.ReadAllBytes(VisualPreferencesPath)))
            throw new InvalidDataException("Startup changed movement, the grid choice or the preference file.");
        MapLayoutWriter.Verify(_map!);
        if (_capture != null) await Capture(Path.ChangeExtension(_capture, "preferences-startup.png"));
        GD.Print("VISUAL_PREFERENCES_STARTUP_OK: fresh Godot process restored non-default grid/floor/reveal before map load; HUD, floor visibility, walking and unchanged test file/source verified.");
    }
}
