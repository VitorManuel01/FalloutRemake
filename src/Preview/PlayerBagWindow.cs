using System;
using System.Collections.Generic;
using System.Linq;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class PlayerBagWindow : AcceptDialog
{
    private readonly Tree _items = new() { Name = "CarriedItems", Columns = 4, HideRoot = true, ColumnTitlesVisible = true,
        CustomMinimumSize = new Vector2(860, 240), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
    private readonly RichTextLabel _detail = new() { BbcodeEnabled = false, CustomMinimumSize = new Vector2(860, 100) };
    private readonly Label _summary = new();
    private readonly Button _returnOne = new() { Name = "ReturnOneAction", Text = "Devolver 1", Disabled = true },
        _returnStack = new() { Name = "ReturnStackAction", Text = "Devolver esta pilha", Disabled = true },
        _approach = new() { Name = "BagOriginApproachAction", Text = "Voltar ao recipiente", Disabled = true };
    private readonly RichTextLabel _returnStatus = new() { BbcodeEnabled = false, CustomMinimumSize = new Vector2(860, 40) };
    private readonly Dictionary<string, CarriedItem> _lines = new();
    private Func<CarriedItem, PrototypeText?>? _text;
    private Func<int, string>? _mapName;
    private Func<CarriedItem, int, ItemTakeStatus>? _canReturn;
    private Func<CarriedItem, bool>? _canApproach;
    public Action<CarriedItem, int>? ReturnRequested;
    public Action<CarriedItem>? ApproachRequested;
    public CarriedItem? SelectedItem { get; private set; }
    public long TotalQuantity { get; private set; }
    public int RowCount => _lines.Count;
    public string DetailText => _detail.Text;
    public string SummaryText => _summary.Text;
    public string SelectedWeightText => _items.GetSelected()?.GetText(2) ?? "";
    public string ReturnStatusText => _returnStatus.Text;
    public bool ReadableLayout => _items.Size.X >= 800 && _items.Size.Y >= 200;
    public override void _Ready()
    {
        Title = "Bolsa do personagem"; Exclusive = true; Transient = true; GetOkButton().Text = "Fechar";
        WindowInput += input => { if (input is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.B }) { SetInputAsHandled(); EmitSignal(SignalName.Canceled); } };
        var body = new VBoxContainer(); body.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        body.OffsetLeft = 20; body.OffsetRight = -20; body.OffsetTop = 20; body.OffsetBottom = -60; body.AddThemeConstantOverride("separation", 12); AddChild(body);
        body.AddChild(_summary); body.AddChild(_items); body.AddChild(_detail);
        body.AddChild(_returnStatus); var actions = new HBoxContainer(); body.AddChild(actions); actions.AddChild(_approach); actions.AddChild(_returnOne); actions.AddChild(_returnStack);
        _approach.Pressed += () => { if (!_approach.Disabled && SelectedItem != null) ApproachRequested?.Invoke(SelectedItem); };
        _returnOne.Pressed += () => { if (!_returnOne.Disabled && SelectedItem != null) ReturnRequested?.Invoke(SelectedItem, 1); };
        _returnStack.Pressed += () => { if (!_returnStack.Disabled && SelectedItem != null) ReturnRequested?.Invoke(SelectedItem, SelectedItem.Quantity); };
        body.AddChild(new Label { Text = "K salva a bolsa junto da posição, portas e XP, depois de fechar esta janela." });
        _items.SetColumnTitle(0, "Item"); _items.SetColumnTitle(1, "Quantidade"); _items.SetColumnTitle(2, "Peso"); _items.SetColumnTitle(3, "Encontrado em");
        _items.SetColumnExpand(1, false); _items.SetColumnCustomMinimumWidth(1, 100);
        _items.SetColumnExpand(2, false); _items.SetColumnCustomMinimumWidth(2, 110);
        _items.SetColumnExpand(3, false); _items.SetColumnCustomMinimumWidth(3, 220);
        _items.ItemSelected += ShowSelected;
    }
    public void Refresh(CarriedItem[] items, Func<CarriedItem, PrototypeText?> text, Func<int, string> mapName, long? carriedWeight, int capacity,
        Func<CarriedItem, long?> unitWeight, Func<CarriedItem, int, ItemTakeStatus>? canReturn = null, Func<CarriedItem, bool>? canApproach = null)
    {
        string? selected = SelectedItem == null ? null : RowKey(SelectedItem);
        _text = text; _mapName = mapName; _canReturn = canReturn; _canApproach = canApproach; _lines.Clear(); _items.Clear(); TreeItem root = _items.CreateItem(); TreeItem? select = null;
        TotalQuantity = items.Sum(i => (long)i.Quantity);
        _summary.Text = items.Length == 0 ? "Sua bolsa está vazia." : $"{TotalQuantity} {(TotalQuantity == 1 ? "unidade" : "unidades")} em {items.Length} {(items.Length == 1 ? "pilha" : "pilhas")}";
        _summary.Text += $" · Carga: {CarryWeightText.Load(carriedWeight, capacity)} · Força {CarryWeight.TestStrength} (perfil de teste)";
        foreach (CarriedItem item in items)
        {
            string key = RowKey(item); _lines.Add(key, item); TreeItem row = _items.CreateItem(root);
            row.SetText(0, text(item)?.Name ?? "Item sem nome"); row.SetText(1, item.Quantity.ToString()); row.SetText(2, CarryWeightText.Stack(unitWeight(item), item.Quantity));
            row.SetText(3, mapName(item.Map)); row.SetMetadata(0, key);
            row.SetTooltipText(0, $"ID {item.Source.Header.Id} · PID {item.Source.Header.Pid} · {item.Source.Key}");
            if (key == selected || select == null) select = row;
        }
        SelectedItem = null; _detail.Text = items.Length == 0 ? "Recolha itens de recipientes com J. Itens recolhidos acompanham você ao trocar de área." : "Selecione um item para consultar a descrição.";
        _returnOne.Disabled = _returnStack.Disabled = _approach.Disabled = true; _returnStatus.Text = "";
        if (select != null) { select.Select(0); ShowSelected(); }
    }
    private static string RowKey(CarriedItem item) => $"{item.Map}:{item.Source.Key}";
    private void ShowSelected()
    {
        TreeItem? selected = _items.GetSelected(); if (selected == null || !_lines.TryGetValue(selected.GetMetadata(0).AsString(), out var item)) return;
        SelectedItem = item; PrototypeText? text = _text!(item);
        _detail.Text = $"{text?.Name ?? "Item sem nome"}\n\n{text?.Description ?? "Descrição indisponível."}\n\nEncontrado em {_mapName!(item.Map)}.";
        ItemTakeStatus status = _canReturn?.Invoke(item, 1) ?? ItemTakeStatus.Unsupported;
        _returnStatus.Text = status == ItemTakeStatus.Ready ? "Você pode devolver ao recipiente de origem, sem usar o item."
            : status == ItemTakeStatus.Unavailable ? "Volte à área e elevação de origem para devolver este item."
            : status == ItemTakeStatus.TooFar ? "Aproxime-se do recipiente de origem para devolver."
            : status == ItemTakeStatus.Empty ? "Você já não carrega esta pilha." : ContainerLootWindow.Hint(status);
        _returnOne.Disabled = status != ItemTakeStatus.Ready;
        _returnStack.Disabled = status != ItemTakeStatus.Ready || _canReturn!(item, item.Quantity) != ItemTakeStatus.Ready;
        _approach.Disabled = status != ItemTakeStatus.TooFar || _canApproach?.Invoke(item) != true;
        if (status == ItemTakeStatus.TooFar && _approach.Disabled) _returnStatus.Text = "Sem caminho até o recipiente com os bloqueios atuais.";
    }
}
