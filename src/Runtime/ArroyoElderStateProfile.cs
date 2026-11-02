using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public sealed record ElderDetachedProjection(
    [property:JsonRequired] string Profile,
    [property:JsonRequired] int[] Choices,
    [property:JsonRequired] string Procedure,
    [property:JsonRequired] bool Complete,
    [property:JsonRequired] int Reaction,
    [property:JsonRequired] ScriptStateSnapshot Scripts)
{
    public int Local(int index)
    {
        var block=Scripts?.Blocks?.SingleOrDefault(b=>b.Scope==ScriptVariableScope.ScriptLocal)
            ?? throw new InvalidDataException("Missing elder local variable block.");
        int slot=block.Indices!=null ? Array.IndexOf(block.Indices,index) : -1;
        return slot>=0 && block.Values!=null && slot<block.Values.Length
            ? block.Values[slot]
            : throw new InvalidDataException($"Elder local {index} is outside the bounded schema.");
    }
    public int Global(int index)
    {
        var block=Scripts?.Blocks?.SingleOrDefault(b=>b.Scope==ScriptVariableScope.GameGlobal)
            ?? throw new InvalidDataException("Missing elder global variable block.");
        int slot=block.Indices!=null ? Array.IndexOf(block.Indices,index) : -1;
        return slot>=0 && block.Values!=null && slot<block.Values.Length
            ? block.Values[slot]
            : throw new InvalidDataException($"Elder global {index} is outside the bounded schema.");
    }
}

/// <summary>Fresh, detached neutral first-talk and repeat-talk state for the supported paths, plus validation for WorldSession commits.</summary>
public sealed class ArroyoElderStateProfile
{
    public const int TalkedToElderGlobal=531;
    public const string PrototypeHash="782FEACF76511BBE415789C4BF8DB9F01C8B126122BC7BF4E7937E1AE3DB3876";
    public string ActorKey=>ArroyoRewardProfile.MapHash+":126588";
    public string Fingerprint {get;}
    private const string GameKey="data/vault13.gam";
    internal ArroyoElderStateProfile()=>Fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        CreateInitial().SchemaHash+"|"+TempleInitialGateProfile.ListHash+"|"+PrototypeHash+"|elder-neutral-first-talk-detached-v1")));
    public BoundedScriptState CreateInitial()
    {
        int[] indices=new[] {0,1,10,11}.Concat(Enumerable.Range(37,9)).Concat(new[] {47,TalkedToElderGlobal,619}).ToArray();
        int Value(int index)=>index switch {0=>15,10=>2,41=>1,47=>65,619=>1,_=>0};
        return new(new[] {
            new ScriptVariableBlock(ScriptVariableScope.ScriptLocal,ActorKey,ArroyoElderSource.ProgramHash,Enumerable.Range(0,10).ToArray(),new int[10]),
            new ScriptVariableBlock(ScriptVariableScope.GameGlobal,GameKey,TempleSpeechProfile.GlobalHash,indices,indices.Select(Value).ToArray())});
    }
    private static void Adjust(int[] locals,int delta)
    {
        locals[3]+=delta; locals[0]+=delta;
        locals[1]=locals[0] switch {<=-75=>-4,<=-50=>-3,<=-25=>-2,<=-10=>-1,<=10=>0,<=25=>1,<=50=>2,<=75=>3,_=>4};
    }
    private ElderDetachedProjection CaptureProjection(int[] route,string procedure,bool complete,int[] locals)
    {
        var candidate=CreateInitial();
        var writes=Enumerable.Range(0,10).Select(i=>new ScriptVariableWrite(ScriptVariableScope.ScriptLocal,ActorKey,i,locals[i]))
            .Append(new(ScriptVariableScope.GameGlobal,GameKey,TalkedToElderGlobal,1)).ToArray();
        if (candidate.Apply(writes,candidate.SessionToken,0)!=ScriptWriteStatus.Ready)
            throw new InvalidDataException("Elder detached state projection failed.");
        return new(Fingerprint,route.ToArray(),procedure,complete,locals[0],candidate.Capture());
    }
    private static bool SameScripts(ScriptStateSnapshot a,ScriptStateSnapshot b)=>a!=null && b!=null
        && a.Version==b.Version && a.SchemaHash==b.SchemaHash && a.Blocks!=null && b.Blocks!=null
        && a.Blocks.Length==b.Blocks.Length && a.Blocks.Zip(b.Blocks).All(pair=>pair.First!=null && pair.Second!=null
        && pair.First.Scope==pair.Second.Scope
        && pair.First.Key==pair.Second.Key && pair.First.SourceHash==pair.Second.SourceHash
        && pair.First.Indices!=null && pair.Second.Indices!=null && pair.First.Values!=null && pair.Second.Values!=null
        && pair.First.Indices.SequenceEqual(pair.Second.Indices) && pair.First.Values.SequenceEqual(pair.Second.Values));
    public ElderDetachedProjection Validate(ElderDetachedProjection? projection)
    {
        if (projection==null || projection.Choices==null || projection.Profile!=Fingerprint || !projection.Complete || projection.Procedure!="Node999")
            throw new InvalidDataException("Elder dialogue requires a completed neutral first-talk projection.");
        var expected=Project(projection.Choices);
        if (!expected.Complete || expected.Procedure!="Node999" || expected.Reaction!=projection.Reaction || !SameScripts(expected.Scripts,projection.Scripts))
            throw new InvalidDataException("Elder dialogue projection contradicts its audited script state.");
        return expected;
    }
    public ElderDetachedProjection ValidateRepeat(ElderDetachedProjection? first,ElderDetachedProjection? repeat)
    {
        var baseline=Validate(first);
        if (repeat==null || repeat.Choices==null || repeat.Profile!=Fingerprint || !repeat.Complete || repeat.Procedure!="Node999")
            throw new InvalidDataException("Elder repeat dialogue requires a completed neutral repeat-talk projection.");
        var expected=ProjectRepeat(baseline,repeat.Choices);
        if (!expected.Complete || expected.Procedure!="Node999" || expected.Reaction!=repeat.Reaction || !SameScripts(expected.Scripts,repeat.Scripts))
            throw new InvalidDataException("Elder repeat dialogue projection contradicts its audited script state.");
        return expected;
    }
    public ElderDetachedProjection Project(IEnumerable<int> choices)
    {
        if (choices==null) throw new InvalidDataException("Missing elder rehearsal choices.");
        int[] route=choices.Take(4).ToArray();
        if (route.Length>3) throw new InvalidDataException("Elder rehearsal exceeds the supported path length.");
        var text=new ArroyoElderConsultation(new ArroyoElderConsultationProfile());
        var locals=new int[10];locals[0]=32;locals[2]=1;locals[4]=1;
        foreach (int offset in route)
        {
            var choice=text.Current.Choices.SingleOrDefault(c=>c.Offset==offset);
            if (choice==null || !text.Choose(offset,text.Revision)) throw new InvalidDataException("Choice outside the supported elder state path.");
            switch (choice.TargetProcedure)
            {
                case "Node008": Adjust(locals,-10); break;
                case "Node006a": Adjust(locals,-10); Adjust(locals,-10); break; // The original bridge calls Node008, which adjusts again.
                case "Node013": Adjust(locals,-20); break;
            }
        }
        return CaptureProjection(route,text.Complete ? "Node999" : text.Current.Procedure,text.Complete,locals);
    }
    public ElderDetachedProjection ProjectRepeat(ElderDetachedProjection first,IEnumerable<int> choices)
    {
        if (choices==null) throw new InvalidDataException("Elder repeat talk requires a completed neutral first talk.");
        var baseline=Validate(first);
        var locals=baseline.Scripts.Blocks.Single(b=>b.Scope==ScriptVariableScope.ScriptLocal).Values.ToArray();
        if (locals[2]!=1 || locals[4]!=1 || locals[5]!=0 || locals[7]!=0 || locals[1]<0)
            throw new InvalidDataException("Elder repeat talk guards are not satisfied.");
        int[] route=choices.Take(3).ToArray();
        if (route.Length>2) throw new InvalidDataException("Elder repeat rehearsal exceeds the supported path length.");
        locals[0]=locals[3]+32;
        string node="Node004"; bool complete=false;
        foreach (int offset in route)
        {
            if (complete) throw new InvalidDataException("Choice outside the supported elder repeat path.");
            if (node=="Node004" && offset==14006) {node="Node999"; complete=true;}
            else if (node=="Node004" && offset==13902) {Adjust(locals,-20); node="Node013";}
            else if (node=="Node013" && offset==17510) {node="Node999"; complete=true;}
            else throw new InvalidDataException("Choice outside the supported elder repeat path.");
        }
        return CaptureProjection(route,node,complete,locals);
    }
    public static ArroyoElderStateProfile Create(MapDocument map,Func<string,GameResource> read)
    {
        _=ArroyoElderConsultationProfile.Create(map,read);
        _=ArroyoRewardProfile.Create(map,read);
        var source=ArroyoElderSource.Match(map)!;
        var stats=CritterPrototypeData.Read(map.Prototypes.Single(p=>p.Pid==source.Header.Pid));
        var globals=GameGlobalFile.Read(read(GameKey));
        if (stats.Source.Sha256!=PrototypeHash || stats.BaseStat(3)!=5 || stats.BonusStat(3)!=0
            || globals.Source.Sha256!=TempleSpeechProfile.GlobalHash || globals.Entries[1].Value!=0
            || globals.Entries[11].Value!=0 || globals.Entries[531].Value!=0 || globals.Entries[619].Value!=1)
            throw new InvalidDataException("Unknown neutral elder state sources.");
        var program=IntProgram.Read(read("scripts/ahelder.int"));
        if (program.Source.Sha256!=ArroyoElderSource.ProgramHash) throw new InvalidDataException("Elder state program changed during source validation.");
        void Span(int start,params (ushort,int?)[] expected)
        {
            var code=program.Instructions.Where(i=>i.Offset>=start).Take(expected.Length).ToArray();
            if (code.FirstOrDefault()?.Offset!=start || !code.Select(i=>(i.Opcode,i.OperandBits)).SequenceEqual(expected))
                throw new InvalidDataException($"Elder state adjustment changed at {start}.");
        }
        void Adjustment(int start,int delta,int classify,int end)
        {
            Span(start,(0xC001,3),(0xC001,3),(0x80C1,null),(0xC001,delta),(0x8046,null),(0x8039,null),(0x80C2,null),
                (0xC001,0),(0xC001,0),(0x80C1,null),(0xC001,delta),(0x8046,null),(0x8039,null),(0x80C2,null));
            int cursor=classify;
            int[] bounds={-75,-50,-25,-10,10,25,50,75};
            for (int i=0;i<bounds.Length;i++)
            {
                int next=cursor+(i<4 ? 50 : 46),band=i-4;
                var code=new List<(ushort,int?)> {(0xC001,next),(0xC001,0),(0x80C1,null),(0xC001,Math.Abs(bounds[i]))};
                if (bounds[i]<0) code.Add((0x8046,null));
                code.AddRange(new (ushort,int?)[] {(0x8035,null),(0x802F,null),(0xC001,1),(0xC001,Math.Abs(band))});
                if (band<0) code.Add((0x8046,null));
                code.AddRange(new (ushort,int?)[] {(0x80C2,null),(0xC001,end),(0x8004,null)});
                Span(cursor,code.ToArray());cursor=next;
            }
            Span(cursor,(0xC001,1),(0xC001,4),(0x80C2,null));
        }
        Adjustment(15854,10,15906,16304);Adjustment(14724,10,14776,15174);Adjustment(17016,20,17068,17466);
        Span(9240,(0xC001,9344),(0xC001,4),(0x80C1,null),(0xC001,0),(0x8033,null),(0x802F,null),(0xC001,4),(0xC001,1),(0x80C2,null));
        Span(9344,(0xC001,11),(0x80BC,null),(0xC001,4),(0xC001,3),(0xC001,19),(0x80DE,null),(0x811C,null),
            (0xC001,9396),(0x800D,null),(0xC001,0),(0xC001,19),(0x8005,null));
        Span(13740,(0xC001,11),(0xC001,117),(0x811E,null));
        Span(13754,(0xC001,13816),(0xC001,17),(0xC001,11),(0x810B,null),(0xC001,0),(0x8034,null),(0x802F,null));
        Span(13816,(0xC001,13912),(0xC001,7),(0x80C1,null),(0xC001,0),(0x8033,null),(0x802F,null));
        Span(13904,(0xC001,13944),(0x8004,null));
        var repeatOptions=IntDialogueOptions.Inspect(program).Where(c=>c.OwningProcedures.Contains(19)).ToArray();
        if (!repeatOptions.Select(c=>(c.Offset,c.Message,c.TargetName,c.Intelligence,c.MessageList,c.Reaction)).SequenceEqual(new[] {
            (13814,118,(string?)"Node005",(int?)4,11,50),(13870,119,(string?)"Node015",(int?)4,11,50),
            (13902,121,(string?)"Node013",(int?)4,11,51),(13942,120,(string?)"Node009",(int?)4,11,50),
            (13974,122,(string?)"Node007",(int?)4,11,51),(14006,123,(string?)"Node999",(int?)4,11,50)}))
            throw new InvalidDataException("Elder repeat options changed.");
        var local7Writes=IntVariableWrites.Inspect(program).Where(w=>w.Scope==IntVariableScope.ScriptLocal && w.Index==7).ToArray();
        if (local7Writes is not [{Offset:17786,Value:1}] || !local7Writes[0].OwningProcedures.SequenceEqual(new[] {31}))
            throw new InvalidDataException("Elder local-7 quest write changed.");
        return new();
    }
}
