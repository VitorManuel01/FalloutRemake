using System;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TempleSpeechWindow : AcceptDialog
{
    private readonly Label _speaker = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart },
        _progress = new() { Name = "SpeechProgress", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly RichTextLabel _replies = new() { Name = "SpeechReplies", BbcodeEnabled = false,
        CustomMinimumSize = new(0, 140), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
    private readonly Button _choice = new() { Name = "SpeechChoice", AutowrapMode = TextServer.AutowrapMode.WordSmart,
        CustomMinimumSize = new(0, 72), Alignment = HorizontalAlignment.Left },
        _restart = new() { Name = "SpeechRestart", Text = "Recomeçar ensaio" };
    private readonly Button _save = new() { Name = "SpeechSave", Text = "Salvar resultado", Disabled = true,
        TooltipText = "Salva somente o resultado do ensaio concluído, separado da partida." },
        _load = new() { Name = "SpeechLoad", Text = "Carregar ensaio", Disabled = true,
            TooltipText = "Substitui as escolhas deste ensaio pelo arquivo salvo. Não carrega a partida." };
    private readonly Label _status = new() { Name = "SpeechStatus", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private TempleSpeechProfile? _profile;
    private TempleSpeechText? _text;
    private readonly Button _departure = new() { Name = "SpeechDeparture", Text = "Saída do NPC · ensaio", Visible = false };
    private readonly Button _apply = new() { Name = "SpeechApplyWorld", Text = "Aplicar à partida", Visible = false,
        TooltipText = "Aceita Speech com o perfil de teste e inicia a saída manual do NPC. K salva no checkpoint da partida." };
    public Action? ChooseRequested, RestartRequested, SaveRequested, LoadRequested, DepartureRequested, ApplyRequested;
    public string ReplyText => _replies.Text;
    public string ChoiceText => _choice.Text;
    public string StatusText => _status.Text;
    public bool ReadableLayout => _replies.Size.X >= 400 && _replies.Size.Y >= 140 && _choice.Size.X >= 400;
    public override void _Ready()
    {
        Title = "Conversa original · ensaio"; Exclusive = true; Transient = true; MinSize = new(560, 440);
        GetOkButton().Text = "Fechar ensaio";
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); AddChild(scroll);
        scroll.OffsetLeft = 20; scroll.OffsetRight = -20; scroll.OffsetTop = 16; scroll.OffsetBottom = -60;
        var body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12); scroll.AddChild(body);
        body.AddChild(new Label { Text = "Ensaio separado da partida · INT 5 e Speech marcado. Somente cinco escolhas verificadas estão disponíveis. Nenhuma escolha concede XP, move o NPC ou muda seus itens e progresso.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color("e8c78d") });
        body.AddChild(_speaker); body.AddChild(_progress); body.AddChild(_replies); body.AddChild(_choice); body.AddChild(_status); body.AddChild(_restart);
        var files = new HFlowContainer(); files.AddChild(_save); files.AddChild(_load); files.AddChild(_departure); files.AddChild(_apply); body.AddChild(files);
        _choice.Pressed += () => { if (!_choice.Disabled) ChooseRequested?.Invoke(); };
        _restart.Pressed += () => RestartRequested?.Invoke();
        _save.Pressed += () => { if (!_save.Disabled) SaveRequested?.Invoke(); };
        _load.Pressed += () => { if (!_load.Disabled) LoadRequested?.Invoke(); };
        _departure.Pressed += () => { if (_departure.Visible && !_departure.Disabled) DepartureRequested?.Invoke(); };
        _apply.Pressed += () => { if (_apply.Visible && !_apply.Disabled) ApplyRequested?.Invoke(); };
    }
    public void Inspect(TempleSpeechProfile profile, TempleSpeechText text)
    { _profile = profile; _text = text; _speaker.Text = "Descrição original: " + text.Speaker; }
    public void Refresh(TempleSpeechDraft? draft, TempleSpeechState state)
    {
        bool done = state == TempleSpeechState.Accepted;
        _departure.Visible = done;
        _progress.Text = done ? "Ensaio concluído · estado da partida preservado" : $"Escolha {Math.Min(draft!.StepIndex + 1, 5)} de 5";
        _choice.Visible = !done; _choice.Disabled = done;
        if (done)
        {
            _replies.Text = "A alternativa de Speech foi aceita no ensaio separado. Aplicar à partida inicia a saída manual do NPC com esse resultado. O duelo ainda não está disponível.";
            _status.Text = "Você pode fechar ou recomeçar. O personagem permanece no mesmo lugar e os itens continuam com seus donos.";
            return;
        }
        var step = _profile!.Step(draft!.StepIndex);
        _replies.Text = _text!.Reply(step, "[nome do personagem]");
        _choice.Text = _text!.Message(step.ChoiceMessage);
        _status.Text = step.ReplyMessages.Length > 1 ? "O nome aparece como marcador porque o personagem deste protótipo ainda não tem nome definido." : "Escolha a resposta para continuar o ensaio.";
    }
    public void Message(string text) { _status.Text = text; _choice.Disabled = true; }
    public void PersistenceActions(bool canSave, bool canLoad) { _save.Disabled = !canSave; _load.Disabled = !canLoad; }
    public void Notice(string text) => _status.Text = text;
    public void ApplyAction(bool available) { _apply.Visible = available; _apply.Disabled = !available; }
}
