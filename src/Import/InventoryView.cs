using System.Collections.Generic;
using System.IO;

namespace FalloutRemake.Import;

public sealed record InventoryLine(string ParentKey, int Depth, int Quantity, MapObject Object);
public sealed record InventoryView(string OwnerKey, IReadOnlyList<InventoryLine> Items, bool Truncated)
{
    public const int MaxRows = 1000;
    public static InventoryView Create(MapObject owner, int limit = MaxRows)
    {
        if (limit is < 1 or > MaxRows) throw new InvalidDataException("Invalid inventory inspection limit.");
        var lines = new List<InventoryLine>();
        var keys = new HashSet<string> { owner.Key };
        bool truncated = false;
        void Children(MapObject parent, int depth)
        {
            if (depth > 32) throw new InvalidDataException("Inventory inspection nesting limit exceeded.");
            foreach (InventoryEntry entry in parent.Inventory)
            {
                if (lines.Count == limit) { truncated = true; return; }
                if (entry.Quantity <= 0 || !keys.Add(entry.Object.Key)) throw new InvalidDataException("Invalid quantity or repeated source key in inventory.");
                lines.Add(new InventoryLine(parent.Key, depth, entry.Quantity, entry.Object));
                Children(entry.Object, depth + 1);
                if (truncated) return;
            }
        }
        Children(owner, 0);
        return new InventoryView(owner.Key, lines.AsReadOnly(), truncated);
    }
}
