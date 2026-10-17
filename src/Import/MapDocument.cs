using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FalloutRemake.Import;

public sealed record MapScript(int Offset, int Length, int Sid, int Unknown4, int[] TypeWords, int[] Fields)
{
    public int Flags => Fields[0];
    public int ScriptIndex => Fields[1];
    public int OwnerId => Fields[3];
    public int LocalVariablesOffset => Fields[4];
    public int LocalVariablesCount => Fields[5];
}
public sealed record ScriptBlock(int Offset, int Length, int Used, int RawNextPointer, MapScript[] Slots);
public sealed record ScriptGroup(int Type, int Offset, int DeclaredCount, ScriptBlock[] Blocks);
public sealed record ObjectHeader(int Id, int Tile, int X, int Y, int ScreenX, int ScreenY, int Frame,
    int Rotation, int Fid, int Flags, int Elevation, int Pid, int Cid, int LightDistance,
    int LightIntensity, int RawOutline, int Sid, int ScriptIndex);
public sealed record InventoryEntry(int QuantityOffset, int Quantity, MapObject Object);
public sealed record MapObject(string Key, int Offset, int RecordLength, int SubtreeLength,
    int ContainerElevation, ObjectHeader Header, int InventoryCount, int InventoryCapacity,
    int RawInventoryPointer, int? Subtype, IReadOnlyDictionary<string, int> State, InventoryEntry[] Inventory);
public sealed record ObjectLayer(int Elevation, int CountOffset, int DeclaredCount, MapObject[] Objects);
public sealed record ScriptBinding(int Sid, int Index, ResourceIdentity Program);
public sealed record ImportDiagnostic(string Code, int Offset, string Message);

/// <summary>Structural MAP v20 import. Never runs scripts or applies runtime corrections.</summary>
public sealed record MapDocument(ResourceIdentity Source, MapPrefix Prefix, ScriptGroup[] ScriptGroups,
    int ObjectsOffset, int DeclaredRootObjects, ObjectLayer[] ObjectLayers, Prototype[] Prototypes,
    ScriptBinding[] ScriptBindings, ResourceIdentity? MapProgram, ResourceIdentity[] Dependencies,
    ImportDiagnostic[] Diagnostics, int ConsumedBytes)
{
    public IEnumerable<MapScript> ActiveScripts => ScriptGroups.SelectMany(g => g.Blocks).SelectMany(b => b.Slots.Take(b.Used));
    public IEnumerable<MapObject> AllObjects => ObjectLayers.SelectMany(l => l.Objects).SelectMany(Flatten);
    public static IEnumerable<MapObject> Flatten(MapObject obj)
    {
        yield return obj;
        foreach (InventoryEntry entry in obj.Inventory)
            foreach (MapObject child in Flatten(entry.Object)) yield return child;
    }

    public static MapDocument Read(GameResource source, Func<string, GameResource> read)
    {
        MapPrefix prefix = MapPrefix.Read(source.Bytes);
        var identity = ResourceIdentity.From(source);
        var resources = new ResourceCatalog(read);
        var protos = new PrototypeCatalog(resources);
        var cursor = new BigEndianCursor(prefix.OriginalBytes, prefix.UnparsedOffset);
        var groups = new List<ScriptGroup>();
        const int maxRecords = 100000;
        var active = new Dictionary<int, MapScript>();
        var diagnostics = new List<ImportDiagnostic>();

        for (int type = 0; type < 5; type++)
        {
            int start = cursor.Position;
            int count = cursor.Count("scripts", maxRecords);
            if ((long)((count + 15) / 16) * (16 * 64 + 8) > cursor.Remaining)
                throw new InvalidDataException("Script blocks exceed remaining MAP bytes.");
            var blocks = new List<ScriptBlock>();
            for (int consumed = 0; consumed < count; consumed += 16)
            {
                int blockStart = cursor.Position;
                var slots = new MapScript[16];
                for (int slot = 0; slot < slots.Length; slot++)
                {
                    int offset = cursor.Position;
                    int sid = cursor.Int32();
                    int unknown4 = cursor.Int32();
                    // Even padding slots use their own serialized SID to determine size.
                    int[] typeWords = cursor.Words((sid >> 24) switch { 1 => 2, 2 => 1, _ => 0 });
                    int[] fields = cursor.Words(14);
                    slots[slot] = new MapScript(offset, cursor.Position - offset, sid, unknown4, typeWords, fields);
                }
                int used = cursor.Count("used script slots", 16);
                int pointer = cursor.Int32();
                if (used != Math.Min(16, count - consumed)) throw new InvalidDataException("Script block count mismatch.");
                foreach (MapScript script in slots.Take(used))
                {
                    if ((script.Sid >> 24) != type || !active.TryAdd(script.Sid, script))
                        throw new InvalidDataException($"Invalid or duplicate active SID at byte {script.Offset}.");
                }
                blocks.Add(new ScriptBlock(blockStart, cursor.Position - blockStart, used, pointer, slots));
            }
            groups.Add(new ScriptGroup(type, start, count, blocks.ToArray()));
        }

        int objectsOffset = cursor.Position;
        int roots = cursor.Count("root objects", maxRecords);
        int total = 0;
        MapObject ReadObject(int elevation, int depth)
        {
            if (depth > 32 || ++total > maxRecords) throw new InvalidDataException("Inventory depth or total object limit exceeded.");
            int offset = cursor.Position;
            int[] h = cursor.Words(18);
            var header = new ObjectHeader(h[0], h[1], h[2], h[3], h[4], h[5], h[6], h[7], h[8], h[9], h[10], h[11], h[12], h[13], h[14], h[15], h[16], h[17]);
            if (header.Tile < -1 || header.Tile >= 40000 || header.Rotation < 0 || header.Rotation > 5 || header.Elevation < 0 || header.Elevation > 2)
                throw new InvalidDataException($"Invalid object coordinates at byte {offset}.");
            if (header.Elevation != elevation)
                diagnostics.Add(new ImportDiagnostic("serialized-elevation", offset, $"Stored elevation {header.Elevation}; container elevation {elevation}. Both preserved."));
            Prototype proto = protos.Get(header.Pid);
            int inventoryCount = cursor.Count("inventory", maxRecords);
            int capacity = cursor.Int32(); // Historical capacity/pointer are metadata, never allocation sizes.
            int pointer = cursor.Int32();
            var state = new Dictionary<string, int>();
            void Fields(params string[] names)
            {
                foreach (string name in names) state.Add(name, cursor.Int32());
            }
            if (proto.Type == 1)
                Fields("unknown0", "damageLastTurn", "maneuver", "ap", "results", "aiPacket", "team", "whoHitMeCid", "hp", "radiation", "poison");
            else
            {
                Fields("flags");
                if (proto.Type == 0)
                {
                    switch (proto.Subtype)
                    {
                        case 3: Fields("ammoQuantity", "ammoTypePid"); break;
                        case 4: Fields("ammoQuantity"); break;
                        case 5: Fields("charges"); break;
                        case 6: Fields("keyCode"); break;
                    }
                }
                else if (proto.Type == 2)
                {
                    switch (proto.Subtype)
                    {
                        case 0: Fields("openFlags"); break;
                        case 1: Fields("destinationBuiltTile", "destinationMap"); break;
                        case 2: Fields("elevatorType", "elevatorLevel"); break;
                        case 3:
                        case 4: Fields("destinationMap", "destinationBuiltTile"); break;
                    }
                }
                else if (header.Pid >= 0x05000010 && header.Pid <= 0x05000017)
                    Fields("destinationMap", "destinationTile", "destinationElevation", "destinationRotation");
            }
            int recordLength = cursor.Position - offset;
            if ((long)inventoryCount * 92 > cursor.Remaining) throw new InvalidDataException("Inventory count exceeds remaining MAP bytes.");
            var inventory = new InventoryEntry[inventoryCount];
            for (int i = 0; i < inventoryCount; i++)
            {
                int quantityOffset = cursor.Position;
                int quantity = cursor.Int32();
                if (quantity <= 0) throw new InvalidDataException($"Invalid inventory quantity at byte {quantityOffset}.");
                inventory[i] = new InventoryEntry(quantityOffset, quantity, ReadObject(elevation, depth + 1));
            }
            return new MapObject($"{identity.Sha256}:{offset}", offset, recordLength, cursor.Position - offset,
                elevation, header, inventoryCount, capacity, pointer, proto.Subtype, state, inventory);
        }

        var layers = new List<ObjectLayer>();
        int rootSum = 0;
        for (int elevation = 0; elevation < 3; elevation++)
        {
            int countOffset = cursor.Position;
            int count = cursor.Count("objects at elevation", maxRecords);
            if (count > cursor.Remaining / 88 || count > roots - rootSum) throw new InvalidDataException("Object count exceeds MAP bounds or declared total.");
            var objects = new MapObject[count];
            for (int i = 0; i < count; i++) objects[i] = ReadObject(elevation, 0);
            layers.Add(new ObjectLayer(elevation, countOffset, count, objects));
            rootSum += count;
        }
        if (rootSum != roots || cursor.Remaining != 0) throw new InvalidDataException($"MAP count/EOF mismatch: {rootSum}/{roots} roots, {cursor.Remaining} trailing bytes.");

        // Resolve real script references after structural parsing, without executing INT.
        var bindings = active.Values.Select(s => new ScriptBinding(s.Sid, s.ScriptIndex,
            resources.Script(s.ScriptIndex & 0xFFFFFF))).ToArray();
        ResourceIdentity? mapProgram = prefix.Header.ScriptIndex > 0 ? resources.Script(prefix.Header.ScriptIndex - 1) : null;
        foreach (MapObject obj in layers.SelectMany(l => l.Objects).SelectMany(Flatten))
        {
            if (obj.Header.Sid == -1) continue;
            if (!active.TryGetValue(obj.Header.Sid, out MapScript? script))
                diagnostics.Add(new ImportDiagnostic("missing-sid", obj.Offset, $"SID {obj.Header.Sid} has no active record; retained unchanged."));
            else
            {
                if (script.OwnerId != obj.Header.Id)
                    diagnostics.Add(new ImportDiagnostic("owner-id", obj.Offset, $"Script owner {script.OwnerId} differs from object ID {obj.Header.Id}; retained unchanged."));
                if (script.ScriptIndex != obj.Header.ScriptIndex)
                    diagnostics.Add(new ImportDiagnostic("script-index", obj.Offset, $"Stored index {obj.Header.ScriptIndex}; SID record index {script.ScriptIndex}; retained unchanged."));
            }
        }
        return new MapDocument(identity, prefix, groups.ToArray(), objectsOffset, roots, layers.ToArray(),
            protos.Prototypes.Values.OrderBy(p => p.Pid).ToArray(), bindings, mapProgram, resources.Identities,
            diagnostics.ToArray(), cursor.Position);
    }
}
