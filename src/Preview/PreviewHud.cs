using System;
using System.Collections.Generic;
using Godot;

namespace FalloutRemake.Preview;

public partial class PreviewHud : CanvasLayer
{
    private readonly ResourceScope _resources = new();
    private readonly Label _status = new();
    private readonly Label _area = new() { Text = "FALLOUT 2", Modulate = new Color("e4e6df") };
    private readonly Button _save = new() { Name = "SaveAction", Text = "Salvar [K]", Disabled = true };
    private readonly Button _load = new() { Name = "LoadAction", Text = "Carregar [R]", Disabled = true };
    private readonly Button _bag = new() { Name = "BagAction", Text = "Bolsa [B]" };
    private readonly Button _cycle = new() { Name = "CycleAction", Text = "Próximo", Disabled = true, TooltipText = "Próximo objeto original no mesmo hex" };
    private readonly Button _inventory = new() { Name = "InventoryAction", Text = "Inventário [I]", Disabled = true,
        TooltipText = "Consultar o inventário original do objeto selecionado, sem transferir itens" };
    private readonly Button _focusCharacter = new() { Name = "CharacterFocusAction", Text = "Personagem [C]" };
    private readonly Button _originalFloor = new() { Name = "OriginalFloorAction", Text = "Piso original [O]", ToggleMode = true,
        TooltipText = "Comparar com o piso da instalação local" };
    private readonly Button _revealActor = new() { Name = "WallOcclusionAction", Text = "Auto [V]", ToggleMode = true, ButtonPressed = true,
        TooltipText = "Ocultar paredes que encobrem o personagem. Os bloqueios permanecem." };
    private readonly Label _selectionTitle = new();
    private readonly RichTextLabel _details = new();
    private readonly Label _counts = new();
    private readonly OptionButton _elevation = new() { Name = "ElevationSelection", CustomMinimumSize = new Vector2(0, 32) };
    private readonly Label _navigationStatus = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private string _navigationMessage = "";
    private string? _routeHint;
    private readonly Button _stop = new() { Name = "StopWalkingAction", Text = "Parar [Esc]", Disabled = true };
    private readonly CheckButton _grid = new() { Name = "NavigationGridAction", Text = "Grade [G]", ButtonPressed = true };
    private readonly Button _frameRoute = new() { Name = "FrameRouteAction", Text = "Ver percurso [T]", Disabled = true };
    private readonly Button _loot = new() { Name = "LootAction", Text = "Recolher [J]", Disabled = true };
    private readonly Button _useClock = new() { Name = "UseClockAction", Text = "Tempo · ensaio", Disabled = true,
        TooltipText = "Consultar e avançar manualmente os prazos dos explosivos posicionados" };
    private readonly Button _walkMode = new() { Text = "Caminhar [M]", CustomMinimumSize = new Vector2(0, 30) };
    private readonly Button _resetWalker = new() { Text = "Reiniciar prévia", CustomMinimumSize = new Vector2(0, 30) };
    private readonly Button _doorUse = new() { Text = "Usar porta [U]", Disabled = true, CustomMinimumSize = new Vector2(0, 30) };
    private readonly Button _lockpick = new() { Name = "LockpickAction", Text = "Arrombar [L]", Disabled = true, CustomMinimumSize = new Vector2(0, 30) };
    private readonly Label _character = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Button _exit = new() { Name = "ExitAction", Text = "Passar [Enter]", Visible = false, Disabled = true };
    private PanelContainer? _navigationPanel;
    public Action? WalkModeRequested, ResetWalkerRequested;
    public Action? DoorUseRequested;
    public Action? LockpickRequested;
    public Action? ExitRequested;
    public Action? SaveRequested, LoadRequested;
    public Action? CharacterFocusRequested;
    public Action<bool>? OriginalFloorRequested;
    public Action? StopRequested;
    public Action<bool>? RevealActorRequested;
    public Action? InventoryRequested;
    public Action? SpeechRequested;
    private readonly Button _speech = new() { Name = "SpeechAction", Text = "Conversa · ensaio", Visible = false, Disabled = true };
    public Action? NpcApproachRequested;
    private readonly Button _npcApproach = new() { Name = "NpcApproachAction", Text = "Chegar ao NPC", Visible = false, Disabled = true };
    public Action<bool>? NavigationGridRequested;
    public Action? FrameRouteRequested;
    public Action? LootRequested;
    public Action? BagRequested;
    public Action? UseClockRequested;
    public Action? OpenRequested, FrameRequested, EntryRequested, CycleRequested;
    public Action<int>? ElevationRequested;
    public Action<string, bool>? FilterChanged;
    public readonly Dictionary<string, bool> Filters = new()
    {
        ["floor"] = true, ["wall"] = true, ["scenery"] = true, ["critter"] = true,
        ["exit"] = true, ["marker"] = false, ["hidden"] = false
    };
    public bool MouseOverPanel => MouseOverPanelAt(GetViewport().GetMousePosition());
    public bool MouseOverPanelAt(Vector2 screen) => screen.X > GetViewport().GetVisibleRect().Size.X - 306
        || screen.Y < 78 || screen.Y > GetViewport().GetVisibleRect().Size.Y - 56
        || (_navigationPanel?.GetGlobalRect().HasPoint(screen) ?? false);

    public override void _Ready()
    {
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        var theme = _resources.Own(new Theme { DefaultFontSize = 14 });
        theme.SetColor("font_color", "Label", new Color("d9dfdb"));
        theme.SetColor("font_color", "CheckBox", new Color("b5c3bd"));
        theme.SetStylebox("normal", "Button", Box("253531", 6));
        theme.SetStylebox("hover", "Button", Box("3a5146", 6));
        theme.SetStylebox("pressed", "Button", Box("45664f", 6));
        root.Theme = theme;

        PanelContainer header = Panel(root, 16, 12, -16, 72, true);
        var headerScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        header.AddChild(headerScroll);
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; row.AddThemeConstantOverride("separation", 8); headerScroll.AddChild(row);
        var badge = new Label { Text = "F2", VerticalAlignment = VerticalAlignment.Center };
        badge.AddThemeColorOverride("font_color", new Color("c2a66d")); badge.AddThemeFontSizeOverride("font_size", 26); row.AddChild(badge);
        var heading = new VBoxContainer { CustomMinimumSize = new(170, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; row.AddChild(heading);
        _area.ClipText = true; _status.ClipText = true; heading.AddChild(_area);
        _status.Text = "Prévia espacial · carregando os dados…"; _status.AddThemeFontSizeOverride("font_size", 12); _status.Modulate = new Color("8fa69a"); heading.AddChild(_status);
        Button(row, "Abrir dados", () => OpenRequested?.Invoke());
        Button(row, "Entrada  [F]", () => EntryRequested?.Invoke());
        Button(row, "Ver tudo  [Home]", () => FrameRequested?.Invoke());
        _focusCharacter.Pressed += () => CharacterFocusRequested?.Invoke(); row.AddChild(_focusCharacter);
        _bag.Pressed += () => BagRequested?.Invoke(); row.AddChild(_bag);
        _save.Pressed += () => SaveRequested?.Invoke(); row.AddChild(_save);
        _load.Pressed += () => LoadRequested?.Invoke(); row.AddChild(_load);

        var side = new PanelContainer { AnchorLeft = 1, AnchorRight = 1, AnchorTop = 0, AnchorBottom = 1,
            OffsetLeft = -300, OffsetRight = -16, OffsetTop = 84, OffsetBottom = -64 };
        side.AddThemeStyleboxOverride("panel", Box("15211ff2", 10)); root.AddChild(side);
        var sideSections = new VBoxContainer(); sideSections.AddThemeConstantOverride("separation", 8); side.AddChild(sideSections);
        var filtersScroll = new ScrollContainer { Name = "FiltersScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new(0, 140) }; sideSections.AddChild(filtersScroll);
        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; column.AddThemeConstantOverride("separation", 6); filtersScroll.AddChild(column);
        Label overline = new() { Text = "EXPLORAR O CENÁRIO", Modulate = new Color("c2a66d") }; overline.AddThemeFontSizeOverride("font_size", 12); column.AddChild(overline);
        for (int i = 0; i < 3; i++) _elevation.AddItem($"Elevação {i}");
        _elevation.ItemSelected += index => ElevationRequested?.Invoke((int)index); column.AddChild(_elevation);
        AddContainerLocator(column);
        _counts.AddThemeFontSizeOverride("font_size", 12); _counts.Modulate = new Color("8fa69a"); column.AddChild(_counts);
        AddTempleProgress(column);
        column.AddChild(new HSeparator());
        _originalFloor.Toggled += enabled => OriginalFloorRequested?.Invoke(enabled); column.AddChild(_originalFloor);
        AddNatureControls(column);
        AddFilter(column, "floor", "Piso", "68766d");
        _revealActor.Toggled += enabled => RevealActorRequested?.Invoke(enabled);
        AddFilter(column, "wall", "Paredes", "b6a17d").AddChild(_revealActor);
        AddFilter(column, "scenery", "Cenário e itens", "a9865b");
        AddFilter(column, "critter", "Criaturas", "c87761");
        AddFilter(column, "exit", "Portas e saídas", "65bea9");
        AddFilter(column, "marker", "Marcadores técnicos", "7b87a0");
        AddFilter(column, "hidden", "Incluir objetos ocultos", "8b9690");
        var entityLegend = new RichTextLabel { Name = "EntityLegend", BbcodeEnabled = true, FitContent = true, ScrollActive = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = "[color=#e18b88]▰ Obstáculo[/color]   [color=#ffd166]● Luz[/color]\n[color=#45c9b2]▣ Com itens[/color]   [color=#a3b3bf]□ Vazio[/color]\n[color=#b09add]◇ Interação especial[/color]" };
        entityLegend.AddThemeFontSizeOverride("normal_font_size", 12); sideSections.AddChild(entityLegend);
        column.AddChild(new HSeparator());
        _selectionTitle.Text = "Nenhum elemento selecionado";
        _selectionTitle.AutowrapMode = TextServer.AutowrapMode.WordSmart; _selectionTitle.MaxLinesVisible = 2; _selectionTitle.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _selectionTitle.AddThemeColorOverride("font_color", new Color("dcc390")); sideSections.AddChild(_selectionTitle);
        _details.BbcodeEnabled = false; _details.SizeFlagsVertical = Control.SizeFlags.ExpandFill; _details.SizeFlagsStretchRatio = 1.2f;
        _details.CustomMinimumSize = new Vector2(0, 70); _details.AddThemeFontSizeOverride("normal_font_size", 13);
        _details.Text = "Clique em um volume para consultar os IDs e a posição do objeto original.\n\nAs formas e alturas são provisórias."; sideSections.AddChild(_details);
        var selectionActions = new HFlowContainer { Name = "SelectionActions" }; sideSections.AddChild(selectionActions);
        _cycle.Pressed += () => CycleRequested?.Invoke(); selectionActions.AddChild(_cycle);
        _inventory.Pressed += () => InventoryRequested?.Invoke(); selectionActions.AddChild(_inventory);
        _speech.Pressed += () => { if (!_speech.Disabled) SpeechRequested?.Invoke(); }; selectionActions.AddChild(_speech);
        _npcApproach.Pressed += () => { if (!_npcApproach.Disabled) NpcApproachRequested?.Invoke(); }; selectionActions.AddChild(_npcApproach);

        _navigationPanel = new PanelContainer { AnchorTop = 1, AnchorBottom = 1,
            AnchorRight = 1, OffsetLeft = 16, OffsetRight = -316, OffsetTop = -272, OffsetBottom = -64 };
        _navigationPanel.AddThemeStyleboxOverride("panel", Box("15211ff2", 10)); root.AddChild(_navigationPanel);
        var navigation = new VBoxContainer(); navigation.AddThemeConstantOverride("separation", 6); _navigationPanel.AddChild(navigation);
        var actions = new HFlowContainer(); actions.AddThemeConstantOverride("h_separation", 6); navigation.AddChild(actions);
        _walkMode.Pressed += () => WalkModeRequested?.Invoke(); actions.AddChild(_walkMode);
        _resetWalker.Pressed += () => ResetWalkerRequested?.Invoke(); actions.AddChild(_resetWalker);
        _doorUse.Pressed += () => DoorUseRequested?.Invoke(); actions.AddChild(_doorUse);
        _lockpick.Pressed += () => LockpickRequested?.Invoke(); actions.AddChild(_lockpick);
        _exit.Pressed += () => ExitRequested?.Invoke(); actions.AddChild(_exit);
        _character.AddThemeFontSizeOverride("font_size", 12); _character.Modulate = new Color("d2c397"); navigation.AddChild(_character);
        var movement = new HBoxContainer(); navigation.AddChild(movement);
        _navigationStatus.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _navigationStatus.AddThemeFontSizeOverride("font_size", 12); movement.AddChild(_navigationStatus);
        _stop.Pressed += () => StopRequested?.Invoke(); movement.AddChild(_stop);
        var legend = new Label { Text = "Verde: alcançável  ·  Vermelho: bloqueio  ·  Âmbar: saída", Modulate = new Color("9fb2a8") };
        var guide = new HFlowContainer(); navigation.AddChild(legend); navigation.AddChild(guide);
        legend.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        legend.AddThemeFontSizeOverride("font_size", 12); legend.ClipText = true;
        _grid.Toggled += enabled => NavigationGridRequested?.Invoke(enabled); guide.AddChild(_grid);
        _frameRoute.Pressed += () => FrameRouteRequested?.Invoke(); guide.AddChild(_frameRoute);
        _loot.Pressed += () => LootRequested?.Invoke(); guide.AddChild(_loot);
        _useClock.Pressed += () => UseClockRequested?.Invoke(); guide.AddChild(_useClock);
        guide.AddChild(_templeReveal);
        guide.AddChild(_templeRoute); _templeRoute.Toggled += value => TempleRouteRequested?.Invoke(value);

        var footer = Panel(root, 16, -52, -16, -12, true);
        footer.AnchorTop = 1; footer.AnchorBottom = 1;
        var help = new Label { Text = "W A S D  mover    ·    Botão direito  arrastar    ·    Roda  zoom    ·    Q / E  girar    ·    M  caminhar    ·    U  porta    ·    L  arrombar    ·    Enter  passar",
            HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color("9fb2a8") };
        help.ClipText = true; help.AddThemeFontSizeOverride("font_size", 12); footer.AddChild(help);
    }

    public void Status(string text) => _status.Text = text;
    public void Area(string text) => _area.Text = text;
    public void CameraFollow(bool following) => _focusCharacter.Text = following ? "Seguindo [C]" : "Personagem [C]";
    public void OriginalFloor(bool enabled) => _originalFloor.SetPressedNoSignal(enabled);
    public void RevealActor(bool enabled) => _revealActor.SetPressedNoSignal(enabled);
    public void StopAction(bool moving, bool stopping)
    { _stop.Disabled = !moving || stopping; _stop.Text = stopping ? "Parando…" : "Parar [Esc]"; }
    public void CheckpointActions(bool saveAvailable, bool loadAvailable)
    { _save.Disabled = !saveAvailable; _load.Disabled = !loadAvailable; }
    public void Counts(string text) => _counts.Text = text;
    public void Elevation(int elevation) => _elevation.Select(elevation);
    public void Navigation(bool available, bool walkingMode, string status)
    {
        _walkMode.Disabled = !available; _resetWalker.Disabled = false;
        _walkMode.Text = walkingMode ? "Inspecionar [M]" : "Caminhar [M]";
        _navigationMessage = status; _navigationStatus.Text = _routeHint ?? status;
    }
    public void Select(string title, string text) { _selectionTitle.Text = title; _selectionTitle.TooltipText = title; _details.Text = text; }
    public string SelectionHeading => _selectionTitle.Text;
    public string SelectionDetails => _details.Text;
    public void CycleAction(bool enabled) => _cycle.Disabled = !enabled;
    public void InventoryAction(bool enabled) => _inventory.Disabled = !enabled;
    public void SpeechAction(bool visible, bool enabled, string hint)
    { _speech.Visible = visible; _speech.Disabled = !enabled; _speech.TooltipText = hint; }
    public void NpcApproachAction(bool visible, bool enabled, string hint)
    { _npcApproach.Visible = visible; _npcApproach.Disabled = !enabled; _npcApproach.TooltipText = hint; }
    public void NavigationGrid(bool enabled) => _grid.SetPressedNoSignal(enabled);
    public void FrameRouteAction(bool enabled) => _frameRoute.Disabled = !enabled;
    public void LootAction(bool enabled) => _loot.Disabled = !enabled;
    public void UseClockAction(bool enabled, bool pending) { _useClock.Disabled = !enabled; _useClock.Text = pending ? "Tempo · pendente" : "Tempo · ensaio"; }
    public Rect2 CameraSafeRect => new(32, 94, Math.Max(0, GetViewport().GetVisibleRect().Size.X - 354),
        Math.Max(0, (_navigationPanel?.GetGlobalRect().Position.Y ?? GetViewport().GetVisibleRect().Size.Y - 272) - 110));
    public bool NavigationFits => _navigationPanel != null && _navigationPanel.GetGlobalRect().End.Y <= GetViewport().GetVisibleRect().Size.Y - 56
        && _navigationPanel.GetGlobalRect().End.X <= GetViewport().GetVisibleRect().Size.X - 306;
    public string? RouteHintText => _routeHint;
    public void RouteHint(string? text) { _routeHint = text; _navigationStatus.Text = text ?? _navigationMessage; }
    public void DoorAction(bool enabled, bool open, string hint)
    {
        _doorUse.Disabled = !enabled; _doorUse.Text = open ? "Fechar porta [U]" : "Abrir porta [U]";
        _doorUse.TooltipText = hint;
    }
    public void LockpickAction(bool enabled, bool locked, string hint, string character)
    {
        _lockpick.Disabled = !enabled; _lockpick.Text = locked ? "Arrombar [L]" : "Retrancar [L]";
        _lockpick.TooltipText = hint; _character.Text = character;
    }
    public void ExitAction(bool visible, bool enabled, string hint)
    { _exit.Visible = visible; _exit.Disabled = !enabled; _exit.TooltipText = hint; }

    private HBoxContainer AddFilter(VBoxContainer column, string key, string label, string color)
    {
        var line = new HBoxContainer(); column.AddChild(line);
        line.AddChild(new Label { Text = "●", Modulate = new Color(color), CustomMinimumSize = new Vector2(18, 0) });
        var check = new CheckBox { Text = label, ButtonPressed = Filters[key], SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        check.AddThemeFontSizeOverride("font_size", 13); check.Toggled += value => { Filters[key] = value; FilterChanged?.Invoke(key, value); }; line.AddChild(check);
        return line;
    }
    private static Button Button(Container container, string text, Action pressed)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 32), MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        button.Pressed += pressed; container.AddChild(button);
        return button;
    }
    private PanelContainer Panel(Control parent, float left, float top, float right, float bottom, bool stretch)
    {
        var panel = new PanelContainer { AnchorRight = stretch ? 1 : 0, OffsetLeft = left, OffsetTop = top, OffsetRight = right, OffsetBottom = bottom };
        panel.AddThemeStyleboxOverride("panel", Box("15211ff2", 10)); parent.AddChild(panel); return panel;
    }
    private StyleBoxFlat Box(string color, int radius) => _resources.Own(new StyleBoxFlat
    {
        BgColor = new Color(color), CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        ContentMarginLeft = radius == 6 ? 8 : 16, ContentMarginRight = radius == 6 ? 8 : 16, ContentMarginTop = radius == 6 ? 5 : 10, ContentMarginBottom = radius == 6 ? 5 : 10
    });
    public override void _ExitTree() => _resources.Dispose();
}
