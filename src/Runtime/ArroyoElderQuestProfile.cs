using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public sealed record ElderQuestProjection(
    [property:JsonRequired] string Profile,
    [property:JsonRequired] int[] Choices,
    [property:JsonRequired] string Procedure,
    [property:JsonRequired] bool Complete,
    [property:JsonRequired] int Reaction,
    [property:JsonRequired] int VicKnown,
    [property:JsonRequired] bool QuestGranted,
    [property:JsonRequired] int CapsTransferred,
    [property:JsonRequired] int FlaskGranted,
    int? ClosingMessage,
    [property:JsonRequired] ScriptStateSnapshot Scripts)
{
    public int Local(int index)
    {
        var block=Scripts?.Blocks?.SingleOrDefault(b=>b.Scope==ScriptVariableScope.ScriptLocal)
            ?? throw new InvalidDataException("Missing elder quest local variable block.");
        int slot=block.Indices!=null ? Array.IndexOf(block.Indices,index) : -1;
        return slot>=0 && block.Values!=null && slot<block.Values.Length
            ? block.Values[slot]
            : throw new InvalidDataException($"Elder local {index} is outside the bounded quest schema.");
    }
    public int Global(int index)
    {
        var block=Scripts?.Blocks?.SingleOrDefault(b=>b.Scope==ScriptVariableScope.GameGlobal)
            ?? throw new InvalidDataException("Missing elder quest global variable block.");
        int slot=block.Indices!=null ? Array.IndexOf(block.Indices,index) : -1;
        return slot>=0 && block.Values!=null && slot<block.Values.Length
            ? block.Values[slot]
            : throw new InvalidDataException($"Elder global {index} is outside the bounded quest schema.");
    }
}

/// <summary>
/// Audited quest-acceptance and reminder state for Arroyo Elder (Node015, Node007, Node016, and post-quest Node004).
/// Models L7 (quest/items granted once), GVAR_DEN_VIC_KNOWN (452 = 2), 152 Bottle Caps (PID 41 at offset 126820),
/// and 1x Vic's Water Flask (PID 267 created dynamically in Node015), while refusing Node009 (out-of-range L10).
/// </summary>
public sealed class ArroyoElderQuestProfile
{
    public const int TalkedToElderGlobal=531;
    public const int VicKnownGlobal=452;
    public const int ArroyoSpecGlobal=619;
    public const int QuestLocal=7;
    public const int CapsPid=41;
    public const int FlaskPid=267;
    public const int ElderCapsOffset=126820;
    public const int ElderCapsAmount=152;
    public const int ElderKnifeOffset=126720;
    public const int ElderKnifePid=4;
    public const int FlaskQuantity=1;
    public const int ClosingReplyMessage=163;
    public const string CapsPrototypeHash="F817461512EB21A31238CE8AB41CB8E5AC2CAD415DD14F509D50B062B5BE5AAD";
    public const string FlaskPrototypeHash="425A7A25DBB5ACC579D19F734FA1B50F0891AB24D67576EBDC6F883AA2738435";
    private const string GameKey="data/vault13.gam";
    public string ActorKey=>ArroyoRewardProfile.MapHash+":126588";
    public string CapsItemKey=>ArroyoRewardProfile.MapHash+":126820";
    public string Fingerprint {get;}
    internal ArroyoElderQuestProfile()=>Fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        CreateInitial().SchemaHash+"|"+TempleInitialGateProfile.ListHash+"|"+ArroyoElderStateProfile.PrototypeHash
        +"|"+CapsPrototypeHash+"|"+FlaskPrototypeHash+"|elder-quest-reminder-detached-v1")));
    public BoundedScriptState CreateInitial()
    {
        int[] indices=new[] {0,1,10,11}.Concat(Enumerable.Range(37,9)).Concat(new[] {47,VicKnownGlobal,TalkedToElderGlobal,ArroyoSpecGlobal}).ToArray();
        int Value(int index)=>index switch {0=>15,10=>2,41=>1,47=>65,ArroyoSpecGlobal=>1,_=>0};
        return new(new[] {
            new ScriptVariableBlock(ScriptVariableScope.ScriptLocal,ActorKey,ArroyoElderSource.ProgramHash,Enumerable.Range(0,10).ToArray(),new int[10]),
            new ScriptVariableBlock(ScriptVariableScope.GameGlobal,GameKey,TempleSpeechProfile.GlobalHash,indices,indices.Select(Value).ToArray())});
    }
    private static void Adjust(int[] locals,int delta)
    {
        locals[3]+=delta; locals[0]+=delta;
        locals[1]=locals[0] switch {<=-75=>-4,<=-50=>-3,<=-25=>-2,<=-10=>-1,<=10=>0,<=25=>1,<=50=>2,<=75=>3,_=>4};
    }
    private ElderQuestProjection CaptureProjection(int[] route,string procedure,bool complete,int[] locals,int vicKnown,int caps,int flask)
    {
        var candidate=CreateInitial();
        var writes=Enumerable.Range(0,10).Select(i=>new ScriptVariableWrite(ScriptVariableScope.ScriptLocal,ActorKey,i,locals[i]))
            .Append(new(ScriptVariableScope.GameGlobal,GameKey,TalkedToElderGlobal,1))
            .Append(new(ScriptVariableScope.GameGlobal,GameKey,VicKnownGlobal,vicKnown))
            .Append(new(ScriptVariableScope.GameGlobal,GameKey,ArroyoSpecGlobal,1)).ToArray();
        if (candidate.Apply(writes,candidate.SessionToken,0)!=ScriptWriteStatus.Ready)
            throw new InvalidDataException("Elder quest state projection failed.");
        int? closing=procedure=="Node016" ? ClosingReplyMessage : null;
        return new(Fingerprint,route.ToArray(),procedure,complete,locals[0],vicKnown,locals[QuestLocal]==1,caps,flask,closing,candidate.Capture());
    }
    private static bool SameScripts(ScriptStateSnapshot a,ScriptStateSnapshot b)=>a!=null && b!=null
        && a.Version==b.Version && a.SchemaHash==b.SchemaHash && a.Blocks!=null && b.Blocks!=null
        && a.Blocks.Length==b.Blocks.Length && a.Blocks.Zip(b.Blocks).All(pair=>pair.First!=null && pair.Second!=null
        && pair.First.Scope==pair.Second.Scope
        && pair.First.Key==pair.Second.Key && pair.First.SourceHash==pair.Second.SourceHash
        && pair.First.Indices!=null && pair.Second.Indices!=null && pair.First.Values!=null && pair.Second.Values!=null
        && pair.First.Indices.SequenceEqual(pair.Second.Indices) && pair.First.Values.SequenceEqual(pair.Second.Values));
    public ElderQuestProjection ProjectFirst(IEnumerable<int> choices)
    {
        if (choices==null) throw new InvalidDataException("Missing elder quest rehearsal choices.");
        int[] route=choices.Take(6).ToArray();
        if (route.Length>5) throw new InvalidDataException("Elder quest rehearsal exceeds the supported path length.");
        var text=new ArroyoElderConsultation(new ArroyoElderConsultationProfile(),repeat:false,questMode:true);
        var locals=new int[10]; locals[0]=32; locals[2]=1; locals[4]=1;
        int vicKnown=0,caps=0,flask=0;
        foreach (int offset in route)
        {
            var choice=text.Current.Choices.SingleOrDefault(c=>c.Offset==offset);
            if (choice==null || !text.Choose(offset,text.Revision))
                throw new InvalidDataException("Choice outside the supported elder quest path.");
            switch (choice.TargetProcedure)
            {
                case "Node008": Adjust(locals,-10); break;
                case "Node006a": Adjust(locals,-10); Adjust(locals,-10); break;
                case "Node013": Adjust(locals,-20); break;
                case "Node015":
                    locals[QuestLocal]=1;
                    vicKnown=2;
                    caps=ElderCapsAmount;
                    flask=FlaskQuantity;
                    break;
                case "Node007":
                    Adjust(locals,-10);
                    vicKnown=2;
                    break;
                case "Node016":
                    vicKnown=2;
                    break;
            }
        }
        return CaptureProjection(route,text.Procedure,text.Complete,locals,vicKnown,caps,flask);
    }
    public ElderQuestProjection ProjectRepeat(ElderQuestProjection first,IEnumerable<int> choices)
    {
        if (choices==null) throw new InvalidDataException("Elder repeat quest talk requires a completed first talk.");
        var baseline=ValidateFirst(first);
        var locals=baseline.Scripts.Blocks.Single(b=>b.Scope==ScriptVariableScope.ScriptLocal).Values.ToArray();
        if (locals[2]!=1 || locals[4]!=1 || locals[5]!=0 || locals[QuestLocal] is not (0 or 1) || locals[1]<0)
            throw new InvalidDataException("Elder repeat quest talk guards are not satisfied.");
        int[] route=choices.Take(5).ToArray();
        if (route.Length>4) throw new InvalidDataException("Elder repeat quest rehearsal exceeds the supported path length.");
        bool alreadyGranted=locals[QuestLocal]==1;
        locals[0]=locals[3]+32;
        int vicKnown=baseline.VicKnown;
        int caps=baseline.CapsTransferred;
        int flask=baseline.FlaskGranted;
        var text=new ArroyoElderConsultation(new ArroyoElderConsultationProfile(),repeat:true,questMode:true,questCompleted:alreadyGranted);
        foreach (int offset in route)
        {
            var choice=text.Current.Choices.SingleOrDefault(c=>c.Offset==offset);
            if (choice==null || !text.Choose(offset,text.Revision))
                throw new InvalidDataException("Choice outside the supported elder repeat quest path.");
            switch (choice.TargetProcedure)
            {
                case "Node013": Adjust(locals,-20); break;
                case "Node015":
                    locals[QuestLocal]=1;
                    vicKnown=2;
                    caps=ElderCapsAmount;
                    flask=FlaskQuantity;
                    break;
                case "Node007":
                    Adjust(locals,-10);
                    vicKnown=2;
                    break;
                case "Node016":
                    vicKnown=2;
                    break;
            }
        }
        return CaptureProjection(route,text.Procedure,text.Complete,locals,vicKnown,caps,flask);
    }
    public ElderQuestProjection ValidateFirst(ElderQuestProjection? projection)
    {
        if (projection==null || projection.Choices==null || projection.Profile!=Fingerprint || !projection.Complete
            || projection.Procedure is not ("Node999" or "Node016"))
            throw new InvalidDataException("Elder quest dialogue requires a completed first-talk projection.");
        var expected=ProjectFirst(projection.Choices);
        if (!Matches(expected,projection))
            throw new InvalidDataException("Elder quest projection contradicts its audited script and item state.");
        return expected;
    }
    public ElderQuestProjection ValidateRepeat(ElderQuestProjection? first,ElderQuestProjection? repeat)
    {
        var baseline=ValidateFirst(first);
        if (repeat==null || repeat.Choices==null || repeat.Profile!=Fingerprint || !repeat.Complete
            || repeat.Procedure is not ("Node999" or "Node016"))
            throw new InvalidDataException("Elder repeat quest dialogue requires a completed repeat-talk projection.");
        var expected=ProjectRepeat(baseline,repeat.Choices);
        if (!Matches(expected,repeat))
            throw new InvalidDataException("Elder repeat quest projection contradicts its audited script and item state.");
        return expected;
    }
    private static bool Matches(ElderQuestProjection expected,ElderQuestProjection candidate)=>
        expected.Complete && expected.Procedure==candidate.Procedure && expected.Reaction==candidate.Reaction
        && expected.VicKnown==candidate.VicKnown && expected.QuestGranted==candidate.QuestGranted
        && expected.CapsTransferred==candidate.CapsTransferred && expected.FlaskGranted==candidate.FlaskGranted
        && expected.ClosingMessage==candidate.ClosingMessage && SameScripts(expected.Scripts,candidate.Scripts);
    public static ArroyoElderQuestProfile Create(MapDocument map,Func<string,GameResource> read)
    {
        _=ArroyoElderStateProfile.Create(map,read);
        var actor=ArroyoElderSource.Match(map)!;
        if (actor.Inventory.Length!=2
            || actor.Inventory[0] is not {Quantity:1,Object:{Offset:ElderKnifeOffset,Header:{Id:78,Pid:ElderKnifePid,Sid:-1,Flags:8}}}
            || actor.Inventory[1] is not {Quantity:ElderCapsAmount,Object:{Offset:ElderCapsOffset,Header:{Id:322,Pid:CapsPid,Sid:-1,Flags:8}}})
            throw new InvalidDataException("Unknown Arroyo elder inventory for quest item transfer.");
        var caps=PrototypeCatalog.Read(read("proto/items/00000041.pro"),CapsPid);
        var flask=PrototypeCatalog.Read(read("proto/items/00000267.pro"),FlaskPid);
        if (caps.Resource is not {Path:"proto/items/00000041.pro",Size:69,Sha256:CapsPrototypeHash}
            || caps is not {Pid:CapsPid,Type:0,Subtype:5,Fid:3,ScriptId:-1,BaseWeight:0,InventoryFid:117440552,MessageId:4100}
            || flask.Resource is not {Path:"proto/items/00000267.pro",Size:69,Sha256:FlaskPrototypeHash}
            || flask is not {Pid:FlaskPid,Type:0,Subtype:5,Fid:41,ScriptId:-1,BaseWeight:2,InventoryFid:117440676,MessageId:26700})
            throw new InvalidDataException("Unknown Arroyo elder quest item prototypes.");
        var globals=GameGlobalFile.Read(read(GameKey));
        if (globals.Source.Sha256!=TempleSpeechProfile.GlobalHash || globals.Entries[VicKnownGlobal].Value!=0)
            throw new InvalidDataException("Unknown GVAR_DEN_VIC_KNOWN initial state.");
        var program=IntProgram.Read(read("scripts/ahelder.int"));
        if (program.Source.Sha256!=ArroyoElderSource.ProgramHash)
            throw new InvalidDataException("Elder quest program changed during validation.");
        void Span(int start,params (ushort,int?)[] expected)
        {
            var code=program.Instructions.Where(i=>i.Offset>=start).Take(expected.Length).ToArray();
            if (code.FirstOrDefault()?.Offset!=start || !code.Select(i=>(i.Opcode,i.OperandBits)).SequenceEqual(expected))
                throw new InvalidDataException($"Elder quest bytecode changed at {start}.");
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
                Span(cursor,code.ToArray()); cursor=next;
            }
            Span(cursor,(0xC001,1),(0xC001,4),(0x80C2,null));
        }
        // Node007: -10 reaction adjustment, GVAR 452=2, GVAR 619=1, gsay_reply(11, 131).
        Adjustment(15224,10,15276,15674);
        Span(15674,(0xC001,15712),(0xC001,2),(0xC001,VicKnownGlobal),(0x80C5,null),(0x8038,null),(0x802F,null),
            (0xC001,VicKnownGlobal),(0xC001,2),(0x80C6,null),(0xC001,15750),(0xC001,ArroyoSpecGlobal),(0x80C5,null),
            (0xC001,0),(0x8033,null),(0x802F,null),(0xC001,ArroyoSpecGlobal),(0xC001,1),(0x80C6,null),
            (0xC001,11),(0xC001,131),(0x811E,null));
        // Node015: item_caps_total/adjust, create_object_sid(267), add_mult_objs_to_inven, GVAR 619=1, L7=1, gsay_reply(11, 159), GVAR 452=2.
        Span(17628,(0xC001,0),(0xC001,0),(0x80BC,null),(0x8138,null),(0xC001,1),(0x8031,null),(0x80BC,null),
            (0xC001,1),(0x8032,null),(0x8046,null),(0x8139,null),(0x801A,null),(0x80BF,null),(0xC001,1),(0x8032,null),(0x8139,null),(0x801A,null));
        Span(17682,(0xC001,FlaskPid),(0xC001,0),(0xC001,0),(0xC001,1),(0x8046,null),(0x80B7,null),
            (0xC001,0),(0x8031,null),(0x80BF,null),(0xC001,0),(0x8032,null),(0xC001,FlaskQuantity),(0x8116,null));
        Span(17736,(0xC001,17774),(0xC001,ArroyoSpecGlobal),(0x80C5,null),(0xC001,0),(0x8033,null),(0x802F,null),
            (0xC001,ArroyoSpecGlobal),(0xC001,1),(0x80C6,null),(0xC001,QuestLocal),(0xC001,1),(0x80C2,null),
            (0xC001,11),(0xC001,159),(0x811E,null),(0xC001,17840),(0xC001,2),(0xC001,VicKnownGlobal),(0x80C5,null),
            (0x8038,null),(0x802F,null),(0xC001,VicKnownGlobal),(0xC001,2),(0x80C6,null));
        // Node016: GVAR 452=2, GVAR 619=1, gsay_message(11, 163, 50).
        Span(17962,(0xC001,18000),(0xC001,2),(0xC001,VicKnownGlobal),(0x80C5,null),(0x8038,null),(0x802F,null),
            (0xC001,VicKnownGlobal),(0xC001,2),(0x80C6,null),(0xC001,18038),(0xC001,ArroyoSpecGlobal),(0x80C5,null),
            (0xC001,0),(0x8033,null),(0x802F,null),(0xC001,ArroyoSpecGlobal),(0xC001,1),(0x80C6,null),
            (0xC001,11),(0xC001,ClosingReplyMessage),(0xC001,50),(0x8120,null));
        return new();
    }
}
