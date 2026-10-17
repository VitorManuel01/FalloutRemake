using System;
using System.Buffers.Binary;
using System.IO;

namespace FalloutRemake.Import;

/// <summary>Diagnostic reconstruction from parsed fields, never writes to the installation.</summary>
public static class MapLayoutWriter
{
    public static byte[] Rebuild(MapDocument map)
    {
        using var stream = new MemoryStream();
        void Word(int value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(bytes, value);
            stream.Write(bytes);
        }
        void Words(params int[] values) { foreach (int value in values) Word(value); }
        void Position(int expected)
        {
            if (stream.Position != expected) throw new InvalidDataException($"Rebuilt position {stream.Position} != expected {expected}.");
        }
        MapHeader h = map.Prefix.Header;
        Word(h.Version);
        stream.Write(h.RawName);
        Words(h.EnteringTile, h.EnteringElevation, h.EnteringRotation, h.LocalVariablesCount, h.ScriptIndex,
            h.Flags, h.Darkness, h.GlobalVariablesCount, h.MapIndex, unchecked((int)h.LastVisitTime));
        Words(h.Reserved);
        Words(map.Prefix.GlobalVariables);
        Words(map.Prefix.LocalVariables);
        foreach (MapLayer layer in map.Prefix.Layers)
            foreach (SquareTile tile in layer.Tiles) Word(unchecked((int)tile.Raw));
        Position(map.Prefix.UnparsedOffset);
        foreach (ScriptGroup group in map.ScriptGroups)
        {
            Position(group.Offset);
            Word(group.DeclaredCount);
            foreach (ScriptBlock block in group.Blocks)
            {
                Position(block.Offset);
                foreach (MapScript script in block.Slots)
                {
                    Position(script.Offset);
                    Words(script.Sid, script.Unknown4);
                    Words(script.TypeWords);
                    Words(script.Fields);
                    Position(script.Offset + script.Length);
                }
                Words(block.Used, block.RawNextPointer);
                Position(block.Offset + block.Length);
            }
        }
        void Object(MapObject obj)
        {
            Position(obj.Offset);
            ObjectHeader o = obj.Header;
            Words(o.Id, o.Tile, o.X, o.Y, o.ScreenX, o.ScreenY, o.Frame, o.Rotation, o.Fid, o.Flags,
                o.Elevation, o.Pid, o.Cid, o.LightDistance, o.LightIntensity, o.RawOutline, o.Sid, o.ScriptIndex,
                obj.InventoryCount, obj.InventoryCapacity, obj.RawInventoryPointer);
            foreach (string name in ObjectStateLayout.Names(o.Pid, obj.Subtype)) Word(obj.State[name]);
            Position(obj.Offset + obj.RecordLength);
            foreach (InventoryEntry entry in obj.Inventory)
            {
                Position(entry.QuantityOffset);
                Word(entry.Quantity);
                Object(entry.Object);
            }
            Position(obj.Offset + obj.SubtreeLength);
        }
        Position(map.ObjectsOffset);
        Word(map.DeclaredRootObjects);
        foreach (ObjectLayer layer in map.ObjectLayers)
        {
            Position(layer.CountOffset);
            Word(layer.DeclaredCount);
            foreach (MapObject obj in layer.Objects) Object(obj);
        }
        Position(map.ConsumedBytes);
        return stream.ToArray();
    }

    public static void Verify(MapDocument map)
    {
        if (!Rebuild(map).AsSpan().SequenceEqual(map.Prefix.OriginalBytes.Span))
            throw new InvalidDataException("Parsed MAP fields do not reconstruct the original bytes.");
    }
}
