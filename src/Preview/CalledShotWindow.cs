using System;
using System.Collections.Generic;
using FalloutRemake.Runtime.Combat;
using Godot;

namespace FalloutRemake.Preview;

/// <summary>
/// CE calledShotSelectHitLocation: the eight locations in the original's two columns (_hit_loc_left: head, eyes, right
/// arm, right leg; _hit_loc_right: torso, groin, left arm, left leg), each with the hit chance _determine_to_hit gives
/// from where the player stands. Choosing one attacks it; closing the window attacks nothing.
/// </summary>
public partial class CalledShotWindow : AcceptDialog
{
    public static readonly HitLocation[] LeftColumn = { HitLocation.Head, HitLocation.Eyes, HitLocation.RightArm, HitLocation.RightLeg },
        RightColumn = { HitLocation.Torso, HitLocation.Groin, HitLocation.LeftArm, HitLocation.LeftLeg };
    private readonly Dictionary<HitLocation, Button> _buttons = new();
    private readonly Label _summary = new() { Name = "CalledShotSummary", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(520, 0) };
    public Action<HitLocation>? Chosen;
    public string SummaryText => _summary.Text;

    public static string LocationName(HitLocation location) => location switch
    {
        HitLocation.Head => "Cabeça", HitLocation.Eyes => "Olhos", HitLocation.RightArm => "Braço direito", HitLocation.RightLeg => "Perna direita",
        HitLocation.Torso => "Tronco", HitLocation.Groin => "Virilha", HitLocation.LeftArm => "Braço esquerdo", HitLocation.LeftLeg => "Perna esquerda",
        _ => "Sem mira"
    };

    public CalledShotWindow()
    {
        foreach (HitLocation location in LeftColumn) _buttons[location] = NewButton(location);
        foreach (HitLocation location in RightColumn) _buttons[location] = NewButton(location);
    }

    private Button NewButton(HitLocation location)
    {
        var button = new Button { Name = "CalledShot" + location, CustomMinimumSize = new Vector2(240, 36), Disabled = true };
        button.Pressed += () => { if (!button.Disabled) Chosen?.Invoke(location); };
        return button;
    }

    public override void _Ready()
    {
        Title = "Mirar"; Exclusive = true; Transient = true; MinSize = new Vector2I(560, 320); GetOkButton().Text = "Cancelar";
        var body = new VBoxContainer(); body.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); AddChild(body);
        body.OffsetLeft = 20; body.OffsetRight = -20; body.OffsetTop = 16; body.OffsetBottom = -60; body.AddThemeConstantOverride("separation", 10);
        body.AddChild(_summary);
        var columns = new HBoxContainer(); columns.AddThemeConstantOverride("separation", 16); body.AddChild(columns);
        foreach (HitLocation[] column in new[] { LeftColumn, RightColumn })
        {
            var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; list.AddThemeConstantOverride("separation", 6); columns.AddChild(list);
            foreach (HitLocation location in column) list.AddChild(_buttons[location]);
        }
    }

    /// <summary>Each location's chance and AP from the fight's own preview; a refused one stays disabled and says why.</summary>
    public void Refresh(string target, string blow, Func<HitLocation, CombatActionPreview> preview, Func<CombatActionStatus, string> refusal)
    {
        _summary.Text = $"{blow} → {target}. Mirar custa 1 AP a mais; olhos e cabeça são mais difíceis, mas os críticos dependem da parte atingida.";
        foreach (var (location, button) in _buttons)
        {
            CombatActionPreview result = preview(location);
            button.Text = $"{LocationName(location)} · {(result.ToHit is { } hit ? $"{hit.Chance}%" : "—")} · {result.ActionPointCost} AP";
            button.Disabled = result.Status != CombatActionStatus.Ready;
            button.TooltipText = result.Status == CombatActionStatus.Ready ? $"Dano {result.Damage?.MinDamage}–{result.Damage?.MaxDamage}" : refusal(result.Status);
        }
    }

    public Button ButtonFor(HitLocation location) => _buttons[location];
}
