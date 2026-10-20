using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class TempleDialogueReport
{
    public static object Create(GameData data)
    {
        MapDocument map = MapDocument.Read(data.Read("maps/arcaves.map"), data.Read); MapLayoutWriter.Verify(map);
        if (map.Source.Sha256 != TempleBarrierEffectProposal.MapHash) throw new InvalidDataException("Unknown temple dialogue source.");
        MapObject actor = map.AllObjects.Single(o => o.Offset == 425776 && o.Header.Id == 126 && o.Header.Sid == 67108869);
        ScriptBinding binding = map.ScriptBindings.Single(b => b.Sid == actor.Header.Sid && b.Index == 748);
        IntProgram program = IntProgram.Read(data.Read(binding.Program.Path));
        if (program.Source.Sha256 != "D865DF15AA53995F69D9A211034622E34A5D9D9AC10E406AEBBCE6E3B19D61F5")
            throw new InvalidDataException("Unknown Cameron program.");
        IntDialogueOption[] options = IntDialogueOptions.Inspect(program);
        if (options.Length != 53 || program.Instructions.Count(i => i.Opcode == 0x8121) != 53)
            throw new InvalidDataException("Dialogue choice census changed.");
        IntDialogueOption Choice(int offset, string from, string to, int message)
        {
            var option = options.Single(o => o.Offset == offset);
            if (option.TargetName != to || option.Message != message || option.MessageList != 749 || option.Intelligence != 4
                || !option.OwningProcedures.Any(i => program.Procedures[i].Name == from))
                throw new InvalidDataException("Known Speech dialogue edge changed.");
            return option;
        }
        var path = new[] { Choice(12650, "Node003", "Node014", 165), Choice(14192, "Node014", "Node015", 168),
            Choice(14328, "Node015", "Node015a", 171), Choice(14382, "Node015", "Node002", 172) };
        Expect(program, (12598, 0xC001, 12652), (12604, 0xC001, 40), (12610, 0xC001, 14), (12616, 0x810B, null), (12618, 0x802F, null));
        Expect(program, (14226, 0x80BF, null), (14228, 0xC001, 14), (14234, 0xC001, 0), (14240, 0x80AC, null),
            (14242, 0xC001, 0), (14248, 0x8031, null), (14250, 0xC001, 14338), (14256, 0xC001, 40),
            (14262, 0xC001, 14), (14268, 0x810B, null), (14270, 0xC001, 0), (14276, 0x8032, null),
            (14278, 0x80AF, null), (14280, 0x803F, null), (14282, 0x802F, null));
        Expect(program, (14410, 0xC001, 12), (14416, 0xC001, 1), (14422, 0x80C2, null),
            (14424, 0xC001, 13), (14430, 0xC001, 2), (14436, 0x80C2, null),
            (14438, 0xC001, 20), (14444, 0xC001, 2), (14450, 0x80C4, null),
            (14452, 0xC001, 10), (14458, 0xC001, 2), (14464, 0x80C6, null));
        Expect(program, (4396, 0xC001, 4882), (4402, 0xC001, 13), (4408, 0x80C1, null),
            (4410, 0xC001, 2), (4416, 0x8033, null), (4418, 0x802F, null),
            (4420, 0xC001, 4520), (4426, 0xC001, 14), (4432, 0x80C1, null), (4434, 0xC001, 0),
            (4440, 0x8033, null), (4442, 0x80BC, null), (4444, 0x80D4, null),
            (4446, 0xC001, 19728), (4452, 0x8034, null), (4454, 0x803E, null), (4456, 0x802F, null),
            (4458, 0xC001, 19928), (4464, 0xC001, 2), (4470, 0xC001, 33555366), (4476, 0x80A7, null),
            (4478, 0xC001, 0), (4484, 0x8031, null), (4486, 0xC001, 0), (4492, 0x8032, null), (4494, 0x812F, null));
        int[] peaceful = new[] { "Node014", "Node015", "Node015a" }.Select(n => program.Procedures.Single(p => p.Name == n).Index).ToArray();
        if (peaceful.SelectMany(program.ProcedureInstructions).Any(i => i.Opcode == 0x8147))
            throw new InvalidDataException("Speech branch acquired an inventory transfer.");
        MapObject gate = map.AllObjects.Single(o => o.Offset == 462744 && o.Header.Id == 104 && o.Header.Tile == 19928 && o.Header.Pid == 33555366);
        ScriptBinding gateBinding = map.ScriptBindings.Single(b => b.Sid == gate.Header.Sid);
        if (gateBinding.Program.Sha256 != "3E8AE39C72E0347330FF967EE7DEE36DBBA31CA27087F26D037A08F5E6C1B9DB")
            throw new InvalidDataException("Gate script differs from the recorded dependency.");
        IntVariableWrite[] writes = IntVariableWrites.Inspect(program).Where(w => w.OwningProcedures.Contains(35)).ToArray();
        if (!writes.Select(w => (w.Scope, w.Index, w.Value)).SequenceEqual(new[] {
            (IntVariableScope.ScriptLocal, 12, 1), (IntVariableScope.ScriptLocal, 13, 2),
            (IntVariableScope.Map, 20, 2), (IntVariableScope.GameGlobal, 10, 2) }))
            throw new InvalidDataException("Speech state-write extraction changed.");
        IntProgram gateProgram = IntProgram.Read(data.Read(gateBinding.Program.Path));
        if (gateProgram.Source.Sha256 != gateBinding.Program.Sha256) throw new InvalidDataException("Gate program changed after MAP inspection.");
        Expect(gateProgram, (5530, 0xC001, 5596), (5536, 0xC001, 3), (5542, 0x80C1, null),
            (5544, 0xC001, 0), (5550, 0x8033, null), (5552, 0x802F, null),
            (5554, 0xC001, 3), (5560, 0xC001, 1), (5566, 0x80C2, null),
            (5568, 0xC001, 1), (5574, 0xC001, 1), (5580, 0x80C2, null),
            (5582, 0xC001, 0), (5588, 0xC001, 1), (5594, 0x80C2, null),
            (5602, 0x80BC, null), (5604, 0x80D4, null), (5606, 0xC001, 13528), (5612, 0x8033, null),
            (5614, 0xC001, 20), (5620, 0x80C3, null), (5622, 0xC001, 2), (5628, 0x8034, null),
            (5630, 0x803E, null), (5632, 0xC001, 7), (5638, 0x80C1, null), (5640, 0xC001, 0),
            (5646, 0x8033, null), (5648, 0x803E, null), (5650, 0x802F, null),
            (5652, 0xC001, 0), (5658, 0xC001, 0), (5664, 0x80C2, null),
            (5672, 0xC001, 0), (5678, 0x80C1, null), (5680, 0xC001, 0), (5686, 0x8033, null),
            (5688, 0x802F, null), (5690, 0x80BC, null), (5692, 0x812E, null),
            (5694, 0xC001, 5706), (5700, 0x8004, null), (5702, 0x80BC, null), (5704, 0x812F, null));
        Expect(gateProgram, (5782, 0xC001, 5828), (5788, 0xC001, 20), (5794, 0x80C3, null),
            (5796, 0xC001, 1), (5802, 0x8033, null), (5804, 0x802F, null),
            (5806, 0xC001, 0), (5812, 0xC001, 0), (5818, 0x80C2, null),
            (5982, 0xC001, 0), (5988, 0x80C1, null), (5990, 0xC001, 0), (5996, 0x8033, null),
            (5998, 0x802F, null), (6000, 0x80BC, null), (6002, 0x812E, null),
            (6004, 0xC001, 6016), (6010, 0x8004, null), (6012, 0x80BC, null), (6014, 0x812F, null));
        return new
        {
            Status = "TEMPLE_DIALOGUE_EVIDENCE_OK", Scope = "Hash-bound static physical patterns and CE handler semantics; no dialogue, skill roll or state machine executed",
            Map = map.Source, Actor = new { actor.Key, actor.Header.Id, actor.Header.Pid, actor.Header.Sid }, Program = program.Source,
            LiteralChoiceCount = options.Length, SpeechChoices = path,
            Availability = "Node003 offers message 165 only with Speech tagged; these four choices require effective intelligence >= 4",
            Roll = new { Skill = 14, Name = "Speech", Modifier = 0, Offset = 14240, StoredAtFrameSlot = 0,
                AcceptCondition = "Speech tagged OR successful/critical-success roll", FailureTarget = "Node002" },
            SuccessWrites = writes.Select(w => new { w.Offset, Scope = w.Scope.ToString(), w.Index, w.Value }),
            LaterGateBranch = new { gate.Key, gate.Header.Id, gate.Header.Tile, gate.Header.Elevation, Program = gateBinding.Program,
                Condition = "actor script local 13 == 2 AND local 14 == 0 AND self tile != 19728", UnlockOffset = 4494,
                WholeInventoryMoveCountInSpeechBodies = 0 },
            NativeGateLifecycle = new { SourceMapVariable20 = map.Prefix.GlobalVariables[20], SourceOpenFlags = gate.State["openFlags"],
                FirstEntryWrites = "if local 3 == 0: local 3=1, local 1=1, local 0=1",
                EntryException = "tile 13528 AND map variable 20 != 2 AND local 7 == 0 sets local 0=0; does not match gate tile 19928",
                OnMapVariable20Equals1 = "map_update sets local 0=0, then locks this door",
                LockRule = "local 0 == 0 locks; otherwise unlocks",
                Inference = "Under zero initial locals this gate enters unlocked; later map state may lock it. This does not enable or prove the full NPC lifecycle." },
            RuntimeAdapterEnabled = false,
            RequiredBeforeEnabling = new[] { "actor local-variable initialization and conversation entry conditions", "effective intelligence and tagged-skill profile",
                "map/global variable ownership and compatible checkpoint", "NPC movement and gate script lifecycle" },
            OriginalReconstruction = "verified"
        };
    }
    private static void Expect(IntProgram program, params (int Offset, ushort Opcode, int? Operand)[] expected)
    {
        foreach (var item in expected)
        {
            var actual = program.Instructions.SingleOrDefault(i => i.Offset == item.Offset);
            if (actual == null || actual.Opcode != item.Opcode || actual.OperandBits != item.Operand)
                throw new InvalidDataException($"Dialogue evidence changed at byte {item.Offset}.");
        }
    }
}
