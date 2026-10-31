using System;
using Godot;

namespace FalloutRemake.Preview;

/// <summary>Turn banner for the first fight: round and active combatant, HP/AP/AC, the hover preview and the last events.</summary>
public partial class PreviewHud
{
    private readonly PanelContainer _combatBar = new() { Name = "CombatBar", Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
    private readonly Label _combatTitle = new(), _combatStats = new(), _combatPreview = new(), _combatLog = new();
    private readonly Button _combatEnd = new() { Name = "CombatEndTurnAction", Text = "Encerrar turno [Espaço]" };
    public Action? CombatEndTurnRequested;
    public string CombatPreviewText => _combatPreview.Text;

    private void AddCombatBar(Control root)
    {
        _combatBar.AnchorLeft = .5f; _combatBar.AnchorRight = .5f; _combatBar.OffsetLeft = -330; _combatBar.OffsetRight = 330; _combatBar.OffsetTop = 84;
        _combatBar.AddThemeStyleboxOverride("panel", Box("2a1512f0", 10)); root.AddChild(_combatBar);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 4); _combatBar.AddChild(column);
        var header = new HBoxContainer(); column.AddChild(header);
        _combatTitle.AddThemeColorOverride("font_color", new Color("f2b38a")); _combatTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; header.AddChild(_combatTitle);
        _combatEnd.Pressed += () => CombatEndTurnRequested?.Invoke(); header.AddChild(_combatEnd);
        foreach (Label label in new[] { _combatStats, _combatPreview, _combatLog })
        {
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart; label.AddThemeFontSizeOverride("font_size", 13); column.AddChild(label);
        }
        _combatPreview.AddThemeColorOverride("font_color", new Color("ffe1a3")); _combatLog.Modulate = new Color("c9b9b0");
    }

    public void Combat(bool visible, string title, string stats, bool playerTurn)
    {
        _combatBar.Visible = visible; _combatTitle.Text = title; _combatStats.Text = stats; _combatEnd.Disabled = !playerTurn;
        if (!visible) { _combatPreview.Text = ""; _combatLog.Text = ""; }
        if (!playerTurn) _combatPreview.Text = "A criatura está agindo…";
    }
    public void CombatPreview(string text) => _combatPreview.Text = text;
    public void CombatLog(string text) => _combatLog.Text = text;
}
