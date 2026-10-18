using FalloutRemake.Import;

internal static class InventoryViewTests
{
    public static void Run(Action<string, Action> check)
    {
        check("inventory view keeps parent relationships, nested quantities and repeated object IDs by distinct source key", () =>
        {
            MapObject leaf = Item("leaf"), bag = Item("bag", new InventoryEntry(0, 5, leaf));
            MapObject owner = Item("owner", new InventoryEntry(0, 2, bag), new InventoryEntry(0, 3, Item("sibling")));
            InventoryView view = InventoryView.Create(owner);
            Assert(view.Items.Count == 3 && !view.Truncated && view.Items.Select(x => x.Quantity).SequenceEqual(new[] { 2, 5, 3 }));
            Assert(view.Items[0].ParentKey == "owner" && view.Items[1].ParentKey == "bag" && view.Items[1].Depth == 1);
            Assert(view.Items.Select(x => x.Object.Header.Id).Distinct().Count() == 1 && view.Items.Select(x => x.Object.Key).Distinct().Count() == 3);
        });
        check("inventory inspection cap is explicit and never rewrites the underlying owner or child entries", () =>
        {
            MapObject owner = Item("owner", Enumerable.Range(0, 4).Select(i => new InventoryEntry(i, i + 1, Item("child" + i))).ToArray());
            InventoryView view = InventoryView.Create(owner, 2);
            Assert(view.Truncated && view.Items.Count == 2 && owner.Inventory.Length == 4 && owner.Inventory[3].Quantity == 4);
            Assert(!InventoryView.Create(owner, 4).Truncated && InventoryView.Create(Item("empty")).Items.Count == 0);
        });
        check("inventory inspection rejects cyclic/repeated keys, invalid quantities and invalid caps", () =>
        {
            Reject(() => InventoryView.Create(Item("same", new InventoryEntry(0, 1, Item("same")))));
            Reject(() => InventoryView.Create(Item("owner", new InventoryEntry(0, 0, Item("child")))));
            Reject(() => InventoryView.Create(Item("owner"), 0));
            Reject(() => InventoryView.Create(Item("owner"), InventoryView.MaxRows + 1));
            MapObject chain = Item("leaf");
            for (int i = 0; i < 34; i++) chain = Item("parent" + i, new InventoryEntry(0, 1, chain));
            MapObject tooDeep = chain; Reject(() => InventoryView.Create(tooDeep));
        });
    }
    private static MapObject Item(string key, params InventoryEntry[] entries) => new(key, 0, 0, 0, 0,
        new(42, -1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, -1, 0, 0, 0, -1, -1),
        entries.Length, entries.Length, 0, 1, new Dictionary<string, int>(), entries);
    private static void Assert(bool value) { if (!value) throw new Exception("Inventory view assertion failed."); }
    private static void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Invalid inventory view accepted."); }
}
