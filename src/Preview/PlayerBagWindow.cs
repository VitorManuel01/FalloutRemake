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
    private readonly TextureRect _art = new() { CustomMinimumSize = new Vector2(96, 100), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = Control.TextureFilterEnum.Nearest, Visible = false, TooltipText = "Imagem original do item" };
    private readonly RichTextLabel _detail = new() { BbcodeEnabled = false, CustomMinimumSize = new Vector2(0, 100), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
    private readonly Label _summary = new();
    private readonly Button _returnOne = new() { Name = "ReturnOneAction", Text = "Devolver 1", Disabled = true },
        _returnStack = new() { Name = "ReturnStackAction", Text = "Devolver esta pilha", Disabled = true },
        _returnOrigin = new() { Name = "ReturnOriginAction", Text = "Devolver tudo à origem", Disabled = true },
        _approach = new() { Name = "BagOriginApproachAction", Text = "Voltar ao recipiente", Disabled = true };
    private readonly RichTextLabel _returnStatus = new() { BbcodeEnabled = false, CustomMinimumSize = new Vector2(860, 64) };
    private readonly Dictionary<string, CarriedItem> _lines = new();
    private readonly QuantityTransfer _quantity = new() { Name = "ReturnQuantity", Visible = false };
    private readonly Button _prepareExplosive = new() { Name = "PrepareExplosiveAction", Text = "Posicionar · ensaio", Visible = false, Disabled = true };
    private readonly Button _useDrug = new() { Name = "UseDrugAction", Text = "Usar (8–18 HP)", Visible = false, Disabled = true };
    private Func<CarriedItem, DrugUseStatus>? _canUseDrug;
    public Action<CarriedItem>? UseDrugRequested;
    public Action<CarriedItem>? PrepareExplosiveRequested;
    private Func<CarriedItem, ExplosiveProposalStatus>? _canPrepareExplosive;
    private readonly Button _barrierApproach = new() { Name = "BarrierPlacementApproach", Text = "Ir à barreira", Visible = false, Disabled = true };
    private Func<CarriedItem, bool>? _canApproachBarrier;
    private Func<ExplosiveProposalStatus, string>? _preparationHint;
    public Action<CarriedItem>? BarrierApproachRequested;
    private Func<CarriedItem, PrototypeText?>? _text;
    private Func<int, string>? _mapName;
    private Func<CarriedItem, int, ItemTakeStatus>? _canReturn;
    private Func<CarriedItem, ItemTakeStatus>? _canReturnAll;
    private Func<CarriedItem, ItemOriginPlan?>? _originRoute;
    public Action<CarriedItem, int>? ReturnRequested;
    public Action<CarriedItem>? ReturnAllRequested;
    public Action<CarriedItem>? ApproachRequested;
    public CarriedItem? SelectedItem { get; private set; }
    public long TotalQuantity { get; private set; }
    public int RowCount => _lines.Count;
    public string DetailText => _detail.Text;
    public string SummaryText => _summary.Text;
    public string SelectedWeightText => _items.GetSelected()?.GetText(2) ?? "";
    public int? SelectedArtFid => _art.Texture?.GetMeta("inventory_fid").AsInt32();
    public string ReturnStatusText => _returnStatus.Text;
    public bool ReadableLayout => _items.Size.X >= 800 && _items.Size.Y >= 200;
    public override void _Ready()
    {
        Title = "Bolsa do personagem"; Exclusive = true; Transient = true; GetOkButton().Text = "Fechar";
        WindowInput += input => { if (input is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.B }) { SetInputAsHandled(); EmitSignal(SignalName.Canceled); } };
        var scroll = new ScrollContainer { Name = "DialogScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); AddChild(scroll);
        var body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill }; scroll.AddChild(body);
        scroll.OffsetLeft = 20; scroll.OffsetRight = -20; scroll.OffsetTop = 20; scroll.OffsetBottom = -60; body.AddThemeConstantOverride("separation", 12);
        body.AddChild(_summary); body.AddChild(_items); var detail = new HBoxContainer(); detail.AddThemeConstantOverride("separation", 16);
        detail.AddChild(_art); detail.AddChild(_detail); body.AddChild(detail);
        body.AddChild(_returnStatus); var actions = new HFlowContainer(); body.AddChild(actions); actions.AddChild(_approach); actions.AddChild(_returnOne); actions.AddChild(_returnStack); actions.AddChild(_returnOrigin);
        actions.AddChild(_quantity); _quantity.Requested = qty => { if (SelectedItem != null) ReturnRequested?.Invoke(SelectedItem, qty); };
        actions.AddChild(_useDrug);
        actions.AddChild(_prepareExplosive);
        actions.AddChild(_barrierApproach);
        _useDrug.Pressed += () => { if (!_useDrug.Disabled && SelectedItem != null) UseDrugRequested?.Invoke(SelectedItem); };
        _barrierApproach.Pressed += () => { if (!_barrierApproach.Disabled && SelectedItem != null) BarrierApproachRequested?.Invoke(SelectedItem); };
        _prepareExplosive.Pressed += () => { if (!_prepareExplosive.Disabled && SelectedItem != null) PrepareExplosiveRequested?.Invoke(SelectedItem); };
        _approach.Pressed += () => { if (!_approach.Disabled && SelectedItem != null) ApproachRequested?.Invoke(SelectedItem); };
        _returnOne.Pressed += () => { if (!_returnOne.Disabled && SelectedItem != null) ReturnRequested?.Invoke(SelectedItem, 1); };
        _returnStack.Pressed += () => { if (!_returnStack.Disabled && SelectedItem != null) ReturnRequested?.Invoke(SelectedItem, SelectedItem.Quantity); };
        _returnOrigin.Pressed += () => { if (!_returnOrigin.Disabled && SelectedItem != null) ReturnAllRequested?.Invoke(SelectedItem); };
        body.AddChild(new Label { Text = "K salva a bolsa junto da posição, portas e XP, depois de fechar esta janela." });
        _items.SetColumnTitle(0, "Item"); _items.SetColumnTitle(1, "Quantidade"); _items.SetColumnTitle(2, "Peso"); _items.SetColumnTitle(3, "Encontrado em");
        _items.SetColumnExpand(1, false); _items.SetColumnCustomMinimumWidth(1, 100);
        _items.SetColumnExpand(2, false); _items.SetColumnCustomMinimumWidth(2, 110);
        _items.SetColumnExpand(3, false); _items.SetColumnCustomMinimumWidth(3, 220);
        _items.ItemSelected += ShowSelected;
    }
    public void Refresh(CarriedItem[] items, Func<CarriedItem, PrototypeText?> text, Func<int, string> mapName, long? carriedWeight, int capacity,
        Func<CarriedItem, long?> unitWeight, Func<CarriedItem, int, ItemTakeStatus>? canReturn = null, Func<CarriedItem, ItemOriginPlan?>? originRoute = null,
        Func<CarriedItem, Texture2D?>? artwork = null, Func<CarriedItem, ExplosiveProposalStatus>? canPrepareExplosive = null,
        Func<ExplosiveProposalStatus, string>? preparationHint = null, Func<CarriedItem, bool>? canApproachBarrier = null,
        Func<CarriedItem, ItemTakeStatus>? canReturnAll = null, Func<CarriedItem, DrugUseStatus>? canUseDrug = null, string? carrier = null)
    {
        string? selected = SelectedItem == null ? null : RowKey(SelectedItem);
        _text = text; _mapName = mapName; _canReturn = canReturn; _originRoute = originRoute; _canPrepareExplosive = canPrepareExplosive;
        _preparationHint = preparationHint; _canApproachBarrier = canApproachBarrier; _canReturnAll = canReturnAll; _canUseDrug = canUseDrug;
        _lines.Clear(); _items.Clear(); TreeItem root = _items.CreateItem(); TreeItem? select = null;
        TotalQuantity = items.Sum(i => (long)i.Quantity);
        _summary.Text = items.Length == 0 ? "Sua bolsa está vazia." : $"{TotalQuantity} {(TotalQuantity == 1 ? "unidade" : "unidades")} em {items.Length} {(items.Length == 1 ? "pilha" : "pilhas")}";
        _summary.Text += $" · Carga: {CarryWeightText.Load(carriedWeight, capacity)} · {carrier ?? $"Força {CarryWeight.TestStrength} (perfil de teste)"}";
        foreach (CarriedItem item in items)
        {
            string key = RowKey(item); _lines.Add(key, item); TreeItem row = _items.CreateItem(root);
            row.SetText(0, text(item)?.Name ?? "Item sem nome"); row.SetText(1, item.Quantity.ToString()); row.SetText(2, CarryWeightText.Stack(unitWeight(item), item.Quantity));
            row.SetText(3, mapName(item.Map)); row.SetMetadata(0, key);
            row.SetTooltipText(0, $"ID {item.Source.Header.Id} · PID {item.Source.Header.Pid} · {item.Source.Key}");
            Texture2D? icon = artwork?.Invoke(item); if (icon != null) { row.SetIcon(0, icon); row.SetIconMaxWidth(0, 28); }
            if (key == selected || select == null) select = row;
        }
        SelectedItem = null; _art.Texture = null; _art.Visible = false;
        _detail.Text = items.Length == 0 ? "Recolha itens de recipientes com J. Itens recolhidos acompanham você ao trocar de área." : "Selecione um item para consultar a descrição.";
        _returnOne.Disabled = _returnStack.Disabled = _returnOrigin.Disabled = _approach.Disabled = true; _returnOrigin.TooltipText = "";
        _returnStatus.Text = ""; _quantity.Configure(0, "Devolver quantidade", null);
        _prepareExplosive.Visible = false; _prepareExplosive.Disabled = true;
        _useDrug.Visible = false; _useDrug.Disabled = true;
        _barrierApproach.Visible = false; _barrierApproach.Disabled = true;
        if (select != null) { select.Select(0); ShowSelected(); }
    }
    public static string DrugHint(DrugUseStatus status) => status switch
    {
        DrugUseStatus.Ready => "Tomar uma unidade agora: cura de 8 a 18 HP (sem custo de AP fora do combate). O que passar do máximo se perde.",
        DrugUseStatus.Busy => "Pare de andar para usar o item.",
        DrugUseStatus.InCombat => "Em combate, use H (4 AP).",
        DrugUseStatus.NoItem => "Você já não carrega esta unidade.",
        _ => "Este item ainda não tem uso modelado."
    };
    private static string RowKey(CarriedItem item) => $"{item.Map}:{item.Source.Key}";
    private void ShowSelected()
    {
        TreeItem? selected = _items.GetSelected(); if (selected == null || !_lines.TryGetValue(selected.GetMetadata(0).AsString(), out var item)) return;
        SelectedItem = item; PrototypeText? text = _text!(item);
        ExplosiveProposalStatus preparation = _canPrepareExplosive?.Invoke(item) ?? ExplosiveProposalStatus.Unsupported;
        _prepareExplosive.Visible = item.Source.Header.Pid == ExplosiveUseProposal.InactivePid;
        _prepareExplosive.Disabled = preparation != ExplosiveProposalStatus.Ready;
        _prepareExplosive.TooltipText = preparation == ExplosiveProposalStatus.Ready ? "Escolher prazo e posicionar uma unidade em ensaio controlado" : _preparationHint?.Invoke(preparation) ?? ExplosivePreparationWindow.Hint(preparation);
        _barrierApproach.Visible = _prepareExplosive.Visible;
        DrugUseStatus drug = _canUseDrug?.Invoke(item) ?? DrugUseStatus.Unsupported;
        _useDrug.Visible = item.Source.Header.Pid == HealingDrugProfile.Pid;
        _useDrug.Disabled = drug != DrugUseStatus.Ready;
        _useDrug.TooltipText = DrugHint(drug);
        _barrierApproach.Disabled = _canApproachBarrier?.Invoke(item) != true;
        _art.Texture = selected.GetIcon(0); _art.Visible = _art.Texture != null;
        _detail.Text = $"{text?.Name ?? "Item sem nome"}\n\n{text?.Description ?? "Descrição indisponível."}\n\nEncontrado em {_mapName!(item.Map)}.";
        ItemTakeStatus status = _canReturn?.Invoke(item, 1) ?? ItemTakeStatus.Unsupported;
        _returnStatus.Text = status == ItemTakeStatus.Ready ? "Você pode devolver ao recipiente de origem, sem usar o item."
            : status == ItemTakeStatus.Unavailable ? "Volte à área e elevação de origem para devolver este item."
            : status == ItemTakeStatus.TooFar ? "Aproxime-se do recipiente de origem para devolver."
            : status == ItemTakeStatus.Empty ? "Você já não carrega esta pilha." : ContainerLootWindow.Hint(status);
        _returnOne.Disabled = status != ItemTakeStatus.Ready;
        _returnStack.Disabled = status != ItemTakeStatus.Ready || _canReturn!(item, item.Quantity) != ItemTakeStatus.Ready;
        ItemTakeStatus allStatus = _canReturnAll?.Invoke(item) ?? ItemTakeStatus.Unsupported;
        _returnOrigin.Disabled = allStatus != ItemTakeStatus.Ready;
        long originQuantity = _lines.Values.Where(i => i.Map == item.Map && i.OwnerKey == item.OwnerKey).Sum(i => (long)i.Quantity);
        _returnOrigin.TooltipText = allStatus == ItemTakeStatus.Ready
            ? $"Devolver {originQuantity} unidade(s) ao recipiente de origem deste item. Itens de outras origens continuam na bolsa."
            : "Devolver tudo à origem: " + ContainerLootWindow.Hint(allStatus);
        ItemOriginPlan? plan = status is ItemTakeStatus.TooFar or ItemTakeStatus.Unavailable or ItemTakeStatus.AtExit ? _originRoute?.Invoke(item) : null;
        _approach.Disabled = plan == null;
        _approach.Text = plan?.Legs[0].Destination != null ? (plan.Legs[0].Tiles.Length == 1 ? "Ver passagem" : "Ir à passagem") : "Voltar ao recipiente";
        if (plan?.Legs[0].Destination is ActorLocation destination)
            _returnStatus.Text += $"\nRota: {plan.WalkingSteps} passos e {plan.Transitions} {(plan.Transitions == 1 ? "passagem" : "passagens")}. Vá até {_mapName!(destination.Map)} e use Enter. Abra B depois para continuar.";
        if (status == ItemTakeStatus.TooFar && _approach.Disabled) _returnStatus.Text = "Sem caminho até o recipiente com os bloqueios atuais.";
        _quantity.Configure(item.Quantity, "Devolver quantidade", qty => _canReturn?.Invoke(item, qty) ?? ItemTakeStatus.Unsupported);
    }
}
