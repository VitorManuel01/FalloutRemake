using FalloutRemake.Import;

internal static class ScriptReport
{
    public static object Create(GameData data, string path, bool includeInstructions)
    {
        path = DatArchive.NormalizePath(path);
        if (!path.StartsWith("scripts/") || !path.EndsWith(".int"))
            throw new ArgumentException("Expected scripts/<name>.int.");
        IntProgram program = IntProgram.Read(data.Read(path));
        int firstBody=program.Procedures.Where(p=>(p.Flags&4)==0).Select(p=>p.BodyOffset).DefaultIfEmpty(program.Source.Size).Min();
        var initialization=program.Instructions.Where(i=>i.Offset>=program.CodeOffset && i.Offset<firstBody).ToArray();
        return new
        {
            Status = "Static structure only; no execution, decompilation or control-flow proof",
            Profile = "Classic 42-byte bootstrap, 24-byte procedure records; string heap retained opaque",
            program.Source, program.IdentifiersOffset, program.StaticStringsOffset, program.CodeOffset,
            InstructionCount = program.Instructions.Count,
            InitializationInstructionCount=initialization.Length,
            InitializationInstructions=includeInstructions ? initialization.Select(i=>new {i.Offset,Opcode=$"0x{i.Opcode:X4}",i.OperandBits,i.Length}).ToArray() : null,
            ExternalReferencePatterns = IntExternalReferences.Inspect(program),
            DialogueOptionPatterns = IntDialogueOptions.Inspect(program),
            ConstantVariableWrites = IntVariableWrites.Inspect(program).Select(w => new { w.Offset, w.OwningProcedures,
                Scope = w.Scope.ToString(), w.Index, w.Value }).ToArray(),
            LiteralVariableReads = IntVariableReads.Inspect(program).Select(r => new { r.Offset, r.OwningProcedures,
                Scope = r.Scope.ToString(), r.Index }).ToArray(),
            Procedures = program.Procedures.Select(p => new
            {
                Procedure = p,
                PhysicalInstructionCount = program.ProcedureInstructions(p.Index).Count(),
                Instructions = includeInstructions ? program.ProcedureInstructions(p.Index)
                    .Select(i => new { i.Offset, Opcode = $"0x{i.Opcode:X4}", i.OperandBits, i.Length }).ToArray() : null
            }).ToArray()
        };
    }
}
