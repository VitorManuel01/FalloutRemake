using System;
using System.Collections.Generic;
using System.Linq;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;
using Godot;

namespace FalloutRemake.Preview;

/// <summary>Original names and descriptions of stats, traits and skills (stat.msg, trait.msg, skill.msg: name 100 + id, text 200 + id).</summary>
public sealed record CharacterTexts(Func<int, string> Stat, Func<int, string> Trait, Func<int, string> TraitDescription,
    Func<int, string> Skill, Func<int, string> SkillDescription)
{
    /// <summary>perk.msg: name 101 + perk, text 1101 + perk.</summary>
    public Func<int, string> Perk { get; init; } = i => $"Perk {i}";
    public Func<int, string> PerkDescription { get; init; } = _ => "";
}

/// <summary>A premade character with its original biography (premade/*.bio).</summary>
public sealed record PremadeChoice(PlayerCharacter Character, string Biography);

/// <summary>
/// New game: the three premade characters of the original (Narg, Mingan, Chitsa), a character built with the editor
/// rules (40 SPECIAL points, up to 2 traits, exactly 3 tags) or the neutral test profile of earlier milestones.
/// </summary>
public partial class CharacterCreationWindow : AcceptDialog
{
    private readonly ItemList _choices = new() { Name = "CharacterChoices", CustomMinimumSize = new Vector2(220, 0), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
    private readonly RichTextLabel _sheet = new() { Name = "CharacterSheet", BbcodeEnabled = false, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 220), SelectionEnabled = true };
    private readonly VBoxContainer _editor = new() { Name = "CharacterEditor", Visible = false };
    private readonly LineEdit _name = new() { Name = "CharacterName", PlaceholderText = "Nome", MaxLength = PlayerCharacter.MaxNameLength, CustomMinimumSize = new Vector2(200, 0) };
    private readonly SpinBox _age = new() { Name = "CharacterAge", MinValue = PlayerCharacter.MinAge, MaxValue = PlayerCharacter.MaxAge, Value = 25 };
    private readonly OptionButton _gender = new() { Name = "CharacterGender" };
    private readonly SpinBox[] _special = Enumerable.Range(0, 7).Select(i => new SpinBox { Name = $"Special{i}", MinValue = 1, MaxValue = 10, Value = 5 }).ToArray();
    private readonly Label _remaining = new() { Name = "SpecialRemaining" };
    private readonly CheckBox[] _traits = Enumerable.Range(0, 16).Select(i => new CheckBox { Name = $"Trait{i}" }).ToArray();
    private readonly CheckBox[] _tags = Enumerable.Range(0, CharacterSkill.Count).Select(i => new CheckBox { Name = $"Tag{i}" }).ToArray();
    private readonly Label _problem = new() { Name = "CharacterProblem", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private IReadOnlyList<PremadeChoice> _premades = Array.Empty<PremadeChoice>();
    private CharacterTexts? _texts;
    private bool _updating;
    public Action<PlayerCharacter>? StartRequested;
    public PlayerCharacter? Selected { get; private set; }
    public string SheetText => _sheet.Text;
    public string ProblemText => _problem.Text;
    public bool CanStart => !GetOkButton().Disabled;
    public int CreateIndex => _premades.Count;
    public int NeutralIndex => _premades.Count + 1;

    public override void _Ready()
    {
        Title = "Escolha do personagem"; Exclusive = true; Transient = true; GetOkButton().Text = "Começar";
        var scroll = new ScrollContainer { Name = "DialogScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); scroll.OffsetLeft = 20; scroll.OffsetRight = -20; scroll.OffsetTop = 20; scroll.OffsetBottom = -60;
        AddChild(scroll);
        var body = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 16); scroll.AddChild(body);
        var left = new VBoxContainer(); body.AddChild(left);
        left.AddChild(new Label { Text = "Personagens do Fallout 2" }); left.AddChild(_choices);
        left.AddChild(new Label { Text = "Começar reinicia o Templo\ncom o personagem escolhido.", Modulate = new Color("8fa69a") });
        var right = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; right.AddThemeConstantOverride("separation", 10); body.AddChild(right);
        right.AddChild(_editor); right.AddChild(_problem); right.AddChild(_sheet);

        var identity = new HFlowContainer(); _editor.AddChild(identity);
        identity.AddChild(new Label { Text = "Nome" }); identity.AddChild(_name);
        identity.AddChild(new Label { Text = "Idade" }); identity.AddChild(_age);
        _gender.AddItem("Masculino", 0); _gender.AddItem("Feminino", 1); identity.AddChild(_gender);
        var special = new GridContainer { Columns = 8 }; _editor.AddChild(special);
        for (int i = 0; i < 7; i++) { special.AddChild(new Label { Name = $"SpecialName{i}" }); special.AddChild(_special[i]); if (i % 4 == 3) special.AddChild(new Control()); }
        _editor.AddChild(_remaining);
        _editor.AddChild(new Label { Text = "Traits (até 2)", Modulate = new Color("c2a66d") });
        var traits = new GridContainer { Columns = 4 }; _editor.AddChild(traits); foreach (CheckBox box in _traits) traits.AddChild(box);
        _editor.AddChild(new Label { Text = "Perícias marcadas (exatamente 3): +20% e pontos em dobro", Modulate = new Color("c2a66d") });
        var tags = new GridContainer { Columns = 3 }; _editor.AddChild(tags); foreach (CheckBox box in _tags) tags.AddChild(box);

        _choices.ItemSelected += index => Select((int)index);
        _name.TextChanged += _ => Changed(); _age.ValueChanged += _ => Changed(); _gender.ItemSelected += _ => Changed();
        foreach (SpinBox box in _special) box.ValueChanged += _ => Changed();
        foreach (CheckBox box in _traits.Concat(_tags)) box.Toggled += _ => Changed();
        Confirmed += () => { if (Selected != null && CanStart) StartRequested?.Invoke(Selected); };
    }

    public void Configure(IReadOnlyList<PremadeChoice> premades, CharacterTexts texts, PlayerCharacter current)
    {
        _premades = premades; _texts = texts; _choices.Clear();
        foreach (PremadeChoice choice in premades) _choices.AddItem(choice.Character.Name);
        _choices.AddItem("Criar personagem"); _choices.AddItem("Perfil de teste (neutro)");
        for (int i = 0; i < 7; i++) ((Label)_editor.FindChild($"SpecialName{i}", true, false)).Text = texts.Stat(i);
        for (int i = 0; i < _traits.Length; i++) { _traits[i].Text = texts.Trait(i); _traits[i].TooltipText = texts.TraitDescription(i); }
        for (int i = 0; i < _tags.Length; i++) _tags[i].TooltipText = texts.SkillDescription(i);
        int index = current.Kind switch
        {
            CharacterKind.Premade => Math.Max(0, premades.ToList().FindIndex(p => p.Character.Premade == current.Premade)),
            CharacterKind.Created => CreateIndex, _ => NeutralIndex
        };
        if (current.Kind == CharacterKind.Created) Load(current);
        Select(index);
    }

    public void Select(int index)
    {
        if (index < 0 || index > NeutralIndex) return;
        _choices.Select(index); _editor.Visible = index == CreateIndex;
        if (index < _premades.Count) Show(_premades[index].Character, _premades[index].Biography, null);
        else if (index == NeutralIndex) Show(PlayerCharacter.Neutral, "Perfil usado pelos marcos anteriores e pelos testes automáticos: SPECIAL 5, sem traits nem perícias marcadas. A conversa com Cameron continua liberada para ele.", null);
        else Changed();
    }

    // Editor helpers for automated checks; they act through the same controls a person uses.
    // Setting LineEdit.Text from code emits no text_changed, unlike typing; recompute as typing would.
    public void SetCharacterName(string name) { _name.Text = name; Changed(); }
    public void SetSpecial(int stat, int value) => _special[stat].Value = value;
    public void SetTrait(CharacterTrait trait, bool on) => _traits[(int)trait].ButtonPressed = on;
    public void SetTag(int skill, bool on) => _tags[skill].ButtonPressed = on;
    public bool TraitEnabled(CharacterTrait trait) => !_traits[(int)trait].Disabled;
    public bool TagEnabled(int skill) => !_tags[skill].Disabled;

    private void Load(PlayerCharacter character)
    {
        _updating = true;
        _name.Text = character.Name; _age.Value = character.Age; _gender.Select(character.Gender);
        int[] stats = character.Base.ToArray(); for (int i = 0; i < 7; i++) _special[i].Value = stats[i];
        for (int i = 0; i < _traits.Length; i++) _traits[i].ButtonPressed = character.Has((CharacterTrait)i);
        for (int i = 0; i < _tags.Length; i++) _tags[i].ButtonPressed = character.IsTagged(i);
        _updating = false;
    }

    private void Changed()
    {
        if (_updating || _texts == null || !_editor.Visible) return;
        var special = Special.From(_special.Select(s => (int)s.Value).ToArray());
        CharacterTrait[] traits = Enumerable.Range(0, _traits.Length).Where(i => _traits[i].ButtonPressed).Select(i => (CharacterTrait)i).ToArray();
        int[] tags = Enumerable.Range(0, _tags.Length).Where(i => _tags[i].ButtonPressed).ToArray();
        int left = PlayerCharacter.SpecialTotal - special.ToArray().Sum();
        _remaining.Text = left >= 0 ? $"Pontos de atributo a distribuir: {left}" : $"Pontos de atributo além do total: {-left}";
        // Choices beyond the limits are greyed out, as the editor refuses them.
        for (int i = 0; i < _traits.Length; i++) _traits[i].Disabled = !_traits[i].ButtonPressed && traits.Length >= PlayerCharacter.MaxTraits;
        for (int i = 0; i < _tags.Length; i++) _tags[i].Disabled = !_tags[i].ButtonPressed && tags.Length >= PlayerCharacter.Tags;
        string? problem = PlayerCharacter.CreationProblem(_name.Text, special, traits, tags, (int)_age.Value, _gender.Selected);
        // The sheet previews the values even before the character is complete; each tag box shows its value if tagged.
        var draft = PlayerCharacter.Draft(_name.Text, special, traits, tags, (int)_age.Value, _gender.Selected);
        for (int i = 0; i < _tags.Length; i++)
        {
            int value = tags.Contains(i) || tags.Length >= PlayerCharacter.Tags ? draft.Skill(i)
                : PlayerCharacter.Draft(_name.Text, special, traits, tags.Append(i), (int)_age.Value, _gender.Selected).Skill(i);
            _tags[i].Text = $"{_texts.Skill(i)} {value}%";
        }
        Show(problem == null ? PlayerCharacter.Create(_name.Text, special, traits, tags, (int)_age.Value, _gender.Selected) : draft, "", problem);
    }
    private void Show(PlayerCharacter character, string biography, string? problem)
    {
        CharacterTexts t = _texts!;
        Selected = problem == null ? character : null;
        GetOkButton().Disabled = problem != null;
        _problem.Text = problem == null ? "" : $"Para começar falta: {problem}.";
        _problem.Visible = problem != null;
        int[] stats = character.Base.ToArray(), effective = character.Effective.ToArray();
        string Stat(int i) => $"{t.Stat(i)} {stats[i]}{(effective[i] != stats[i] ? $" → {effective[i]}" : "")}";
        var lines = new List<string>
        {
            $"{character.Name} · {character.Age} anos · {(character.Gender == 0 ? "masculino" : "feminino")}",
            string.Join("  ", Enumerable.Range(0, 7).Select(Stat)),
            "Traits: " + (character.Traits.Count == 0 ? "nenhum" : string.Join(", ", character.Traits.Select(x => t.Trait((int)x)))),
            "Perícias marcadas: " + (character.TaggedSkills.Count == 0 ? "nenhuma" : string.Join(", ", character.TaggedSkills.Select(t.Skill))),
            $"HP {character.BaseMaxHitPoints} · AP {character.MaxActionPoints} · AC {character.ArmorClass} · Melee Damage {character.MeleeDamage} · Sequence {character.Sequence}"
                + $" · Crítico {character.CriticalChance}% · Carga {character.CarryWeight} lb · Cura {character.HealingRate}",
            $"Por nível: +{character.HitPointsPerLevel} HP, +{character.SkillPointsPerLevel} pontos de perícia, perk a cada {character.PerkInterval} níveis",
            "",
            string.Join("   ", Enumerable.Range(0, CharacterSkill.Count).Select(i => $"{t.Skill(i)}{(character.IsTagged(i) ? "*" : "")} {character.Skill(i)}%"))
        };
        if (biography.Length > 0) { lines.Add(""); lines.Add(biography.Trim()); }
        _sheet.Text = string.Join("\n", lines);
    }
}
