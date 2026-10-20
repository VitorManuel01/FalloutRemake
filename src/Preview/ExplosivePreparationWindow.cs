using System;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

/// <summary>A draft owns no item. Only the preview's revalidated commit can place one.</summary>
public partial class ExplosivePreparationWindow : ConfirmationDialog
{
    private readonly SpinBox _seconds = new() { Name = "ExplosiveSeconds", MinValue = ExplosiveTimer.MinimumSeconds,
        MaxValue = ExplosiveTimer.MaximumSeconds, Step = ExplosiveTimer.StepSeconds, Value = ExplosiveTimer.DefaultSeconds,
        Rounded = true, CustomMinimumSize = new(130, 0), Suffix = "s" };
    private readonly Label _status = new() { Name = "ExplosivePreparationStatus", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    public Func<int, ExplosiveProposal>? Propose;
    public Action<ExplosiveUsePlan?>? SubmitRequested;
    public ExplosiveUsePlan? Plan { get; private set; }
    public string StatusText => _status.Text;
    public override void _Ready()
    {
        Title = "Posicionar explosivo · ensaio"; Exclusive = true; Transient = true; DialogHideOnOk = false; MinSize = new(520, 280);
        GetOkButton().Text = "Posicionar 1 unidade"; GetCancelButton().Text = "Cancelar";
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); AddChild(scroll);
        scroll.OffsetLeft = 20; scroll.OffsetRight = -20; scroll.OffsetTop = 16; scroll.OffsetBottom = -60;
        var body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 14); scroll.AddChild(body);
        body.AddChild(new Label { Text = "Uma unidade será deixada no chão, junto do personagem.", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        body.AddChild(new Label { Text = "Ensaio controlado: a preparação sempre tem sucesso. Depois, use Tempo · ensaio para avançar o prazo manualmente. Posicionar não provoca uma explosão.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color("e8c78d") });
        var row = new HBoxContainer(); row.AddChild(new Label { Text = "Temporizador (10–180 s):" }); row.AddChild(_seconds); body.AddChild(row);
        body.AddChild(_status); _seconds.ValueChanged += _ => RefreshPlan();
        Confirmed += () => { _seconds.Apply(); if (!GetOkButton().Disabled) SubmitRequested?.Invoke(Plan); };
        RefreshPlan();
    }
    private void RefreshPlan()
    {
        ExplosiveProposal proposal = Propose?.Invoke((int)_seconds.Value) ?? new(ExplosiveProposalStatus.Unavailable);
        Plan = proposal.Plan; GetOkButton().Disabled = proposal.Status != ExplosiveProposalStatus.Ready;
        _status.Text = Plan != null ? $"Prazo escolhido: {Plan.Schedule.SelectedSeconds} s. Cancelar mantém o item na bolsa." : Hint(proposal.Status);
    }
    public void Reject(string hint) { Plan = null; _status.Text = hint; GetOkButton().Disabled = true; }
    public static string Hint(ExplosiveProposalStatus status) => status switch
    {
        ExplosiveProposalStatus.Busy => "Termine ou pare a caminhada antes de posicionar.",
        ExplosiveProposalStatus.NoItem => "Esta unidade já não está disponível na bolsa.",
        ExplosiveProposalStatus.AtExit => "Saia da passagem antes de posicionar.",
        ExplosiveProposalStatus.InvalidTimer => "Escolha de 10 a 180 segundos, em passos de 10.",
        ExplosiveProposalStatus.Unsupported => "Este item ainda não tem preparação disponível.",
        _ => "Volte à elevação do personagem e escolha uma posição caminhável."
    };
}
