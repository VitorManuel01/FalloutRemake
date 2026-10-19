using System.Text;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class CheckpointTests
{
    private static readonly int[] Allowed = { 3, 126 };
    private static MapDocument Load(int id) => WorldTests.Map(id, id == 3 ? 126 : 3);
    private static WorldSession Restore(WorldCheckpoint saved, Func<int, MapDocument>? load = null) => WorldSession.Restore(load ?? Load, Allowed, saved, "catalog");
    public static void Run(Action<string, Action> check)
    {
        check("checkpoint round-trip restores exterior actor and inactive open doors into a fresh world", () =>
        {
            var original = new WorldSession(Load, Allowed);
            string key = original.Current.Map.AllObjects.Last().Key;
            original.Current.Doors.Toggle(key, FalloutGrid.Neighbor(20300, 2)!.Value, 0, false);
            original.UseExit(false);
            WorldCheckpoint saved = CheckpointFile.Decode(CheckpointFile.Encode(original.Capture("catalog")));
            original.Reset(); WorldSession restored = Restore(saved);
            Assert(restored.CurrentMap == 126 && restored.Location.Hex == 20100 && restored.Areas.Count == 2);
            restored.UseExit(false);
            Assert(restored.Current.Doors.Get(key)!.IsOpen && !original.Current.Doors.Get(key)!.IsOpen);
        });
        check("checkpoint rejects incompatible schema/profile/catalog and malformed or oversized JSON", () =>
        {
            WorldCheckpoint saved = new WorldSession(Load, Allowed).Capture("catalog");
            Reject(() => Restore(saved with { Version = 5 })); Reject(() => Restore(saved with { Profile = "other" }));
            Reject(() => Restore(saved with { CatalogHash = "other" }));
            Reject(() => CheckpointFile.Decode(Encoding.UTF8.GetBytes("{")));
            Reject(() => CheckpointFile.Decode(new byte[CheckpointFile.MaxBytes + 1]));
            Reject(() => CheckpointFile.Decode(Encoding.UTF8.GetBytes("{\"Unexpected\":1}")));
        });
        check("changed MAP or dependency fingerprints reject restoration without touching the live world", () =>
        {
            var live = new WorldSession(Load, Allowed); WorldCheckpoint saved = live.Capture("catalog");
            Reject(() => Restore(saved, id => Load(id) with { Source = Load(id).Source with { Sha256 = "changed" } }));
            Reject(() => Restore(saved, id => Load(id) with { Dependencies = new[] { new ResourceIdentity("changed.pro", "synthetic", 1, "changed") } }));
            Assert(live.CurrentMap == 3 && live.Location.Hex == 20100 && live.Experience == 0);
        });
        check("checkpoint area IDs and actor destination must belong to a complete valid area set", () =>
        {
            WorldCheckpoint saved = new WorldSession(Load, Allowed).Capture("catalog"); SavedArea area = saved.Areas[0];
            Reject(() => Restore(saved with { CurrentMap = 126 }));
            Reject(() => Restore(saved with { Areas = new[] { area, area } }));
            Reject(() => Restore(saved with { Areas = new[] { area with { Map = 35 } } }));
            foreach (ActorLocation location in new[] { area.Location with { Map = 126 }, area.Location with { Hex = 20300 },
                area.Location with { Elevation = 3 }, area.Location with { Rotation = 6 } })
                Reject(() => Restore(saved with { Areas = new[] { area with { Location = location } } }));
        });
        check("door checkpoint rejects foreign keys, unrelated flags and mutations of unknown scripts", () =>
        {
            var live = new WorldSession(Load, Allowed); WorldCheckpoint saved = live.Capture("catalog");
            SavedArea area = saved.Areas[0]; SavedDoor door = area.Session.Doors[0];
            foreach (SavedDoor corrupt in new[] { door with { Key = "unknown" }, door with { Flags = door.Flags ^ 1 },
                door with { OpenFlags = DoorSession.Locked }, door with { Flags = HexNavigation.NoBlock, OpenFlags = 1 } })
                Reject(() => Restore(saved with { Areas = new[] { area with { Session = area.Session with { Doors = new[] { corrupt } } } } }));
            MapDocument scripted = Load(3); MapObject[] roots = scripted.ObjectLayers[0].Objects;
            roots = roots.Select(o => o.Subtype == 0 ? o with { Header = o.Header with { Sid = 123 } } : o).ToArray();
            scripted = scripted with { ObjectLayers = new[] { new ObjectLayer(0, 0, roots.Length, roots) } };
            SavedDoor opened = door with { Flags = door.Flags | DoorSession.OpenDoorMask, OpenFlags = 1 };
            Reject(() => Restore(saved with { Areas = new[] { area with { Session = area.Session with { Doors = new[] { opened } } } } }, _ => scripted));
            Assert(!live.Current.Doors.Get(door.Key)!.IsOpen);
        });
        check("checkpoint preserves known script locals, single XP reward and jam refusal", () =>
        {
            MapDocument map = TempleAdapterTests.Fixture();
            var live = new WorldSession(_ => map, new[] { 3 });
            string key = live.Current.Doors.TempleDoor!.SourceKey; int adjacent = FalloutGrid.Neighbor(11108, 2)!.Value;
            live.Current.Doors.UseLockpick(key, adjacent, 0, false, _ => SkillRollOutcome.Success);
            WorldSession restored = WorldSession.Restore(_ => map, new[] { 3 }, live.Capture("catalog"), "catalog");
            Assert(restored.Experience == 25 && !restored.Current.Doors.Get(key)!.IsLocked);
            Assert(restored.Current.Doors.UseLockpick(key, adjacent, 0, false, _ => SkillRollOutcome.Success).ExperienceAwarded == 0);
            live.Reset(); live.Current.Doors.UseLockpick(key, adjacent, 0, false, _ => SkillRollOutcome.CriticalFailure);
            restored = WorldSession.Restore(_ => map, new[] { 3 }, live.Capture("catalog"), "catalog");
            int calls = 0;
            Assert(restored.Current.Doors.UseLockpick(key, adjacent, 0, false, _ => { calls++; return SkillRollOutcome.Success; }).Status == DoorUseStatus.Jammed && calls == 0);
        });
        check("inconsistent temple XP or active-trap locals are refused atomically", () =>
        {
            DoorSession doors = DoorSession.FromMap(TempleAdapterTests.Fixture(), true);
            SavedDoorSession saved = doors.Capture();
            Reject(() => doors.Restore(saved with { Experience = 25 }));
            int[] locals = (int[])saved.TempleLocals!.Clone(); locals[1] = 0;
            Reject(() => doors.Restore(saved with { TempleLocals = locals }));
            Assert(doors.TempleDoor!.Experience == 0 && doors.TempleDoor.Locals[1] == 1);
        });
        check("checkpoint metadata contains no source asset archive and snapshots own their local arrays", () =>
        {
            var world = new WorldSession(_ => TempleAdapterTests.Fixture(), new[] { 3 });
            WorldCheckpoint saved = world.Capture("catalog");
            saved.Areas[0].Session.TempleLocals![0] = 1;
            Assert(world.Current.Doors.TempleDoor!.Locals[0] == 0);
            string json = Encoding.UTF8.GetString(CheckpointFile.Encode(world.Capture("catalog")));
            Assert(!json.Contains("OriginalMap") && !json.Contains("RawBytes") && json.Contains("SourceHash"));
        });
    }
    private static void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Invalid checkpoint accepted."); }
    private static void Assert(bool condition) { if (!condition) throw new Exception("Checkpoint assertion failed."); }
}
