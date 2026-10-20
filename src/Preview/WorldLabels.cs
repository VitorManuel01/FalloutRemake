using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private readonly List<Label3D> _worldLabels = new();

    private void RegisterWorldLabel(Label3D label)
    {
        label.OutlineSize = 10;
        label.OutlineModulate = new Color("111b20");
        _worldLabels.Add(label);
    }

    // Orthographic zoom changes world units per pixel. Keep captions at 14 screen pixels,
    // clip their entire footprint before the HUD, and avoid overlapping nearby captions.
    private void RefreshWorldLabels()
    {
        if (_camera == null || _hud == null) return;
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        var occupied = new List<Rect2>();
        _worldLabels.RemoveAll(label => !GodotObject.IsInstanceValid(label) || label.IsQueuedForDeletion());
        foreach (Label3D label in _worldLabels.OrderBy(l => l.GetParent() == (_selected == null ? null : _bodies.GetValueOrDefault(_selected.Source.Key)) ? 0 : 1))
        {
            label.PixelSize = _camera.Size / Math.Max(1, viewport.Y) * 14 / label.FontSize;
            Vector2 center = _camera.UnprojectPosition(label.GlobalPosition);
            string[] lines = label.Text.Split('\n');
            Vector2 size = new(lines.Max(l => l.Length) * 9 + 12, lines.Length * 20 + 8);
            Rect2 rect = new(center - size / 2, size);
            bool selected = _selected != null && label.GetParent() == _bodies.GetValueOrDefault(_selected.Source.Key);
            bool visible = !_camera.IsPositionBehind(label.GlobalPosition) && (_camera.Size <= 64 || selected)
                && rect.Position.X >= 24 && rect.Position.Y >= 90 && rect.End.X <= viewport.X - 312 && rect.End.Y <= viewport.Y - 64
                && !new[] { rect.Position, new Vector2(rect.End.X, rect.Position.Y), rect.End, new Vector2(rect.Position.X, rect.End.Y) }.Any(_hud.MouseOverPanelAt)
                && !occupied.Any(r => r.Intersects(rect));
            label.Visible = visible;
            if (visible && label.GetParent<Node3D>().IsVisibleInTree()) occupied.Add(rect);
        }
    }
}
