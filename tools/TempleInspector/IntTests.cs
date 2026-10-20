using System.Buffers.Binary;
using System.Text;
using FalloutRemake.Import;

internal static class IntTests
{
    public static void Run(Action<string, Action> check)
    {
        check("INT literal getters distinguish all three variable spaces and retain aliases once without reading state", () =>
        {
            foreach (var (opcode, scope) in new[] { ((ushort)0x80C1, IntVariableScope.ScriptLocal), ((ushort)0x80C3, IntVariableScope.Map), ((ushort)0x80C5, IntVariableScope.GameGlobal) })
            {
                var p = DialogueFixture((0xC001, 16), (opcode, null)); byte[] before = p.CopySourceBytes();
                var read = IntVariableReads.Inspect(p).Single();
                Assert(read.Scope == scope && read.Index == 16 && read.OwningProcedures.SequenceEqual(new[] { 0, 1 }) && before.SequenceEqual(p.CopySourceBytes()));
            }
        });
        check("INT getter evidence retains negative indices but does not invent string or computed indices", () =>
        {
            Assert(IntVariableReads.Inspect(DialogueFixture((0xC001, -1), (0x80C1, null))).Single().Index == -1);
            Assert(IntVariableReads.Inspect(DialogueFixture((0x9001, 16), (0x80C1, null))).Length == 0);
            Assert(IntVariableReads.Inspect(DialogueFixture((0xC001, 16), (0x8000, null), (0x80C1, null))).Length == 0);
            Assert(IntVariableReads.Inspect(DialogueFixture((0xC001, 16), (0x8012, null))).Length == 0);
        });
        check("INT getter patterns do not borrow an index from another physical procedure", () =>
        {
            var p = DialogueFixture((0xC001, 16), (0x80C1, null)); byte[] split = p.CopySourceBytes();
            I32(split, 86, p.Instructions.Last().Offset); Assert(IntVariableReads.Inspect(Read(split)).Length == 0);
        });
        check("INT literal setters distinguish script-local map and game-global spaces without alias duplication", () =>
        {
            foreach (var (opcode, scope) in new[] { ((ushort)0x80C2, IntVariableScope.ScriptLocal), ((ushort)0x80C4, IntVariableScope.Map), ((ushort)0x80C6, IntVariableScope.GameGlobal) })
            {
                var p = DialogueFixture((0xC001, 12), (0xC001, -1), (opcode, null)); byte[] before = p.CopySourceBytes();
                var write = IntVariableWrites.Inspect(p).Single();
                Assert(write.Index == 12 && write.Value == -1 && write.Scope == scope && write.OwningProcedures.SequenceEqual(new[] { 0, 1 })
                    && before.SequenceEqual(p.CopySourceBytes()));
            }
        });
        check("INT literal setter patterns do not infer strings expressions or unrelated program-global stores", () =>
        {
            Assert(IntVariableWrites.Inspect(DialogueFixture((0xC001, 1), (0x9001, 2), (0x80C2, null))).Length == 0);
            Assert(IntVariableWrites.Inspect(DialogueFixture((0xC001, 1), (0xC001, 2), (0x8000, null), (0x80C4, null))).Length == 0);
            Assert(IntVariableWrites.Inspect(DialogueFixture((0xC001, 1), (0xC001, 2), (0x8013, null))).Length == 0);
        });
        check("INT literal setter evidence stays within its physical procedure even when argument pushes precede another body", () =>
        {
            var p = DialogueFixture((0xC001, 1), (0xC001, 2), (0x80C6, null)); byte[] split = p.CopySourceBytes();
            I32(split, 86, p.Instructions.Last().Offset); Assert(IntVariableWrites.Inspect(Read(split)).Length == 0);
        });
        check("INT dialogue options preserve literal messages targets signed IQ and aliased ownership once", () =>
        {
            var p = DialogueFixture((0xC001, 3), (0x8046, null), (0xC001, 749), (0xC001, 134), (0xC001, 1), (0xC001, 50), (0x8121, null));
            var option = IntDialogueOptions.Inspect(p).Single();
            Assert(option.Intelligence == -3 && option.MessageList == 749 && option.Message == 134 && option.TargetIndex == 1
                && option.TargetName == "entry" && option.Reaction == 50 && option.OwningProcedures.SequenceEqual(new[] { 0, 1 }));
            p = DialogueFixture((0xC001, 4), (0xC001, 749), (0xC001, 136), (0xC001, 0), (0xC001, 49), (0x8121, null));
            Assert(IntDialogueOptions.Inspect(p).Single().Intelligence == 4);
        });
        check("INT dialogue analysis leaves unknown IQ and foreign target unresolved without inventing an edge", () =>
        {
            var p = DialogueFixture((0x80BF, null), (0xC001, 749), (0xC001, 134), (0xC001, 999), (0xC001, 50), (0x8121, null));
            byte[] before = p.CopySourceBytes(); var option = IntDialogueOptions.Inspect(p).Single();
            Assert(option.Intelligence == null && option.TargetIndex == 999 && option.TargetName == null && p.CopySourceBytes().SequenceEqual(before));
            p = DialogueFixture((0xC001, 4), (0xC001, 749), (0xC001, 134), (0xC001, -1), (0xC001, 50), (0x8121, null));
            Assert(IntDialogueOptions.Inspect(p).Single().TargetName == null);
        });
        check("INT dialogue patterns do not cross procedure spans or infer computed and string arguments", () =>
        {
            foreach (ushort tag in new ushort[] { 0x9001, 0xA001 })
                Assert(IntDialogueOptions.Inspect(DialogueFixture((0xC001, 4), (0xC001, 749), (0xC001, 134), (tag, 1), (0xC001, 50), (0x8121, null))).Length == 0);
            Assert(IntDialogueOptions.Inspect(DialogueFixture((0xC001, 4), (0xC001, 749), (0xC001, 134), (0xC001, 1), (0xC001, 50), (0x8000, null), (0x8121, null))).Length == 0);
            var p = DialogueFixture((0xC001, 4), (0xC001, 749), (0xC001, 134), (0xC001, 1), (0xC001, 50), (0x8121, null));
            byte[] split = p.CopySourceBytes(); I32(split, 86, p.Instructions.Last().Offset);
            Assert(IntDialogueOptions.Inspect(Read(split)).Length == 0);
        });
        check("INT identifier references stay within their own table and preserve exact offsets", () =>
        {
            IntProgram p = Read(Fixture()); Assert(p.IdentifierAt(4) == "entry" && p.IdentifierAt(5) == "ntry");
            foreach (int offset in new[] { -1, 0, 3, 10, int.MaxValue })
            { bool failed = false; try { p.IdentifierAt(offset); } catch (InvalidDataException) { failed = true; } Assert(failed); }
        });
        check("INT external reference patterns resolve identifiers once despite aliased procedures", () =>
        {
            foreach ((ushort opcode, string operation) in new[] { ((ushort)0x8014, "fetch"), ((ushort)0x8015, "store"), ((ushort)0x8016, "export") })
            {
                byte[] bytes = ExternalFixture(opcode); var p = Read(bytes); var r = IntExternalReferences.Inspect(p).Single();
                Assert(r.Offset == 120 && r.IdentifierOffset == 4 && r.Identifier == "entry" && r.Operation == operation);
                Assert(r.ValueSource == (operation == "store" ? "self_obj" : null) && p.CopySourceBytes().SequenceEqual(bytes));
            }
        });
        check("INT reference analysis refuses foreign table offsets and makes no claims for nonadjacent pushes", () =>
        {
            var bytes = ExternalFixture(0x8015); I32(bytes, 116, int.MaxValue);
            bool failed = false; try { IntExternalReferences.Inspect(Read(bytes)); } catch (InvalidDataException) { failed = true; } Assert(failed);
            bytes = ExternalFixture(0x8015); U16(bytes, 114, 0xA001); Assert(IntExternalReferences.Inspect(Read(bytes)).Length == 0);
            bytes = ExternalFixture(0x8015); U16(bytes, 112, 0x8000); Assert(IntExternalReferences.Inspect(Read(bytes)).Single().ValueSource == null);
            var separated = new byte[126]; ExternalFixture(0x8015).AsSpan(0, 120).CopyTo(separated);
            U16(separated, 120, 0x8000); U16(separated, 122, 0x8015); U16(separated, 124, 0x8000);
            Assert(IntExternalReferences.Inspect(Read(separated)).Length == 0);
        });
        check("INT reads aliased procedures and preserves an independent source snapshot", () =>
        {
            byte[] bytes = Fixture(); byte[] expected = (byte[])bytes.Clone();
            IntProgram program = Read(bytes); bytes[0] = 0;
            byte[] copy = program.CopySourceBytes(); copy[0] = 0;
            Assert(program.CopySourceBytes().SequenceEqual(expected));
            Assert(program.Procedures.Count == 2 && program.Procedures.All(p => p.Name == "entry" && p.BodyOffset == 112));
            Assert(program.ProcedureInstructions(0).SequenceEqual(program.ProcedureInstructions(1)));
            Assert(program.Instructions.Last().Offset == 118 && program.IdentifiersOffset == 94 && program.StaticStringsOffset == 104);
        });
        check("INT preserves typed push bits and unknown two-byte operations without executing", () =>
        {
            byte[] bytes = Fixture(); U16(bytes, 112, 0xA001); I32(bytes, 114, unchecked((int)0xFFC12345)); U16(bytes, 118, 0x82FF);
            IntInstruction[] code = Read(bytes).ProcedureInstructions(0).ToArray();
            Assert(code[0].Opcode == 0xA001 && code[0].OperandBits == unchecked((int)0xFFC12345) && code[0].Length == 6);
            Assert(code[1].Opcode == 0x82FF && code[1].OperandBits == null && code[1].Length == 2);
        });
        check("INT rejects truncated, overflowing and negative procedure/table counts", () =>
        {
            Reject(new byte[12]);
            foreach (int value in new[] { -1, 0, 4097, int.MaxValue }) { byte[] bytes = Fixture(); I32(bytes, 42, value); Reject(bytes); }
            foreach (int value in new[] { -1, int.MaxValue }) { byte[] bytes = Fixture(); I32(bytes, 94, value); Reject(bytes); }
        });
        check("INT bounds procedure names and requires their terminating zero inside the table", () =>
        {
            foreach (int value in new[] { -1, 0, 3, 10, int.MaxValue }) { byte[] bytes = Fixture(); I32(bytes, 46, value); Reject(bytes); }
            byte[] unterminated = Fixture(); unterminated[103] = 65; Reject(unterminated);
        });
        check("INT rejects unsupported bootstrap and code targets inside metadata or immediates", () =>
        {
            byte[] badBootstrap = Fixture(); U16(badBootstrap, 16, 0x8000); Reject(badBootstrap);
            foreach (int value in new[] { -1, 106, 111, 120, int.MaxValue }) { byte[] bytes = Fixture(); I32(bytes, 12, value); Reject(bytes); }
            byte[] badBody = Fixture(); I32(badBody, 62, 114); Reject(badBody);
            byte[] badCondition = Fixture(); I32(badCondition, 58, 114); Reject(badCondition);
            byte[] badArgs = Fixture(); I32(badArgs, 66, -1); Reject(badArgs);
        });
        check("INT rejects partial instructions and bytes lacking the opcode flag", () =>
        {
            Reject(Fixture()[..^1]);
            byte[] shortPush = Fixture(); U16(shortPush, 118, 0xC001); Reject(shortPush);
            byte[] notOpcode = Fixture(); U16(notOpcode, 118, 0x0001); Reject(notOpcode);
            byte[] invalidIndex = Fixture(); U16(invalidIndex, 118, 0xFFFF); Reject(invalidIndex);
        });
        check("INT permits imported zero-body records and separates physical procedure spans", () =>
        {
            byte[] bytes = Fixture(); I32(bytes, 74, 4); I32(bytes, 86, 0);
            Assert(!Read(bytes).ProcedureInstructions(1).Any());
            I32(bytes, 74, 0); I32(bytes, 86, 118);
            IntProgram program = Read(bytes);
            Assert(program.ProcedureInstructions(0).Single().Offset == 112 && program.ProcedureInstructions(1).Single().Offset == 118);
        });
    }
    private static IntProgram Read(byte[] bytes) => IntProgram.Read(new GameResource("scripts/fixture.int", "synthetic", bytes));
    private static void Reject(byte[] bytes)
    {
        try { Read(bytes); } catch (InvalidDataException) { return; }
        throw new Exception("Malformed INT accepted.");
    }
    private static byte[] Fixture()
    {
        var bytes = new byte[120];
        for (int offset = 0; offset < 42; offset += 2) U16(bytes, offset, 0x8000);
        U16(bytes, 0, 0x8002); U16(bytes, 2, 0xC001); I32(bytes, 4, 18); U16(bytes, 8, 0x800D);
        U16(bytes, 10, 0xC001); I32(bytes, 12, 112); U16(bytes, 16, 0x8004); U16(bytes, 18, 0x8010);
        I32(bytes, 42, 2);
        foreach (int offset in new[] { 46, 70 }) { I32(bytes, offset, 4); I32(bytes, offset + 16, 112); }
        I32(bytes, 94, 6); Encoding.ASCII.GetBytes("entry\0").CopyTo(bytes, 98);
        I32(bytes, 104, -1); I32(bytes, 108, -1);
        U16(bytes, 112, 0xC001); I32(bytes, 114, 42); U16(bytes, 118, 0x8000);
        return bytes;
    }
    private static byte[] ExternalFixture(ushort operation)
    {
        var bytes = new byte[124]; Fixture().AsSpan(0, 112).CopyTo(bytes);
        U16(bytes, 112, 0x80BC); U16(bytes, 114, operation == 0x8016 ? (ushort)0xC001 : (ushort)0x9001); I32(bytes, 116, 4); U16(bytes, 120, operation); U16(bytes, 122, 0x8000);
        return bytes;
    }
    private static IntProgram DialogueFixture(params (ushort Opcode, int? Operand)[] instructions)
    {
        var bytes = new byte[112 + instructions.Sum(i => i.Operand.HasValue ? 6 : 2)];
        Fixture().AsSpan(0, 112).CopyTo(bytes); int offset = 112;
        foreach (var instruction in instructions)
        {
            U16(bytes, offset, instruction.Opcode);
            if (instruction.Operand.HasValue) I32(bytes, offset + 2, instruction.Operand.Value);
            offset += instruction.Operand.HasValue ? 6 : 2;
        }
        return Read(bytes);
    }
    private static void I32(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(offset, 4), value);
    private static void U16(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(offset, 2), value);
    private static void Assert(bool condition) { if (!condition) throw new Exception("INT inspector assertion failed."); }
}
