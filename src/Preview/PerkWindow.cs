using System;
using System.Collections.Generic;
using System.Linq;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

/// <summary>
/// The character editor's perk choice: every perk the editor can offer below level 99 (perk.msg names and texts), its ranks,
/// minimum level and why it can or cannot be chosen now (CE perkCanAdd), and what it does in this game today.
/// </summary>
public partial class PerkWindow : AcceptDialog
{
    private readonly Tree _perks = new() { Name = "PerkList", Columns = 4, HideRoot = true, ColumnTitlesVisible = true, SelectMode = Tree.SelectModeEnum.Row,
        CustomMinimumSize = new Vector2(820, 300), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
    private readonly Label _summary = new() { Name = "PerkSummary", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(820, 0) };
    private readonly RichTextLabel _detail = new() { Name = "PerkDetail", BbcodeEnabled = false, FitContent = true, CustomMinimumSize = new Vector2(820, 120) };
    private readonly Button _choose = new() { Name = "ChoosePerkAction", Text = "Escolher este perk", Disabled = true };
    // Tag!'s fourth skill (perkDialogHandleTagPerk lists the skills not tagged yet).
    private readonly OptionButton _tagSkill = new() { Name = "TagSkillChoice", Visible = false };
    private Func<int, bool>? _tagged;
    private readonly Dictionary<int, TreeItem> _rows = new();
    private Func<int, int?, PerkChoiceStatus>? _status;
    private Func<int, string>? _name, _description, _stat, _skill;
    private PerkRanks _ranks = PerkRanks.None;
    public Action<int, int?>? ChooseRequested;
    public int? SelectedPerk { get; private set; }
    public string SummaryText => _summary.Text;
    public string DetailText => _detail.Text;
    public bool ChooseEnabled => !_choose.Disabled;
    public int RowCount => _rows.Count;

    public override void _Ready()
    {
        Title = "Perks"; Exclusive = true; Transient = true; GetOkButton().Text = "Fechar";
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); AddChild(scroll);
        scroll.OffsetLeft = 20; scroll.OffsetRight = -20; scroll.OffsetTop = 16; scroll.OffsetBottom = -60;
        var body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; body.AddThemeConstantOverride("separation", 10); scroll.AddChild(body);
        body.AddChild(_summary); body.AddChild(_perks); body.AddChild(_detail); body.AddChild(_tagSkill); body.AddChild(_choose);
        _tagSkill.ItemSelected += _ => ShowSelected();
        _perks.SetColumnTitle(0, "Perk"); _perks.SetColumnTitle(1, "Graus"); _perks.SetColumnTitle(2, "Nível"); _perks.SetColumnTitle(3, "Agora");
        _perks.SetColumnExpand(1, false); _perks.SetColumnCustomMinimumWidth(1, 80);
        _perks.SetColumnExpand(2, false); _perks.SetColumnCustomMinimumWidth(2, 80);
        _perks.SetColumnExpand(3, false); _perks.SetColumnCustomMinimumWidth(3, 260);
        _perks.ItemSelected += ShowSelected;
        _choose.Pressed += () => { if (!_choose.Disabled && SelectedPerk is int perk) ChooseRequested?.Invoke(perk, TagChoice(perk)); };
    }

    public void Refresh(int available, int nextLevel, PerkRanks ranks, Func<int, int?, PerkChoiceStatus> status, Func<int, string> name, Func<int, string> description,
        Func<int, string> stat, Func<int, string> skill, Func<int, bool> tagged)
    {
        _status = status; _name = name; _description = description; _stat = stat; _skill = skill; _ranks = ranks; _tagged = tagged;
        _tagSkill.Clear();
        foreach (int s in Enumerable.Range(0, CharacterSkill.Count).Where(s => !tagged(s)).OrderBy(skill)) { _tagSkill.AddItem(skill(s), s); }
        int? selected = SelectedPerk;
        _summary.Text = (available > 0 ? $"{available} perk(s) para escolher." : nextLevel > 0 ? $"Nenhum perk para escolher agora: o próximo vem no nível {nextLevel}." : "Nenhum perk para escolher.")
            + (ranks.Count > 0 ? $" Escolhidos: {string.Join(", ", ranks.Chosen.Select(c => $"{name(c.Perk)} (nível {c.Level})"))}." : "");
        _perks.Clear(); _rows.Clear(); TreeItem root = _perks.CreateItem(); TreeItem? select = null;
        foreach (PerkDefinition d in PerkCatalog.Rows.Where(r => r.Selectable && r.MinLevel <= CharacterProgression.MaxLevel).OrderBy(r => r.MinLevel).ThenBy(r => name(r.Id)))
        {
            TreeItem row = _perks.CreateItem(root);
            PerkChoiceStatus now = status(d.Id, null);
            row.SetText(0, name(d.Id)); row.SetText(1, $"{ranks.Rank(d.Id)}/{d.MaxRank}"); row.SetText(2, d.MinLevel.ToString()); row.SetText(3, StatusText(now));
            row.SetMetadata(0, d.Id); _rows[d.Id] = row;
            if (d.Id == selected || select == null && now == PerkChoiceStatus.Ready) select = row;
        }
        SelectedPerk = null; _choose.Disabled = true; _detail.Text = "Selecione um perk para ver o que ele faz e o que pede.";
        (select ?? root.GetFirstChild())?.Select(0); ShowSelected();
    }

    // The skill picked for Tag!, null for any other perk.
    private int? TagChoice(int perk) => perk == Perk.Tag && _tagSkill.Selected >= 0 ? _tagSkill.GetItemId(_tagSkill.Selected) : null;

    /// <summary>Picks Tag!'s fourth skill, as a click in the list would.</summary>
    public bool SelectTagSkill(int skill)
    {
        int index = _tagSkill.GetItemIndex(skill);
        if (index < 0) return false;
        _tagSkill.Select(index); ShowSelected(); return true;
    }
    public bool TagSkillVisible => _tagSkill.Visible;

    public bool Select(int perk)
    {
        if (!_rows.TryGetValue(perk, out TreeItem? row)) return false;
        row.Select(0); ShowSelected(); return true;
    }

    private void ShowSelected()
    {
        if (_perks.GetSelected() is not { } row || _status == null) return;
        int perk = row.GetMetadata(0).AsInt32(); SelectedPerk = perk;
        _tagSkill.Visible = perk == Perk.Tag && _tagSkill.ItemCount > 0;
        PerkDefinition d = PerkCatalog.Get(perk); PerkChoiceStatus status = _status(perk, TagChoice(perk));
        _choose.Disabled = status != PerkChoiceStatus.Ready;
        _detail.Text = $"{_name!(perk)} · grau {_ranks.Rank(perk)} de {d.MaxRank}\n{_description!(perk)}\n\nPede: {Requirements(d)}\nNeste jogo: {EffectText(PerkCatalog.Effect(perk))}\nAgora: {StatusText(status)}";
    }

    private string Requirements(PerkDefinition d)
    {
        var parts = new List<string> { $"nível {d.MinLevel}" };
        for (int stat = 0; stat < 7; stat++)
            if (d.Stats[stat] > 0) parts.Add($"{_stat!(stat)} {d.Stats[stat]}+");
            else if (d.Stats[stat] < 0) parts.Add($"{_stat!(stat)} abaixo de {-d.Stats[stat]}");
        string Skill(int param, int value) => (param & PerkCatalog.GlobalFlag) != 0 ? "carma (não acompanhado ainda)"
            : value < 0 ? $"{_skill!(param)} abaixo de {-value}%" : $"{_skill!(param)} {value}%";
        if (d.Param1 != -1 && d.Param2 != -1) parts.Add($"{Skill(d.Param1, d.Value1)} {(d.ParamMode == PerkCatalog.Both ? "e" : "ou")} {Skill(d.Param2, d.Value2)}");
        else if (d.Param1 != -1) parts.Add(Skill(d.Param1, d.Value1));
        return string.Join(", ", parts);
    }

    public static string EffectText(PerkEffect effect) => effect switch
    {
        PerkEffect.Applied => "aplicado",
        PerkEffect.SheetOnly => "vale na ficha, mas nenhum sistema do jogo usa esse número ainda",
        _ => "efeito ainda não modelado; não pode ser escolhido"
    };

    public static string StatusText(PerkChoiceStatus status) => status switch
    {
        PerkChoiceStatus.Ready => "disponível",
        PerkChoiceStatus.NoPerkAvailable => "nenhum perk a escolher agora",
        PerkChoiceStatus.MaxRank => "grau máximo",
        PerkChoiceStatus.Level => "nível insuficiente",
        PerkChoiceStatus.Special => "atributos insuficientes",
        PerkChoiceStatus.Requirement => "perícias insuficientes",
        PerkChoiceStatus.NotModeled => "não modelado ainda",
        PerkChoiceStatus.InCombat => "fora do combate",
        PerkChoiceStatus.SkillChoice => "escolha a perícia",
        _ => "indisponível"
    };
}
