using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class CheckpointTests
{
    private static readonly int[] Allowed = { 3, 126 };
    private static MapDocument Load(int id) => WorldTests.Map(id, id == 3 ? 126 : 3);
    private static WorldSession Restore(WorldCheckpoint saved, Func<int, MapDocument>? load = null) => WorldSession.Restore(load ?? Load, Allowed, saved, "catalog");
    private static (WorldSession World, Func<int, MapDocument> Load, TempleNpcDepartureProfile TempleProfile, ArroyoElderStateProfile ElderProfile) ElderSessionFixture()
    {
        var (temple, profile, original) = WorldTempleSpeechTests.Fixture();
        var exit = WorldTests.Map(3, 4).ObjectLayers[0].Objects[0];
        exit = exit with
        {
            Key = temple.Source.Sha256 + ":422028",
            ContainerElevation = 2,
            Header = exit.Header with { Tile = 13326, Elevation = 2 },
            State = new Dictionary<string, int> { ["destinationMap"] = 4, ["destinationElevation"] = 0, ["destinationTile"] = 20517, ["destinationRotation"] = 0 }
        };
        temple = temple with { ObjectLayers = temple.ObjectLayers.Select(l => l.Elevation == 2 ? l with { Objects = l.Objects.Append(exit).ToArray() } : l).ToArray() };
        var village = WorldTests.Map(4, 3);
        var template = village.ObjectLayers[0].Objects[0];
        var elder = template with
        {
            Key = ArroyoRewardProfile.MapHash + ":126588",
            Offset = 126588,
            State = new Dictionary<string, int>(),
            Header = template.Header with { Id = 20, Pid = 16777229, Fid = 16777247, Tile = 18915, Elevation = 0, Rotation = 2, Sid = 67108867, ScriptIndex = 10, Flags = 536870912 }
        };
        village = village with
        {
            Source = village.Source with { Path = "maps/arvillag.map", Size = 216200, Sha256 = ArroyoRewardProfile.MapHash },
            Prefix = village.Prefix with { Header = village.Prefix.Header with { ScriptIndex = 27, EnteringTile = 20517 } },
            MapProgram = new("scripts/arvillag.int", "synthetic verified profile", 5188, ArroyoRewardProfile.ProgramHash),
            ObjectLayers = village.ObjectLayers.Select((l, i) => l with
            {
                Objects = l.Objects.Select(o => (o.Header.Pid >> 24) != 5 ? o : o with
                {
                    Header = o.Header with { Tile = 20517 },
                    State = new Dictionary<string, int> { ["destinationMap"] = 3, ["destinationElevation"] = 2, ["destinationTile"] = 13326, ["destinationRotation"] = 0 }
                }).Concat(i == 0 ? new[] { elder } : Array.Empty<MapObject>()).ToArray()
            }).ToArray(),
            ScriptBindings = new[] { new ScriptBinding(67108867, 10, new("scripts/ahelder.int", "synthetic", 20658, ArroyoElderSource.ProgramHash)) }
        };
        var elderProfile = new ArroyoElderStateProfile();
        MapDocument Loader(int id) => id == 3 ? temple : id == 4 ? village : WorldTests.Map(126, 3);
        var world = new WorldSession(Loader, new[] { 3, 4, 126 }, templeProfile: _ => profile, arroyoProfile: _ => new ArroyoRewardProfile(), elderProfile: _ => elderProfile);
        world.Current.Travel.Arrive(original.Location);
        world.CommitTempleSpeech(WorldTempleSpeechTests.Plan(world, profile), false);
        world.Current.Travel.Arrive(new(3, 2, 13326, 0));
        Assert(world.UseExit(false).Status == ExitStatus.Ready && world.CurrentMap == 4);
        var nav = world.Current.Doors.Navigation(0);
        int adjacent = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(18915, d)!.Value).First(h => nav.Walkable(h) && !nav.IsExit(h));
        world.Current.Travel.Arrive(new(4, 0, adjacent, 0));
        return (world, Loader, profile, elderProfile);
    }
    private static WorldSession RestoreElder(WorldCheckpoint saved, Func<int, MapDocument> load, TempleNpcDepartureProfile templeProfile, ArroyoElderStateProfile elderProfile)
        => WorldSession.Restore(load, new[] { 3, 4, 126 }, saved, "catalog", templeProfile: _ => templeProfile, arroyoProfile: _ => new ArroyoRewardProfile(), elderProfile: _ => elderProfile);
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
            Reject(() => Restore(saved with { Version = 99 })); Reject(() => Restore(saved with { Profile = "other" }));
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
        check("v10 checkpoint JSON round-trips SavedElderState across all audited first-talk paths preserving XP and doors", () =>
        {
            foreach (var (route, reaction, delta) in new[] {
                (new[] { 11868, 16348 }, 22, -10),
                (new[] { 11804, 14664, 16348 }, 12, -20),
                (new[] { 11804, 14696, 17510 }, 12, -20) })
            {
                var (world, load, templeProfile, elderProfile) = ElderSessionFixture();
                string villageDoor = world.Current.Map.AllObjects.First(o => o.Subtype == 0).Key;
                world.Current.Doors.Toggle(villageDoor, FalloutGrid.Neighbor(20300, 2)!.Value, 0, false);
                world.CommitElderDialogue(route);
                byte[] encoded = CheckpointFile.Encode(world.Capture("catalog"));
                WorldCheckpoint decoded = CheckpointFile.Decode(encoded);
                Assert(decoded.Version == 13 && decoded.Profile == "neutral-v13" && decoded.Elder != null
                    && decoded.Elder.Complete && decoded.Elder.Procedure == "Node999" && decoded.Elder.Reaction == reaction
                    && decoded.Elder.Choices.SequenceEqual(route) && decoded.Elder.Global(531) == 1
                    && decoded.Elder.Local(0) == reaction && decoded.Elder.Local(1) == 1 && decoded.Elder.Local(2) == 1
                    && decoded.Elder.Local(3) == delta && decoded.Elder.Local(4) == 1);
                var restored = RestoreElder(decoded, load, templeProfile, elderProfile);
                Assert(encoded.SequenceEqual(CheckpointFile.Encode(restored.Capture("catalog")))
                    && restored.TalkedToElder == 1 && restored.Experience == 600
                    && restored.Current.Doors.Get(villageDoor)!.IsOpen
                    && restored.ElderDialogue!.Reaction == reaction && restored.ElderDialogue.Local(3) == delta);
            }
        });
        check("restored v10 SavedElderState refuses duplicate session commit and enables repeat Node004 projections", () =>
        {
            var (world, load, templeProfile, elderProfile) = ElderSessionFixture();
            world.CommitElderDialogue(new[] { 11868, 16348 });
            var restored = RestoreElder(CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog"))), load, templeProfile, elderProfile);
            Reject(() => restored.CommitElderDialogue(new[] { 11868, 16348 }));
            var repeatExit = elderProfile.ProjectRepeat(restored.ElderDialogue!, new[] { 14006 });
            var repeatNode013 = elderProfile.ProjectRepeat(restored.ElderDialogue!, new[] { 13902, 17510 });
            Assert(repeatExit.Complete && repeatExit.Procedure == "Node999" && repeatExit.Reaction == 22
                && repeatNode013.Complete && repeatNode013.Procedure == "Node999" && repeatNode013.Reaction == 2
                && restored.TalkedToElder == 1 && restored.ElderDialogue!.Reaction == 22);
        });
        check("legacy checkpoints v1 through v9 migrate deterministically to v10 with null Elder state and zero TalkedToElder", () =>
        {
            var (world, load, templeProfile, elderProfile) = ElderSessionFixture();
            var baseV10 = world.Capture("catalog");
            for (int version = 1; version <= 9; version++)
            {
                var areas = version < 6 ? baseV10.Areas.Where(a => a.Map == 3).ToArray() : baseV10.Areas;
                var legacy = baseV10 with
                {
                    Version = version,
                    Profile = $"neutral-v{version}",
                    CurrentMap = version < 6 ? 3 : baseV10.CurrentMap,
                    Areas = areas.Select(a => a with
                    {
                        Taken = version < 2 ? null : a.Taken,
                        Uses = version < 3 ? null : a.Uses,
                        Replacements = version < 4 ? null : a.Replacements,
                        Consumed = null,
                        Ground = null,
                        Session = version < 7 && a.Map == 3
                            ? a.Session with { Doors = a.Session.Doors.Select(d => d.Key == templeProfile.GateKey ? new SavedDoor(d.Key, 0, 0) : d).ToArray() }
                            : a.Session
                    }).ToArray(),
                    Temple = version < 6 ? null : baseV10.Temple! with { InitialGateOpen = version >= 7 && baseV10.Temple.InitialGateOpen },
                    Entries = version < 8 ? null : version == 8 ? baseV10.Entries! with { ArroyoGlobals = null } : baseV10.Entries,
                    Combat = null,
                    Character = null,
                    Duel = null,
                    Skills = null,
                    Firearms = null,
                    Elder = null
                };
                var node = JsonNode.Parse(CheckpointFile.Encode(legacy))!.AsObject();
                node.Remove("Combat"); node.Remove("Character"); node.Remove("Duel"); node.Remove("Skills"); node.Remove("Firearms"); node.Remove("Elder");
                if (version < 6) node.Remove("Temple");
                if (version < 8) node.Remove("Entries");
                else if (version == 8) node["Entries"]!.AsObject().Remove("ArroyoGlobals");
                var decoded = CheckpointFile.Decode(Encoding.UTF8.GetBytes(node.ToJsonString()));
                var migrated = RestoreElder(decoded, load, templeProfile, elderProfile);
                Assert(migrated.TalkedToElder == 0 && migrated.ElderDialogue == null && migrated.SavedElder == null
                    && migrated.Capture("catalog").Version == 13 && migrated.Capture("catalog").Elder == null);
            }
        });
        check("legacy checkpoints v1 through v9 with non-null SavedElderState are rejected atomically", () =>
        {
            var (world, load, templeProfile, elderProfile) = ElderSessionFixture();
            world.CommitElderDialogue(new[] { 11868, 16348 });
            var saved = world.Capture("catalog");
            for (int version = 1; version <= 9; version++)
                Reject(() => RestoreElder(saved with { Version = version, Profile = $"neutral-v{version}", Combat = null, Character = null, Duel = null, Skills = null, Firearms = null }, load, templeProfile, elderProfile));
        });
        check("checkpoint encode decode and file read refuse payloads exceeding the 1 MB limit", () =>
        {
            var (world, _, _, _) = ElderSessionFixture();
            world.CommitElderDialogue(new[] { 11868, 16348 });
            var saved = world.Capture("catalog");
            Reject(() => CheckpointFile.Encode(saved with { CatalogHash = new string('A', CheckpointFile.MaxBytes) }));
            Reject(() => CheckpointFile.Decode(new byte[CheckpointFile.MaxBytes + 1]));
            Reject(() => CheckpointFile.Decode(Array.Empty<byte>()));
        });
        check("v10 checkpoint rejects tampered SavedElderState metadata choices reaction and completion flags", () =>
        {
            var (world, load, templeProfile, elderProfile) = ElderSessionFixture();
            world.CommitElderDialogue(new[] { 11868, 16348 });
            var saved = world.Capture("catalog");
            var elder = saved.Elder!;
            foreach (var bad in new[] {
                elder with { Profile = "foreign" },
                elder with { Procedure = "Node001" },
                elder with { Complete = false },
                elder with { Reaction = 12 },
                elder with { Choices = new[] { 11804, 14664, 16348 } },
                elder with { Choices = new[] { 11804 } },
                elder with { Choices = Array.Empty<int>() } })
                Reject(() => RestoreElder(saved with { Elder = bad }, load, templeProfile, elderProfile));
        });
        check("v10 checkpoint rejects tampered SavedElderState local and global script variable blocks", () =>
        {
            var (world, load, templeProfile, elderProfile) = ElderSessionFixture();
            world.CommitElderDialogue(new[] { 11868, 16348 });
            byte[] baseline = CheckpointFile.Encode(world.Capture("catalog"));
            var zeroG531 = CheckpointFile.Decode(baseline);
            var globals = zeroG531.Elder!.Scripts.Blocks.Single(b => b.Scope == ScriptVariableScope.GameGlobal);
            globals.Values[Array.IndexOf(globals.Indices, 531)] = 0;
            Reject(() => RestoreElder(zeroG531, load, templeProfile, elderProfile));
            var badLocal = CheckpointFile.Decode(baseline);
            badLocal.Elder!.Scripts.Blocks.Single(b => b.Scope == ScriptVariableScope.ScriptLocal).Values[0] = 32;
            Reject(() => RestoreElder(badLocal, load, templeProfile, elderProfile));
            var badQuest = CheckpointFile.Decode(baseline);
            badLocal = CheckpointFile.Decode(baseline);
            badLocal.Elder!.Scripts.Blocks.Single(b => b.Scope == ScriptVariableScope.ScriptLocal).Values[7] = 1;
            Reject(() => RestoreElder(badLocal, load, templeProfile, elderProfile));
            var badSchema = CheckpointFile.Decode(baseline);
            badSchema = badSchema with { Elder = badSchema.Elder! with { Scripts = badSchema.Elder.Scripts with { SchemaHash = "foreign" } } };
            Reject(() => RestoreElder(badSchema, load, templeProfile, elderProfile));
        });
        check("v10 checkpoint rejects SavedElderState when Arroyo entry reward globals Temple progress or actor are missing", () =>
        {
            var (world, load, templeProfile, elderProfile) = ElderSessionFixture();
            world.CommitElderDialogue(new[] { 11868, 16348 });
            var saved = world.Capture("catalog");
            Reject(() => RestoreElder(saved with { Temple = null }, load, templeProfile, elderProfile));
            Reject(() => RestoreElder(saved with { Entries = saved.Entries! with { ArroyoReward = null, ArroyoGlobals = null } }, load, templeProfile, elderProfile));
            Reject(() => RestoreElder(saved with { CurrentMap = 3, Areas = saved.Areas.Where(a => a.Map == 3).ToArray(), Entries = new(new[] { 3 }, null, null) }, load, templeProfile, elderProfile));
            Reject(() => WorldSession.Restore(id => id == 4 ? WorldTests.Map(4, 3) : load(id), new[] { 3, 4, 126 }, saved, "catalog",
                templeProfile: _ => templeProfile, arroyoProfile: _ => new ArroyoRewardProfile(), elderProfile: _ => elderProfile));
        });
        check("v10 JSON decoder enforces explicit Elder property required fields and rejects duplicate or unknown properties", () =>
        {
            var (world, _, _, _) = ElderSessionFixture();
            world.CommitElderDialogue(new[] { 11868, 16348 });
            byte[] bytes = CheckpointFile.Encode(world.Capture("catalog"));
            var missingRoot = JsonNode.Parse(bytes)!.AsObject();
            missingRoot.Remove("Elder");
            Reject(() => CheckpointFile.Decode(Encoding.UTF8.GetBytes(missingRoot.ToJsonString())));
            foreach (string field in new[] { "Profile", "Choices", "Procedure", "Complete", "Reaction", "Scripts" })
            {
                var node = JsonNode.Parse(bytes)!;
                node["Elder"]!.AsObject().Remove(field);
                Reject(() => CheckpointFile.Decode(Encoding.UTF8.GetBytes(node.ToJsonString())));
            }
            var extra = JsonNode.Parse(bytes)!;
            extra["Elder"]!.AsObject()["Extra"] = 1;
            Reject(() => CheckpointFile.Decode(Encoding.UTF8.GetBytes(extra.ToJsonString())));
            string raw = Encoding.UTF8.GetString(bytes);
            Reject(() => CheckpointFile.Decode(Encoding.UTF8.GetBytes(raw.Replace("\"Reaction\": 22", "\"Reaction\": 22, \"Reaction\": 22"))));
        });
        check("SavedElderState and WorldSession Capture own all choice and script arrays against external mutation", () =>
        {
            var (world, _, _, _) = ElderSessionFixture();
            world.CommitElderDialogue(new[] { 11868, 16348 });
            string expected = Encoding.UTF8.GetString(CheckpointFile.Encode(world.Capture("catalog")));
            var captured = world.Capture("catalog");
            captured.Elder!.Choices[0] = -1;
            captured.Elder.Scripts.Blocks[0].Values[0] = 999;
            var savedElder = world.SavedElder!;
            savedElder.Choices[0] = -1;
            savedElder.Scripts.Blocks[0].Values[0] = 999;
            Assert(Encoding.UTF8.GetString(CheckpointFile.Encode(world.Capture("catalog"))) == expected
                && world.ElderDialogue!.Reaction == 22 && world.TalkedToElder == 1);
        });
        check("checkpoint JSON round-trips repeat SavedElderState RepeatChoices and rejects tampered repeat routes or reactions", () =>
        {
            foreach (var (firstRoute, firstReaction, repeatRoute, repeatReaction, repeatDelta) in new[] {
                (new[] { 11868, 16348 }, 22, new[] { 14006 }, 22, -10),
                (new[] { 11868, 16348 }, 22, new[] { 13902, 17510 }, 2, -30),
                (new[] { 11804, 14664, 16348 }, 12, new[] { 13902, 17510 }, -8, -40) })
            {
                var (world, load, templeProfile, elderProfile) = ElderSessionFixture();
                world.CommitElderDialogue(firstRoute);
                var afterFirst = RestoreElder(CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog"))), load, templeProfile, elderProfile);
                Assert(afterFirst.ElderRepeatDialogue == null && afterFirst.SavedElder!.RepeatChoices == null);
                afterFirst.CommitElderRepeatDialogue(repeatRoute);
                byte[] encoded = CheckpointFile.Encode(afterFirst.Capture("catalog"));
                WorldCheckpoint decoded = CheckpointFile.Decode(encoded);
                Assert(decoded.Elder != null && decoded.Elder.Choices.SequenceEqual(firstRoute)
                    && decoded.Elder.RepeatChoices != null && decoded.Elder.RepeatChoices.SequenceEqual(repeatRoute)
                    && decoded.Elder.Reaction == repeatReaction && decoded.Elder.Local(0) == repeatReaction
                    && decoded.Elder.Local(3) == repeatDelta && decoded.Elder.Global(531) == 1);
                var restored = RestoreElder(decoded, load, templeProfile, elderProfile);
                Assert(encoded.SequenceEqual(CheckpointFile.Encode(restored.Capture("catalog")))
                    && restored.TalkedToElder == 1 && restored.ElderDialogue!.Reaction == firstReaction
                    && restored.ElderRepeatDialogue!.Reaction == repeatReaction
                    && restored.ElderRepeatDialogue.Choices.SequenceEqual(repeatRoute)
                    && restored.ElderState!.Reaction == repeatReaction && restored.ElderState.Local(3) == repeatDelta);
                Reject(() => restored.CommitElderDialogue(firstRoute));
                Reject(() => restored.CommitElderRepeatDialogue(repeatRoute));
                var elder = decoded.Elder!;
                foreach (var bad in new[] {
                    elder with { RepeatChoices = Array.Empty<int>() },
                    elder with { RepeatChoices = new[] { 13902 } },
                    elder with { RepeatChoices = new[] { 13814 } },
                    elder with { Choices = new[] { 11804 } },
                    elder with { Reaction = firstReaction + 5 } })
                    Reject(() => RestoreElder(decoded with { Elder = bad }, load, templeProfile, elderProfile));
            }
        });
    }
    private static void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Invalid checkpoint accepted."); }
    private static void Assert(bool condition) { if (!condition) throw new Exception("Checkpoint assertion failed."); }
}
