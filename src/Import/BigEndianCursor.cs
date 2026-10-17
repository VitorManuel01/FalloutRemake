using System;
using System.Buffers.Binary;
using System.IO;

namespace FalloutRemake.Import;

internal sealed class BigEndianCursor
{
    private readonly ReadOnlyMemory<byte> _bytes;
    public int Position { get; private set; }
    public int Remaining => _bytes.Length - Position;
    public BigEndianCursor(ReadOnlyMemory<byte> bytes, int offset = 0)
    {
        if (offset < 0 || offset > bytes.Length) throw new InvalidDataException("Invalid read offset.");
        _bytes = bytes;
        Position = offset;
    }
    public int Int32()
    {
        if (Remaining < 4) throw new InvalidDataException($"Truncated integer at byte {Position}.");
        int value = BinaryPrimitives.ReadInt32BigEndian(_bytes.Span.Slice(Position, 4));
        Position += 4;
        return value;
    }
    public int[] Words(int count)
    {
        if (count < 0 || count > Remaining / 4) throw new InvalidDataException($"Invalid word count at byte {Position}.");
        var words = new int[count];
        for (int i = 0; i < count; i++) words[i] = Int32();
        return words;
    }
    public int Count(string label, int maximum)
    {
        int value = Int32();
        if (value < 0 || value > maximum) throw new InvalidDataException($"Invalid {label} count {value} at byte {Position - 4}.");
        return value;
    }
}
