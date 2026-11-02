using System;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class ArroyoElderWindow : AcceptDialog
{
    private readonly RichTextLabel _reply=new() {Name="ElderReply",BbcodeEnabled=false,CustomMinimumSize=new(0,130)};
    private readonly VBoxContainer _choices=new() {Name="ElderChoices"};
    private readonly Label _status=new() {Name="ElderStatus",AutowrapMode=TextServer.AutowrapMode.WordSmart};
    private readonly Button _restart=new() {Name="ElderRestart",Text="Recomeçar consulta"};
    private readonly Button _commit=new() {Name="ElderCommitWorld",Text="Aplicar à partida",Visible=false,
        TooltipText="Registra o fim da primeira conversa com a anciã na sessão ativa (GVAR_TALKED_TO_ELDER = 1 e reação)."};
    public Action<int>? ChoiceRequested;
    public Action? RestartRequested;
    public Action? CommitElderDialogueRequested;
    public string ReplyText=>_reply.Text;
    public string StatusText=>_status.Text;
    public override void _Ready()
    {
        Title="Anciã · consulta original"; Exclusive=true; Transient=true; MinSize=new(580,440); GetOkButton().Text="Fechar consulta";
        var scroll=new ScrollContainer {HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};
        scroll.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); AddChild(scroll);
        scroll.OffsetLeft=20; scroll.OffsetRight=-20; scroll.OffsetTop=16; scroll.OffsetBottom=-60;
        var body=new VBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill}; body.AddThemeConstantOverride("separation",12); scroll.AddChild(body);
        body.AddChild(new Label {Text="Consulta dos textos originais · não altera a partida, a reação da anciã ou seus itens. Algumas respostas ainda não estão disponíveis.",
            AutowrapMode=TextServer.AutowrapMode.WordSmart,Modulate=new Color("e8c78d")});
        body.AddChild(_reply); body.AddChild(_choices); body.AddChild(_status);
        var actions=new HFlowContainer(); actions.AddChild(_restart); actions.AddChild(_commit); body.AddChild(actions);
        _restart.Pressed+=()=>RestartRequested?.Invoke();
        _commit.Pressed+=()=>{if (_commit.Visible && !_commit.Disabled) CommitElderDialogueRequested?.Invoke();};
    }
    public void Refresh(ArroyoElderConsultation consultation,ArroyoElderText text,bool canCommit=false,bool committed=false)
    {
        foreach (Node child in _choices.GetChildren()) {_choices.RemoveChild(child); child.QueueFree();}
        var node=consultation.Current; _reply.Text=text.Message(node.Reply);
        foreach (var choice in node.Choices)
        {
            int offset=choice.Offset;
            var choose=ChoiceRequested;
            string tooltip=choice.Supported ? "Continuar a consulta"
                : offset==13814 ? "Indisponível: Área 11 ainda não conhecida no mapa-múndi (metarule(17, 11) == 0)."
                : "Este trecho ainda não está disponível.";
            var button=new Button {Name="ElderChoice"+offset,Text=text.Message(choice.Message),Disabled=consultation.Complete || !choice.Supported,
                AutowrapMode=TextServer.AutowrapMode.WordSmart,Alignment=HorizontalAlignment.Left,CustomMinimumSize=new(0,48),
                TooltipText=tooltip};
            _choices.AddChild(button); button.Pressed+=()=>{if (!button.Disabled) choose?.Invoke(offset);};
        }
        _commit.Visible=canCommit; _commit.Disabled=!canCommit;
        _commit.TooltipText=consultation.Repeat
            ? "Registra o resultado da conversa repetida (Node004) na sessão ativa e atualiza a reação da anciã."
            : "Registra o fim da primeira conversa com a anciã na sessão ativa (GVAR_TALKED_TO_ELDER = 1 e reação).";
        _status.Text=consultation.Complete ? canCommit
                ? consultation.Repeat
                    ? "Consulta repetida concluída (Node999) · partida preservada · Aplicar à partida atualiza a reação da anciã"
                    : "Consulta concluída (Node999) · partida preservada · Aplicar à partida grava GVAR 531=1"
                : "Consulta concluída (Node999) · partida preservada"
            : consultation.Repeat ? committed
                ? "Conversa repetida (Node004) · primeira conversa já aplicada na sessão (GVAR 531=1)."
                : "Conversa repetida (Node004) · textos lidos da instalação original sem aplicar efeitos."
            : "Textos lidos da sua instalação original. Os efeitos da conversa não são aplicados.";
    }
    public void Notice(string text)=>_status.Text=text;
}
