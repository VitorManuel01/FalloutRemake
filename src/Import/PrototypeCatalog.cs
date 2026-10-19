using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace FalloutRemake.Import;

public sealed record Prototype(int Pid, int Type, int? Subtype, int Fid, int? ScriptId,
    ResourceIdentity Resource, byte[] RawBytes)
{
    public int MessageId => BinaryPrimitives.ReadInt32BigEndian(RawBytes.AsSpan(4, 4));
    public int ExtendedFlags => Type is 0 or 2 ? BinaryPrimitives.ReadInt32BigEndian(RawBytes.AsSpan(24, 4)) : 0;
    // Serialized base weight; loaded ammunition and runtime modifiers are separate rules.
    public int? BaseWeight => Type == 0 ? BinaryPrimitives.ReadInt32BigEndian(RawBytes.AsSpan(44, 4)) : null;
}

/// <summary>Validates PRO layout/length and reads identity/subtype; stats stay raw.</summary>
public sealed class PrototypeCatalog
{
    private static readonly string[] Types = { "items", "critters", "scenery", "walls", "tiles", "misc" };
    private readonly ResourceCatalog _resources;
    private readonly Dictionary<int, Prototype> _cache = new();
    public PrototypeCatalog(ResourceCatalog resources) => _resources = resources;
    public IReadOnlyDictionary<int, Prototype> Prototypes => _cache;

    public Prototype Get(int pid)
    {
        if (_cache.TryGetValue(pid, out Prototype? cached)) return cached;
        int type = pid >> 24;
        int number = pid & 0xFFFFFF;
        if (type < 0 || type >= Types.Length || number == 0)
            throw new InvalidDataException($"Unsupported prototype PID 0x{pid:X8}.");
        string directory = "proto/" + Types[type] + "/";
        string name = _resources.ListEntry(directory + Types[type] + ".lst", number - 1, ".pro");
        GameResource resource = _resources.Read(directory + name);
        Prototype proto = Read(resource, pid);
        _cache.Add(pid, proto);
        return proto;
    }

    public static Prototype Read(GameResource resource, int expectedPid)
    {
        var cursor = new BigEndianCursor(resource.Bytes);
        int[] common = cursor.Words(3);
        int pid = common[0];
        int type = pid >> 24;
        if (pid != expectedPid) throw new InvalidDataException($"PRO PID mismatch in {resource.Path}.");
        int? subtype = null;
        int? sid = null;
        int length;
        switch (type)
        {
            case 0:
            case 2:
                int[] fields = cursor.Words(6); // light, intensity, flags, extended flags, SID, subtype.
                sid = fields[4];
                subtype = fields[5];
                int[] sizes = type == 0 ? new[] { 129, 65, 125, 122, 81, 69, 61 } : new[] { 49, 49, 49, 45, 45, 45 };
                if (subtype < 0 || subtype >= sizes.Length) throw new InvalidDataException($"Unknown PRO subtype {subtype} in {resource.Path}.");
                length = sizes[subtype.Value];
                break;
            case 1:
                sid = cursor.Words(5)[4];
                // Original critter files may omit the final natural-damage-type word.
                length = resource.Bytes.Length == 412 ? 412 : 416;
                break;
            case 3:
                sid = cursor.Words(5)[4];
                length = 36;
                break;
            case 4:
                sid = cursor.Words(3)[2];
                length = 28;
                break;
            case 5:
                length = 28;
                break;
            default:
                throw new InvalidDataException($"Unsupported PRO type {type}.");
        }
        if (resource.Bytes.Length != length)
            throw new InvalidDataException($"PRO length mismatch in {resource.Path}: expected {length}, got {resource.Bytes.Length}.");
        if (type == 0 && BinaryPrimitives.ReadInt32BigEndian(resource.Bytes.AsSpan(44, 4)) < 0)
            throw new InvalidDataException($"Negative item base weight in {resource.Path}.");
        return new Prototype(pid, type, subtype, common[2], sid, ResourceIdentity.From(resource), (byte[])resource.Bytes.Clone());
    }
}
