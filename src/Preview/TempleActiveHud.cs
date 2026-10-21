using System;
using Godot;

namespace FalloutRemake.Preview;

public partial class PreviewHud
{
    private readonly VBoxContainer _templeProgress = new() { Name = "TempleProgressPanel", Visible = false };
    private readonly Label _templeState = new() { Name = "TempleProgressState", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _templeHeading = new() { Text = "SAÍDA DO NPC · PARTIDA", Modulate = new Color("c2a66d") };
    private readonly CheckButton _templeAuto = new() { Name = "TempleWorldAutomatic", Text = "Avançar automaticamente",
        TooltipText = "Ritmo provisório. Pausa com janelas, caminhada, outra elevação e ao salvar." };
    private readonly Button _templeStep = new() { Name = "TempleWorldStep", Text = "Um passo" },
        _templeLeg = new() { Name = "TempleWorldLeg", Text = "Próxima parada" },
        _templeWait = new() { Name = "TempleWorldWait", Text = "Esperar", TooltipText = "Avançar até o evento pendente, sem iniciar o próximo trecho" },
        _templeTick = new() { Name = "TempleWorldTick", Text = "+1 s" },
        _templeFocus = new() { Name = "TempleWorldFocus", Text = "Ver NPC", TooltipText = "Enquadrar o NPC sem mover o personagem" },
        _templeReveal = new() { Name = "TempleProgressAction", Text = "Saída do NPC", Visible = false };
    public Action? TempleStepRequested, TempleLegRequested, TempleWaitRequested, TempleTickRequested, TempleFocusRequested;
    public Action<bool>? TempleAutoRequested;
    private void AddTempleProgress(VBoxContainer parent)
    {
        parent.AddChild(_templeProgress);
        _templeProgress.AddChild(new HSeparator());
        _templeProgress.AddChild(_templeHeading); _templeState.AddThemeFontSizeOverride("font_size",12);
        _templeProgress.AddChild(_templeState);
        _templeProgress.AddChild(_templeAuto); _templeAuto.Toggled += value => TempleAutoRequested?.Invoke(value);
        var actions = new HFlowContainer(); _templeProgress.AddChild(actions);
        foreach (var button in new[] { _templeStep, _templeLeg, _templeTick, _templeWait, _templeFocus }) actions.AddChild(button);
        _templeStep.Pressed += () => { if (!_templeStep.Disabled) TempleStepRequested?.Invoke(); };
        _templeLeg.Pressed += () => { if (!_templeLeg.Disabled) TempleLegRequested?.Invoke(); };
        _templeWait.Pressed += () => { if (!_templeWait.Disabled) TempleWaitRequested?.Invoke(); };
        _templeTick.Pressed += () => { if (!_templeTick.Disabled) TempleTickRequested?.Invoke(); };
        _templeFocus.Pressed += () => { if (!_templeFocus.Disabled) TempleFocusRequested?.Invoke(); };
        _templeReveal.Pressed += RevealTempleProgress;
    }
    private async void RevealTempleProgress()
    {
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        if (_templeProgress.IsVisibleInTree() && FindChild("FiltersScroll",true,false) is ScrollContainer scroll)
            scroll.ScrollVertical += Mathf.RoundToInt(_templeHeading.GetGlobalRect().Position.Y - scroll.GetGlobalRect().Position.Y);
    }
    public bool TempleProgressHeadingVisible
    {
        get
        {
            var frame = ((ScrollContainer)FindChild("FiltersScroll",true,false)).GetGlobalRect(); var heading = _templeHeading.GetGlobalRect();
            bool Inside(Control control) { var rect = control.GetGlobalRect(); return frame.HasPoint(rect.Position) && frame.HasPoint(rect.End - Vector2.One); }
            return _templeHeading.IsVisibleInTree() && Inside(_templeHeading) && Inside(_templeState) && Inside(_templeStep);
        }
    }
    public void TempleProgress(bool present, string text, bool walking, bool waiting, bool canAct, bool canFocus)
    {
        _templeProgress.Visible = _templeReveal.Visible = present; _templeState.Text = text;
        _templeStep.Disabled = _templeLeg.Disabled = !canAct || !walking;
        _templeTick.Disabled = _templeWait.Disabled = !canAct || !waiting; _templeFocus.Disabled = !canFocus;
    }
    public void TemplePlayback(bool enabled, bool canEnable)
    { _templeAuto.SetPressedNoSignal(enabled); _templeAuto.Disabled = !enabled && !canEnable; }
}
