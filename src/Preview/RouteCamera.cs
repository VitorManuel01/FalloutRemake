using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private sealed record RouteFramePlan(HexNavigation Navigation, int Start, int[] Tiles, string? TargetKey);
    private RouteFramePlan? _framePreview;
    private int[] _committedRoute = Array.Empty<int>();
    private Vector2? _pinnedHoverScreen;
    private bool CanFrameRoute => _walkMode && !InspectorOpen && _walker != null &&
        (_walker.Moving ? _committedRoute.Length > 1 : _framePreview != null
            && ReferenceEquals(_framePreview.Navigation, _navigation) && _framePreview.Start == _walker.CurrentHex);
    private void RefreshFrameRouteAction() => _hud.FrameRouteAction(CanFrameRoute);
    private void RememberFramePreview()
    {
        if (_hoverPath.Length > 1)
            _framePreview = new RouteFramePlan(_navigation!, _walker!.CurrentHex, _hoverPath.ToArray(), _hoverDoorKey ?? _hoverContainerKey);
        RefreshFrameRouteAction();
    }
    private Vector3[] FramingPoints(RouteFramePlan plan)
    {
        var points = new List<Vector3>();
        foreach (int hex in plan.Tiles)
        {
            Vector3 p = Point(FalloutGrid.HexCenter(hex) - _layout!.Origin, 0);
            points.Add(p); points.Add(p + Vector3.Up * 1.3f);
        }
        if (_walkerNode != null) { points.Add(_walkerNode.Position); points.Add(_walkerNode.Position + Vector3.Up * 1.3f); }
        PreviewObject? target = plan.TargetKey == null ? null : _layout!.Objects.FirstOrDefault(o => o.Source.Key == plan.TargetKey);
        if (target != null) { Vector3 p = Point(target.Position, 0); points.Add(p); points.Add(p + Vector3.Up * 1.3f); }
        return points.ToArray();
    }
    private bool FramingPointsVisible(Vector3[] points)
    {
        Rect2 safe = _hud.CameraSafeRect.Grow(.5f);
        return points.All(p => !_camera.IsPositionBehind(p) && safe.HasPoint(_camera.UnprojectPosition(p)));
    }
    private void FrameRoute()
    {
        if (!CanFrameRoute) { NavigationStatus("Aponte um destino ou inicie um percurso para enquadrar"); return; }
        bool moving = _walker!.Moving;
        RouteFramePlan plan = moving ? new RouteFramePlan(_navigation!, _walker.CurrentHex, _committedRoute, null) : _framePreview!;
        Vector3[] points = FramingPoints(plan);
        Vector3 oldFocus = _focus; float oldSize = _camera.Size;
        try
        {
            Vector3 right = _camera.Basis.X, up = _camera.Basis.Y;
            Projection projection = _camera.GetCameraProjection();
            double widthPerSize = 2 / (Math.Abs(projection.X.X) * _camera.Size);
            double heightPerSize = 2 / (Math.Abs(projection.Y.Y) * _camera.Size);
            Rect2 safe = _hud.CameraSafeRect; Vector2 viewport = GetViewport().GetVisibleRect().Size;
            OrthoFrame frame = OrthoFraming.Fit(points.Select(p => new FramePoint(p.Dot(right), p.Dot(up))).ToArray(),
                viewport.X, viewport.Y, new FrameRect(safe.Position.X, safe.Position.Y, safe.Size.X, safe.Size.Y), widthPerSize, heightPerSize);
            Vector3 groundUp = new(up.X, 0, up.Z);
            if (groundUp.LengthSquared() < .0001f) throw new InvalidOperationException("Camera angle cannot be framed from ground focus.");
            _camera.Size = (float)frame.Size;
            // UpdateCamera adds its sidebar offset along right; remove it from the solved ground focus.
            _focus = right * ((float)frame.TargetX - _camera.Size * 145 / viewport.Y)
                + groundUp * ((float)frame.TargetY / groundUp.LengthSquared());
            UpdateCamera();
            if (!FramingPointsVisible(points)) throw new InvalidOperationException("Route did not fit inside the unobscured viewport.");
            SetCameraFollow(false);
            if (!moving)
            {
                if (plan.TargetKey != null)
                {
                    PreviewObject target = _layout!.Objects.Single(o => o.Source.Key == plan.TargetKey);
                    if (target.Kind == PreviewKind.Door) PreviewDoorRoute(target); else PreviewContainerRoute(target);
                }
                else PreviewGroundRoute(plan.Tiles[^1]);
                _pinnedHoverScreen = GetViewport().GetMousePosition();
            }
            _hud.RouteHint($"Percurso enquadrado · {plan.Tiles.Length - 1} passos · C para seguir o personagem");
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            _focus = oldFocus; _camera.Size = oldSize; UpdateCamera();
            NavigationStatus("Não foi possível enquadrar este percurso no limite da câmera");
            GD.Print("ROUTE_FRAME_UNAVAILABLE: " + error.Message);
        }
    }
    private async Task CheckRouteCamera()
    {
        _checkingNavigation = true;
        try
        {
            ResetWalker(); ToggleWalkMode(); SetNavigationGrid(false); SetOriginalFloor(true);
            var location = _campaign!.Location; var doors = _doors!.Capture();
            var button = _hud.FindChild("FrameRouteAction", true, false) as Button ?? throw new InvalidDataException("Route frame button missing.");
            Vector3 initialFocus = _focus; float initialSize = _camera.Size;
            using var key = new InputEventKey { Pressed = true, PhysicalKeycode = Key.T };
            _UnhandledInput(key);
            if (!button.Disabled || _focus != initialFocus || _camera.Size != initialSize) throw new InvalidDataException("Empty route changed camera or enabled framing.");
            PreviewGroundRoute(24508); RouteFramePlan plan = _framePreview!;
            _UnhandledInput(key);
            if (_followActor || !FramingPointsVisible(FramingPoints(plan)) || _campaign.Location != location || _walker!.Moving)
                throw new InvalidDataException("Preview framing clipped the route or changed actor state.");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "route-frame.png"));
            // Moving onto a HUD control hides the hover mesh but keeps the valid plan available to the button.
            _pinnedHoverScreen = null; RouteHoverAtScreen(new Vector2(20, 20));
            if (button.Disabled || _hoverTarget!.Visible) throw new InvalidDataException("HUD movement discarded the frame plan.");
            button.EmitSignal(Button.SignalName.Pressed);
            if (!_hoverTarget.Visible || !FramingPointsVisible(FramingPoints(plan))) throw new InvalidDataException("Frame button failed to restore and fit the preview.");
            float yaw = _yaw;
            foreach (float angle in new[] { yaw - Mathf.Pi / 3, yaw + Mathf.Pi / 2 })
            { _yaw = angle; UpdateCamera(); FrameRoute(); if (!FramingPointsVisible(FramingPoints(plan))) throw new InvalidDataException("Rotated route framing clipped points."); }
            _yaw = yaw; UpdateCamera();
            PreviewObject door = _layout!.Objects.Single(o => o.Source.Offset == 244040);
            PreviewDoorRoute(door); FrameRoute();
            if (_framePreview?.TargetKey != door.Source.Key || !FramingPointsVisible(FramingPoints(_framePreview)))
                throw new InvalidDataException("Door framing lost the approach or clipped the target door.");
            RequestWalk(24508); _walker!.Advance(.05); UpdateWalkerPosition();
            var actorPosition = _walker.Position; double progress = _walker.Progress;
            _UnhandledInput(key);
            if (!_walker.Moving || _walker.Position != actorPosition || _walker.Progress != progress || button.Disabled
                || !FramingPointsVisible(FramingPoints(new RouteFramePlan(_navigation!, _walker.CurrentHex, _committedRoute, null))))
                throw new InvalidDataException("Framing interrupted or clipped the current route.");
            StopWalking(); _checkingNavigation = false; AdvanceWalker(100); _checkingNavigation = true;
            if (!button.Disabled) throw new InvalidDataException("Completed route retained a stale frame action.");
            PreviewGroundRoute(24508); ChangeElevation(1);
            if (!button.Disabled || _framePreview != null) throw new InvalidDataException("Inspection retained a frame plan from another elevation.");
            if (!_doors!.Capture().Doors.SequenceEqual(doors.Doors)) throw new InvalidDataException("Camera framing changed doors.");
            MapLayoutWriter.Verify(_map!); SetOriginalFloor(false); SetNavigationGrid(true); ResetWalker();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print("ROUTE_CAMERA_OK: T/button, full route and actor within unobscured viewport, preview restoration, rotations, target door, moving route/progress, empty/completed/inspection guards, doors and source bytes verified.");
        }
        finally { _checkingNavigation = false; }
    }
}
