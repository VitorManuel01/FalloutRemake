using System.Collections.Generic;
using System.Linq;

namespace FalloutRemake.Import;

public sealed record IntVariableRead(int Offset, int[] OwningProcedures, IntVariableScope Scope, int Index);

/// <summary>Physical getter preceded by a literal integer index. Keeps invalid indices as evidence; never reads runtime state.</summary>
public static class IntVariableReads
{
    public static IntVariableRead[] Inspect(IntProgram program)
    {
        var result = new List<IntVariableRead>();
        foreach (var body in program.Procedures.Where(p => (p.Flags & 4) == 0).GroupBy(p => p.BodyOffset).OrderBy(g => g.Key))
        {
            IntInstruction[] code = program.ProcedureInstructions(body.First().Index).ToArray();
            for (int i = 1; i < code.Length; i++)
            {
                IntVariableScope? scope = code[i].Opcode switch { 0x80C1 => IntVariableScope.ScriptLocal, 0x80C3 => IntVariableScope.Map,
                    0x80C5 => IntVariableScope.GameGlobal, _ => null };
                if (scope.HasValue && code[i - 1] is { Opcode: 0xC001, OperandBits: int index })
                    result.Add(new(code[i].Offset, body.Select(p => p.Index).ToArray(), scope.Value, index));
            }
        }
        return result.ToArray();
    }
}
