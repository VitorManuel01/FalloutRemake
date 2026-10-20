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
        return new
        {
            Status = "TEMPLE_DIALOGUE_EVIDENCE_OK", Scope = "Hash-bound static physical patterns and CE handler semantics; no dialogue, skill roll or state machine executed",
            Map = map.Source, Actor = new { actor.Key, actor.Header.Id, actor.Header.Pid, actor.Header.Sid }, Program = program.Source,
            LiteralChoiceCount = options.Length, SpeechChoices = path,
            Availability = "Node003 offers message 165 only with Speech tagged; these four choices require effective intelligence >= 4",
            Roll = new { Skill = 14, Name = "Speech", Modifier = 0, Offset = 14240, StoredAtFrameSlot = 0,
                AcceptCondition = "Speech tagged OR successful/critical-success roll", FailureTarget = "Node002" },
            SuccessWrites = new[] { new { Scope = "script local", Index = 12, Value = 1 }, new { Scope = "script local", Index = 13, Value = 2 },
                new { Scope = "map variable", Index = 20, Value = 2 }, new { Scope = "game global", Index = 10, Value = 2 } },
            LaterGateBranch = new { gate.Key, gate.Header.Id, gate.Header.Tile, gate.Header.Elevation, Program = gateBinding.Program,
                Condition = "actor script local 13 == 2 AND local 14 == 0 AND self tile != 19728", UnlockOffset = 4494,
                WholeInventoryMoveCountInSpeechBodies = 0 },
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
