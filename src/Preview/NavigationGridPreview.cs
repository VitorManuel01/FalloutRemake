using System.IO;
using System.Threading.Tasks;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private bool _showNavigationGrid = true;
    private void SetNavigationGrid(bool enabled)
    {
        _showNavigationGrid = enabled; _hud.NavigationGrid(enabled);
        if (_navigationOverlay != null) _navigationOverlay.Visible = _walkMode && enabled;
    }
    private async Task CheckNavigationGrid()
    {
        _checkingNavigation = true;
        try
        {
            ResetWalker(); ToggleWalkMode();
            using var toggle = new InputEventKey { Pressed = true, PhysicalKeycode = Key.G };
            _UnhandledInput(toggle);
            var location = _campaign!.Location;
            int reach = _reachable!.Count;
            if (_showNavigationGrid || _navigationOverlay!.Visible || !_walkMode || _walker == null)
                throw new InvalidDataException("Grid toggle disabled walking or retained overlay.");
            HexRoute route = RequestWalk(24508);
            if (route.Status != RouteStatus.Ready || route.Tiles.Length != 24 || !_routeVisual!.Visible || _campaign.Location != location)
                throw new InvalidDataException("Hidden grid lost path display or changed the actor.");
            var button = _hud.FindChild("NavigationGridAction", true, false) as CheckButton ?? throw new InvalidDataException("Grid toggle button missing.");
            button.EmitSignal(BaseButton.SignalName.Toggled, true);
            if (!_showNavigationGrid || !_navigationOverlay.Visible || !_walker.Moving || _reachable.Count != reach)
                throw new InvalidDataException("Grid HUD changed movement or reachability.");
            button.EmitSignal(BaseButton.SignalName.Toggled, false);
            await CheckWorldTravel();
            if (_showNavigationGrid) throw new InvalidDataException("World travel/reset lost the grid preference.");
            ToggleWalkMode(); FocusCharacter(); SetOriginalFloor(true);
            if (!_walkMode || _navigationOverlay!.Visible || !_originalFloor!.Visible)
                throw new InvalidDataException("Floor reference and walking without grid failed.");
            NavigationStatus("Caminhada ativa · grade oculta · G para exibir os hexágonos");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!_hud.NavigationFits) throw new InvalidDataException("Navigation controls overlap the footer.");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "grid.png"));
            SetNavigationGrid(true); SetOriginalFloor(false); ResetWalker();
            MapLayoutWriter.Verify(_map!);
            GD.Print("NAVIGATION_GRID_OK: G/button, route visibility, walking, reachability, doors, exterior/return, reset, floor artwork and source bytes verified.");
        }
        finally { _checkingNavigation = false; }
    }
}
