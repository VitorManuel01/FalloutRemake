using System.Text.Json.Nodes;
using System.Text;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class WorldEntryTests
{
    private static (WorldSession World,Func<int,MapDocument> Load,TempleNpcDepartureProfile Profile) Fixture(Func<MapDocument,ArroyoRewardProfile>? reward = null,bool ambiguousReturn = false,Func<MapDocument,MapDocument>? adjustTemple = null)
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
        if (ambiguousReturn)
        {
            var marker = village.ObjectLayers[0].Objects[0];
            village = village with {ObjectLayers = village.ObjectLayers.Select(l => l with {Objects = l.Objects.Append(marker with {
                Key = marker.Key + ":conflicting",State = new Dictionary<string,int>(marker.State) {["destinationMap"] = 126}}).ToArray()}).ToArray()};
        }
        var source = adjustTemple?.Invoke(temple) ?? temple; var destination = village;
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
            Assert(world.EnteredMaps.SequenceEqual(new[] {3}) && world.Areas.Count == 1 && world.Experience == 0 && world.ArroyoGlobals == null);
            Assert(world.PreviewExitAt(2,13326).Status == ExitStatus.ExternalMap && world.Areas.Count == 1);
            Assert(world.InspectExit(false).Status == ExitStatus.Ready && world.Areas.Count == 2 && world.EnteredMaps.SequenceEqual(new[] {3}) && world.ArroyoReward == null);
            Assert(world.UseExit(true).Status == ExitStatus.Busy && world.Experience == 0);
            Assert(world.UseExit(false).Status == ExitStatus.Ready && world.CurrentMap == 4 && world.Experience == 600 && world.EnteredMaps.SequenceEqual(new[] {3,4}));
            var globals = world.ArroyoGlobals;
            Assert(globals == new ArroyoRewardProfile().EntryGlobals() && globals.Karma == 15 && globals.ArroyoReputation == 65 && globals.KarmaTierGlobal == 41);
            Assert(world.UseExit(false).Status == ExitStatus.Ready && world.CurrentMap == 3);
            Assert(world.UseExit(false).Status == ExitStatus.Ready && world.CurrentMap == 4 && world.Experience == 600 && world.ArroyoGlobals == globals);
            int[] copy = world.EnteredMaps; copy[0] = -1; Assert(world.EnteredMaps[0] == 3);
        });
        check("the Arroyo first-entry reward is granted at any level now that level-ups are modeled",() =>
        {
            var probe = Fixture().World; int near = Enumerable.Range(0,6).Select(d => FalloutRemake.Geometry.FalloutGrid.Neighbor(13326,d)!.Value)
                .First(t => probe.Current.Doors.Navigation(2).Walkable(t) && !probe.Current.Doors.Navigation(2).IsExit(t));
            var (world,load,profile) = Fixture(adjustTemple: map => map with {
                ObjectLayers = map.ObjectLayers.Select(l => l.Elevation != 2 ? l : l with { Objects = l.Objects.Append(CombatTests.Critter(near,0x0100017E,6,1,map.Source.Sha256 + ":veteran") with {
                    ContainerElevation = 2,Header = CombatTests.Critter(near,0x0100017E,6,1,"x").Header with { Elevation = 2 } }).ToArray() }).ToArray(),
                Prototypes = map.Prototypes.Append(CombatTests.AntPrototype(experience: 500)).ToArray() });
            var fight = world.BeginCombat(world.FindEncounter(world.Location)!,FalloutRemake.Runtime.Combat.CombatWeapon.Unarmed(),new CombatTests.Scripted(99,1,3,1,3));
            for (int guard = 0; guard < 10 && !fight.Turn.Over; guard++)
            {
                if (!fight.Active!.IsPlayer) { FalloutRemake.Runtime.Combat.ProvisionalMeleeBehavior.TakeTurn(fight); continue; }
                var target = fight.Combatants.Single(c => !c.IsPlayer).Id;
                if (fight.Execute(FalloutRemake.Runtime.Combat.CombatActionProposal.Attack("player",target)).Status != FalloutRemake.Runtime.Combat.CombatActionStatus.Ready)
                    fight.Execute(FalloutRemake.Runtime.Combat.CombatActionProposal.EndTurn("player"));
            }
            Assert(world.CommitCombat(fight) == CombatCommitStatus.Ready && world.Experience == 500 && world.Level == 1);
            Assert(world.UseExit(false).Status == ExitStatus.Ready && world.CurrentMap == 4 && world.Experience == 1100 && world.Level == 2
                && world.MaxHitPoints == 34 && world.ArroyoReward?.Experience == 600);
            var restored = Restore(world.Capture("catalog"),load,profile);
            Assert(restored.Experience == 1100 && restored.Level == 2 && restored.ArroyoReward?.Experience == 600);
        });
        check("v9 checkpoints preserve unentered cached destinations then retain one reward and native globals through return and reload",() =>
        {
            var (world,load,profile) = Fixture(); world.InspectExit(false);
            var ready = Restore(CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog"))),load,profile);
            Assert(!ready.EnteredMaps.Contains(4) && ready.UseExit(false).Status == ExitStatus.Ready && ready.Experience == 600);
            byte[] awarded = CheckpointFile.Encode(ready.Capture("catalog"));
            var restored = Restore(CheckpointFile.Decode(awarded),load,profile);
            Assert(awarded.SequenceEqual(CheckpointFile.Encode(restored.Capture("catalog"))) && restored.Experience == 600 && restored.ArroyoGlobals == ready.ArroyoGlobals);
            restored.UseExit(false); restored.UseExit(false); Assert(restored.Experience == 600 && restored.ArroyoGlobals == ready.ArroyoGlobals);
            restored.Reset(); Assert(restored.Experience == 0 && restored.ArroyoReward == null && restored.ArroyoGlobals == null && restored.EnteredMaps.SequenceEqual(new[] {3}));
        });
        check("Arroyo return guidance owns its source route and cannot load travel or change XP while being inspected",() =>
        {
            var (world,_,_) = Fixture(); Assert(world.InspectArroyoReturnRoute(false).Status == TempleWorldStatus.Unavailable);
            world.UseExit(false); byte[] before = CheckpointFile.Encode(world.Capture("catalog"));
            var route = world.InspectArroyoReturnRoute(false);
            Assert(route.Status == TempleWorldStatus.Ready && route.ExitHex == 20517 && route.Destination == new ActorLocation(3,2,13326,0)
                && route.Tiles!.SequenceEqual(new[] {20517}) && route.SourceKeys!.Length == 1);
            route.Tiles![0] = -1; route.SourceKeys![0] = "foreign";
            Assert(world.InspectArroyoReturnRoute(false).Tiles![0] == 20517 && world.InspectArroyoReturnRoute(true).Status == TempleWorldStatus.Busy
                && before.SequenceEqual(CheckpointFile.Encode(world.Capture("catalog"))) && world.Experience == 600);
        });
        check("Arroyo return guidance refuses conflicting original exit destinations without changing the awarded world",() =>
        {
            var (world,_,_) = Fixture(ambiguousReturn:true); world.UseExit(false);
            byte[] before = CheckpointFile.Encode(world.Capture("catalog"));
            Assert(world.CurrentMap == 4 && world.InspectArroyoReturnRoute(false).Status == TempleWorldStatus.Blocked
                && before.SequenceEqual(CheckpointFile.Encode(world.Capture("catalog"))));
        });
        check("legacy v7 cached Arroyo migrates conservatively without retroactive reward while uncached Arroyo remains eligible",() =>
        {
            var (world,load,profile) = Fixture();
            var old = world.Capture("catalog") with {Version = 7,Profile = "neutral-v7",Entries = null, Combat = null, Areas = world.Capture("catalog").Areas.Select(a => a with { Consumed = null }).ToArray()};
            var fresh = Restore(old,load,profile); fresh.UseExit(false); Assert(fresh.Experience == 600);
            world.InspectExit(false); old = world.Capture("catalog") with {Version = 7,Profile = "neutral-v7",Entries = null, Combat = null, Areas = world.Capture("catalog").Areas.Select(a => a with { Consumed = null }).ToArray()};
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
        check("v8 receipt migrates exact neutral entry globals once while visited villages without a receipt receive no invented state",() =>
        {
            var (world,load,profile) = Fixture(); world.UseExit(false);
            var old = world.Capture("catalog") with {Version=8,Profile="neutral-v8",Combat=null,Areas=world.Capture("catalog").Areas.Select(a => a with { Consumed = null }).ToArray(),Entries=world.Capture("catalog").Entries! with {ArroyoGlobals=null}};
            var node=JsonNode.Parse(CheckpointFile.Encode(old))!; node["Entries"]!.AsObject().Remove("ArroyoGlobals");
            var restored=Restore(CheckpointFile.Decode(Encoding.UTF8.GetBytes(node.ToJsonString())),load,profile);
            Assert(restored.Capture("catalog").Version==11 && restored.Experience==600 && restored.ArroyoGlobals==new ArroyoRewardProfile().EntryGlobals());
            restored.UseExit(false); restored.UseExit(false); Assert(restored.ArroyoGlobals!.ArroyoReputation==65 && restored.Experience==600);
            var without=Restore(old with {Entries=old.Entries! with {ArroyoReward=null}},load,profile);
            Assert(without.ArroyoGlobals==null && without.Experience==0);
            without.UseExit(false); without.UseExit(false); Assert(without.ArroyoGlobals==null && without.Experience==0);
        });
        check("v9 refuses forged karma reputation tier profile null globals and metadata without the matching first-entry receipt",() =>
        {
            var (world,load,profile)=Fixture(); world.UseExit(false); var saved=world.Capture("catalog"); var globals=saved.Entries!.ArroyoGlobals!;
            byte[] before=CheckpointFile.Encode(saved);
            foreach (var invalid in new SavedArroyoGlobals?[] {null,globals with {Karma=0},globals with {ArroyoReputation=15},
                globals with {KarmaTierGlobal=45},globals with {Profile="foreign"}})
                Reject(() => Restore(saved with {Entries=saved.Entries with {ArroyoGlobals=invalid}},load,profile));
            Reject(() => Restore(saved with {Entries=saved.Entries with {ArroyoReward=null}},load,profile));
            foreach (string name in new[] {"Profile","Karma","ArroyoReputation","KarmaTierGlobal"})
            {
                var node=JsonNode.Parse(before)!; node["Entries"]!["ArroyoGlobals"]!.AsObject().Remove(name);
                Reject(() => CheckpointFile.Decode(Encoding.UTF8.GetBytes(node.ToJsonString())));
            }
            Assert(before.SequenceEqual(CheckpointFile.Encode(world.Capture("catalog"))));
        });
        check("v9 refuses duplicate unknown incomplete entry maps forged reward amounts identities and missing strict JSON",() =>
        {
            var (world,load,profile) = Fixture(); world.UseExit(false); var saved = world.Capture("catalog");
            foreach (int[] maps in new[] {Array.Empty<int>(),new[] {3,3,4},new[] {3,999},new[] {3},new[] {4}})
                Reject(() => Restore(saved with {Entries = saved.Entries! with {EnteredMaps = maps}},load,profile));
            foreach (var reward in new[] {saved.Entries!.ArroyoReward! with {Experience = 601},saved.Entries.ArroyoReward! with {Profile = "foreign"}})
                Reject(() => Restore(saved with {Entries = saved.Entries with {ArroyoReward = reward}},load,profile));
            Reject(() => Restore(saved with {Temple = null},load,profile));
            foreach (string name in new[] {"Entries","EnteredMaps","ArroyoReward","ArroyoGlobals"})
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
