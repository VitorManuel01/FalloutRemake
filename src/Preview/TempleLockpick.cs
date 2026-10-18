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
    private DoorUseStatus LockpickStatus(string key) => _walker == null || _elevation != 0 ? DoorUseStatus.Unavailable
        : _doors?.CanLockpick(key, _walker.CurrentHex, _elevation, _walker.Moving) ?? DoorUseStatus.NoDoor;

    private void UseSelectedLockpick()
    {
        if (_selected == null) { NavigationStatus(DoorHint(DoorUseStatus.NoDoor)); return; }
        UseLockpick(_selected.Source.Key);
    }
    private LockpickResult UseLockpick(string key)
    {
        DoorUseStatus status = LockpickStatus(key);
        if (status != DoorUseStatus.Ready)
        { NavigationStatus(DoorHint(status)); RefreshDoorAction(); return new LockpickResult(status); }
        LockpickResult result = _doors!.UseLockpick(key, _walker!.CurrentHex, _elevation, _walker.Moving, _skills.Resolve);
        if (_selected != null) SelectObject(_selected);
        RefreshDoorAction();
        if (result.Status != DoorUseStatus.Ready) { NavigationStatus(DoorHint(result.Status)); return result; }
        string message = result.Outcome switch
        {
            SkillRollOutcome.Success or SkillRollOutcome.CriticalSuccess => _doors.Get(key)!.IsLocked ? "Porta retrancada" : "Porta destrancada · U para abrir",
            SkillRollOutcome.CriticalFailure => "Falha crítica · fechadura emperrada",
            _ => "Falha no arrombamento"
        };
        SkillCheckResult check = _skills.LastCheck!;
        NavigationStatus($"{message} · d100 {check.Percentile} / alvo {check.SkillValue + check.Modifier}"
            + (result.ExperienceAwarded > 0 ? $" · +{result.ExperienceAwarded} XP" : ""));
        return result;
    }

    private async Task CheckLockpick()
    {
        _checkingNavigation = true;
        try
        {
            ResetWalker();
            if (_doors!.TempleDoor == null) throw new InvalidDataException("Verified temple adapter missing from preview.");
            PreviewObject first = _layout!.Objects.Single(o => o.Source.Offset == 244040);
            PreviewObject second = _layout.Objects.Single(o => o.Source.Offset == 137396);
            var button = _hud.FindChild("LockpickAction", true, false) as Button
                ?? throw new InvalidDataException("Lockpick action missing from HUD.");
            if (_skills.Character.Lockpick != 20 || !_doors.Get(second.Source.Key)!.IsLocked)
                throw new InvalidDataException("Initial profile/lock state mismatch.");
            if (UseLockpick(second.Source.Key).Status != DoorUseStatus.TooFar || _skills.LastCheck != null)
                throw new InvalidDataException("Distant attempt consumed a roll.");
            ToggleWalkMode();
            RequestWalk(24508); _walker!.Advance(20); UpdateWalkerPosition();
            if (UseDoor(first.Source.Key) != DoorUseStatus.Ready) throw new InvalidDataException("First door failed in lockpick preview test.");

            _focus = Point(second.Position, 0); _camera.Size = 24; UpdateCamera();
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            WalkAtScreen(_camera.UnprojectPosition(Point(second.Position, 1.2f)));
            if (_selected?.Source.Key != second.Source.Key || !_walker!.Moving || !button.Disabled)
                throw new InvalidDataException("Locked door camera click did not approach/select with action disabled during movement.");
            if (UseLockpick(second.Source.Key).Status != DoorUseStatus.Busy || _skills.LastCheck != null)
                throw new InvalidDataException("Moving attempt consumed a roll.");
            _walker.Advance(20); UpdateWalkerPosition(); SelectObject(second);
            if (button.Disabled || !button.Text.Contains("Arrombar")) throw new InvalidDataException("Adjacent lockpick action unavailable.");
            using var lockpickKey = new InputEventKey { Pressed = true, PhysicalKeycode = Key.L };
            _UnhandledInput(lockpickKey);
            SkillCheckResult? check = _skills.LastCheck;
            if (check is not { Outcome: SkillRollOutcome.Success, SkillValue: 20, Modifier: 100, CriticalPercentile: null }
                || check.Percentile is < 1 or > 100 || _doors.Get(second.Source.Key) is not { IsLocked: false, IsOpen: false }
                || _doors.TempleDoor.Experience != 25 || _navigation!.Walkable(11108) || !button.Text.Contains("Retrancar"))
                throw new InvalidDataException("Keyboard lockpick did not apply the real first-day profile roll/XP/closed state.");
            HexReachability before = _navigation.ReachableFrom(28906);
            using var useKey = new InputEventKey { Pressed = true, PhysicalKeycode = Key.U };
            _UnhandledInput(useKey);
            if (!_doors.Get(second.Source.Key)!.IsOpen || _navigation!.ReachableFrom(28906).Count != 2189)
                throw new InvalidDataException("Second door opening failed after lockpick.");
            int beyond = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(11108, d))
                .Where(t => t.HasValue && _navigation.Walkable(t.Value) && !_navigation.IsExit(t.Value))
                .Select(t => t!.Value).First(t => before.Distance(t) < 0);
            RequestWalk(11108); _walker!.Advance(20); UpdateWalkerPosition();
            if (UseDoor(second.Source.Key) != DoorUseStatus.Occupied) throw new InvalidDataException("Second door closed on marker.");
            RequestWalk(beyond); _walker.Advance(20); UpdateWalkerPosition(); RefreshDoorAction();
            NavigationStatus("Porta arrombada · +25 XP · passagem atravessada · U para fechar");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "lockpick.png"));
            if (UseDoor(second.Source.Key) != DoorUseStatus.Ready) throw new InvalidDataException("Second door failed to close.");
            if (button.Disabled) throw new InvalidDataException("Relock button unavailable.");
            button.EmitSignal(Button.SignalName.Pressed);
            if (!_doors.Get(second.Source.Key)!.IsLocked || _doors.TempleDoor.Experience != 25)
                throw new InvalidDataException("HUD relock did not preserve single XP reward.");
            _UnhandledInput(lockpickKey);
            if (_doors.Get(second.Source.Key)!.IsLocked || _doors.TempleDoor.Experience != 25)
                throw new InvalidDataException("Repeated unlock failed or farmed XP.");
            ChangeElevation(1); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!button.Disabled || LockpickStatus(second.Source.Key) != DoorUseStatus.Unavailable)
                throw new InvalidDataException("Lockpick remained available on inspection elevation.");
            GoToEntry(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_doors.Get(second.Source.Key)!.IsLocked || _doors.TempleDoor.Experience != 25)
                throw new InvalidDataException("Layer inspection lost lockpick state.");

            // Test-only injected failure verifies HUD guard; normal play always uses the profile resolver.
            HexReachability reached = _navigation!.ReachableFrom(_walker!.CurrentHex);
            int approach = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(11108, d))
                .Where(t => t.HasValue && reached.Distance(t.Value) >= 0 && !_navigation.IsExit(t.Value))
                .Select(t => t!.Value).OrderBy(reached.Distance).First();
            RequestWalk(approach); _walker.Advance(100); UpdateWalkerPosition(); SelectObject(_layout!.Objects.Single(o => o.Source.Key == second.Source.Key));
            button.EmitSignal(Button.SignalName.Pressed); // Relock, no extra XP.
            _doors.UseLockpick(second.Source.Key, _walker.CurrentHex, 0, false, _ => SkillRollOutcome.CriticalFailure);
            SelectObject(_selected!);
            SkillCheckResult? previous = _skills.LastCheck;
            _UnhandledInput(lockpickKey);
            if (!button.Disabled || !_doors.Get(second.Source.Key)!.IsJammed || !ReferenceEquals(previous, _skills.LastCheck))
                throw new InvalidDataException("Jammed HUD action consumed another roll.");
            ResetWalker();
            if (_doors.Get(second.Source.Key) is not { IsLocked: true, IsJammed: false }
                || _doors.TempleDoor.Experience != 0 || _skills.LastCheck != null || _reachable!.Count != 540)
                throw new InvalidDataException("Reset did not restore profile, lock and XP.");
            MapLayoutWriter.Verify(_map!);
            GD.Print("LOCKPICK_PREVIEW_OK: neutral profile 20 + 100; camera approach, L roll, HUD relock, +25 XP once, crossing, jam guard, layer persistence, reset and source bytes verified.");
        }
        finally { _checkingNavigation = false; }
    }
}
