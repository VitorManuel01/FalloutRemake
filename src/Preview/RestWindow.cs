using System;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

/// <summary>
/// The Pip-Boy's rest (CE pipboyRest): 10 and 30 minutes, 1 to 6 hours, or until healed. Resting is FO2's only natural
/// healing; the window says why it is refused, and offers CE's option to have the Pip-Boy before the vault suit.
/// </summary>
public partial class RestWindow : AcceptDialog
{
    private readonly Label _status = new() { Name = "RestStatus", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(560, 0) };
    private readonly Label _clock = new() { Name = "RestClock" };
    private readonly CheckBox _pipBoy = new() { Name = "PipBoyFromStart", Text = "Pip-Boy desde o início (opção do CE; no original, só depois do traje do Vault)" };
    private readonly HFlowContainer _durations = new();
    public Action<int, int>? RestRequested;
    public Action? RestUntilHealedRequested;
    public Action<bool>? PipBoyToggled;
    public string StatusText => _status.Text;
    public bool RestEnabled { get; private set; }

    public override void _Ready()
    {
        Title = "Descansar"; Exclusive = true; Transient = true; GetOkButton().Text = "Fechar";
        var body = new VBoxContainer(); body.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); AddChild(body);
        body.OffsetLeft = 20; body.OffsetRight = -20; body.OffsetTop = 16; body.OffsetBottom = -60; body.AddThemeConstantOverride("separation", 10);
        body.AddChild(new Label { Text = "No FO2 o HP só volta sozinho descansando: a cada 3 horas, a taxa de cura.", AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(560, 0), Modulate = new Color("e8c78d") });
        body.AddChild(_clock); body.AddChild(_pipBoy); body.AddChild(_durations); body.AddChild(_status);
        _durations.AddThemeConstantOverride("h_separation", 6);
        foreach (var (label, hours, minutes) in new[] { ("10 min", 0, 10), ("30 min", 0, 30), ("1 h", 1, 0), ("2 h", 2, 0), ("3 h", 3, 0), ("4 h", 4, 0), ("5 h", 5, 0), ("6 h", 6, 0) })
        {
            var button = new Button { Name = $"Rest{hours}h{minutes}m", Text = label };
            button.Pressed += () => { if (RestEnabled) RestRequested?.Invoke(hours, minutes); };
            _durations.AddChild(button);
        }
        var heal = new Button { Name = "RestUntilHealed", Text = "Até curar" };
        heal.Pressed += () => { if (RestEnabled) RestUntilHealedRequested?.Invoke(); };
        _durations.AddChild(heal);
        _pipBoy.Toggled += value => PipBoyToggled?.Invoke(value);
    }

    public void Refresh(RestStatus status, bool pipBoyFromStart, string clock, string? message = null)
    {
        RestEnabled = status == RestStatus.Ready;
        _pipBoy.SetPressedNoSignal(pipBoyFromStart);
        foreach (Node child in _durations.GetChildren()) if (child is Button button) button.Disabled = !RestEnabled;
        _clock.Text = clock;
        _status.Text = message ?? Hint(status);
    }

    public void TogglePipBoy(bool value) { _pipBoy.ButtonPressed = value; }

    public static string Hint(RestStatus status) => status switch
    {
        RestStatus.Ready => "Escolha quanto descansar.",
        RestStatus.NoPipBoy => "Sem Pip-Boy: no original, ele vem com o traje do Vault, depois do Templo. A opção acima o libera antes.",
        RestStatus.InCombat => "Não dá para descansar em combate.",
        RestStatus.Busy => "Pare de andar para descansar.",
        RestStatus.EnemiesNearby => "Não dá para descansar aqui: este mapa não deixa com criaturas vivas por perto (maps.txt).",
        RestStatus.Healthy => "Você já está com o HP cheio.",
        _ => "Duração inválida."
    };
}
