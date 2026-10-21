using System.Text.Json.Nodes;
using System.Text;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class WorldEntryTests
{
    private static (WorldSession World,Func<int,MapDocument> Load,TempleNpcDepartureProfile Profile) Fixture(Func<MapDocument,ArroyoRewardProfile>? reward = null)
    {
        var (temple,profile,original) = WorldTempleSpeechTests.Fixture();
        var exit = WorldTests.Map(3,4).ObjectLayers[0].Objects[0];
        exit = exit with { Key = temple.Source.Sha256 + ":422028",ContainerElevation = 2,Header = exit.Header with { Tile = 13326,Elevation = 2 },
            State = new Dictionary<string,int> { ["destinationMap"] = 4,["destinationElevation"] = 0,["destinationTile"] = 20517,["destinationRotation"] = 0 } };
        temple = temple with { ObjectLayers = temple.ObjectLayers.Select(l => l.Elevation == 2 ? l with { Objects = l.Objects.Append(exit).ToArray() } : l).ToArray() };
        var village = WorldTests.Map(4,3);
        village = village with { Source = village.Source with { Path = "maps/arvillag.map",Size = 216200,Sha256 = ArroyoRewardProfile.MapHash },
            Prefix = village.Prefix with { Header = village.Prefix.Header with { ScriptIndex = 27,EnteringTile = 20517 } },
            MapProgram = new("scripts/arvillag.int","synthetic verified profile",5188,ArroyoRewardProfile.ProgramHash),
            ObjectLayers = village.ObjectLayers.Select(l => l with { Objects = l.Objects.Select(o => (o.Header.Pid >> 24) != 5 ? o : o with { Header = o.Header with { Tile = 20517 },
                State = new Dictionary<string,int> { ["destinationMap"] = 3,["destinationElevation"] = 2,["destinationTile"] = 13326,["destinationRotation"] = 0 } }).ToArray() }).ToArray() };
        var source = temple; var destination = village;
        MapDocument Load(int id) => id == 3 ? source : id == 4 ? destination : WorldTests.Map(126,3);
        var world = new WorldSession(Load,new[] {3,4,126},templeProfile:_ => profile,arroyoProfile:reward ?? (_ => new ArroyoRewardProfile()));
        world.Current.Travel.Arrive(original.Location); world.CommitTempleSpeech(WorldTempleSpeechTests.Plan(world,profile),false);
        world.Current.Travel.Arrive(new(3,2,13326,0));
        return (world,Load,profile);
    }
    private static WorldSession Restore(WorldCheckpoint saved,Func<int,MapDocument> load,TempleNpcDepartureProfile profile)
        => WorldSession.Restore(load,new[] {3,4,126},saved,"catalog",templeProfile:_ => profile,arroyoProfile:_ => new ArroyoRewardProfile());
    public static void Run(Action<string,Action> check)
    {
        check("entry history distinguishes hover destination loading and actual travel and grants Speech XP exactly once",() =>
        {
            var (world,_,_) = Fixture();
            Assert(world.EnteredMaps.SequenceEqual(new[] {3}) && world.Areas.Count == 1 && world.Experience == 0);
            Assert(world.PreviewExitAt(2,13326).Status == ExitStatus.ExternalMap && world.Areas.Count == 1);
            Assert(world.InspectExit(false).Status == ExitStatus.Ready && world.Areas.Count == 2 && world.EnteredMaps.SequenceEqual(new[] {3}) && world.ArroyoReward == null);
            Assert(world.UseExit(true).Status == ExitStatus.Busy && world.Experience == 0);
            Assert(world.UseExit(false).Status == ExitStatus.Ready && world.CurrentMap == 4 && world.Experience == 600 && world.EnteredMaps.SequenceEqual(new[] {3,4}));
            Assert(world.UseExit(false).Status == ExitStatus.Ready && world.CurrentMap == 3);
            Assert(world.UseExit(false).Status == ExitStatus.Ready && world.CurrentMap == 4 && world.Experience == 600);
            int[] copy = world.EnteredMaps; copy[0] = -1; Assert(world.EnteredMaps[0] == 3);
        });
        check("v8 checkpoints preserve unentered cached destinations then retain a single reward through return and reload",() =>
        {
            var (world,load,profile) = Fixture(); world.InspectExit(false);
            var ready = Restore(CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog"))),load,profile);
            Assert(!ready.EnteredMaps.Contains(4) && ready.UseExit(false).Status == ExitStatus.Ready && ready.Experience == 600);
            byte[] awarded = CheckpointFile.Encode(ready.Capture("catalog"));
            var restored = Restore(CheckpointFile.Decode(awarded),load,profile);
            Assert(awarded.SequenceEqual(CheckpointFile.Encode(restored.Capture("catalog"))) && restored.Experience == 600);
            restored.UseExit(false); restored.UseExit(false); Assert(restored.Experience == 600);
            restored.Reset(); Assert(restored.Experience == 0 && restored.ArroyoReward == null && restored.EnteredMaps.SequenceEqual(new[] {3}));
        });
        check("legacy v7 cached Arroyo migrates conservatively without retroactive reward while uncached Arroyo remains eligible",() =>
        {
            var (world,load,profile) = Fixture();
            var old = world.Capture("catalog") with {Version = 7,Profile = "neutral-v7",Entries = null};
            var fresh = Restore(old,load,profile); fresh.UseExit(false); Assert(fresh.Experience == 600);
            world.InspectExit(false); old = world.Capture("catalog") with {Version = 7,Profile = "neutral-v7",Entries = null};
            var conservative = Restore(old,load,profile);
            Assert(conservative.EnteredMaps.Contains(4) && conservative.UseExit(false).Status == ExitStatus.Ready && conservative.Experience == 0);
        });
        check("reward validation failure preserves active area authoritative position entry history and all XP",() =>
        {
            var (world,_,_) = Fixture(_ => throw new InvalidDataException("Changed native program")); world.InspectExit(false);
            byte[] before = CheckpointFile.Encode(world.Capture("catalog"));
            Assert(world.UseExit(false).Status == ExitStatus.UnavailableMap && world.CurrentMap == 3 && world.Experience == 0
                && before.SequenceEqual(CheckpointFile.Encode(world.Capture("catalog"))));
        });
        check("v8 refuses duplicate unknown incomplete entry maps forged reward amounts identities and missing strict JSON",() =>
        {
            var (world,load,profile) = Fixture(); world.UseExit(false); var saved = world.Capture("catalog");
            foreach (int[] maps in new[] {Array.Empty<int>(),new[] {3,3,4},new[] {3,999},new[] {3},new[] {4}})
                Reject(() => Restore(saved with {Entries = saved.Entries! with {EnteredMaps = maps}},load,profile));
            foreach (var reward in new[] {saved.Entries!.ArroyoReward! with {Experience = 601},saved.Entries.ArroyoReward! with {Profile = "foreign"}})
                Reject(() => Restore(saved with {Entries = saved.Entries with {ArroyoReward = reward}},load,profile));
            Reject(() => Restore(saved with {Temple = null},load,profile));
            foreach (string name in new[] {"Entries","EnteredMaps","ArroyoReward"})
            {
                var node = JsonNode.Parse(CheckpointFile.Encode(saved))!;
                if (name == "Entries") node.AsObject().Remove(name); else node["Entries"]!.AsObject().Remove(name);
                Reject(() => CheckpointFile.Decode(Encoding.UTF8.GetBytes(node.ToJsonString())));
            }
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("World entry/reward assertion failed."); }
    private static void Reject(Action action) { try {action();} catch (InvalidDataException) {return;} throw new Exception("Invalid world entry checkpoint accepted."); }
}
