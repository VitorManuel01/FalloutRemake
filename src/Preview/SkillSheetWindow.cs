using System;
using System.Collections.Generic;
using System.Linq;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

/// <summary>
/// The character sheet outside creation: spending level-up skill points with the original editor's plus and minus
/// (<see cref="SkillSpending"/>). "Pronto" applies the spending to the world; "Cancelar" drops it, as the editor's cancel.
/// </summary>
public partial class SkillSheetWindow : AcceptDialog
{
    private readonly Label _header = new() { Name = "SkillSheetHeader", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _points = new() { Name = "SkillPoints" };
    private readonly Label _message = new() { Name = "SkillMessage", AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color("d8b46a") };
    private readonly Label[] _values = Enumerable.Range(0, CharacterSkill.Count).Select(i => new Label { Name = $"SkillValue{i}", CustomMinimumSize = new Vector2(230, 0) }).ToArray();
    private readonly Button[] _minus = Enumerable.Range(0, CharacterSkill.Count).Select(i => new Button { Name = $"SkillMinus{i}", Text = "−", CustomMinimumSize = new Vector2(30, 0) }).ToArray();
    private readonly Button[] _plus = Enumerable.Range(0, CharacterSkill.Count).Select(i => new Button { Name = $"SkillPlus{i}", Text = "+", CustomMinimumSize = new Vector2(30, 0) }).ToArray();
    private SkillSpending? _spending;
    private CharacterTexts? _texts;
    public Action<SkillSpending>? DoneRequested;
    public SkillSpending Spending => _spending ?? throw new InvalidOperationException("The sheet is not configured.");
    public string MessageText => _message.Text;
    public string SkillText(int skill) => _values[skill].Text;

    public override void _Ready()
    {
        Title = "Ficha de personagem"; Exclusive = true; Transient = true; GetOkButton().Text = "Pronto";
        AddCancelButton("Cancelar");
        var scroll = new ScrollContainer { Name = "SheetScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); scroll.OffsetLeft = 20; scroll.OffsetRight = -20; scroll.OffsetTop = 20; scroll.OffsetBottom = -60;
        AddChild(scroll);
        var body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; body.AddThemeConstantOverride("separation", 10); scroll.AddChild(body);
        body.AddChild(_header); body.AddChild(_points);
        body.AddChild(new Label { Text = "Cada ponto custa 1 até 100%, 2 até 125%, 3 até 150%, 4 até 175%, 5 até 200% e 6 acima. Perícia marcada (*) sobe 2% por ponto. "
            + "O menos só desfaz o que foi comprado desde que a ficha abriu.", AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color("8fa69a") });
        var grid = new GridContainer { Columns = 6 }; grid.AddThemeConstantOverride("h_separation", 8); body.AddChild(grid);
        for (int i = 0; i < CharacterSkill.Count; i++)
        {
            int skill = i;
            _minus[i].Pressed += () => Subtract(skill); _plus[i].Pressed += () => Add(skill);
            grid.AddChild(_values[i]); grid.AddChild(_minus[i]); grid.AddChild(_plus[i]);
        }
        body.AddChild(_message);
        Confirmed += () => { if (_spending != null) DoneRequested?.Invoke(_spending); };
    }

    public void Configure(SkillSpending spending, CharacterTexts texts, string header)
    {
        _spending = spending; _texts = texts; _header.Text = header; _message.Text = "";
        for (int i = 0; i < CharacterSkill.Count; i++) _values[i].TooltipText = texts.SkillDescription(i);
        Refresh();
    }

    public SkillSpendStatus Add(int skill)
    {
        SkillSpendStatus status = Spending.Add(skill);
        _message.Text = status switch
        {
            SkillSpendStatus.NoPoints => "Não há pontos de perícia para gastar.",
            SkillSpendStatus.AtMaximum => $"{_texts!.Skill(skill)} já está no máximo (300%).",
            SkillSpendStatus.NotEnoughPoints => $"O próximo ponto de {_texts!.Skill(skill)} custa {SkillSpendingRules.Cost(Spending.Sheet.Skill(skill))}; restam {Spending.Unspent}.",
            _ => ""
        };
        Refresh(); return status;
    }

    public SkillSpendStatus Subtract(int skill)
    {
        SkillSpendStatus status = Spending.Subtract(skill);
        _message.Text = status == SkillSpendStatus.AtMinimum ? $"{_texts!.Skill(skill)} está no valor com que a ficha abriu; não dá para baixar mais." : "";
        Refresh(); return status;
    }

    private void Refresh()
    {
        SkillSpending spending = Spending; CharacterTexts t = _texts!;
        _points.Text = $"Pontos de perícia: {spending.Unspent}";
        for (int i = 0; i < CharacterSkill.Count; i++)
        {
            int value = spending.Sheet.Skill(i);
            _values[i].Text = $"{t.Skill(i)}{(spending.Sheet.IsTagged(i) ? "*" : "")} {value}%" + (value < PlayerCharacter.MaxSkill ? $"  (custa {SkillSpendingRules.Cost(value)})" : "");
            // As in the editor, plus without points stays clickable and explains; only the 300% cap and minus at the opening value grey out.
            _plus[i].Disabled = spending.CanAdd(i) == SkillSpendStatus.AtMaximum;
            _minus[i].Disabled = spending.CanSubtract(i) != SkillSpendStatus.Ready;
        }
    }
}
