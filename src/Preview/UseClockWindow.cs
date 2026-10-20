using System;
using System.Globalization;
using System.Linq;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class UseClockWindow : AcceptDialog
{
    private readonly Tree _events = new() { Name = "UseClockEvents", Columns = 3, HideRoot = true, ColumnTitlesVisible = true,
        CustomMinimumSize = new(480, 150), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
    private readonly Button _one = new() { Name = "AdvanceUseOneSecond", Text = "+1 s", Disabled = true },
        _ten = new() { Name = "AdvanceUseTenSeconds", Text = "+10 s", Disabled = true };
    private readonly Label _summary = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart },
        _status = new() { Name = "UseClockStatus", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    public Action<int>? AdvanceRequested;
    public string StatusText => _status.Text;
    public int EventCount { get; private set; }
    public static string Duration(int ticks) => ((decimal)ticks / ExplosiveTimer.TicksPerSecond).ToString("0.#", CultureInfo.GetCultureInfo("pt-BR")) + " s";

    public override void _Ready()
    {
        Title = "Tempo · ensaio"; Exclusive = true; Transient = true; MinSize = new(560, 400); GetOkButton().Text = "Fechar";
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); AddChild(scroll);
        scroll.OffsetLeft = 20; scroll.OffsetRight = -20; scroll.OffsetTop = 16; scroll.OffsetBottom = -60;
        var body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; body.AddThemeConstantOverride("separation", 12); scroll.AddChild(body);
        body.AddChild(new Label { Text = "Avança todos os explosivos posicionados, inclusive em outras áreas. Para no primeiro prazo vencido. Os efeitos aguardam resolução; esta tela não provoca uma explosão.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color("e8c78d") });
        body.AddChild(_summary); body.AddChild(_events);
        _events.SetColumnTitle(0, "Local"); _events.SetColumnTitle(1, "Prazo restante"); _events.SetColumnTitle(2, "Estado");
        _events.SetColumnExpand(1, false); _events.SetColumnCustomMinimumWidth(1, 110);
        _events.SetColumnExpand(2, false); _events.SetColumnCustomMinimumWidth(2, 160);
        var actions = new HBoxContainer(); actions.AddChild(_one); actions.AddChild(_ten); body.AddChild(actions); body.AddChild(_status);
        _one.Pressed += () => { if (!_one.Disabled) AdvanceRequested?.Invoke(ExplosiveTimer.TicksPerSecond); };
        _ten.Pressed += () => { if (!_ten.Disabled) AdvanceRequested?.Invoke(10 * ExplosiveTimer.TicksPerSecond); };
    }
    public void Refresh(WorldItemUse[] uses, Func<int, string> mapName, string? blocked, string? message = null)
    {
        EventCount = uses.Length; _events.Clear(); TreeItem root = _events.CreateItem();
        foreach (WorldItemUse entry in uses.OrderBy(u => u.Allocation.RemainingTicks).ThenBy(u => u.Allocation.Placement.Map).ThenBy(u => u.Allocation.Id))
        {
            var use = entry.Allocation; TreeItem row = _events.CreateItem(root);
            row.SetText(0, $"{mapName(use.Placement.Map)} · elevação {use.Placement.Elevation}");
            row.SetText(1, Duration(use.RemainingTicks)); row.SetText(2, use.RemainingTicks == 0 ? "Aguarda resolução" : "Pausado");
            row.SetMetadata(0, use.Id.ToString("D"));
        }
        int due = uses.Count(u => u.Allocation.RemainingTicks == 0);
        _summary.Text = $"{uses.Length} posicionado(s) · {due} aguardando resolução";
        _one.Disabled = _ten.Disabled = blocked != null;
        _status.Text = message ?? blocked ?? "Escolha quanto avançar. Fechar ou caminhar mantém os prazos pausados.";
    }
}
