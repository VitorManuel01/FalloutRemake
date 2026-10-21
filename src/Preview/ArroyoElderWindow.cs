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
    public Action<int>? ChoiceRequested;
    public Action? RestartRequested;
    public string ReplyText=>_reply.Text;
    public override void _Ready()
    {
        Title="Anciã · consulta original"; Exclusive=true; Transient=true; MinSize=new(580,440); GetOkButton().Text="Fechar consulta";
        var scroll=new ScrollContainer {HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};
        scroll.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); AddChild(scroll);
        scroll.OffsetLeft=20; scroll.OffsetRight=-20; scroll.OffsetTop=16; scroll.OffsetBottom=-60;
        var body=new VBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill}; body.AddThemeConstantOverride("separation",12); scroll.AddChild(body);
        body.AddChild(new Label {Text="Consulta dos textos originais · não altera a partida, a reação da anciã ou seus itens. Algumas respostas ainda não estão disponíveis.",
            AutowrapMode=TextServer.AutowrapMode.WordSmart,Modulate=new Color("e8c78d")});
        body.AddChild(_reply); body.AddChild(_choices); body.AddChild(_status); body.AddChild(_restart);
        _restart.Pressed+=()=>RestartRequested?.Invoke();
    }
    public void Refresh(ArroyoElderConsultation consultation,ArroyoElderText text)
    {
        foreach (Node child in _choices.GetChildren()) {_choices.RemoveChild(child); child.QueueFree();}
        var node=consultation.Current; _reply.Text=text.Message(node.Reply);
        foreach (var choice in node.Choices)
        {
            int offset=choice.Offset;
            var choose=ChoiceRequested;
            var button=new Button {Name="ElderChoice"+offset,Text=text.Message(choice.Message),Disabled=consultation.Complete || !choice.Supported,
                AutowrapMode=TextServer.AutowrapMode.WordSmart,Alignment=HorizontalAlignment.Left,CustomMinimumSize=new(0,48),
                TooltipText=choice.Supported ? "Continuar a consulta" : "Este trecho ainda não está disponível."};
            _choices.AddChild(button); button.Pressed+=()=>{if (!button.Disabled) choose?.Invoke(offset);};
        }
        _status.Text=consultation.Complete ? "Consulta concluída · partida preservada" : "Textos lidos da sua instalação original. Os efeitos da conversa não são aplicados.";
    }
}
