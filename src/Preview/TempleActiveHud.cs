using System;
using Godot;

namespace FalloutRemake.Preview;

public partial class PreviewHud
{
    private readonly VBoxContainer _templeProgress = new() { Name = "TempleProgressPanel", Visible = false };
    private readonly Label _templeState = new() { Name = "TempleProgressState", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Button _templeStep = new() { Name = "TempleWorldStep", Text = "Um passo" },
        _templeLeg = new() { Name = "TempleWorldLeg", Text = "Próxima parada" },
        _templeWait = new() { Name = "TempleWorldWait", Text = "Esperar evento" },
        _templeTick = new() { Name = "TempleWorldTick", Text = "+1 s" },
        _templeFocus = new() { Name = "TempleWorldFocus", Text = "Enquadrar NPC" },
        _templeReveal = new() { Name = "TempleProgressAction", Text = "Saída do NPC", Visible = false };
    public Action? TempleStepRequested, TempleLegRequested, TempleWaitRequested, TempleTickRequested, TempleFocusRequested;
    private void AddTempleProgress(VBoxContainer parent)
    {
        parent.AddChild(_templeProgress);
        _templeProgress.AddChild(new HSeparator());
        _templeProgress.AddChild(new Label { Text = "SAÍDA DO NPC · PARTIDA", Modulate = new Color("c2a66d") });
        _templeProgress.AddChild(_templeState);
        var actions = new HFlowContainer(); _templeProgress.AddChild(actions);
        foreach (var button in new[] { _templeStep, _templeLeg, _templeTick, _templeWait, _templeFocus }) actions.AddChild(button);
        _templeStep.Pressed += () => { if (!_templeStep.Disabled) TempleStepRequested?.Invoke(); };
        _templeLeg.Pressed += () => { if (!_templeLeg.Disabled) TempleLegRequested?.Invoke(); };
        _templeWait.Pressed += () => { if (!_templeWait.Disabled) TempleWaitRequested?.Invoke(); };
        _templeTick.Pressed += () => { if (!_templeTick.Disabled) TempleTickRequested?.Invoke(); };
        _templeFocus.Pressed += () => { if (!_templeFocus.Disabled) TempleFocusRequested?.Invoke(); };
        _templeReveal.Pressed += () => (FindChild("FiltersScroll",true,false) as ScrollContainer)?.EnsureControlVisible(_templeProgress);
    }
    public void TempleProgress(bool present, string text, bool walking, bool waiting, bool canAct, bool canFocus)
    {
        _templeProgress.Visible = _templeReveal.Visible = present; _templeState.Text = text;
        _templeStep.Disabled = _templeLeg.Disabled = !canAct || !walking;
        _templeTick.Disabled = _templeWait.Disabled = !canAct || !waiting; _templeFocus.Disabled = !canFocus;
    }
}
