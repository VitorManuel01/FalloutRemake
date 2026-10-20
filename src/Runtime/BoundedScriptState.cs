using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace FalloutRemake.Runtime;

public enum ScriptVariableScope { ScriptLocal, Map, GameGlobal }
public enum ScriptWriteStatus { Ready, NoChange, Stale, Invalid, UnknownBlock, OutOfRange, Duplicate, LimitReached }
public sealed record ScriptVariableBlock(ScriptVariableScope Scope, string Key, string SourceHash, int[] Indices, int[] InitialValues);
public sealed record ScriptVariableWrite(ScriptVariableScope Scope, string Key, int Index, int Value);
public sealed record SavedScriptBlock(ScriptVariableScope Scope, string Key, string SourceHash, int[] Indices, int[] Values);
public sealed record ScriptStateSnapshot(int Version, string SchemaHash, SavedScriptBlock[] Blocks);

/// <summary>Explicit source-bound variable blocks, not a VM. Unknown indices never alias another script's storage.</summary>
public sealed class BoundedScriptState
{
    public const int MaxBlocks = 32, MaxValues = 16384, MaxWrites = 256;
    private readonly ScriptVariableBlock[] _schema;
    private readonly Dictionary<(ScriptVariableScope Scope, string Key), int> _blocks = new();
    private readonly Dictionary<int, int>[] _indices;
    private int[][] _values;
    public string SchemaHash { get; }
    public Guid SessionToken { get; private set; } = Guid.NewGuid();
    public long Revision { get; private set; }
    public BoundedScriptState(IEnumerable<ScriptVariableBlock> blocks)
    {
        if (blocks == null) throw new InvalidDataException("Missing script state schema.");
        var source = blocks.Take(MaxBlocks + 1).ToArray();
        if (source.Length is < 1 or > MaxBlocks) throw new InvalidDataException("Unsupported script block count.");
        _schema = new ScriptVariableBlock[source.Length]; _indices = new Dictionary<int, int>[source.Length]; _values = new int[source.Length][];
        int total = 0;
        for (int i = 0; i < source.Length; i++)
        {
            ScriptVariableBlock? b = source[i];
            if (b == null || !Enum.IsDefined(b.Scope) || string.IsNullOrWhiteSpace(b.Key) || b.Key.Length > 160 || !Hash(b.SourceHash)
                || b.Indices is not { Length: > 0 and <= 4096 } || b.InitialValues == null || b.InitialValues.Length != b.Indices.Length
                || b.Indices.Any(n => n < 0 || n > 65535) || !b.Indices.SequenceEqual(b.Indices.Distinct().OrderBy(n => n))
                || !_blocks.TryAdd((b.Scope, b.Key), i) || (total += b.Indices.Length) > MaxValues)
                throw new InvalidDataException("Invalid script block identity, indices or initial values.");
            _schema[i] = b with { Indices = b.Indices.ToArray(), InitialValues = b.InitialValues.ToArray() };
            _indices[i] = b.Indices.Select((index, slot) => (index, slot)).ToDictionary(p => p.index, p => p.slot);
            _values[i] = b.InitialValues.ToArray();
        }
        SchemaHash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(_schema)));
    }
    private static bool Hash(string? hash) => hash is { Length: 64 } && hash.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
    public bool TryRead(ScriptVariableScope scope, string key, int index, out int value)
    {
        value = 0;
        if (key == null || !_blocks.TryGetValue((scope, key), out int block) || !_indices[block].TryGetValue(index, out int slot)) return false;
        value = _values[block][slot]; return true;
    }
    public ScriptWriteStatus Apply(IReadOnlyList<ScriptVariableWrite>? writes, Guid token, long revision)
    {
        if (token != SessionToken || revision != Revision) return ScriptWriteStatus.Stale;
        if (writes == null) return ScriptWriteStatus.Invalid;
        if (writes.Count > MaxWrites) return ScriptWriteStatus.LimitReached;
        if (Revision == long.MaxValue) return ScriptWriteStatus.LimitReached;
        var candidate = _values.ToArray(); var copied = new HashSet<int>(); var seen = new HashSet<(int Block, int Index)>(); bool changed = false;
        foreach (ScriptVariableWrite? write in writes)
        {
            if (write == null || write.Key == null || !Enum.IsDefined(write.Scope)) return ScriptWriteStatus.Invalid;
            if (!_blocks.TryGetValue((write.Scope, write.Key), out int block)) return ScriptWriteStatus.UnknownBlock;
            if (!_indices[block].TryGetValue(write.Index, out int slot)) return ScriptWriteStatus.OutOfRange;
            if (!seen.Add((block, write.Index))) return ScriptWriteStatus.Duplicate;
            if (copied.Add(block)) candidate[block] = _values[block].ToArray();
            changed |= candidate[block][slot] != write.Value; candidate[block][slot] = write.Value;
        }
        if (!changed) return ScriptWriteStatus.NoChange;
        _values = candidate; Revision++; return ScriptWriteStatus.Ready;
    }
    public ScriptStateSnapshot Capture() => new(1, SchemaHash, _schema.Select((b, i) => new SavedScriptBlock(b.Scope, b.Key, b.SourceHash,
        b.Indices.ToArray(), _values[i].ToArray())).ToArray());
    public void Restore(ScriptStateSnapshot? snapshot)
    {
        if (snapshot == null || snapshot.Version != 1 || snapshot.SchemaHash != SchemaHash || snapshot.Blocks == null || snapshot.Blocks.Length != _schema.Length)
            throw new InvalidDataException("Script state schema mismatch.");
        var candidate = new int[_schema.Length][];
        for (int i = 0; i < _schema.Length; i++)
        {
            SavedScriptBlock? b = snapshot.Blocks[i]; ScriptVariableBlock expected = _schema[i];
            if (b == null || b.Scope != expected.Scope || b.Key != expected.Key || b.SourceHash != expected.SourceHash || b.Indices == null
                || !b.Indices.SequenceEqual(expected.Indices) || b.Values == null || b.Values.Length != expected.Indices.Length)
                throw new InvalidDataException("Script block identity or indices changed.");
            candidate[i] = b.Values.ToArray();
        }
        _values = candidate; SessionToken = Guid.NewGuid(); Revision = 0;
    }
    public void Reset()
    { _values = _schema.Select(b => b.InitialValues.ToArray()).ToArray(); SessionToken = Guid.NewGuid(); Revision = 0; }
}
