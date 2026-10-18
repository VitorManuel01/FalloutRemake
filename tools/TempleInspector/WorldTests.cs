using System.Text;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class WorldTests
{
    public static void Run(Action<string, Action> check)
    {
        check("map catalog preserves numbered IDs, names and normalized resource paths", () =>
        {
            MapCatalog catalog = Catalog("; comment\n[Map 000]\nlookup_name=First\nmap_name=ZERO ;note\n[Map 001]\nmap_name=one\n");
            Assert(catalog.Get(0) == new MapCatalogEntry(0, "First", "maps/zero.map") && catalog.Get(1).ResourcePath == "maps/one.map");
            Reject(() => catalog.Get(2));
        });
        check("map catalog rejects gaps, repeated IDs, missing names and unsafe paths", () =>
        {
            foreach (string text in new[] { "[Map 001]\nmap_name=a", "[Map 000]\nmap_name=a\n[Map 002]\nmap_name=b",
                "[Map 000]\nmap_name=a\n[Map 000]\nmap_name=b", "[Map 000]\nlookup_name=a",
                "[Map 000]\nmap_name=../outside", "[Map 000]\nmap_name=a\nmap_name=b" }) Reject(() => Catalog(text));
        });
        check("world visits external area and returns to the same cached door session", () =>
        {
            int loads = 0;
            var world = new WorldSession(id => { loads++; return Map(id, id == 3 ? 126 : 3); }, new[] { 3, 126 });
            WorldArea original = world.Current;
            Assert(world.UseExit(false).Status == ExitStatus.Ready && world.CurrentMap == 126 && world.Location.Hex == 20100);
            Assert(world.UseExit(false).Status == ExitStatus.Ready && world.CurrentMap == 3 && ReferenceEquals(original, world.Current) && loads == 2);
        });
        check("failed or mismatched external loads cannot replace the active area", () =>
        {
            var missing = new WorldSession(id => id == 3 ? Map(3, 126) : throw new FileNotFoundException(), new[] { 3, 126 });
            WorldArea before = missing.Current;
            Assert(missing.UseExit(false).Status == ExitStatus.UnavailableMap && ReferenceEquals(before, missing.Current));
            var mismatch = new WorldSession(id => id == 3 ? Map(3, 126) : Map(4, 3), new[] { 3, 126 });
            Assert(mismatch.UseExit(false).Status == ExitStatus.UnavailableMap && mismatch.CurrentMap == 3);
        });
        check("world preflight rejects occupied target and destinations outside its configured area profile", () =>
        {
            var blocked = new WorldSession(id => Map(id, id == 3 ? 126 : 3, id == 126), new[] { 3, 126 });
            Assert(blocked.UseExit(false).Status == ExitStatus.BlockedDestination && blocked.CurrentMap == 3);
            var unsupported = new WorldSession(id => Map(id, 35), new[] { 3, 4, 126 });
            Assert(unsupported.UseExit(false).Status == ExitStatus.UnavailableMap && unsupported.CurrentMap == 3);
        });
        check("busy world exit does not load another MAP and merely inspecting does not move actor", () =>
        {
            int loads = 0;
            var world = new WorldSession(id => { loads++; return Map(id, 126); }, new[] { 3, 126 });
            Assert(world.UseExit(true).Status == ExitStatus.Busy && loads == 1);
            Assert(world.InspectExit(false).Status == ExitStatus.Ready && world.CurrentMap == 3 && loads == 2);
        });
        check("world reset restores all cached door sessions and initial actor location", () =>
        {
            var world = new WorldSession(id => Map(id, id == 3 ? 126 : 3), new[] { 3, 126 });
            string door = world.Current.Map.AllObjects.Last().Key;
            int adjacent = FalloutGrid.Neighbor(20300, 2)!.Value;
            world.Current.Doors.Toggle(door, adjacent, 0, false); world.UseExit(false); world.Reset();
            Assert(world.CurrentMap == 3 && !world.Current.Doors.Get(door)!.IsOpen && world.Location.Hex == 20100 && world.Areas.Count == 2);
        });
        check("same-elevation lifecycle remains explicit instead of discarding cached state", () =>
        {
            var world = new WorldSession(id => Map(id, id), new[] { 3 });
            WorldArea before = world.Current;
            Assert(world.UseExit(false).Status == ExitStatus.ReloadRequired && ReferenceEquals(before, world.Current));
        });
    }
    internal static MapDocument Map(int id, int destination, bool blocked = false)
    {
        var exit = new MapObject($"map{id}:exit", 1, 104, 104, 0,
            new ObjectHeader(1, 20100, 0, 0, 0, 0, 0, 0, 0x05000001, 16, 0, 0x05000010, -1, 0, 0, 0, -1, -1),
            0, 0, 0, null, new Dictionary<string, int> { ["destinationMap"] = destination, ["destinationElevation"] = 0, ["destinationTile"] = 20100, ["destinationRotation"] = 0 }, Array.Empty<InventoryEntry>());
        var door = exit with { Key = $"map{id}:door", Offset = 105, Subtype = 0, Header = exit.Header with { Id = 2, Tile = blocked ? 20100 : 20300, Pid = 0x02000001, Fid = 0x02000001, Flags = 0 }, State = new Dictionary<string, int> { ["openFlags"] = 0 } };
        var header = new MapHeader(20, Encoding.ASCII.GetBytes($"MAP{id}"), 20100, 0, 0, 0, 0, 0, 0, 0, id, 0, new int[44]);
        var prefix = new MapPrefix(header, Array.Empty<int>(), Array.Empty<int>(), new[] { new MapLayer(0, Array.Empty<SquareTile>()) }, 0, ReadOnlyMemory<byte>.Empty);
        return new MapDocument(new ResourceIdentity($"maps/map{id}.map", "synthetic", 0, $"hash{id}"), prefix, Array.Empty<ScriptGroup>(), 0, 2,
            new[] { new ObjectLayer(0, 0, 2, new[] { exit, door }) }, Array.Empty<Prototype>(), Array.Empty<ScriptBinding>(), null, Array.Empty<ResourceIdentity>(), Array.Empty<ImportDiagnostic>(), 0);
    }
    private static MapCatalog Catalog(string text) => new(new GameResource("data/maps.txt", "synthetic", Encoding.Latin1.GetBytes(text)));
    private static void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Invalid catalog accepted."); }
    private static void Assert(bool condition) { if (!condition) throw new Exception("World-session assertion failed."); }
}
