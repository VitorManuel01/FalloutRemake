using System.Collections.Generic;
using System.Linq;

namespace FalloutRemake.Import;

public enum IntVariableScope { ScriptLocal, Map, GameGlobal }
public sealed record IntVariableWrite(int Offset, int[] OwningProcedures, IntVariableScope Scope, int Index, int Value);

/// <summary>Adjacent integer index/value setter patterns; neither a state simulation nor a control-flow proof.</summary>
public static class IntVariableWrites
{
    public static IntVariableWrite[] Inspect(IntProgram program)
    {
        var result = new List<IntVariableWrite>();
        foreach (var body in program.Procedures.Where(p => (p.Flags & 4) == 0).GroupBy(p => p.BodyOffset).OrderBy(g => g.Key))
        {
            IntInstruction[] code = program.ProcedureInstructions(body.First().Index).ToArray();
            for (int i = 2; i < code.Length; i++)
            {
                IntVariableScope? scope = code[i].Opcode switch { 0x80C2 => IntVariableScope.ScriptLocal, 0x80C4 => IntVariableScope.Map,
                    0x80C6 => IntVariableScope.GameGlobal, _ => null };
                if (!scope.HasValue || code[i - 2] is not { Opcode: 0xC001, OperandBits: int index }
                    || code[i - 1] is not { Opcode: 0xC001, OperandBits: int value }) continue;
                result.Add(new(code[i].Offset, body.Select(p => p.Index).ToArray(), scope.Value, index, value));
            }
        }
        return result.ToArray();
    }
}
