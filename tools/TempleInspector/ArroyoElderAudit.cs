using FalloutRemake.Import;
using FalloutRemake.Runtime;

/// <summary>Hash-bound opening guards and entry dependencies; does not execute dialogue or mutate a world.</summary>
internal static class ArroyoElderAudit
{
    private const string ElderHash = "7B33E32FF41BC24504658C300D3ED8C068D6F1621CAC02F697321EE204A8D27F";
    public static object Create(GameData data)
    {
        var map = MapDocument.Read(data.Read("maps/arvillag.map"),data.Read);
        MapLayoutWriter.Verify(map);
        var entry = ArroyoRewardProfile.Create(map,data.Read);
        var actor = map.AllObjects.Single(o => o.Offset == 126588);
        var binding = map.ScriptBindings.Single(b => b.Sid == actor.Header.Sid);
        var metadata = ScriptListMetadata.Read(data.Read("scripts/scripts.lst"),binding.Index);
        var program = IntProgram.Read(data.Read(binding.Program.Path));
        var village = IntProgram.Read(data.Read("scripts/arvillag.int"));
        var globals = GameGlobalFile.Read(data.Read("data/vault13.gam"));
        if (actor.Header is not {Id:20,Pid:16777229,Fid:16777247,Tile:18915,Elevation:0,Rotation:2,Sid:67108867,ScriptIndex:10}
            || binding.Index != 10 || program.Source is not {Path:"scripts/ahelder.int",Size:20658,Sha256:ElderHash}
            || metadata.Source.Sha256 != TempleInitialGateProfile.ListHash || metadata.ProgramPath != program.Source.Path || metadata.DeclaredLocals != 10
            || program.Procedures.Single(p => p.Name == "talk_p_proc").BodyOffset != 4734)
            throw new InvalidDataException($"Unknown Arroyo elder opening source profile (locals {metadata.DeclaredLocals}, binding {binding.Index}, program {program.Source.Sha256}).");
        static void Span(IntProgram p,int start,params (ushort,int?)[] expected)
        {
            var found = p.Instructions.Where(i => i.Offset >= start).Take(expected.Length).ToArray();
            if (found.FirstOrDefault()?.Offset != start || !found.Select(i => (i.Opcode,i.OperandBits)).SequenceEqual(expected))
                throw new InvalidDataException($"Arroyo elder evidence changed at {start}.");
        }
        Span(village,2440,(0xC001,0),(0xC001,0),(0x80C5,null),(0xC001,15),(0x8039,null),(0x80C6,null));
        Span(village,3004,(0xC001,47),(0xC001,47),(0x80C5,null),(0xC001,15),(0x8039,null),(0x80C6,null));
        // The opening procedure itself clears the karma classification flags before recalculating them.
        for (int i=0;i<9;i++) Span(program,4778+14*i,(0xC001,37+i),(0xC001,0),(0x80C6,null));
        Span(program,5534,(0xC001,5590),(0xC001,47),(0x80C5,null),(0xC001,0),(0x8036,null),(0x802F,null),
            (0xC001,47),(0x80C5,null),(0xC001,2),(0x803C,null),(0xC001,23),(0x8013,null));
        Span(program,8452,(0xC001,531),(0xC001,1),(0x80C6,null),(0xC001,9410),(0x8101,null),(0xC001,4),(0x8033,null),(0x802F,null));
        Span(program,8484,(0xC001,8522),(0xC001,619),(0x80C5,null),(0xC001,0),(0x8033,null),(0x802F,null),
            (0xC001,619),(0xC001,1),(0x80C6,null));
        Span(program,8522,(0xC001,8594),(0xC001,5),(0x80C1,null),(0x802F,null));
        int[] timeOffsets = {8594,8710,8826,8942}; int[] days = {360,270,180,90};
        for (int i=0;i<4;i++) Span(program,timeOffsets[i],(0xC001,timeOffsets[i]+116),(0x80EA,null),
            (0xC001,10),(0xC001,60),(0x803B,null),(0xC001,60),(0x803B,null),(0xC001,24),(0x803B,null),
            (0x803C,null),(0xC001,days[i]),(0x8038,null),(0x802F,null));
        Span(program,9058,(0xC001,9150),(0x80BF,null),(0xC001,4),(0x80CA,null),(0xC001,4),(0x8037,null),(0x802F,null));
        Span(program,9150,(0xC001,9240),(0xC001,1),(0x80C1,null),(0xC001,0),(0x8037,null),(0x802F,null));
        Span(program,9240,(0xC001,9344),(0xC001,4),(0x80C1,null),(0xC001,0),(0x8033,null),(0x802F,null),
            (0xC001,4),(0xC001,1),(0x80C2,null));
        Span(program,9308,(0xC001,9330),(0x800D,null),(0xC001,0),(0xC001,14),(0x8005,null));
        Span(program,11760,(0xC001,11),(0xC001,103),(0x811E,null));
        var choices = IntDialogueOptions.Inspect(program).Where(c => c.OwningProcedures.Contains(14)).ToArray();
        if (!choices.Select(c => (c.Offset,c.Intelligence,c.MessageList,c.Message,c.TargetName,c.Reaction)).SequenceEqual(new[] {
            (11804,(int?)4,11,104,(string?)"Node006",51),(11836,(int?)4,11,105,(string?)"Node015",49),
            (11868,(int?)4,11,106,(string?)"Node008",50)})) throw new InvalidDataException("Elder first reply options changed.");
        var language = new PrototypeTextCatalog(data.Read,data.FindPaths("pro_")).Language;
        var messages = MessageTextFile.Read(data.Read($"text/{language}/dialog/ahelder.msg"));
        var consultation=ArroyoElderConsultationProfile.Create(map,data.Read);
        var consultationText=new ArroyoElderText(consultation,messages,language);
        var stats=CritterPrototypeData.Read(map.Prototypes.Single(p=>p.Pid==actor.Header.Pid));
        var stateProfile=ArroyoElderStateProfile.Create(map,data.Read);
        int[] messageIds = {103,104,105,106};
        if (messageIds.Any(id => !messages.Entries.TryGetValue(id,out var row) || string.IsNullOrWhiteSpace(row.Text)))
            throw new InvalidDataException("Elder opening messages are incomplete.");
        var reads = IntVariableReads.Inspect(program);
        return new { Status = "ARROYO_ELDER_AUDIT_OK", Map = map.Source, Actor = new {actor.Key,actor.Header},
            Program = program.Source, Metadata = metadata, EntryProfile = entry.Fingerprint,
            PrototypeStats=new {stats.Source,stats.HeadFid,stats.AiPacket,stats.Team,stats.Flags,stats.BodyType,stats.Experience,stats.KillType,stats.DamageType,
                Special=Enumerable.Range(0,7).Select(i=>new {Index=i,Base=stats.BaseStat(i),Bonus=stats.BonusStat(i)}).ToArray(),
                Scope="Serialized original PRO values only; effective stats and character modifiers are not calculated"},
            InitialGlobals = new[] {0,1,10,11,37,38,39,40,41,42,43,44,45,47,531,619}.Select(i => globals.Entries[i]).ToArray(),
            FirstEntry = new {KarmaDelta=15,ArroyoReputationDelta=15,SourceOffsets=new[] {2462,3026},
                Prerequisite="first actual village entry; original neutral GAM values; other map effects remain outside this audit"},
            Opening = new {Procedure="Node001",ReplyMessage=103,MessageList=11, Choices=choices,
                Conditions="map 4; elder L5=0, L1>=0, L4=0; effective intelligence >=4; integer game day <=90",
                EarlierWrites="talk preamble recalculates reaction/karma flags and sets G531=1; map-4 branch sets G619=1 when zero; first greeting sets L4=1",
                ReputationDependency=$"G47>=0 contributes G47/2 (integer division) to reaction; GAM starts at {globals.Entries[47].Value}; native first entry yields {globals.Entries[47].Value+15}/2={(globals.Entries[47].Value+15)/2}",
                TextSource=messages.Source,ValidatedMessageIds=messageIds},
            OutOfRangeLocalReads=reads.Where(r => r.Scope==IntVariableScope.ScriptLocal && r.Index>=metadata.DeclaredLocals).ToArray(),
            DetachedConsultation=new {TextSource=consultationText.Source,ValidatedMessageIds=consultation.MessageIds,Nodes=4,LiveEffectsApplied=false},
            NeutralReaction=ArroyoElderReactionAudit.Create(program,stats,globals,entry.EntryGlobals()),
            DetachedStates=new {stateProfile.Fingerprint,Opening=stateProfile.Project(Array.Empty<int>()),
                Direct=stateProfile.Project(new[] {11868,16348}),Bridge=stateProfile.Project(new[] {11804,14664,16348}),
                Alternative=stateProfile.Project(new[] {11804,14696,17510}),AppliedToWorld=false},
            RepeatOpening=ArroyoElderRepeatAudit.Create(data,program,stateProfile),
            SourceRefusals=ArroyoElderSourceChecks.Run(data,map),
            RuntimeDialogueEnabled=false,SourceTextExported=false,OriginalReconstruction="verified" };
    }
}
