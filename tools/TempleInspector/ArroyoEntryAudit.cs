using FalloutRemake.Import;

/// <summary>Source-only audit. Does not award experience or execute entry, movies, globals or VM calls.</summary>
internal static class ArroyoEntryAudit
{
    public static object Create(GameData data)
    {
        var map = MapDocument.Read(data.Read("maps/arvillag.map"),data.Read);
        MapLayoutWriter.Verify(map);
        var program = IntProgram.Read(data.Read("scripts/arvillag.int"));
        var globals = GameGlobalFile.Read(data.Read("data/vault13.gam"));
        var runtimeProfile = FalloutRemake.Runtime.ArroyoRewardProfile.Create(map,data.Read);
        const string hash = "DB21D0CEA597C67D8597D7B98FEB252823DDF27D747FE9C710DB17720E319D48";
        if (map.Source is not { Size:216200,Sha256:"0EDCDFF2AFB6FAC7E8203CE9EAE8BA4663D37F3BE112D3EF4713AF3093D8D52A" }
            || map.Prefix.Header.MapIndex != 4 || map.Prefix.Header.ScriptIndex != 27 || map.MapProgram?.Sha256 != hash
            || program.Source.Size != 5188 || program.Source.Sha256 != hash || globals.Source.Sha256 != FalloutRemake.Runtime.TempleSpeechProfile.GlobalHash)
            throw new InvalidDataException("Unknown Arroyo entry source profile.");
        void Span(int start,params (ushort,int?)[] expected)
        {
            if (!program.Instructions.Where(i => i.Offset >= start).Take(expected.Length).Select(i => (i.Opcode,i.OperandBits)).SequenceEqual(expected))
                throw new InvalidDataException($"Arroyo entry instructions changed at {start}.");
        }
        var enter = program.Procedures.Single(p => p.Name == "map_enter_p_proc");
        if (enter.BodyOffset != 2362) throw new InvalidDataException("Arroyo entry procedure moved.");
        // All reward branches are inside the same metarule(14,0) first-run conditional.
        Span(2370,(0xC001,3318),(0xC001,14),(0xC001,0),(0x810B,null),(0x802F,null),(0xC001,3),(0x8115,null));
        Span(3044,(0xC001,3164),(0xC001,10),(0x80C5,null),(0xC001,0),(0x8033,null),(0x802F,null),
            (0xC001,10),(0xC001,3),(0x80C6,null),(0xC001,300),(0xC001,300),(0x8039,null),(0xC001,0),
            (0x8031,null),(0xC001,0),(0x8032,null),(0x80A1,null));
        Span(3156,(0xC001,3318),(0x8004,null),(0xC001,3270),(0xC001,10),(0x80C5,null),(0xC001,2),
            (0x8033,null),(0x802F,null),(0xC001,300),(0xC001,300),(0x8039,null),(0xC001,0),
            (0x8031,null),(0xC001,0),(0x8032,null),(0x80A1,null));
        Span(3262,(0xC001,3318),(0x8004,null),(0xC001,300),(0x80A1,null));
        int[] calls = program.Instructions.Where(i => i.Opcode == 0x80A1).Select(i => i.Offset).ToArray();
        if (!calls.SequenceEqual(new[] { 3112,3218,3276 })) throw new InvalidDataException("Arroyo XP call census changed.");
        return new { Status = "ARROYO_ENTRY_AUDIT_OK", Map = map.Source, Program = program.Source, RuntimeProfile = runtimeProfile.Fingerprint,
            TrialGlobal = globals.Entries[10], FirstRunGuard = new { Offset = 2370, Rule = 14, FalseTarget = 3318,
                NativeMeaning = "METARULE_FIRST_RUN: (gMapHeader.flags & MAP_SAVED) == 0" },
            Branches = new[] {
                new { Condition = "first run and G10 == 0", BaseExperience = 600, Global10After = (int?)3, CallOffset = 3112 },
                new { Condition = "first run and G10 == 2 (verified Speech result)", BaseExperience = 600, Global10After = (int?)null, CallOffset = 3218 },
                new { Condition = "first run and G10 neither 0 nor 2", BaseExperience = 300, Global10After = (int?)null, CallOffset = 3276 } },
            LaterEntry = "guard skips all three reward calls; must persist first-entry lifecycle independently of G10",
            OtherFirstRunEffects = "movie 3, global/reputation initialization, display messages; not executed by this audit",
            Scope = "Audited source branches and base operands only; pcAddExperience character modifiers/level effects require separate validation",
            AuditAppliesReward = false, RuntimeRewardAdapter = "Optional source-bound Speech first-entry reward; neutral character below level two only", SourceReconstruction = "verified; no game assets written" };
    }
}
