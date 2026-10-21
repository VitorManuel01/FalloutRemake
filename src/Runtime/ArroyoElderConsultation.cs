using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public sealed record ElderTextChoice(int Offset,int Message,string? Next,string TargetProcedure,int Reaction)
{ public bool Supported=>Next!=null; }
public sealed record ElderTextNode(string Procedure,int Reply,ElderTextChoice[] Choices);

/// <summary>Original reply/choice graph for a detached text consultation, not the reaction or inventory VM.</summary>
public sealed class ArroyoElderConsultationProfile
{
    private readonly Dictionary<string,ElderTextNode> _nodes;
    internal ArroyoElderConsultationProfile()
    {
        _nodes=new[] {
            new ElderTextNode("Node001",103,new[] {new ElderTextChoice(11804,104,"Node006","Node006",51),
                new(11836,105,null,"Node015",49),new(11868,106,"Node008","Node008",50)}),
            new ElderTextNode("Node006",127,new[] {new ElderTextChoice(14632,128,null,"Node015",50),
                new(14664,129,"Node008","Node006a",51),new(14696,130,"Node013","Node013",51)}),
            new ElderTextNode("Node008",135,new[] {new ElderTextChoice(16348,136,"End","Node999",50)}),
            new ElderTextNode("Node013",152,new[] {new ElderTextChoice(17510,153,"End","Node999",51),
                new(17542,154,null,"Node015",50)})
        }.ToDictionary(n=>n.Procedure);
    }
    public ElderTextNode Node(string name)=>_nodes.TryGetValue(name,out var node)
        ? node with {Choices=node.Choices.ToArray()} : throw new InvalidDataException("Node outside the elder text consultation.");
    public int[] MessageIds=>_nodes.Values.SelectMany(n=>n.Choices.Select(c=>c.Message).Append(n.Reply)).Distinct().OrderBy(i=>i).ToArray();
    public static ArroyoElderConsultationProfile Create(MapDocument map,Func<string,GameResource> read)
    {
        if (ArroyoElderSource.Match(map)==null) throw new InvalidDataException("Unknown elder consultation source identity.");
        var metadata=ScriptListMetadata.Read(read("scripts/scripts.lst"),10);
        var program=IntProgram.Read(read("scripts/ahelder.int"));
        if (metadata.Source.Sha256!=TempleInitialGateProfile.ListHash || metadata.DeclaredLocals!=10
            || metadata.ProgramPath!=program.Source.Path || program.Source.Sha256!=ArroyoElderSource.ProgramHash)
            throw new InvalidDataException("Elder consultation source or local declaration changed.");
        var profile=new ArroyoElderConsultationProfile(); var choices=IntDialogueOptions.Inspect(program);
        foreach (var node in profile._nodes.Values)
        {
            var procedure=program.Procedures.Single(p=>p.Name==node.Procedure);
            foreach (var expected in node.Choices)
            {
                var choice=choices.Single(c=>c.Offset==expected.Offset);
                if (choice.Intelligence!=4 || choice.MessageList!=11 || choice.Message!=expected.Message
                    || choice.TargetName!=expected.TargetProcedure || choice.Reaction!=expected.Reaction
                    || !choice.OwningProcedures.Contains(procedure.Index))
                    throw new InvalidDataException("Elder consultation text edge changed.");
            }
            var code=program.ProcedureInstructions(procedure.Index).ToArray();
            if (!code.Select((i,index)=>(i,index)).Any(pair=>pair.index>=2 && pair.i.Opcode==0x811E
                && code[pair.index-2] is {Opcode:0xC001,OperandBits:11}
                && code[pair.index-1].Opcode==0xC001 && code[pair.index-1].OperandBits==node.Reply))
                throw new InvalidDataException("Elder consultation reply changed.");
        }
        // Node006a adjusts reaction then calls Node008; only the text continuation is traversed here.
        var span=program.Instructions.Where(i=>i.Offset>=15174).Take(5).Select(i=>(i.Opcode,i.OperandBits));
        if (!span.SequenceEqual(new (ushort,int?)[] {(0xC001,15196),(0x800D,null),(0xC001,0),(0xC001,24),(0x8005,null)}))
            throw new InvalidDataException("Elder reaction-only text bridge changed.");
        return profile;
    }
}

public sealed class ArroyoElderConsultation
{
    private readonly ArroyoElderConsultationProfile _profile;
    private string _node="Node001";
    public long Revision {get;private set;}
    public bool Closed {get;private set;}
    public bool Complete {get;private set;}
    public ElderTextNode Current=>_profile.Node(_node);
    public ArroyoElderConsultation(ArroyoElderConsultationProfile profile)=>_profile=profile;
    public bool Choose(int offset,long revision)
    {
        if (Closed || Complete || revision!=Revision) return false;
        var choice=Current.Choices.SingleOrDefault(c=>c.Offset==offset);
        if (choice?.Next==null) return false;
        if (choice.Next=="End") Complete=true; else _node=choice.Next;
        Revision++; return true;
    }
    public void Cancel()=>Closed=true;
}

public sealed class ArroyoElderText
{
    private readonly Dictionary<int,string> _messages;
    public ResourceIdentity Source {get;}
    public ArroyoElderText(ArroyoElderConsultationProfile profile,MessageTextFile file,string language)
    {
        if (string.IsNullOrWhiteSpace(language) || language.Any(c=>!char.IsAsciiLetterOrDigit(c) && c is not '_' and not '-')
            || file.Source.Path!=$"text/{language}/dialog/ahelder.msg"
            || profile.MessageIds.Any(id=>!file.Entries.TryGetValue(id,out var row) || string.IsNullOrWhiteSpace(row.Text)))
            throw new InvalidDataException("Elder original text is incomplete or from another actor/language.");
        Source=file.Source; _messages=profile.MessageIds.ToDictionary(id=>id,id=>file.Entries[id].Text);
    }
    public string Message(int id)=>_messages.TryGetValue(id,out var text) ? text : throw new InvalidDataException("Message outside elder consultation.");
}
