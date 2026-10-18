using System.Text;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class TempleAdapterTests
{
    private static readonly int Adjacent = FalloutGrid.Neighbor(11108, 2)!.Value;
    private static readonly string Key = $"{TempleDoorAdapter.MapHash}:137396";

    public static void Run(Action<string, Action> check)
    {
        check("temple adapter is opt-in and initializes eight locals without changing the MAP", () =>
        {
            MapDocument map = Fixture();
            Assert(DoorSession.FromMap(map).CanToggle(Key, Adjacent, 0, false) == DoorUseStatus.ScriptRequired);
            DoorSession session = DoorSession.FromMap(map, true);
            Assert(session.Get(Key) is { IsLocked: true, IsOpen: false, Scripted: true, IsJammed: false });
            Assert(session.TempleDoor!.Locals.SequenceEqual(new[] { 0, 1, 0, 1, 0, 0, 0, 0 }));
            Assert(map.AllObjects.First().State["openFlags"] == 0 && session.Get(Key)!.Flags == 32768);
            Assert(session.CanToggle(Key, Adjacent, 0, false) == DoorUseStatus.Locked);
        });
        check("temple adapter rejects unknown MAP, INT, instance, binding and prototype identities", () =>
        {
            MapDocument map = Fixture();
            foreach (MapDocument unknown in new[]
            {
                map with { Source = map.Source with { Sha256 = "different" } },
                map with { Source = map.Source with { Path = "maps/other.map" } },
                map with { Source = map.Source with { Size = 1 } },
                map with { ScriptBindings = new[] { map.ScriptBindings[0] with { Program = map.ScriptBindings[0].Program with { Sha256 = "different" } } } },
                map with { ScriptBindings = new[] { map.ScriptBindings[0] with { Program = map.ScriptBindings[0].Program with { Path = "scripts/other.int" } } } },
                map with { ScriptBindings = new[] { map.ScriptBindings[0] with { Index = 26 } } },
                map with { ScriptBindings = Array.Empty<ScriptBinding>() },
                map with { ScriptBindings = new[] { map.ScriptBindings[0], map.ScriptBindings[0] } },
                map with { Prototypes = new[] { map.Prototypes[0] with { ScriptId = 17 } } },
                map with { ScriptGroups = Array.Empty<ScriptGroup>() },
                ChangeDoor(map, d => d with { Key = "wrong-key" }),
                ChangeDoor(map, d => d with { Header = d.Header with { Id = 103 } }),
                ChangeDoor(map, d => d with { Header = d.Header with { Sid = 50331649 } }),
                ChangeDoor(map, d => d with { Header = d.Header with { Tile = 11109 } }),
                ChangeDoor(map, d => d with { Header = d.Header with { Flags = 0 } }),
                ChangeDoor(map, d => d with { State = new Dictionary<string, int> { ["openFlags"] = 1 } })
            }) Reject(() => DoorSession.FromMap(unknown, true));
            Assert(DoorSession.FromMap(map, true).TempleDoor != null);
        });
        check("lockpick success requests skill 9 plus 100, unlocks without opening and awards 25 XP", () =>
        {
            foreach (SkillRollOutcome outcome in new[] { SkillRollOutcome.Success, SkillRollOutcome.CriticalSuccess })
            {
                DoorSession session = New();
                LockpickResult result = session.UseLockpick(Key, Adjacent, 0, false, request =>
                {
                    Assert(request == new LockpickRequest(Key, 9, 100, LockpickAction.Unlock)); return outcome;
                });
                Assert(result == new LockpickResult(DoorUseStatus.Ready, outcome, 25));
                Assert(session.Get(Key) is { IsLocked: false, IsOpen: false, IsJammed: false, Flags: 32768 });
                Assert(session.TempleDoor!.Locals[0] == 1 && session.TempleDoor.Locals[5] == 1 && session.TempleDoor.Experience == 25);
                Assert(!session.Navigation(0).Walkable(11108));
                Assert(session.Toggle(Key, Adjacent, 0, false) == DoorUseStatus.Ready && session.Navigation(0).Walkable(11108));
            }
        });
        check("ordinary lockpick failure leaves flags, locals and experience unchanged", () =>
        {
            DoorSession session = New(); DoorState before = session.Get(Key)!;
            int[] locals = session.TempleDoor!.Locals.ToArray();
            LockpickResult result = Roll(session, SkillRollOutcome.Failure);
            Assert(result == new LockpickResult(DoorUseStatus.Ready, SkillRollOutcome.Failure));
            Assert(session.Get(Key) == before && session.TempleDoor.Locals.SequenceEqual(locals) && session.TempleDoor.Experience == 0);
        });
        check("critical unlocking failure jams before another resolver can run and reset clears it", () =>
        {
            DoorSession session = New(); Roll(session, SkillRollOutcome.CriticalFailure);
            Assert(session.Get(Key) is { IsJammed: true, IsLocked: true, OpenFlags: DoorSession.Jammed | DoorSession.Locked });
            int calls = 0;
            Assert(session.UseLockpick(Key, Adjacent, 0, false, _ => { calls++; return SkillRollOutcome.Success; }).Status == DoorUseStatus.Jammed && calls == 0);
            session.EnterMap(); Assert(session.Get(Key)!.IsJammed);
            session.Reset(); Assert(session.Get(Key) is { IsJammed: false, IsLocked: true });
            Assert(Roll(session, SkillRollOutcome.Success).ExperienceAwarded == 25);
        });
        check("relocking shares the XP guard and critical relocking failure never jams", () =>
        {
            DoorSession session = New(); Roll(session, SkillRollOutcome.Success);
            Assert(Roll(session, SkillRollOutcome.CriticalFailure).ExperienceAwarded == 0);
            Assert(session.Get(Key) is { IsLocked: false, IsJammed: false });
            LockpickResult relock = session.UseLockpick(Key, Adjacent, 0, false, request =>
            { Assert(request.Action == LockpickAction.Lock); return SkillRollOutcome.Success; });
            Assert(relock.ExperienceAwarded == 0 && session.Get(Key)!.IsLocked);
            Assert(Roll(session, SkillRollOutcome.CriticalSuccess).ExperienceAwarded == 0 && session.TempleDoor!.Experience == 25);
        });
        check("map re-entry preserves solved locals and XP, while reset creates a fresh session", () =>
        {
            DoorSession session = New(); Roll(session, SkillRollOutcome.Success);
            TempleDoorSnapshot old = session.TempleDoor!;
            session.EnterMap(); session.EnterMap();
            Assert(!session.Get(Key)!.IsLocked && session.TempleDoor!.Experience == 25 && session.TempleDoor.Locals[5] == 1);
            session.Reset();
            Assert(session.Get(Key)!.IsLocked && session.TempleDoor!.Experience == 0 && session.TempleDoor.Locals[5] == 0);
            Assert(old.Locals[0] == 1 && old.Experience == 25); // Historical snapshot is independent.
        });
        check("lockpick rejects distance, movement, wrong elevation, invalid hex and unknown door before rolling", () =>
        {
            DoorSession session = New(); int calls = 0;
            SkillRollOutcome Resolve(LockpickRequest _) { calls++; return SkillRollOutcome.Success; }
            Assert(session.UseLockpick(Key, 28906, 0, false, Resolve).Status == DoorUseStatus.TooFar);
            Assert(session.UseLockpick(Key, Adjacent, 0, true, Resolve).Status == DoorUseStatus.Busy);
            Assert(session.UseLockpick(Key, Adjacent, 1, false, Resolve).Status == DoorUseStatus.Unavailable);
            Assert(session.UseLockpick(Key, -1, 0, false, Resolve).Status == DoorUseStatus.Unavailable);
            Assert(session.UseLockpick("missing", Adjacent, 0, false, Resolve).Status == DoorUseStatus.NoDoor && calls == 0);
        });
        check("unknown scripts, repeated object IDs and terminal exits cannot inherit the adapter", () =>
        {
            MapDocument map = Fixture(); MapObject known = map.ObjectLayers[0].Objects[0];
            MapObject other = known with { Key = "other-instance", Offset = 137400, Header = known.Header with { Tile = 11308, Sid = 50331649 } };
            MapObject exit = known with { Key = "exit", Offset = 137600, Subtype = null, Header = known.Header with { Pid = 0x05000010, Fid = 0x05000001, Tile = Adjacent, Sid = -1, ScriptIndex = -1 } };
            map = map with { ObjectLayers = new[] { new ObjectLayer(0, 0, 3, new[] { known, other, exit }) } };
            DoorSession session = DoorSession.FromMap(map, true); int calls = 0;
            SkillRollOutcome Resolve(LockpickRequest _) { calls++; return SkillRollOutcome.Success; }
            Assert(session.UseLockpick(other.Key, Adjacent, 0, false, Resolve).Status == DoorUseStatus.ScriptRequired);
            Assert(session.UseLockpick(Key, Adjacent, 0, false, Resolve).Status == DoorUseStatus.AtExit && calls == 0);
        });
        check("resolver exceptions and invalid outcomes do not partially unlock or award XP", () =>
        {
            DoorSession session = New(); DoorState before = session.Get(Key)!;
            try { session.UseLockpick(Key, Adjacent, 0, false, _ => throw new IOException("resolver failure")); throw new Exception("Expected resolver failure."); }
            catch (IOException) { }
            try { Roll(session, (SkillRollOutcome)99); throw new Exception("Expected invalid outcome rejection."); }
            catch (ArgumentOutOfRangeException) { }
            Assert(session.Get(Key) == before && session.TempleDoor!.Experience == 0 && session.TempleDoor.Locals[5] == 0);
            Assert(Roll(session, SkillRollOutcome.Success).ExperienceAwarded == 25);
        });
        check("nested resolution is refused and results from before a reset are discarded", () =>
        {
            DoorSession session = New(); int calls = 0;
            LockpickResult result = session.UseLockpick(Key, Adjacent, 0, false, _ =>
            {
                Assert(session.UseLockpick(Key, Adjacent, 0, false, _ => { calls++; return SkillRollOutcome.Success; }).Status == DoorUseStatus.Busy);
                session.Reset(); return SkillRollOutcome.Success;
            });
            Assert(result.Status == DoorUseStatus.StateChanged && calls == 0 && session.Get(Key)!.IsLocked && session.TempleDoor!.Experience == 0);
        });
        check("adapter sessions are independent and retain closed-door navigation until explicitly opened", () =>
        {
            MapDocument map = Fixture(); DoorSession first = DoorSession.FromMap(map, true), second = DoorSession.FromMap(map, true);
            Roll(first, SkillRollOutcome.Success); first.Toggle(Key, Adjacent, 0, false);
            Assert(first.Navigation(0).Walkable(11108) && !second.Navigation(0).Walkable(11108));
            Assert(second.Get(Key)!.IsLocked && second.TempleDoor!.Experience == 0 && map.AllObjects.First().State["openFlags"] == 0);
        });
        check("relocking preserves open-state and unrelated flags, matching the mapped script", () =>
        {
            DoorSession session = New(); Roll(session, SkillRollOutcome.Success);
            Assert(session.Toggle(Key, Adjacent, 0, false) == DoorUseStatus.Ready);
            int flags = session.Get(Key)!.Flags;
            Roll(session, SkillRollOutcome.Success); session.EnterMap();
            Assert(session.Get(Key) is { IsOpen: true, IsLocked: true } && session.Get(Key)!.Flags == flags);
            Assert(session.CanToggle(Key, Adjacent, 0, false) == DoorUseStatus.Locked);
            Roll(session, SkillRollOutcome.Success);
            Assert(session.Toggle(Key, Adjacent, 0, false) == DoorUseStatus.Ready && session.Get(Key)!.Flags == 32768);
        });
    }

    private static DoorSession New() => DoorSession.FromMap(Fixture(), true);
    private static LockpickResult Roll(DoorSession session, SkillRollOutcome outcome) => session.UseLockpick(Key, Adjacent, 0, false, _ => outcome);
    private static MapDocument ChangeDoor(MapDocument map, Func<MapObject, MapObject> change) => map with
    { ObjectLayers = new[] { new ObjectLayer(0, 0, 1, new[] { change(map.ObjectLayers[0].Objects[0]) }) } };

    // Synthetic metadata fixture, not original MAP/INT/PRO bytes. Real provenance is checked separately by the CLI scenario.
    internal static MapDocument Fixture()
    {
        var source = new ResourceIdentity("maps/arcaves.map", "synthetic", 526972, TempleDoorAdapter.MapHash);
        var program = new ResourceIdentity("scripts/doorl100.int", "synthetic", 12054, TempleDoorAdapter.ProgramHash);
        var door = new MapObject(Key, 137396, 92, 92, 0,
            new ObjectHeader(102, 11108, 0, 0, 0, 0, 0, 0, 33555377, 32768, 0, 33555366, -1, 0, 0, 0, 50331648, 25),
            0, 0, 0, 0, new Dictionary<string, int> { ["openFlags"] = 0 }, Array.Empty<InventoryEntry>());
        var header = new MapHeader(20, Encoding.ASCII.GetBytes("ARCAVES.MAP"), 28906, 0, 0, 0, 25, 0, 1, 0, 3, 0, new int[44]);
        var prefix = new MapPrefix(header, Array.Empty<int>(), Array.Empty<int>(), new[] { new MapLayer(0, Array.Empty<SquareTile>()) }, 0, ReadOnlyMemory<byte>.Empty);
        var fields = new int[14]; fields[1] = 25; fields[3] = 102; fields[4] = -1;
        var script = new MapScript(122560, 64, 50331648, -1, Array.Empty<int>(), fields);
        return new MapDocument(source, prefix,
            new[] { new ScriptGroup(3, 0, 1, new[] { new ScriptBlock(0, 0, 1, 0, new[] { script }) }) }, 0, 1,
            new[] { new ObjectLayer(0, 0, 1, new[] { door }) },
            new[] { new Prototype(33555366, 2, 0, 33555377, -1, new ResourceIdentity("synthetic.pro", "synthetic", 0, ""), Array.Empty<byte>()) },
            new[] { new ScriptBinding(50331648, 25, program) }, null, Array.Empty<ResourceIdentity>(), Array.Empty<ImportDiagnostic>(), 0);
    }
    private static void Reject(Action action)
    {
        try { action(); } catch (InvalidDataException) { return; }
        throw new Exception("Unknown temple adapter identity accepted.");
    }
    private static void Assert(bool condition) { if (!condition) throw new Exception("Temple door adapter assertion failed."); }
}
