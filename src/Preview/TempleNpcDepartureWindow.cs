using System;
using System.Linq;
using FalloutRemake.Geometry;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TempleNpcDepartureWindow : AcceptDialog
{
    private readonly Label _state = new() { Name = "DepartureState", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _notice = new() { Name = "DepartureNotice", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly DepartureRouteView _route = new() { CustomMinimumSize = new(0, 190), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
    private readonly Button _step = new() { Name = "DepartureStep", Text = "Um passo" },
        _leg = new() { Name = "DepartureLeg", Text = "Até a próxima parada" },
        _tick = new() { Name = "DepartureTick", Text = "Avançar 1 s" },
        _event = new() { Name = "DepartureEvent", Text = "Aguardar o evento" },
        _restart = new() { Name = "DepartureRestart", Text = "Recomeçar saída" },
        _save = new() { Name = "DepartureSave", Text = "Salvar saída" },
        _load = new() { Name = "DepartureLoad", Text = "Carregar saída" };
    public Action? StepRequested, LegRequested, TickRequested, EventRequested, RestartRequested, SaveRequested, LoadRequested;
    public string StateText => _state.Text;
    public string NoticeText => _notice.Text;
    public bool ReadableLayout => _route.Size.X >= 400 && _route.Size.Y >= 190 && _state.Size.X >= 400;
    public override void _Ready()
    {
        Title = "Saída do NPC · ensaio"; Exclusive = true; Transient = true; MinSize = new(560, 480);
        GetOkButton().Text = "Voltar à conversa";
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); AddChild(scroll);
        scroll.OffsetLeft = 20; scroll.OffsetRight = -20; scroll.OffsetTop = 16; scroll.OffsetBottom = -60;
        var body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12); scroll.AddChild(body);
        body.AddChild(new Label { Text = "Acompanhe uma cópia do percurso original após Speech. Os passos e a espera avançam pelos botões; a partida permanece no mesmo estado.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color("e8c78d") });
        body.AddChild(_state); body.AddChild(_route);
        body.AddChild(new Label { Text = "Azul: até a porta · Âmbar: trecho intermediário · Verde: saída\nCírculo: NPC · Quadrado: porta final · Traçado esquemático dos hexes originais",
            AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var movement = new HFlowContainer();
        foreach (var button in new[] { _step, _leg, _tick, _event }) movement.AddChild(button);
        body.AddChild(movement); body.AddChild(_notice);
        var files = new HFlowContainer(); foreach (var button in new[] { _restart, _save, _load }) files.AddChild(button); body.AddChild(files);
        _step.Pressed += () => { if (!_step.Disabled) StepRequested?.Invoke(); };
        _leg.Pressed += () => { if (!_leg.Disabled) LegRequested?.Invoke(); };
        _tick.Pressed += () => { if (!_tick.Disabled) TickRequested?.Invoke(); };
        _event.Pressed += () => { if (!_event.Disabled) EventRequested?.Invoke(); };
        _restart.Pressed += () => RestartRequested?.Invoke();
        _save.Pressed += () => SaveRequested?.Invoke();
        _load.Pressed += () => { if (!_load.Disabled) LoadRequested?.Invoke(); };
    }
    public void Refresh(TempleNpcDeparture session, bool canLoad)
    {
        var state = session.Capture(); var gate = session.Profile.Gate(state);
        string phase = state.Phase switch { NpcDeparturePhase.ToGate => "Indo até a porta", NpcDeparturePhase.GateDelay => "Aguardando junto à porta",
            NpcDeparturePhase.ToMiddle => "Percorrendo o trecho intermediário", NpcDeparturePhase.ToExit => "Indo até a saída", _ => "Saída concluída · NPC oculto" };
        _state.Text = $"{phase}\nHex {session.Location.Hex} · Porta {(gate.IsOpen ? "aberta" : "fechada")} · NPC {(state.Phase == NpcDeparturePhase.Hidden ? "sem bloqueio" : "bloqueia o hex ocupado")}";
        _notice.Text = state.Phase == NpcDeparturePhase.GateDelay ? $"Espera restante: {state.RemainingTicks / 10.0:0.0} s. O evento acontece uma vez; aguardar não inicia o próximo percurso."
            : state.Phase == NpcDeparturePhase.Hidden ? "O objeto original foi preservado. Você pode carregar, recomeçar ou voltar à conversa."
            : "Avance um hex ou caminhe até a próxima parada neste ensaio.";
        _step.Disabled = _leg.Disabled = !session.Walking;
        _tick.Disabled = _event.Disabled = state.Phase != NpcDeparturePhase.GateDelay;
        _load.Disabled = !canLoad; _route.Inspect(session.Profile, state);
    }
    public void Notice(string text) => _notice.Text = text;
}

internal partial class DepartureRouteView : Control
{
    private TempleNpcDepartureProfile? _profile;
    private NpcDepartureSnapshot? _state;
    public void Inspect(TempleNpcDepartureProfile profile, NpcDepartureSnapshot state) { _profile = profile; _state = state; QueueRedraw(); }
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("111e25"));
        if (_profile == null || _state == null || Size.X < 1 || Size.Y < 1) return;
        var phases = new[] { NpcDeparturePhase.ToGate, NpcDeparturePhase.ToMiddle, NpcDeparturePhase.ToExit };
        var points = phases.SelectMany(p => _profile.Route(p)).Select(Point).ToArray();
        var min = new Vector2(points.Min(p => p.X), points.Min(p => p.Y));
        var max = new Vector2(points.Max(p => p.X), points.Max(p => p.Y));
        float scale = Math.Min((Size.X - 80) / Math.Max(1, max.X - min.X), (Size.Y - 48) / Math.Max(1, max.Y - min.Y));
        Vector2 offset = (Size - (max - min) * scale) / 2;
        Vector2 At(int hex) => (Point(hex) - min) * scale + offset;
        Color[] colors = { new("70bfff"), new("e8c78d"), new("83d09a") };
        for (int i = 0; i < phases.Length; i++)
        {
            int[] route = _profile.Route(phases[i]);
            DrawPolyline(route.Select(At).ToArray(), colors[i], 2, true);
            DrawCircle(At(route[^1]), 4, colors[i]);
        }
        var font = ThemeDB.FallbackFont;
        foreach (var label in new[] { (13728, "Início · 13728"), (19728, "Porta · 19728"), (22712, "Parada · 22712"), (28715, "Saída · 28715") })
        {
            Vector2 point = At(label.Item1); float width = font.GetStringSize(label.Item2, fontSize: 14).X;
            DrawString(font, new(Math.Clamp(point.X - width / 2, 12, Math.Max(12, Size.X - width - 12)), Math.Max(18, point.Y - 12)),
                label.Item2, fontSize: 14, modulate: new Color("c4d8dc"));
        }
        var gate = _profile.Gate(_state);
        DrawRect(new Rect2(At(gate.Source.Header.Tile) - new Vector2(5, 5), new(10, 10)), gate.IsOpen ? new Color("83d09a") : new Color("ef897e"), false, 2);
        if (_state.Phase != NpcDeparturePhase.Hidden)
        { DrawCircle(At(_profile.Location(_state).Hex), 7, new Color("e4f5ff")); DrawCircle(At(_profile.Location(_state).Hex), 3, new Color("28627f")); }
    }
    // Rotate the schematic so the long corridor uses the panel width; original hex geometry and IDs remain intact.
    private static Vector2 Point(int hex) { var point = FalloutGrid.HexCenter(hex); return new((float)point.Z, -(float)point.X); }
}
