using System;
using System.Collections.Generic;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

/// <summary>
/// First Aid and Doctor on the player (CE skill_use from the Skilldex, or a kit used on oneself): each skill's chance, the
/// successes left today and, for Doctor, the limbs it would treat; a carried kit may be chosen for its bonus.
/// </summary>
public partial class MedicalWindow : AcceptDialog
{
    private readonly Label _status = new() { Name = "MedicalStatus", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(560, 0) };
    private readonly Label _summary = new() { Name = "MedicalSummary", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(560, 0) };
    private readonly OptionButton _kits = new() { Name = "MedicalKit" };
    private readonly Button _firstAid = new() { Name = "UseFirstAid", Text = "Primeiros socorros (30 min)" };
    private readonly Button _doctor = new() { Name = "UseDoctor", Text = "Médico (1 h, mais 1 h por membro)" };
    private readonly List<CarriedItem?> _kitItems = new();
    public Action<int, CarriedItem?>? UseRequested;
    public string StatusText => _status.Text;
    public string SummaryText => _summary.Text;
    public bool FirstAidEnabled => !_firstAid.Disabled;

    public override void _Ready()
    {
        Title = "Primeiros socorros e Médico"; Exclusive = true; Transient = true; GetOkButton().Text = "Fechar";
        var body = new VBoxContainer(); body.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); AddChild(body);
        body.OffsetLeft = 20; body.OffsetRight = -20; body.OffsetTop = 16; body.OffsetBottom = -60; body.AddThemeConstantOverride("separation", 10);
        body.AddChild(new Label { Text = "Cada perícia tem três sucessos por dia. Primeiros socorros só cura HP; Médico também trata membros aleijados e olhos feridos.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(560, 0), Modulate = new Color("e8c78d") });
        body.AddChild(_summary); body.AddChild(_kits);
        var buttons = new HBoxContainer(); buttons.AddThemeConstantOverride("separation", 8); body.AddChild(buttons);
        buttons.AddChild(_firstAid); buttons.AddChild(_doctor); body.AddChild(_status);
        _firstAid.Pressed += () => UseRequested?.Invoke(CharacterSkill.FirstAid, SelectedKit(CharacterSkill.FirstAid));
        _doctor.Pressed += () => UseRequested?.Invoke(CharacterSkill.Doctor, SelectedKit(CharacterSkill.Doctor));
    }

    private CarriedItem? SelectedKit(int skill) => _kits.Selected > 0 && _kitItems[_kits.Selected] is { } kit
        && MedicalKits.For(kit.Source.Header.Pid)?.Skill == skill ? kit : null;

    /// <param name="kits">The carried kits with their names; none on the Temple's or Arroyo's maps.</param>
    public void Refresh(string summary, MedicalStatus firstAid, MedicalStatus doctor, IReadOnlyList<(CarriedItem Item, string Name)> kits, string? message = null)
    {
        _summary.Text = summary;
        _firstAid.Disabled = firstAid != MedicalStatus.Ready; _doctor.Disabled = doctor != MedicalStatus.Ready;
        int selected = Math.Max(0, _kits.Selected);
        _kits.Clear(); _kitItems.Clear();
        _kits.AddItem(kits.Count == 0 ? "Sem kit (nenhum na bolsa)" : "Sem kit"); _kitItems.Add(null);
        foreach (var (item, name) in kits) { _kits.AddItem(name); _kitItems.Add(item); }
        _kits.Select(Math.Min(selected, _kits.ItemCount - 1));
        _status.Text = message ?? (firstAid == MedicalStatus.Ready || doctor == MedicalStatus.Ready ? "Escolha a perícia." : Hint(firstAid == doctor ? firstAid : MedicalStatus.Tired));
    }

    public static string Hint(MedicalStatus status) => status switch
    {
        MedicalStatus.Ready => "Pronto.",
        MedicalStatus.InCombat => "Não dá para fazer isso em combate.",
        MedicalStatus.Busy => "Pare de andar primeiro.",
        MedicalStatus.Tired => "Você já usou essa perícia três vezes hoje. Espere um pouco.",
        MedicalStatus.NoItem => "O kit não está mais na bolsa.",
        _ => "Perícia inválida."
    };
}
