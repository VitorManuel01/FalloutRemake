using System;
using System.Collections.Generic;
using System.Linq;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class ContainerLootWindow : AcceptDialog
{
    private readonly Tree _items = new() { Name = "LootItems", Columns = 3, HideRoot = true, ColumnTitlesVisible = true,
        CustomMinimumSize = new Vector2(800, 180), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
    private readonly Label _owner = new() { ClipText = true }, _bag = new();
    private readonly TextureRect _art = new() { CustomMinimumSize = new Vector2(96, 96), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = Control.TextureFilterEnum.Nearest, Visible = false, TooltipText = "Imagem original do item" };
    private readonly RichTextLabel _description = new() { BbcodeEnabled = false, CustomMinimumSize = new Vector2(0, 96), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill },
        _status = new() { BbcodeEnabled = false, CustomMinimumSize = new Vector2(800, 48) };
    private readonly Button _one = new() { Name = "TakeOneAction", Text = "Retirar 1", Disabled = true },
        _stack = new() { Name = "TakeStackAction", Text = "Retirar esta pilha", Disabled = true },
        _approach = new() { Name = "ApproachContainerAction", Text = "Aproximar-se", Disabled = true };
    private readonly Dictionary<string, ContainerItem> _lines = new();
    private readonly QuantityTransfer _quantity = new() { Name = "TakeQuantity", Visible = false };
    private Func<MapObject, PrototypeText?>? _text;
    private Func<string, int, ItemTakeStatus>? _canTake;
    private bool _canApproach;
    private string? _interactionHint;
    public Action<string, int>? TakeRequested;
    public Action? ApproachRequested;
    public string SelectedKey { get; private set; } = "";
    public string StatusText => _status.Text;
    public string LoadText => _bag.Text;
    public string SelectedWeightText => _items.GetSelected()?.GetText(2) ?? "";
    public int? SelectedArtFid => _art.Texture?.GetMeta("inventory_fid").AsInt32();
    public long Remaining => _lines.Values.Sum(l => (long)l.Quantity);
    public Vector2 ListSize => _items.Size;
    public bool ReadableLayout => _items.Size.Y >= 180 && _items.Size.X >= 500;
    public override void _Ready()
    {
        Title = "Recolher do recipiente"; Exclusive = true; Transient = true; GetOkButton().Text = "Fechar";
        WindowInput += input => { if (input is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.J }) { SetInputAsHandled(); EmitSignal(SignalName.Canceled); } };
        var scroll = new ScrollContainer { Name = "DialogScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); AddChild(scroll);
        var body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill }; scroll.AddChild(body);
        scroll.OffsetLeft = 20; scroll.OffsetRight = -20; scroll.OffsetTop = 20; scroll.OffsetBottom = -60; body.AddThemeConstantOverride("separation", 12);
        body.AddChild(_owner); body.AddChild(_items); var detail = new HBoxContainer(); detail.AddThemeConstantOverride("separation", 16);
        detail.AddChild(_art); detail.AddChild(_description); body.AddChild(detail); body.AddChild(_status);
        var actions = new HFlowContainer(); actions.AddThemeConstantOverride("separation", 12); body.AddChild(actions);
        actions.AddChild(_approach); actions.AddChild(_one); actions.AddChild(_stack); body.AddChild(_bag);
        actions.AddChild(_quantity); _quantity.Requested = qty => TakeRequested?.Invoke(SelectedKey, qty);
        _items.SetColumnTitle(0, "Item"); _items.SetColumnTitle(1, "Restante"); _items.SetColumnExpand(1, false); _items.SetColumnCustomMinimumWidth(1, 90);
        _items.SetColumnTitle(2, "Peso da pilha"); _items.SetColumnExpand(2, false); _items.SetColumnCustomMinimumWidth(2, 140);
        _items.ItemSelected += ShowSelected;
        _one.Pressed += () => { if (!_one.Disabled) TakeRequested?.Invoke(SelectedKey, 1); };
        _stack.Pressed += () => { if (!_stack.Disabled && _lines.TryGetValue(SelectedKey, out var line)) TakeRequested?.Invoke(SelectedKey, line.Quantity); };
        _approach.Pressed += () => { if (!_approach.Disabled) ApproachRequested?.Invoke(); };
    }
    public void Refresh(string owner, ContainerItem[] lines, Func<MapObject, PrototypeText?> text, Func<string, int, ItemTakeStatus> canTake, long carried, bool canApproach,
        long? carriedWeight, int capacity, Func<MapObject, long?> unitWeight, ItemTakeStatus ownerStatus = ItemTakeStatus.Ready, Func<MapObject, Texture2D?>? artwork = null,
        string? interactionHint = null)
    {
        _owner.Text = owner; _text = text; _canTake = canTake; _canApproach = canApproach; _interactionHint = interactionHint;
        _bag.Text = $"Bolsa: {carried} {(carried == 1 ? "unidade" : "unidades")} · Carga: {CarryWeightText.Load(carriedWeight, capacity)} · K para salvar";
        string selected = SelectedKey; _lines.Clear(); _items.Clear(); TreeItem root = _items.CreateItem(); TreeItem? select = null;
        foreach (ContainerItem line in lines)
        {
            _lines.Add(line.Source.Key, line); TreeItem row = _items.CreateItem(root);
            row.SetText(0, text(line.Source)?.Name ?? "Item sem nome"); row.SetText(1, line.Quantity.ToString()); row.SetMetadata(0, line.Source.Key);
            row.SetText(2, CarryWeightText.Stack(unitWeight(line.Source), line.Quantity));
            Texture2D? icon = artwork?.Invoke(line.Source); if (icon != null) { row.SetIcon(0, icon); row.SetIconMaxWidth(0, 28); }
            if (line.Source.Key == selected || select == null) select = row;
        }
        SelectedKey = ""; _one.Disabled = _stack.Disabled = _approach.Disabled = true; _quantity.Configure(0, "Retirar quantidade", null);
        _art.Texture = null; _art.Visible = false;
        _description.Text = ""; _status.Text = ownerStatus == ItemTakeStatus.Ready ? "Este recipiente está vazio." : StatusHint(ownerStatus);
        if (select != null) { select.Select(0); ShowSelected(); }
    }
    private void ShowSelected()
    {
        TreeItem? row = _items.GetSelected();
        if (row == null || !_lines.TryGetValue(row.GetMetadata(0).AsString(), out var line)) return;
        SelectedKey = line.Source.Key; _description.Text = _text!(line.Source)?.Description ?? "Descrição indisponível.";
        _art.Texture = row.GetIcon(0); _art.Visible = _art.Texture != null;
        ItemTakeStatus status = _canTake!(SelectedKey, 1);
        _status.Text = StatusHint(status);
        if (status == ItemTakeStatus.TooFar && !_canApproach) _status.Text = "Sem caminho até o recipiente com os bloqueios atuais.";
        _one.Disabled = status != ItemTakeStatus.Ready;
        ItemTakeStatus stackStatus = _canTake(SelectedKey, line.Quantity);
        _stack.Disabled = status != ItemTakeStatus.Ready || stackStatus != ItemTakeStatus.Ready;
        _stack.TooltipText = _stack.Disabled ? Hint(stackStatus) : "Retirar toda a quantidade restante desta pilha";
        _approach.Disabled = status != ItemTakeStatus.TooFar || !_canApproach;
        _quantity.Configure(line.Quantity, "Retirar quantidade", qty => _canTake!(SelectedKey, qty));
    }
    private string StatusHint(ItemTakeStatus status) => status == ItemTakeStatus.ScriptRequired && !string.IsNullOrWhiteSpace(_interactionHint)
        ? _interactionHint : Hint(status);
    public static string Hint(ItemTakeStatus status) => status switch
    {
        ItemTakeStatus.Ready => "Escolha quanto deseja retirar. A transferência não usa nem equipa o item.",
        ItemTakeStatus.TooFar => "Aproxime o personagem antes de retirar. Depois, abra novamente com J.",
        ItemTakeStatus.Busy => "Feche esta janela e aguarde a caminhada terminar.",
        ItemTakeStatus.Empty => "Esta pilha já foi recolhida.",
        ItemTakeStatus.Locked => "Recipiente trancado ou emperrado. A abertura ainda não está disponível.",
        ItemTakeStatus.ScriptRequired => "Este objeto depende de uma interação ainda não disponível.",
        ItemTakeStatus.AtExit => "Saia da passagem antes de retirar um item.",
        ItemTakeStatus.InvalidQuantity => "Quantidade indisponível.",
        ItemTakeStatus.OverCapacity => "Esta retirada ultrapassa a capacidade de carga. Devolva itens antes de recolher mais.",
        ItemTakeStatus.WeightUnavailable => "Peso indisponível para este item ou bolsa. A coleta ainda não está disponível neste caso.",
        ItemTakeStatus.Unavailable => "O personagem deve estar nesta área e elevação, ao alcance do recipiente.",
        _ => "A retirada deste item ainda não está disponível."
    };
}
