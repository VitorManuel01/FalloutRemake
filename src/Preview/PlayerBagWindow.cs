using System;
using System.Collections.Generic;
using System.Linq;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class PlayerBagWindow : AcceptDialog
{
    private readonly Tree _items = new() { Name = "CarriedItems", Columns = 3, HideRoot = true, ColumnTitlesVisible = true,
        CustomMinimumSize = new Vector2(860, 240), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
    private readonly RichTextLabel _detail = new() { BbcodeEnabled = false, CustomMinimumSize = new Vector2(860, 100) };
    private readonly Label _summary = new();
    private readonly Dictionary<string, CarriedItem> _lines = new();
    private Func<CarriedItem, PrototypeText?>? _text;
    private Func<int, string>? _mapName;
    public CarriedItem? SelectedItem { get; private set; }
    public long TotalQuantity { get; private set; }
    public int RowCount => _lines.Count;
    public string DetailText => _detail.Text;
    public bool ReadableLayout => _items.Size.X >= 800 && _items.Size.Y >= 200;
    public override void _Ready()
    {
        Title = "Bolsa do personagem"; Exclusive = true; Transient = true; GetOkButton().Text = "Fechar";
        WindowInput += input => { if (input is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.B }) { SetInputAsHandled(); EmitSignal(SignalName.Canceled); } };
        var body = new VBoxContainer(); body.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        body.OffsetLeft = 20; body.OffsetRight = -20; body.OffsetTop = 20; body.OffsetBottom = -60; body.AddThemeConstantOverride("separation", 12); AddChild(body);
        body.AddChild(_summary); body.AddChild(_items); body.AddChild(_detail);
        body.AddChild(new Label { Text = "K salva a bolsa junto da posição, portas e XP, depois de fechar esta janela." });
        _items.SetColumnTitle(0, "Item"); _items.SetColumnTitle(1, "Quantidade"); _items.SetColumnTitle(2, "Encontrado em");
        _items.SetColumnExpand(1, false); _items.SetColumnCustomMinimumWidth(1, 100);
        _items.SetColumnExpand(2, false); _items.SetColumnCustomMinimumWidth(2, 220);
        _items.ItemSelected += ShowSelected;
    }
    public void Refresh(CarriedItem[] items, Func<CarriedItem, PrototypeText?> text, Func<int, string> mapName)
    {
        string? selected = SelectedItem == null ? null : RowKey(SelectedItem);
        _text = text; _mapName = mapName; _lines.Clear(); _items.Clear(); TreeItem root = _items.CreateItem(); TreeItem? select = null;
        TotalQuantity = items.Sum(i => (long)i.Quantity);
        _summary.Text = items.Length == 0 ? "Sua bolsa está vazia." : $"{TotalQuantity} {(TotalQuantity == 1 ? "unidade" : "unidades")} em {items.Length} {(items.Length == 1 ? "pilha" : "pilhas")}";
        foreach (CarriedItem item in items)
        {
            string key = RowKey(item); _lines.Add(key, item); TreeItem row = _items.CreateItem(root);
            row.SetText(0, text(item)?.Name ?? "Item sem nome"); row.SetText(1, item.Quantity.ToString()); row.SetText(2, mapName(item.Map)); row.SetMetadata(0, key);
            row.SetTooltipText(0, $"ID {item.Source.Header.Id} · PID {item.Source.Header.Pid} · {item.Source.Key}");
            if (key == selected || select == null) select = row;
        }
        SelectedItem = null; _detail.Text = items.Length == 0 ? "Recolha itens de recipientes com J. Itens recolhidos acompanham você ao trocar de área." : "Selecione um item para consultar a descrição.";
        if (select != null) { select.Select(0); ShowSelected(); }
    }
    private static string RowKey(CarriedItem item) => $"{item.Map}:{item.Source.Key}";
    private void ShowSelected()
    {
        TreeItem? selected = _items.GetSelected(); if (selected == null || !_lines.TryGetValue(selected.GetMetadata(0).AsString(), out var item)) return;
        SelectedItem = item; PrototypeText? text = _text!(item);
        _detail.Text = $"{text?.Name ?? "Item sem nome"}\n\n{text?.Description ?? "Descrição indisponível."}\n\nEncontrado em {_mapName!(item.Map)}.";
    }
}
