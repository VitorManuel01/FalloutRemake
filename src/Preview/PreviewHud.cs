using System;
using System.Collections.Generic;
using Godot;

namespace FalloutRemake.Preview;

public partial class PreviewHud : CanvasLayer
{
    private readonly ResourceScope _resources = new();
    private readonly Label _status = new();
    private readonly Label _selectionTitle = new();
    private readonly RichTextLabel _details = new();
    private readonly Label _counts = new();
    private readonly OptionButton _elevation = new() { CustomMinimumSize = new Vector2(0, 32) };
    private readonly Label _navigationStatus = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Button _walkMode = new() { Text = "Caminhar [M]", CustomMinimumSize = new Vector2(0, 30) };
    private readonly Button _resetWalker = new() { Text = "Reiniciar prévia", CustomMinimumSize = new Vector2(0, 30) };
    private readonly Button _doorUse = new() { Text = "Usar porta [U]", Disabled = true, CustomMinimumSize = new Vector2(0, 30) };
    private readonly Button _lockpick = new() { Name = "LockpickAction", Text = "Arrombar [L]", Disabled = true, CustomMinimumSize = new Vector2(0, 30) };
    private readonly Label _character = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private PanelContainer? _navigationPanel;
    public Action? WalkModeRequested, ResetWalkerRequested;
    public Action? DoorUseRequested;
    public Action? LockpickRequested;
    public Action? OpenRequested, FrameRequested, EntryRequested, CycleRequested;
    public Action<int>? ElevationRequested;
    public Action<string, bool>? FilterChanged;
    public readonly Dictionary<string, bool> Filters = new()
    {
        ["floor"] = true, ["wall"] = true, ["scenery"] = true, ["critter"] = true,
        ["exit"] = true, ["marker"] = false, ["hidden"] = false
    };
    public bool MouseOverPanel => GetViewport().GetMousePosition().X > GetViewport().GetVisibleRect().Size.X - 306
        || GetViewport().GetMousePosition().Y < 78 || GetViewport().GetMousePosition().Y > GetViewport().GetVisibleRect().Size.Y - 56
        || (_navigationPanel?.GetGlobalRect().HasPoint(GetViewport().GetMousePosition()) ?? false);

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
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 22); header.AddChild(row);
        var badge = new Label { Text = "F2", VerticalAlignment = VerticalAlignment.Center };
        badge.AddThemeColorOverride("font_color", new Color("c2a66d")); badge.AddThemeFontSizeOverride("font_size", 26); row.AddChild(badge);
        var heading = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; row.AddChild(heading);
        heading.AddChild(new Label { Text = "TEMPLE OF TRIALS", Modulate = new Color("e4e6df") });
        _status.Text = "Prévia espacial · carregando os dados…"; _status.AddThemeFontSizeOverride("font_size", 12); _status.Modulate = new Color("8fa69a"); heading.AddChild(_status);
        Button(row, "Abrir dados", () => OpenRequested?.Invoke());
        Button(row, "Entrada  [F]", () => EntryRequested?.Invoke());
        Button(row, "Ver tudo  [Home]", () => FrameRequested?.Invoke());

        var side = new PanelContainer { AnchorLeft = 1, AnchorRight = 1, AnchorTop = 0, AnchorBottom = 1,
            OffsetLeft = -300, OffsetRight = -16, OffsetTop = 84, OffsetBottom = -64 };
        side.AddThemeStyleboxOverride("panel", Box("15211ff2", 10)); root.AddChild(side);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 12); side.AddChild(column);
        Label overline = new() { Text = "EXPLORAR O CENÁRIO", Modulate = new Color("c2a66d") }; overline.AddThemeFontSizeOverride("font_size", 12); column.AddChild(overline);
        for (int i = 0; i < 3; i++) _elevation.AddItem($"Elevação {i}");
        _elevation.ItemSelected += index => ElevationRequested?.Invoke((int)index); column.AddChild(_elevation);
        _counts.AddThemeFontSizeOverride("font_size", 12); _counts.Modulate = new Color("8fa69a"); column.AddChild(_counts);
        column.AddChild(new HSeparator());
        AddFilter(column, "floor", "Piso", "68766d");
        AddFilter(column, "wall", "Paredes", "b6a17d");
        AddFilter(column, "scenery", "Cenário e itens", "a9865b");
        AddFilter(column, "critter", "Criaturas", "c87761");
        AddFilter(column, "exit", "Portas e saídas", "65bea9");
        AddFilter(column, "marker", "Marcadores técnicos", "7b87a0");
        AddFilter(column, "hidden", "Incluir objetos ocultos", "8b9690");
        column.AddChild(new HSeparator());
        _selectionTitle.Text = "Nenhum elemento selecionado";
        _selectionTitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _selectionTitle.AddThemeColorOverride("font_color", new Color("dcc390")); column.AddChild(_selectionTitle);
        _details.BbcodeEnabled = false; _details.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _details.CustomMinimumSize = new Vector2(0, 90); _details.AddThemeFontSizeOverride("normal_font_size", 13);
        _details.Text = "Clique em um volume para consultar os IDs e a posição do objeto original.\n\nAs formas e alturas são provisórias."; column.AddChild(_details);
        Button(column, "Próximo no mesmo hex", () => CycleRequested?.Invoke());

        _navigationPanel = new PanelContainer { AnchorTop = 1, AnchorBottom = 1,
            OffsetLeft = 16, OffsetRight = 760, OffsetTop = -212, OffsetBottom = -64 };
        _navigationPanel.AddThemeStyleboxOverride("panel", Box("15211ff2", 10)); root.AddChild(_navigationPanel);
        var navigation = new VBoxContainer(); navigation.AddThemeConstantOverride("separation", 6); _navigationPanel.AddChild(navigation);
        var actions = new HBoxContainer(); actions.AddThemeConstantOverride("separation", 12); navigation.AddChild(actions);
        _walkMode.Pressed += () => WalkModeRequested?.Invoke(); actions.AddChild(_walkMode);
        _resetWalker.Pressed += () => ResetWalkerRequested?.Invoke(); actions.AddChild(_resetWalker);
        _doorUse.Pressed += () => DoorUseRequested?.Invoke(); actions.AddChild(_doorUse);
        _lockpick.Pressed += () => LockpickRequested?.Invoke(); actions.AddChild(_lockpick);
        _character.AddThemeFontSizeOverride("font_size", 12); _character.Modulate = new Color("d2c397"); navigation.AddChild(_character);
        _navigationStatus.AddThemeFontSizeOverride("font_size", 12); navigation.AddChild(_navigationStatus);
        var legend = new Label { Text = "Verde: alcançável  ·  Vermelho: bloqueio  ·  Âmbar: saída", Modulate = new Color("9fb2a8") };
        legend.AddThemeFontSizeOverride("font_size", 12); navigation.AddChild(legend);

        var footer = Panel(root, 16, -52, -16, -12, true);
        footer.AnchorTop = 1; footer.AnchorBottom = 1;
        var help = new Label { Text = "W A S D  mover    ·    Botão direito  arrastar    ·    Roda  zoom    ·    Q / E  girar    ·    M  caminhar    ·    U  porta    ·    L  arrombar",
            HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color("9fb2a8") };
        help.AddThemeFontSizeOverride("font_size", 12); footer.AddChild(help);
    }

    public void Status(string text) => _status.Text = text;
    public void Counts(string text) => _counts.Text = text;
    public void Elevation(int elevation) => _elevation.Select(elevation);
    public void Navigation(bool available, bool walkingMode, string status)
    {
        _walkMode.Disabled = !available; _resetWalker.Disabled = !available;
        _walkMode.Text = walkingMode ? "Inspecionar [M]" : "Caminhar [M]";
        _navigationStatus.Text = status;
    }
    public void Select(string title, string text) { _selectionTitle.Text = title; _details.Text = text; }
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

    private void AddFilter(VBoxContainer column, string key, string label, string color)
    {
        var line = new HBoxContainer(); column.AddChild(line);
        line.AddChild(new Label { Text = "●", Modulate = new Color(color), CustomMinimumSize = new Vector2(18, 0) });
        var check = new CheckBox { Text = label, ButtonPressed = Filters[key], SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        check.AddThemeFontSizeOverride("font_size", 13); check.Toggled += value => { Filters[key] = value; FilterChanged?.Invoke(key, value); }; line.AddChild(check);
    }
    private static void Button(Container container, string text, Action pressed)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 32), MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        button.Pressed += pressed; container.AddChild(button);
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
        ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 10, ContentMarginBottom = 10
    });
    public override void _ExitTree() => _resources.Dispose();
}
