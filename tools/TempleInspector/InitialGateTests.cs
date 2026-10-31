using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class InitialGateTests
{
    internal static MapDocument Fixture()
    {
        var map = TempleAdapterTests.Fixture(); var first = map.ObjectLayers[0].Objects[0];
        var gate = first with { Key = TempleBarrierEffectProposal.MapHash + ":462744", Offset = 462744, ContainerElevation = 2,
            Header = first.Header with { Id = 104, Tile = 19928, Elevation = 2, Flags = 0, Sid = 50331653, ScriptIndex = 749 } };
        var fields = new int[14]; fields[1] = 749; fields[3] = 104; fields[4] = -1;
        var script = new MapScript(0, 64, 50331653, -1, Array.Empty<int>(), fields);
        var old = map.ScriptGroups.Single(); var block = old.Blocks.Single();
        var prototype = map.Prototypes.Single() with { Resource = new("proto/scenery/00000935.pro", "synthetic trusted metadata", 49,
            "7239F5F5A8A9A595C104E0E72024598E86DDB0B764BA2820F3A4F2DC689BD2C8"), RawBytes = new byte[49] };
        return map with { Prefix = map.Prefix with { GlobalVariables = new int[21],
                Layers = new[] { new MapLayer(0, Array.Empty<SquareTile>()), new MapLayer(2, Array.Empty<SquareTile>()) } },
            ObjectLayers = new[] { map.ObjectLayers[0], new ObjectLayer(2, 0, 1, new[] { gate }) },
            ScriptGroups = new[] { old with { DeclaredCount = 2, Blocks = new[] { block with { Used = 2, Slots = block.Slots.Append(script).ToArray() } } } },
            ScriptBindings = map.ScriptBindings.Append(new ScriptBinding(50331653, 749, new("scripts/aitemdor.int", "synthetic", 13372, TempleInitialGateProfile.ProgramHash))).ToArray(),
            Prototypes = new[] { prototype }, Dependencies = new[] { new ResourceIdentity("scripts/scripts.lst", "synthetic", 104534, TempleInitialGateProfile.ListHash) } };
    }
    private static string Key => TempleBarrierEffectProposal.MapHash + ":462744";
    private static int Adjacent => FalloutGrid.Neighbor(19928, 2)!.Value;
    public static void Run(Action<string, Action> check)
    {
        check("verified initial temple gate is opt-in unlocked trap-inactive and supports open-close without lockpick XP or source mutation", () =>
        {
            var map = Fixture(); Assert(TempleInitialGateProfile.Match(map)?.Key == Key);
            Assert(DoorSession.FromMap(map).CanToggle(Key, Adjacent, 2, false) == DoorUseStatus.ScriptRequired);
            var doors = DoorSession.FromMap(map, true); Assert(doors.InitialTempleGateKey == Key && doors.CanLockpick(Key, Adjacent, 2, false) == DoorUseStatus.ScriptRequired);
            Assert(doors.Toggle(Key, Adjacent, 2, false) == DoorUseStatus.Ready && doors.Navigation(2).Walkable(19928));
            Assert(doors.Toggle(Key, 19928, 2, false) == DoorUseStatus.Occupied && doors.Toggle(Key, Adjacent, 2, false) == DoorUseStatus.Ready);
            Assert(!doors.Navigation(2).Walkable(19928) && doors.TempleDoor!.Experience == 0 && map.AllObjects.Last().State["openFlags"] == 0);
        });
        check("initial gate rejects changed script metadata quest source state instance and prototype identities", () =>
        {
            var map = Fixture(); var vars = new int[21]; vars[20] = 1;
            foreach (var changed in new[] { map with { Source = map.Source with { Sha256 = "different" } },
                map with { Prefix = map.Prefix with { GlobalVariables = vars } }, map with { Dependencies = Array.Empty<ResourceIdentity>() },
                map with { Prototypes = new[] { map.Prototypes[0] with { ScriptId = 1 } } },
                map with { ScriptBindings = map.ScriptBindings.Select(b => b.Index == 749 ? b with { Program = b.Program with { Sha256 = "different" } } : b).ToArray() },
                map with { ObjectLayers = new[] { map.ObjectLayers[0], map.ObjectLayers[1] with { Objects = new[] { map.AllObjects.Last() with {
                    Header = map.AllObjects.Last().Header with { Tile = 13528 } } } } } } })
                Assert(TempleInitialGateProfile.Match(changed) == null);
        });
        check("initial gate interaction still checks actor distance elevation movement and occupancy", () =>
        {
            var doors = DoorSession.FromMap(Fixture(), true);
            Assert(doors.Toggle(Key, Adjacent, 2, true) == DoorUseStatus.Busy && doors.Toggle(Key, Adjacent, 0, false) == DoorUseStatus.Unavailable
                && doors.Toggle(Key, 100, 2, false) == DoorUseStatus.TooFar && doors.Get(Key)?.IsOpen == false);
        });
        check("checkpoint v5 restores an open initial gate and actor on its former blocker without granting lock or jam flags", () =>
        {
            var map = Fixture(); var world = new WorldSession(_ => map, new[] { 3 }); world.Current.Doors.Toggle(Key, Adjacent, 2, false);
            world.Current.Travel.Arrive(new(3, 2, 19928, 0)); var saved = world.Capture("catalog");
            var restored = WorldSession.Restore(_ => map, new[] { 3 }, CheckpointFile.Decode(CheckpointFile.Encode(saved)), "catalog");
            Assert(saved.Version == 10 && restored.Current.Doors.Get(Key)!.IsOpen && restored.Location.Hex == 19928);
            foreach (int flag in new[] { DoorSession.Locked, DoorSession.Jammed })
                Reject(() => WorldSession.Restore(_ => map, new[] { 3 }, saved with { Areas = saved.Areas.Select(a => a with {
                    Session = a.Session with { Doors = a.Session.Doors.Select(d => d.Key == Key ? d with { OpenFlags = d.OpenFlags | flag } : d).ToArray() } }).ToArray() }, "catalog"));
        });
        check("legacy v1-v4 migrate closed original gate but cannot inject the newly supported open state", () =>
        {
            var map = Fixture(); var world = new WorldSession(_ => map, new[] { 3 }); var source = world.Capture("catalog");
            for (int version = 1; version <= 4; version++)
            {
                var legacy = source with { Entries = null, Combat = null, Version = version, Profile = $"neutral-v{version}", Areas = source.Areas.Select(a => a with {
                    Taken = version == 1 ? null : a.Taken, Uses = version < 3 ? null : a.Uses, Replacements = version < 4 ? null : a.Replacements }).ToArray() };
                var restored = WorldSession.Restore(_ => map, new[] { 3 }, legacy, "catalog"); Assert(restored.Capture("catalog").Version == 10 && !restored.Current.Doors.Get(Key)!.IsOpen);
                var forged = legacy with { Areas = legacy.Areas.Select(a => a with { Session = a.Session with { Doors = a.Session.Doors.Select(d => d.Key == Key
                    ? d with { Flags = DoorSession.OpenDoorMask, OpenFlags = 1 } : d).ToArray() } }).ToArray() };
                Reject(() => WorldSession.Restore(_ => map, new[] { 3 }, forged, "catalog"));
            }
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Initial gate assertion failed."); }
    private static void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Unsupported gate state accepted."); }
}
