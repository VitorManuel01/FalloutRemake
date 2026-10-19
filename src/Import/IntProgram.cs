using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FalloutRemake.Import;

public sealed record IntProcedure(int Index, int NameOffset, string Name, int Flags, int Time,
    int ConditionOffset, int BodyOffset, int ArgumentCount);
public sealed record IntInstruction(int Offset, ushort Opcode, int? OperandBits)
{
    public int Length => OperandBits.HasValue ? 6 : 2;
}

/// <summary>Read-only classic INT structure. Decodes instruction boundaries, never executes code.</summary>
public sealed class IntProgram
{
    private readonly byte[] _bytes;
    public ResourceIdentity Source { get; }
    public int IdentifiersOffset { get; }
    public int StaticStringsOffset { get; }
    public int CodeOffset { get; }
    public IReadOnlyList<IntProcedure> Procedures { get; }
    public IReadOnlyList<IntInstruction> Instructions { get; }
    public byte[] CopySourceBytes() => (byte[])_bytes.Clone();
    public string IdentifierAt(int offset) => ReadIdentifier(_bytes, IdentifiersOffset, StaticStringsOffset, offset);
    private static string ReadIdentifier(byte[] bytes, int tableStart, int tableEnd, int offset)
    {
        if (offset < 4 || offset >= tableEnd - tableStart) throw new InvalidDataException("INT identifier outside its table.");
        int start = tableStart + offset, end = Array.IndexOf(bytes, (byte)0, start, tableEnd - start);
        if (end < 0) throw new InvalidDataException("Unterminated INT identifier.");
        return Encoding.Latin1.GetString(bytes, start, end - start);
    }
    public IEnumerable<IntInstruction> ProcedureInstructions(int index)
    {
        if (index < 0 || index >= Procedures.Count) throw new ArgumentOutOfRangeException(nameof(index));
        IntProcedure procedure = Procedures[index];
        if ((procedure.Flags & 4) != 0) return Array.Empty<IntInstruction>();
        int end = Procedures.Where(p => (p.Flags & 4) == 0 && p.BodyOffset > procedure.BodyOffset)
            .Select(p => p.BodyOffset).DefaultIfEmpty(_bytes.Length).Min();
        // This is a physical span, not a control-flow/reachability claim. Aliases share spans.
        return Instructions.Where(i => i.Offset >= procedure.BodyOffset && i.Offset < end);
    }

    private IntProgram(GameResource resource, byte[] bytes, int identifiers, int strings, int code,
        IntProcedure[] procedures, IntInstruction[] instructions)
    {
        _bytes = bytes; Source = ResourceIdentity.From(resource with { Bytes = bytes });
        IdentifiersOffset = identifiers; StaticStringsOffset = strings; CodeOffset = code;
        Procedures = Array.AsReadOnly(procedures); Instructions = Array.AsReadOnly(instructions);
    }

    public static IntProgram Read(GameResource resource)
    {
        if (resource.Bytes.Length < 54 || resource.Bytes.Length > 16 * 1024 * 1024)
            throw new InvalidDataException("INT size outside the inspector's supported bounds.");
        byte[] bytes = (byte[])resource.Bytes.Clone();
        void Require(int offset, int size)
        {
            if (offset < 0 || size < 0 || offset > bytes.Length - size)
                throw new InvalidDataException($"INT range outside source at byte {offset}.");
        }
        int I32(int offset) { Require(offset, 4); return BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset, 4)); }
        ushort U16(int offset) { Require(offset, 2); return BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset, 2)); }
        int count = I32(42);
        if (count < 1 || count > 4096 || count > (bytes.Length - 50) / 24)
            throw new InvalidDataException("Invalid INT procedure count.");
        int identifiers = 46 + count * 24;
        int nameBytes = I32(identifiers);
        if (nameBytes < 0) throw new InvalidDataException("Negative INT identifier-table length.");
        Require(identifiers + 4, nameBytes);
        int strings = identifiers + 4 + nameBytes;
        Require(strings, 4);

        // Recognize the observed classic compiler bootstrap. Other layouts fail explicitly.
        // The absolute jump locates code without guessing the opaque static-string heap size.
        if (U16(0) != 0x8002 || U16(2) != 0xC001 || I32(4) != 18 || U16(8) != 0x800D
            || U16(10) != 0xC001 || U16(16) != 0x8004 || U16(18) != 0x8010)
            throw new InvalidDataException("Unsupported INT bootstrap profile.");
        int code = I32(12);
        if (code < strings + 4 || code >= bytes.Length || (code & 1) != 0)
            throw new InvalidDataException("Invalid INT startup jump target.");
        var procedures = new IntProcedure[count];
        for (int index = 0; index < count; index++)
        {
            int offset = 46 + index * 24, name = I32(offset), arguments = I32(offset + 20);
            if (arguments < 0 || arguments > 4096) throw new InvalidDataException("Invalid INT argument count.");
            procedures[index] = new IntProcedure(index, name, ReadIdentifier(bytes, identifiers, strings, name), I32(offset + 4), I32(offset + 8),
                I32(offset + 12), I32(offset + 16), arguments);
        }
        var instructions = new List<IntInstruction>();
        void Decode(int start, int end)
        {
            for (int offset = start; offset < end;)
            {
                ushort opcode = U16(offset);
                if ((opcode & 0x8000) == 0 || (opcode & 0x3FF) >= 768)
                    throw new InvalidDataException($"Invalid INT opcode at byte {offset}.");
                int length = (opcode & 0x3FF) == 1 ? 6 : 2;
                if (length > end - offset) throw new InvalidDataException("Truncated INT instruction.");
                instructions.Add(new IntInstruction(offset, opcode, length == 6 ? I32(offset + 2) : null));
                offset += length;
            }
        }
        Decode(0, 42); Decode(code, bytes.Length);
        var boundaries = instructions.Where(i => i.Offset >= code).Select(i => i.Offset).ToHashSet();
        foreach (IntProcedure procedure in procedures)
        {
            if ((procedure.Flags & 4) == 0 && !boundaries.Contains(procedure.BodyOffset)
                || procedure.ConditionOffset != 0 && !boundaries.Contains(procedure.ConditionOffset))
                throw new InvalidDataException("INT procedure/condition points outside an instruction boundary.");
        }
        return new IntProgram(resource, bytes, identifiers, strings, code, procedures, instructions.ToArray());
    }
}
