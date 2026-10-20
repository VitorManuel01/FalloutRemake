using System.Collections.Generic;
using System.Linq;

namespace FalloutRemake.Import;

public sealed record IntDialogueOption(int Offset, int[] OwningProcedures, int? Intelligence,
    int MessageList, int Message, int TargetIndex, string? TargetName, int Reaction);

/// <summary>Adjacent literal giq_option arguments in physical procedure spans. No execution or reachability proof.</summary>
public static class IntDialogueOptions
{
    public static IntDialogueOption[] Inspect(IntProgram program)
    {
        var result = new List<IntDialogueOption>();
        foreach (var body in program.Procedures.Where(p => (p.Flags & 4) == 0).GroupBy(p => p.BodyOffset).OrderBy(g => g.Key))
        {
            IntInstruction[] code = program.ProcedureInstructions(body.First().Index).ToArray();
            for (int i = 4; i < code.Length; i++)
            {
                if (code[i].Opcode != 0x8121) continue;
                IntInstruction[] args = code.Skip(i - 4).Take(4).ToArray();
                if (args.Any(a => a.Opcode != 0xC001 || !a.OperandBits.HasValue)) continue;
                int? intelligence = null;
                if (i >= 5 && code[i - 5] is { Opcode: 0xC001, OperandBits: int iq }) intelligence = iq;
                else if (i >= 6 && code[i - 5].Opcode == 0x8046 && code[i - 6] is { Opcode: 0xC001, OperandBits: int negativeIq })
                    intelligence = unchecked(-negativeIq);
                int target = args[2].OperandBits!.Value;
                result.Add(new(code[i].Offset, body.Select(p => p.Index).ToArray(), intelligence,
                    args[0].OperandBits!.Value, args[1].OperandBits!.Value, target,
                    target >= 0 && target < program.Procedures.Count ? program.Procedures[target].Name : null, args[3].OperandBits!.Value));
            }
        }
        return result.ToArray();
    }
}
