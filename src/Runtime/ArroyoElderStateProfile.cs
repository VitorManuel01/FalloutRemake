using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public sealed record ElderDetachedProjection(string Profile,int[] Choices,string Procedure,bool Complete,int Reaction,ScriptStateSnapshot Scripts);

/// <summary>Fresh, detached neutral first-talk state for the supported text paths. Never owns or commits a WorldSession.</summary>
public sealed class ArroyoElderStateProfile
{
    public const string PrototypeHash="782FEACF76511BBE415789C4BF8DB9F01C8B126122BC7BF4E7937E1AE3DB3876";
    public string ActorKey=>ArroyoRewardProfile.MapHash+":126588";
    public string Fingerprint {get;}
    private const string GameKey="data/vault13.gam";
    internal ArroyoElderStateProfile()=>Fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        CreateInitial().SchemaHash+"|"+TempleInitialGateProfile.ListHash+"|"+PrototypeHash+"|elder-neutral-first-talk-detached-v1")));
    public BoundedScriptState CreateInitial()
    {
        int[] indices=new[] {0,1,10,11}.Concat(Enumerable.Range(37,9)).Concat(new[] {47,531,619}).ToArray();
        int Value(int index)=>index switch {0=>15,10=>2,41=>1,47=>65,619=>1,_=>0};
        return new(new[] {
            new ScriptVariableBlock(ScriptVariableScope.ScriptLocal,ActorKey,ArroyoElderSource.ProgramHash,Enumerable.Range(0,10).ToArray(),new int[10]),
            new ScriptVariableBlock(ScriptVariableScope.GameGlobal,GameKey,TempleSpeechProfile.GlobalHash,indices,indices.Select(Value).ToArray())});
    }
    public ElderDetachedProjection Project(IEnumerable<int> choices)
    {
        if (choices==null) throw new InvalidDataException("Missing elder rehearsal choices.");
        int[] route=choices.Take(4).ToArray();
        if (route.Length>3) throw new InvalidDataException("Elder rehearsal exceeds the supported path length.");
        var text=new ArroyoElderConsultation(new ArroyoElderConsultationProfile());
        var locals=new int[10];locals[0]=32;locals[2]=1;locals[4]=1;
        void Adjust(int delta)
        {
            locals[3]+=delta; locals[0]+=delta;
            locals[1]=locals[0] switch {<=-75=>-4,<=-50=>-3,<=-25=>-2,<=-10=>-1,<=10=>0,<=25=>1,<=50=>2,<=75=>3,_=>4};
        }
        foreach (int offset in route)
        {
            var choice=text.Current.Choices.SingleOrDefault(c=>c.Offset==offset);
            if (choice==null || !text.Choose(offset,text.Revision)) throw new InvalidDataException("Choice outside the supported elder state path.");
            switch (choice.TargetProcedure)
            {
                case "Node008": Adjust(-10); break;
                case "Node006a": Adjust(-10); Adjust(-10); break; // The original bridge calls Node008, which adjusts again.
                case "Node013": Adjust(-20); break;
            }
        }
        var candidate=CreateInitial();
        var writes=Enumerable.Range(0,10).Select(i=>new ScriptVariableWrite(ScriptVariableScope.ScriptLocal,ActorKey,i,locals[i]))
            .Append(new(ScriptVariableScope.GameGlobal,GameKey,531,1)).ToArray();
        if (candidate.Apply(writes,candidate.SessionToken,0)!=ScriptWriteStatus.Ready)
            throw new InvalidDataException("Elder detached state projection failed.");
        return new(Fingerprint,route.ToArray(),text.Complete ? "Node999" : text.Current.Procedure,text.Complete,locals[0],candidate.Capture());
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
        return new();
    }
}
