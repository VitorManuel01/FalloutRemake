using System;
using System.Collections.Generic;
using System.Linq;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class ContainerLootWindow : AcceptDialog
{
    private readonly Tree _items = new() { Name = "LootItems", Columns = 2, HideRoot = true, ColumnTitlesVisible = true,
        CustomMinimumSize = new Vector2(800, 180), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
    private readonly Label _owner = new() { ClipText = true }, _bag = new();
    private readonly RichTextLabel _description = new() { BbcodeEnabled = false, CustomMinimumSize = new Vector2(800, 64) },
        _status = new() { BbcodeEnabled = false, CustomMinimumSize = new Vector2(800, 48) };
    private readonly Button _one = new() { Name = "TakeOneAction", Text = "Retirar 1", Disabled = true },
        _stack = new() { Name = "TakeStackAction", Text = "Retirar esta pilha", Disabled = true },
        _approach = new() { Name = "ApproachContainerAction", Text = "Aproximar-se", Disabled = true };
    private readonly Dictionary<string, ContainerItem> _lines = new();
    private Func<MapObject, PrototypeText?>? _text;
    private Func<string, int, ItemTakeStatus>? _canTake;
    private bool _canApproach;
    public Action<string, int>? TakeRequested;
    public Action? ApproachRequested;
    public string SelectedKey { get; private set; } = "";
    public string StatusText => _status.Text;
    public long Remaining => _lines.Values.Sum(l => (long)l.Quantity);
    public Vector2 ListSize => _items.Size;
    public bool ReadableLayout => _items.Size.Y >= 180 && _items.Size.X >= 500;
    public override void _Ready()
    {
        Title = "Recolher do recipiente"; Exclusive = true; Transient = true; GetOkButton().Text = "Fechar";
        WindowInput += input => { if (input is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.J }) { SetInputAsHandled(); EmitSignal(SignalName.Canceled); } };
        var body = new VBoxContainer(); body.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        body.OffsetLeft = 20; body.OffsetRight = -20; body.OffsetTop = 20; body.OffsetBottom = -60; body.AddThemeConstantOverride("separation", 12); AddChild(body);
        body.AddChild(_owner); body.AddChild(_items); body.AddChild(_description); body.AddChild(_status);
        var actions = new HBoxContainer(); actions.AddThemeConstantOverride("separation", 12); body.AddChild(actions);
        actions.AddChild(_approach); actions.AddChild(_one); actions.AddChild(_stack); body.AddChild(_bag);
        _items.SetColumnTitle(0, "Item"); _items.SetColumnTitle(1, "Restante"); _items.SetColumnExpand(1, false); _items.SetColumnCustomMinimumWidth(1, 90);
        _items.ItemSelected += ShowSelected;
        _one.Pressed += () => { if (!_one.Disabled) TakeRequested?.Invoke(SelectedKey, 1); };
        _stack.Pressed += () => { if (!_stack.Disabled && _lines.TryGetValue(SelectedKey, out var line)) TakeRequested?.Invoke(SelectedKey, line.Quantity); };
        _approach.Pressed += () => { if (!_approach.Disabled) ApproachRequested?.Invoke(); };
    }
    public void Refresh(string owner, ContainerItem[] lines, Func<MapObject, PrototypeText?> text, Func<string, int, ItemTakeStatus> canTake, long carried, bool canApproach)
    {
        _owner.Text = owner; _text = text; _canTake = canTake; _canApproach = canApproach;
        _bag.Text = $"Bolsa: {carried} {(carried == 1 ? "unidade" : "unidades")} · K para salvar depois de fechar";
        string selected = SelectedKey; _lines.Clear(); _items.Clear(); TreeItem root = _items.CreateItem(); TreeItem? select = null;
        foreach (ContainerItem line in lines)
        {
            _lines.Add(line.Source.Key, line); TreeItem row = _items.CreateItem(root);
            row.SetText(0, text(line.Source)?.Name ?? "Item sem nome"); row.SetText(1, line.Quantity.ToString()); row.SetMetadata(0, line.Source.Key);
            if (line.Source.Key == selected || select == null) select = row;
        }
        SelectedKey = ""; _one.Disabled = _stack.Disabled = _approach.Disabled = true;
        _description.Text = ""; _status.Text = "Este recipiente não tem itens disponíveis neste perfil.";
        if (select != null) { select.Select(0); ShowSelected(); }
    }
    private void ShowSelected()
    {
        TreeItem? row = _items.GetSelected();
        if (row == null || !_lines.TryGetValue(row.GetMetadata(0).AsString(), out var line)) return;
        SelectedKey = line.Source.Key; _description.Text = _text!(line.Source)?.Description ?? "Descrição indisponível.";
        ItemTakeStatus status = _canTake!(SelectedKey, 1);
        _status.Text = Hint(status);
        if (status == ItemTakeStatus.TooFar && !_canApproach) _status.Text = "Sem caminho até o recipiente com os bloqueios atuais.";
        _one.Disabled = status != ItemTakeStatus.Ready;
        _stack.Disabled = status != ItemTakeStatus.Ready || _canTake(SelectedKey, line.Quantity) != ItemTakeStatus.Ready;
        _approach.Disabled = status != ItemTakeStatus.TooFar || !_canApproach;
    }
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
