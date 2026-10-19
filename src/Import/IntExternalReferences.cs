using System.Collections.Generic;
using System.Linq;

namespace FalloutRemake.Import;

public sealed record IntExternalReference(int Offset, string Operation, int IdentifierOffset, string Identifier, string? ValueSource);

/// <summary>Physical adjacent push/reference patterns, not an execution or control-flow analysis.</summary>
public static class IntExternalReferences
{
    public static IntExternalReference[] Inspect(IntProgram program)
    {
        var result = new List<IntExternalReference>();
        IntInstruction[] code = program.Instructions.ToArray();
        for (int i = 1; i < code.Length; i++)
        {
            string? operation = code[i].Opcode switch { 0x8014 => "fetch", 0x8015 => "store", 0x8016 => "export", _ => null };
            IntInstruction name = code[i - 1];
            bool identifierPush = name.Opcode is 0x9001 or 0xC001;
            if (operation == null || !identifierPush || !name.OperandBits.HasValue || name.Offset + name.Length != code[i].Offset) continue;
            bool self = operation == "store" && i >= 2 && code[i - 2].Opcode == 0x80BC && code[i - 2].Offset + code[i - 2].Length == name.Offset;
            result.Add(new(code[i].Offset, operation, name.OperandBits.Value, program.IdentifierAt(name.OperandBits.Value), self ? "self_obj" : null));
        }
        return result.ToArray();
    }
}
