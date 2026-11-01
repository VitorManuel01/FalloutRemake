using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class TempleExitDoorTests
{
    private static (MapDocument Map,TempleNpcDepartureProfile Profile,WorldSession World,string Key) Fixture()
    {
        var (map,old,previous) = WorldTempleSpeechTests.Fixture();
        var gate = map.AllObjects.Single(o => o.Key == old.GateKey);
        var door = gate with { Key = map.Source.Sha256 + ":423748",Offset = 423748,
            Header = gate.Header with { Id = 100,Tile = 13528,Flags = 32768,Sid = 50331651 } };
        var fields = new int[14]; fields[1] = 749; fields[3] = 100; fields[4] = -1;
        var script = new MapScript(0,64,50331651,-1,Array.Empty<int>(),fields);
        var group = map.ScriptGroups.Single(); var block = group.Blocks.Single();
        map = map with { ObjectLayers = map.ObjectLayers.Select(l => l.Elevation == 2 ? l with { Objects = l.Objects.Append(door).ToArray() } : l).ToArray(),
            ScriptBindings = map.ScriptBindings.Append(new ScriptBinding(50331651,749,map.ScriptBindings.Single(b => b.Index == 749).Program)).ToArray(),
            ScriptGroups = new[] { group with { DeclaredCount = group.DeclaredCount + 1,Blocks = new[] { block with { Used = block.Used + 1,Slots = block.Slots.Append(script).ToArray() } } } } };
        var profile = new TempleNpcDepartureProfile(old.Speech,map.ObjectLayers.SelectMany(l => l.Objects).ToArray(),map.AllObjects.Single(o => o.Key == old.ActorKey),gate,
            new(door.Key,new(369,378,"GVAR_DUMAR_STATUS",0)));
        var source = map; var world = new WorldSession(_ => source,new[] { 3 },templeProfile:_ => profile);
        world.Current.Travel.Arrive(previous.Location);
        return (map,profile,world,door.Key);
    }
    public static void Run(Action<string,Action> check)
    {
        check("exit door 100 is source-matched and opens only after accepted Speech without XP or lockpick",() =>
        {
            var (map,profile,world,key) = Fixture(); int adjacent = FalloutGrid.Neighbor(13528,2)!.Value;
            Assert(TempleExitDoorProfile.Match(map)?.Key == key && world.Current.Doors.CanToggle(key,adjacent,2,false) == DoorUseStatus.ScriptRequired);
            world.CommitTempleSpeech(WorldTempleSpeechTests.Plan(world,profile),false);
            Assert(world.Current.Doors.CanLockpick(key,adjacent,2,false) == DoorUseStatus.ScriptRequired
                && world.Current.Doors.Toggle(key,adjacent,2,true) == DoorUseStatus.Busy
                && world.Current.Doors.Toggle(key,adjacent,2,false) == DoorUseStatus.Ready && world.Current.Doors.Navigation(2).Walkable(13528)
                && world.Current.Doors.Toggle(key,13528,2,false) == DoorUseStatus.Occupied && world.Experience == 0
                && map.AllObjects.Single(o => o.Key == key).State["openFlags"] == 0);
            world.Reset(); Assert(world.Current.Doors.CanToggle(key,adjacent,2,false) == DoorUseStatus.ScriptRequired);
        });
        check("v7 restores the open source exit and actor on its hex but rejects legacy or unaccepted opening",() =>
        {
            var (map,profile,world,key) = Fixture(); world.CommitTempleSpeech(WorldTempleSpeechTests.Plan(world,profile),false);
            world.Current.Doors.Toggle(key,FalloutGrid.Neighbor(13528,2)!.Value,2,false); world.Current.Travel.Arrive(new(3,2,13528,0));
            var saved = world.Capture("catalog");
            WorldSession Load(WorldCheckpoint value) => WorldSession.Restore(_ => map,new[] { 3 },value,"catalog",templeProfile:_ => profile);
            var restored = Load(CheckpointFile.Decode(CheckpointFile.Encode(saved)));
            Assert(restored.Location.Hex == 13528 && restored.Current.Doors.Get(key)!.IsOpen && restored.Capture("catalog").Version == 13);
            Reject(() => Load(saved with { Entries = null, Combat = null, Character = null, Duel = null, Skills = null, Areas = saved.Areas.Select(a => a with { Consumed = null }).ToArray(), Version = 6,Profile = "neutral-v6" }));
            Reject(() => Load(saved with { Temple = null }));
            Reject(() => Load(saved with { Temple = saved.Temple! with { Profile = "foreign" } }));
        });
        check("closed v6 Speech progress migrates without opening the exit or changing detached profile identity",() =>
        {
            var (map,profile,world,key) = Fixture(); world.CommitTempleSpeech(WorldTempleSpeechTests.Plan(world,profile),false);
            var saved = world.Capture("catalog") with { Entries = null, Combat = null, Character = null, Duel = null, Skills = null, Areas = world.Capture("catalog").Areas.Select(a => a with { Consumed = null }).ToArray(), Version = 6,Profile = "neutral-v6" };
            var restored = WorldSession.Restore(_ => map,new[] { 3 },saved,"catalog",templeProfile:_ => profile);
            Assert(!restored.Current.Doors.Get(key)!.IsOpen && restored.TempleProgress!.Profile == saved.Temple!.Profile && restored.Capture("catalog").Version == 13);
        });
        check("exit adapter refuses a changed native instance binding and owner",() =>
        {
            var (map,_,_,key) = Fixture();
            Assert(TempleExitDoorProfile.Match(map with { ScriptBindings = map.ScriptBindings.Where(b => b.Sid != 50331651).ToArray() }) == null);
            Assert(TempleExitDoorProfile.Match(map with { ObjectLayers = map.ObjectLayers.Select(l => l with { Objects = l.Objects.Select(o => o.Key == key ? o with { Header = o.Header with { Tile = 13529 } } : o).ToArray() }).ToArray() }) == null);
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Temple exit door assertion failed."); }
    private static void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Unsupported exit state accepted."); }
}
