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
    private DoorSession? _doors;
    private PreviewSkillResolver _skills = new(PrototypeCharacter.Neutral);

    private DoorUseStatus DoorStatus(string key) => _walker == null ? DoorUseStatus.Unavailable
        : _doors?.CanToggle(key, _walker.CurrentHex, _elevation, _walker.Moving) ?? DoorUseStatus.NoDoor;
    private static string DoorHint(DoorUseStatus status) => status switch
    {
        DoorUseStatus.Ready => "Use U ou o botão para abrir/fechar.",
        DoorUseStatus.ScriptRequired => "Esta porta depende de um script ainda não implementado.",
        DoorUseStatus.Locked => "Porta trancada; use Arrombar [L] se disponível.",
        DoorUseStatus.Jammed => "Fechadura emperrada; reinicie a prévia para restaurar a sessão.",
        DoorUseStatus.StateChanged => "A sessão mudou durante a tentativa; tente novamente.",
        DoorUseStatus.TooFar => "No modo Caminhar, clique na porta para se aproximar.",
        DoorUseStatus.Busy => "Aguarde o marcador terminar o percurso.",
        DoorUseStatus.Occupied => "Não é possível fechar: há alguém ou um bloqueador na passagem.",
        DoorUseStatus.Inconsistent => "Flags da porta inconsistentes; interação indisponível.",
        DoorUseStatus.AtExit => "Marcador em uma saída; reinicie a prévia para continuar.",
        DoorUseStatus.NoDoor => "Selecione uma porta para usar.",
        _ => "Interação indisponível nesta elevação ou tipo de porta."
    };
    private void RefreshDoorAction()
    {
        RefreshSpeechAction();
        DoorState? door = _selected == null ? null : _doors?.Get(_selected.Source.Key);
        DoorUseStatus status = door == null ? DoorUseStatus.NoDoor : DoorStatus(door.Source.Key);
        _hud.DoorAction(status == DoorUseStatus.Ready, door?.IsOpen == true, DoorHint(status));
        DoorUseStatus lockpick = door == null ? DoorUseStatus.NoDoor : LockpickStatus(door.Source.Key);
        PrototypeCharacter character = _skills.Character;
        string hint = lockpick == DoorUseStatus.Ready ? "L tenta destrancar ou retrancar usando a perícia de Arrombamento." : DoorHint(lockpick);
        _hud.LockpickAction(lockpick == DoorUseStatus.Ready, door?.IsLocked != false, hint,
            $"{CharacterLine()} · Nível {_campaign?.Level ?? 1} · XP {_campaign?.Experience ?? 0} · HP {_campaign?.HitPoints ?? 30}/{_campaign?.MaxHitPoints ?? 30}{(_campaign?.Poison is { Active: true } poison ? $" · envenenado ({poison.Level})" : "")}{(_campaign?.UnspentSkillPoints is > 0 and int points ? $" · {points} pontos de perícia [H]" : "")}{(_campaign?.PerksAvailable is > 0 ? " · perk disponível" : "")} · Personagem na elevação {_travel?.Location.Elevation ?? 0}{HandsSuffix()}");
    }
    private void UseSelectedDoor()
    {
        if (CombatRefuses("usar portas")) return;
        if (_selected == null) { NavigationStatus(DoorHint(DoorUseStatus.NoDoor)); return; }
        UseDoor(_selected.Source.Key);
    }
    private DoorUseStatus UseDoor(string key)
    {
        if (CombatRefuses("usar portas")) return DoorUseStatus.Busy;
        ClearRouteHover();
        DoorUseStatus status = DoorStatus(key);
        if (status != DoorUseStatus.Ready) { NavigationStatus(DoorHint(status)); RefreshDoorAction(); return status; }
        status = _doors!.Toggle(key, _walker!.CurrentHex, _elevation, _walker.Moving);
        if (status != DoorUseStatus.Ready) return status;
        int current = _walker.CurrentHex;
        _navigation = _doors.Navigation(_elevation); _walker = new HexWalker(_navigation, current);
        ClearRouteVisual();
        ApplyDoorVisuals(); RefreshReachability(); UpdateWalkerPosition();
        if (_selected != null) SelectObject(_selected);
        RefreshDoorAction();
        NavigationStatus($"Porta {(_doors.Get(key)!.IsOpen ? "aberta" : "fechada")} · {_reachable!.Count} hexes alcançáveis");
        return status;
    }

    private void ApplyDoorVisuals()
    {
        if (_layout == null || _doors == null) return;
        foreach (PreviewObject obj in _layout.Objects.Where(o => o.Kind == PreviewKind.Door))
        {
            bool open = _doors.Get(obj.Source.Key)?.IsOpen == true;
            // Provisional hinge motion; the source root and its hex/ID never move.
            Transform3D pose = open ? new Transform3D(new Basis(Vector3.Up, Mathf.Pi / 2), new Vector3(-.4f, 0, .25f)) : Transform3D.Identity;
            StaticBody3D body = _bodies[obj.Source.Key];
            body.GetChild<MeshInstance3D>(0).Transform = pose;
            body.GetChild<CollisionShape3D>(1).Transform = pose; // Picking only, not navigation.
        }
    }

    private PreviewObject? WorldObjectAtScreen(Vector2 screen)
    {
        Vector3 origin = _camera.ProjectRayOrigin(screen), end = origin + _camera.ProjectRayNormal(screen) * 700;
        using var query = PhysicsRayQueryParameters3D.Create(origin, end, 1);
        using var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.TryGetValue("collider", out Variant value) && value.AsGodotObject() is StaticBody3D body
            && _lookup.TryGetValue(body.GetInstanceId(), out PreviewObject? obj) ? obj : null;
    }
    private PreviewObject? TryPickDoor(Vector2 screen) => WorldObjectAtScreen(screen) is { Kind: PreviewKind.Door } door ? door : null;
    private void ApproachDoor(PreviewObject door)
    {
        ClearRouteHover();
        SelectObject(door);
        if (_walker == null || _navigation == null) return;
        DoorUseStatus status = DoorApproachStatus(door);
        if (status != DoorUseStatus.TooFar) { NavigationStatus(DoorHint(status)); return; }
        HexReachability reached = _navigation.ReachableFrom(_walker.CurrentHex);
        int? candidate = ClosestDoorApproach(door, reached, _navigation);
        if (!candidate.HasValue) { NavigationStatus("Não há caminho até o lado desta porta."); return; }
        RequestWalk(candidate.Value);
    }

    private async Task CheckDoors()
    {
        _checkingNavigation = true;
        try
        {
            PreviewObject door = _layout!.Objects.Single(o => o.Source.Offset == 244040);
            PreviewObject scripted = _layout.Objects.Single(o => o.Source.Offset == 137396);
            HexReachability originalReach = _navigation!.ReachableFrom(28906);
            if (_doors!.Get(door.Source.Key)!.Scripted || door.Source.Header.Sid != -1 || originalReach.Count != 540)
                throw new InvalidDataException("First door source/script identification mismatch.");
            if (UseDoor(door.Source.Key) != DoorUseStatus.TooFar) throw new InvalidDataException("Door used from a distance.");
            ToggleWalkMode();
            _focus = Point(door.Position, 0); _camera.Size = 24; UpdateCamera();
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            WalkAtScreen(_camera.UnprojectPosition(Point(door.Position, 1.2f)));
            if (_selected?.Source.Key != door.Source.Key || !_walker!.Moving) throw new InvalidDataException("Door click failed to approach/select.");
            if (UseDoor(door.Source.Key) != DoorUseStatus.Busy) throw new InvalidDataException("Door changed during movement.");
            _walker.Advance(20); UpdateWalkerPosition(); RefreshDoorAction();
            using var useKey = new InputEventKey { Pressed = true, PhysicalKeycode = Key.U };
            _UnhandledInput(useKey);
            if (!_doors.Get(door.Source.Key)!.IsOpen || !_navigation!.Walkable(24308) || _reachable!.Count <= 540)
                throw new InvalidDataException("Door opening did not expand navigation.");
            int openedCount = _reachable.Count;
            Vector3 root = Point(door.Position, Dimensions(door.Kind).Y / 2);
            if (_bodies[door.Source.Key].Position != root || _bodies[door.Source.Key].GetChild<MeshInstance3D>(0).Transform == Transform3D.Identity)
                throw new InvalidDataException("Door pose/source anchor mismatch.");
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Vector3 openPick = _bodies[door.Source.Key].ToGlobal(new Vector3(-.4f, .5f, .25f));
            if (TryPickDoor(_camera.UnprojectPosition(openPick))?.Source.Key != door.Source.Key)
                throw new InvalidDataException("Open door picking no longer follows its visual pose.");
            int beyond = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(24308, d)!.Value)
                .First(t => _reachable.Distance(t) >= 0 && originalReach.Distance(t) < 0 && !_navigation.IsExit(t));
            HexRoute crossing = _navigation.FindRoute(_walker.CurrentHex, beyond);
            if (crossing.Status != RouteStatus.Ready || !crossing.Tiles.Contains(24308)) throw new InvalidDataException("Opened route does not cross the doorway.");
            RequestWalk(24308); _walker.Advance(20); UpdateWalkerPosition();
            if (_walker.CurrentHex != 24308 || UseDoor(door.Source.Key) != DoorUseStatus.Occupied)
                throw new InvalidDataException("Door closed on the marker.");
            RequestWalk(beyond); _walker.Advance(20); UpdateWalkerPosition(); RefreshReachability(); RefreshDoorAction();
            NavigationStatus($"Porta aberta · passagem atravessada · {openedCount} hexes alcançáveis");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "door.png"));
            if (UseDoor(door.Source.Key) != DoorUseStatus.Ready || _navigation.Walkable(24308)
                || _navigation.ReachableFrom(28906).Count != 540) throw new InvalidDataException("Door closing did not restore the initial boundary.");
            if (UseDoor(scripted.Source.Key) != DoorUseStatus.Locked) throw new InvalidDataException("Adapted locked door was bypassed.");
            // Preserve runtime state across an inspection-layer switch, then explicitly reset it.
            if (UseDoor(door.Source.Key) != DoorUseStatus.Ready) throw new InvalidDataException("Reopening failed.");
            ChangeElevation(1); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GoToEntry(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!_doors.Get(door.Source.Key)!.IsOpen || !_navigation!.Walkable(24308)) throw new InvalidDataException("Layer inspection lost door state.");
            ResetWalker();
            if (_doors.Get(door.Source.Key)!.IsOpen || _reachable!.Count != 540 || _walker!.CurrentHex != 28906)
                throw new InvalidDataException("Preview reset did not restore door/marker state.");
            MapLayoutWriter.Verify(_map!);
            if (door.Source.Header.Flags != 0 || door.Source.State["openFlags"] != 0) throw new InvalidDataException("Source door was mutated.");
            GD.Print($"DOOR_TEST_OK: source SID -1; 540 -> {openedCount} reachable hexes; approach, open, cross, occupied-close rejection, close, scripted-door guard, layer persistence, reset and source bytes verified.");
        }
        finally { _checkingNavigation = false; }
    }
}
