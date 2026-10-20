using System;
using System.Collections.Generic;
using System.Linq;
using FalloutRemake.Import;
using Godot;

namespace FalloutRemake.Preview;

/// <summary>Read-only structural inspection, independent of the runtime inventory rules.</summary>
public partial class InventoryInspector : AcceptDialog
{
    private readonly Tree _tree = new() { Name = "InventoryTree", Columns = 3, HideRoot = true, ColumnTitlesVisible = true,
        CustomMinimumSize = new Vector2(420, 200),
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
    private readonly RichTextLabel _detail = new() { BbcodeEnabled = false, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        CustomMinimumSize = new Vector2(320, 200),
        SizeFlagsVertical = Control.SizeFlags.ExpandFill };
    private readonly TextureRect _art = new() { CustomMinimumSize = new Vector2(128, 100), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = Control.TextureFilterEnum.Nearest, Visible = false };
    private readonly Dictionary<string, MapObject> _objects = new();
    private Func<MapObject, PrototypeText?>? _text;
    public string SelectedSourceKey { get; private set; } = "";
    public string DetailText => _detail.Text;
    public int? SelectedArtFid => _art.Texture?.GetMeta("inventory_fid").AsInt32();
    public int ItemRows { get; private set; }
    public bool ReadableLayout => _tree.Size.X >= 300 && _detail.Size.X >= 300 && _tree.Size.Y >= 200 && _detail.Size.Y >= 200;
    public override void _Ready()
    {
        Title = "Inventário original · consulta"; Exclusive = true; Transient = true;
        WindowInput += input =>
        {
            if (input is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.I })
            { SetInputAsHandled(); EmitSignal(SignalName.Canceled); }
        };
        GetOkButton().Text = "Fechar";
        var scroll = new ScrollContainer { Name = "DialogScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); AddChild(scroll);
        var body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill }; scroll.AddChild(body);
        scroll.OffsetLeft = 16; scroll.OffsetRight = -16; scroll.OffsetTop = 16; scroll.OffsetBottom = -60;
        body.AddChild(new Label { Name = "InventoryOwner", ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis });
        var split = new HSplitContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; body.AddChild(split);
        split.AddChild(_tree); var detail = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        detail.AddChild(_art); detail.AddChild(_detail); split.AddChild(detail);
        _tree.SetColumnTitle(0, "Objeto"); _tree.SetColumnTitle(1, "Qtd."); _tree.SetColumnTitle(2, "PID");
        _tree.SetColumnExpand(1, false); _tree.SetColumnCustomMinimumWidth(1, 48);
        _tree.SetColumnExpand(2, false); _tree.SetColumnCustomMinimumWidth(2, 96);
        _tree.ItemSelected += ShowSelected;
        body.AddChild(new Label { Name = "InventorySummary", ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis });
    }
    public void Inspect(MapObject owner, Func<MapObject, PrototypeText?> text, Func<MapObject, Texture2D?>? artwork = null)
    {
        InventoryView view = InventoryView.Create(owner);
        _text = text; _objects.Clear(); _tree.Clear(); ItemRows = view.Items.Count;
        string NameFor(MapObject obj) => text(obj)?.Name ?? $"Objeto {obj.Header.Id}";
        string ownerName = NameFor(owner);
        if (ownerName.Length > 80) ownerName = ownerName[..79] + "…";
        (FindChild("InventoryOwner", true, false) as Label)!.Text = $"{ownerName} · objeto {owner.Header.Id} · PID {owner.Header.Pid}";
        TreeItem root = _tree.CreateItem(); var parents = new Dictionary<string, TreeItem> { [owner.Key] = root };
        foreach (InventoryLine line in view.Items)
        {
            MapObject obj = line.Object; _objects.Add(obj.Key, obj);
            TreeItem row = _tree.CreateItem(parents[line.ParentKey]); parents.Add(obj.Key, row);
            row.SetText(0, NameFor(obj)); row.SetText(1, line.Quantity.ToString()); row.SetText(2, obj.Header.Pid.ToString());
            row.SetMetadata(0, obj.Key); row.SetTooltipText(0, $"ID {obj.Header.Id} · {obj.Key}");
            Texture2D? icon = artwork?.Invoke(obj); if (icon != null) { row.SetIcon(0, icon); row.SetIconMaxWidth(0, 28); }
        }
        (FindChild("InventorySummary", true, false) as Label)!.Text = view.Truncated
            ? $"Limite de {InventoryView.MaxRows} registros exibidos. Quantidades por recipiente, sem multiplicação entre níveis."
            : $"{view.Items.Count} {(view.Items.Count == 1 ? "registro" : "registros")} · quantidades por recipiente · consulta sem transferência de itens";
        SelectedSourceKey = ""; _art.Texture = null; _art.Visible = false;
        _detail.Text = view.Items.Count == 0 ? "Este objeto não contém registros de inventário no MAP." : "Selecione um item para consultar sua descrição e os dados originais.";
        if (view.Items.Count > 0) { parents[view.Items[0].Object.Key].Select(0); ShowSelected(); }
    }
    private void ShowSelected()
    {
        TreeItem? selected = _tree.GetSelected();
        if (selected == null || !_objects.TryGetValue(selected.GetMetadata(0).AsString(), out MapObject? obj)) return;
        SelectedSourceKey = obj.Key; PrototypeText? text = _text!(obj);
        _art.Texture = selected.GetIcon(0); _art.Visible = _art.Texture != null;
        ObjectHeader h = obj.Header;
        _detail.Text = $"{text?.Name ?? "Nome indisponível"}\n\n{text?.Description ?? "Sem descrição original disponível."}"
            + $"\n\nID  {h.Id}\nPID  {h.Pid}  (0x{h.Pid:X8})\nFID  {h.Fid}  (0x{h.Fid:X8})\nHex  {h.Tile}\nOffset  {obj.Offset}\nChave fonte  {obj.Key}"
            + "\n\nCampos originais:\n" + string.Join("\n", obj.State.OrderBy(p => p.Key).Select(p => $"{p.Key}  {p.Value}"));
        if (SelectedArtFid.HasValue) _detail.Text += $"\n\nFID de inventário  {SelectedArtFid.Value}  (0x{SelectedArtFid.Value:X8})";
    }
}
