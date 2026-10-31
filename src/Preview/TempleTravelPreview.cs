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
    private TempleTravel? _travel;
    private ExitOption CurrentExit() => _walker == null || _campaign == null ? new(ExitStatus.NoExit) : _campaign.InspectExit(_walker.Moving);
    private string ExitHint(ExitOption option) => option.Status switch
    {
        ExitStatus.Ready => $"Saída → {DestinationName(option.Destination!)} · Enter para passar",
        ExitStatus.ExternalMap => $"Saída → {DestinationName(option.Destination!)} · destino ainda não validado",
        ExitStatus.ReloadRequired => "Saída requer recarregar a área; ainda indisponível",
        ExitStatus.UnavailableMap => "Área de destino indisponível neste protótipo; sessão preservada",
        ExitStatus.BlockedDestination => "Destino da saída bloqueado ou ausente; passagem recusada",
        ExitStatus.Busy => "Aguarde o personagem terminar de caminhar",
        ExitStatus.NoExit => "Caminhe até uma saída para passar",
        _ => "Destino da saída inconsistente; passagem recusada"
    };
    private void RefreshTravelAction()
    {
        ExitOption exit = CurrentExit();
        bool visible = _walker != null && !_walker.Moving && _navigation?.IsExit(_walker.CurrentHex) == true;
        _hud.ExitAction(visible, visible && exit.Status == ExitStatus.Ready, ExitHint(exit));
    }
    private void UseExit()
    {
        if (CombatRefuses("sair da área")) return;
        ExitOption option = CurrentExit();
        if (option.Status != ExitStatus.Ready) { NavigationStatus(ExitHint(option)); return; }
        bool walking = _walkMode;
        bool following = _followActor;
        int xp = _campaign!.Experience, level = _campaign.Level;
        option = _campaign!.UseExit(_walker!.Moving);
        if (option.Status != ExitStatus.Ready) { NavigationStatus(ExitHint(option)); return; }
        BindCurrentArea();
        ActorLocation destination = _travel!.Location;
        ChangeElevation(destination.Elevation);
        if (walking) ToggleWalkMode();
        _focus = Point(FalloutGrid.HexCenter(destination.Hex) - _layout!.Origin, 0); _camera.Size = 32; UpdateCamera();
        SetCameraFollow(following);
        int award = _campaign.Experience - xp;
        NavigationStatus(award > 0 ? $"Chegou a {DestinationName(destination)} · recompensa +{award} XP{LevelUpText(level)}"
            : $"Chegou a {DestinationName(destination)} · portas e XP preservados");
    }

    private async Task CheckTravel()
    {
        _checkingNavigation = true;
        try
        {
            ResetWalker(); ToggleWalkMode();
            PreviewObject first = _layout!.Objects.Single(o => o.Source.Offset == 244040);
            string second = _doors!.TempleDoor!.SourceKey;
            RequestWalk(24508); _walker!.Advance(20); UpdateWalkerPosition(); UseDoor(first.Source.Key);
            HexReachability reach = _navigation!.ReachableFrom(_walker.CurrentHex);
            int approach = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(11108, d))
                .Where(t => t.HasValue && reach.Distance(t.Value) >= 0 && !_navigation.IsExit(t.Value))
                .Select(t => t!.Value).OrderBy(reach.Distance).First();
            RequestWalk(approach); _walker.Advance(100); UpdateWalkerPosition();
            SelectObject(_layout.Objects.Single(o => o.Source.Key == second)); UseSelectedLockpick(); UseDoor(second);
            reach = _navigation!.ReachableFrom(_walker.CurrentHex);
            int exitHex = _layout.Objects.Where(o => o.Kind == PreviewKind.Exit && o.Source.State.GetValueOrDefault("destinationMap") == 3
                && o.Source.State.GetValueOrDefault("destinationElevation") == 1 && reach.Distance(o.Source.Header.Tile) >= 0)
                .Select(o => o.Source.Header.Tile).OrderBy(reach.Distance).First();
            RequestWalk(exitHex);
            ActorLocation before = _travel!.Location;
            UseExit();
            if (_travel.Location != before) throw new InvalidDataException("Travel occurred while moving.");
            _walker.Advance(100); UpdateWalkerPosition(); NavigationStatus(ExitHint(CurrentExit()));
            if (CurrentExit() is not { Status: ExitStatus.Ready, Destination.Hex: 29097, Destination.Elevation: 1 })
                throw new InvalidDataException("Second-door exit metadata mismatch.");
            if (_capture != null)
            {
                _focus = Point(FalloutGrid.HexCenter(exitHex) - _layout!.Origin, 0); _camera.Size = 24; UpdateCamera();
                await Capture(Path.ChangeExtension(_capture, "exit.png"));
            }
            using var enter = new InputEventKey { Pressed = true, PhysicalKeycode = Key.Enter };
            _UnhandledInput(enter);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_elevation != 1 || _walker?.CurrentHex != 29097 || _travel.Location.Rotation != 0 || !_walkMode
                || !_doors.Get(second)!.IsOpen || _doors.TempleDoor.Experience != 25)
                throw new InvalidDataException("Internal transition lost destination or session state.");
            if (_walkerNode!.Position.DistanceTo(Point(FalloutGrid.HexCenter(29097) - _layout!.Origin, 0)) > .0001f)
                throw new InvalidDataException("Arriving marker transform mismatch.");
            int nearby = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(_walker.CurrentHex, d))
                .Where(t => t.HasValue && _navigation!.Walkable(t.Value) && !_navigation.IsExit(t.Value)).First()!.Value;
            RequestWalk(nearby); _walker.Advance(.05); UpdateWalkerPosition(); var progress = _walker.Position;
            GoToEntry();
            if (_elevation != 1 || !_walker.Moving || _walker.Position != progress || _campaign!.Location.Hex != 29097)
                throw new InvalidDataException("Entry focus switched away from a walking actor on another elevation.");
            _walker.Advance(100); UpdateWalkerPosition(); RefreshReachability();
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "travel.png"));
            ActorLocation arrived = _travel.Location;
            ChangeElevation(2);
            if (_walker != null || _travel.Location != arrived) throw new InvalidDataException("Inspection teleported the actor.");
            ChangeElevation(1);
            if (_walker?.CurrentHex != arrived.Hex) throw new InvalidDataException("Inspection lost actor location.");
            reach = _navigation!.ReachableFrom(_walker.CurrentHex);
            int back = _layout!.Objects.Where(o => o.Kind == PreviewKind.Exit
                && o.Source.State.GetValueOrDefault("destinationMap") == 3 && o.Source.State.GetValueOrDefault("destinationElevation") == 0
                && reach.Distance(o.Source.Header.Tile) >= 0)
                .Select(o => o.Source.Header.Tile).OrderBy(reach.Distance).First();
            RequestWalk(back); _walker.Advance(100); UpdateWalkerPosition();
            var button = _hud.FindChild("ExitAction", true, false) as Button ?? throw new InvalidDataException("Exit button missing.");
            NavigationStatus(ExitHint(CurrentExit()));
            if (button.Disabled || !button.Visible) throw new InvalidDataException("Return action unavailable.");
            button.EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_elevation != 0 || _walker?.CurrentHex != 11508 || !_doors.Get(second)!.IsOpen || _doors.TempleDoor.Experience != 25)
                throw new InvalidDataException("Return transition lost source destination/session.");
            ResetWalker();
            RequestWalk(31306); _walker!.Advance(100); UpdateWalkerPosition();
            ActorLocation externalSource = _travel.Location;
            if (CurrentExit() is not { Status: ExitStatus.Ready, Destination.Map: 126 } || _travel.Location != externalSource)
                throw new InvalidDataException("External destination preparation changed the actor or failed.");
            ChangeElevation(1); ResetWalker();
            if (_travel.Location is not { Elevation: 0, Hex: 28906 } || _walker?.CurrentHex != 28906 || _doors.TempleDoor.Experience != 0)
                throw new InvalidDataException("Reset from inspection failed.");
            MapLayoutWriter.Verify(_map!);
            GD.Print($"TRAVEL_PREVIEW_OK: exit {exitHex} -> elevation 1 / 29097; return -> elevation 0 / 11508; Enter, HUD, inspection, external guard, XP, doors, reset and source bytes verified.");
        }
        finally { _checkingNavigation = false; }
    }
}
