using FalloutRemake.Import;
using FalloutRemake.Runtime;

/// <summary>Source-bound neutral first-talk projection. No VM, world, character mutation or persistent dialogue state.</summary>
internal static class ArroyoElderReactionAudit
{
    public static object Create(IntProgram program,CritterPrototypeData stats,GameGlobalFile globals,SavedArroyoGlobals entry)
    {
        if (program.Source.Sha256!=ArroyoElderSource.ProgramHash || globals.Source.Sha256!=TempleSpeechProfile.GlobalHash
            || stats.Source.Sha256!="782FEACF76511BBE415789C4BF8DB9F01C8B126122BC7BF4E7937E1AE3DB3876"
            || stats.BaseStat(3)!=5 || stats.BonusStat(3)!=0 || globals.Entries[1].Value!=0 || globals.Entries[11].Value!=0
            || globals.Entries[531].Value!=0 || globals.Entries[619].Value!=1 || entry is not {Karma:15,ArroyoReputation:65,KarmaTierGlobal:41})
            throw new InvalidDataException("Unknown elder neutral reaction inputs.");
        void Span(int start,params (ushort,int?)[] expected)
        {
            var code=program.Instructions.Where(i=>i.Offset>=start).Take(expected.Length).ToArray();
            if (code.FirstOrDefault()?.Offset!=start || !code.Select(i=>(i.Opcode,i.OperandBits)).SequenceEqual(expected))
                throw new InvalidDataException($"Elder neutral reaction instructions changed at {start}.");
        }
        // Program temporaries are not scripts.lst locals or game globals. Slot24 starts at zero.
        Span(2848,(0x802C,null));
        for (int i=0;i<27;i++) Span(2850+6*i,(0xC001,i==1 ? 1 : i==21 ? 2 : 0));
        Span(3012,(0x8003,null),(0xC001,4466),(0x8004,null));
        Span(4734,(0x802B,null),(0xC001,0),(0xC001,20),(0x8013,null),
            (0xC001,1),(0xC001,21),(0x8013,null),(0xC001,10),(0xC001,22),(0x8013,null));
        Span(5294,(0xC001,5388),(0xC001,2),(0x80C1,null),(0xC001,0),(0x8033,null),(0x802F,null),
            (0xC001,2),(0xC001,1),(0x80C2,null),(0xC001,3),(0x80BF,null),(0xC001,3),(0x80CA,null),
            (0x80BC,null),(0xC001,3),(0x80CA,null),(0x803A,null),(0xC001,5),(0x803B,null),(0x80C2,null));
        Span(5388,(0xC001,0),(0xC001,19),(0x8013,null),(0xC001,0),(0xC001,0),(0x80C2,null));
        Span(5434,(0xC001,5490),(0xC001,24),(0x8012,null),(0xC001,0),(0x8036,null),(0x802F,null));
        Span(5490,(0xC001,1),(0x8046,null),(0xC001,1),(0x8046,null),(0xC001,24),(0x8012,null),
            (0x803B,null),(0xC001,1000),(0x803C,null),(0x803B,null),(0xC001,24),(0x8013,null));
        Span(5534,(0xC001,5590),(0xC001,47),(0x80C5,null),(0xC001,0),(0x8036,null),(0x802F,null),
            (0xC001,47),(0x80C5,null),(0xC001,2),(0x803C,null),(0xC001,23),(0x8013,null));
        Span(5634,(0xC001,5772),(0xC001,2),(0x80BF,null),(0xC001,13),(0x80F3,null),(0x802F,null));
        Span(5780,(0xC001,6914),(0xC001,20),(0x8012,null),(0xC001,1),(0x8033,null),(0x802F,null));
        Span(6914,(0xC001,19),(0x8012,null),(0xC001,24),(0x8012,null),(0x8039,null),(0xC001,19),(0x8013,null));
        Span(6958,(0xC001,19),(0x8012,null),(0xC001,23),(0x8012,null),(0x8039,null),(0xC001,19),(0x8013,null));
        for (int i=0;i<9;i++) Span(7002+14*i,(0xC001,37+i),(0xC001,0),(0x80C6,null));
        for (int i=0;i<4;i++) Span(7128+46*i,(0xC001,7174+46*i),(0xC001,0),(0x80C5,null),
            (0xC001,1000-250*i),(0x8036,null),(0x802F,null),(0xC001,37+i),(0xC001,1),(0x80C6,null),(0xC001,7518),(0x8004,null));
        Span(7312,(0xC001,7360),(0xC001,0),(0x80C5,null),(0xC001,250),(0x8046,null),(0x8038,null),(0x802F,null),
            (0xC001,41),(0xC001,1),(0x80C6,null),(0xC001,7518),(0x8004,null));
        Span(7742,(0xC001,7798),(0xC001,41),(0x80C5,null),(0xC001,1),(0x8033,null),(0x802F,null),
            (0xC001,19),(0x8012,null),(0xC001,0),(0x8039,null),(0xC001,19),(0x8013,null),(0xC001,7998),(0x8004,null));
        Span(8106,(0xC001,8188),(0xC001,21),(0x8012,null),(0xC001,1),(0x8033,null),(0x802F,null),
            (0xC001,8180),(0xC001,11),(0x80C5,null),(0xC001,1),(0x8033,null),(0x802F,null));
        Span(8280,(0xC001,19),(0x8012,null),(0xC001,10),(0xC001,0),(0x80BF,null),(0xC001,10),(0x80F3,null),
            (0x803B,null),(0x8039,null),(0xC001,19),(0x8013,null));
        Span(8340,(0xC001,8390),(0xC001,1),(0x80C5,null),(0xC001,2),(0x8036,null),(0x802F,null));
        Span(8408,(0xC001,0),(0xC001,3),(0x80C1,null),(0xC001,19),(0x8012,null),(0x8039,null),(0x80C2,null));
        int charisma=(5-stats.BaseStat(3)-stats.BonusStat(3))*5;
        int town=entry.ArroyoReputation/2;
        int reaction=charisma+town;
        var locals=new int[10]; locals[0]=reaction; locals[2]=1; locals[3]=charisma; locals[4]=1;
        return new {Status="ARROYO_ELDER_NEUTRAL_REACTION_AUDIT_OK",
            CharacterProfile=new {Intelligence=5,Charisma=5,PresenceRank=0,SexAppeal=false,GameDay=0,RuntimeCharacterExists=false},
            Prerequisites="first elder talk in village after verified neutral Speech entry; initial actor locals 0..9 are zero; no stat-changing equipment/drugs/perks",
            Contributions=new {CharismaDifference=charisma,ProgramTemporary24=0,TownReputation=town,Wanderer=0,Slaver=0,Presence=0,Childkiller=0},
            InitialReaction=reaction,ProjectedLocals=locals,ProjectedTalkedToElder=1,FindVicUnchanged=globals.Entries[619].Value,
            Opening="Node001; L4 becomes 1 before the first reply; other nodes, repeat talks and item grants are outside this projection",
            AppliedToWorld=false,ScriptExecuted=false};
    }
}
