using System.Text.Json;
using System.Text;
using System.Text.Json.Nodes;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class WorldTempleSpeechTests
{
    internal static (MapDocument Map, TempleNpcDepartureProfile Profile, WorldSession World) Fixture(bool open = true)
    {
        var map = InitialGateTests.Fixture(); var gate = map.ObjectLayers.Single(l => l.Elevation == 2).Objects.Single();
        var actor = gate with { Key = map.Source.Sha256 + ":425776", Offset = 425776, Subtype = null,
            Header = gate.Header with { Id = 126, Tile = 13728, Pid = 16777490, Fid = 0x01000000, Sid = 67108869, ScriptIndex = 748 },
            State = new Dictionary<string, int>() };
        var exit = WorldTests.Map(3,126).ObjectLayers[0].Objects[0];
        exit = exit with { Header = exit.Header with { Tile = 28716, Elevation = 2 }, ContainerElevation = 2 };
        map = map with { ObjectLayers = map.ObjectLayers.Select(l => l.Elevation == 2 ? l with { Objects = new[] { gate, actor, exit }, DeclaredCount = 3 } : l).ToArray() };
        var speech = new TempleSpeechProfile(actor.Key, gate.Key, map.Source.Sha256, "data/vault13.gam", new int[21]);
        var profile = new TempleNpcDepartureProfile(speech, map.ObjectLayers.SelectMany(l => l.Objects).ToArray(), actor, gate);
        var sourceMap = map;
        var world = new WorldSession(id => id == 3 ? sourceMap : WorldTests.Map(id,3), new[] { 3,126 }, templeProfile: _ => profile);
        if (open) world.Current.Doors.Toggle(gate.Key, FalloutGrid.Neighbor(19928,2)!.Value, 2, false);
        int[] route = new[] { NpcDeparturePhase.ToGate, NpcDeparturePhase.ToMiddle, NpcDeparturePhase.ToExit }.SelectMany(profile.Route).ToArray();
        int adjacent = Enumerable.Range(0,6).Select(d => FalloutGrid.Neighbor(13728,d)!.Value).First(t => !route.Contains(t));
        world.Current.Travel.Arrive(new(3,2,adjacent,0));
        return (map, profile, world);
    }
    internal static ScriptStateSnapshot Accepted(TempleNpcDepartureProfile profile)
    {
        var state = profile.Speech.CreateInitial(); var draft = TempleSpeechDraft.Begin(profile.Speech,state,5,true)!;
        TempleSpeechDraftTests.Complete(draft); draft.CommitDetached(); return state.Capture();
    }
    internal static TempleSpeechCommitPlan Plan(WorldSession world, TempleNpcDepartureProfile profile)
    { var proposal = world.PrepareTempleSpeech(profile,Accepted(profile),false,5,true); Assert(proposal.Status == TempleWorldStatus.Ready); return proposal.Plan!; }
    private static byte[] State(WorldSession world) => CheckpointFile.Encode(world.Capture("catalog"));
    public static void Run(Action<string,Action> check)
    {
        check("world Speech preparation is read-only and commit preserves an already open gate and original source records", () =>
        {
            var (map,profile,world) = Fixture(); byte[] before = State(world); var plan = Plan(world,profile);
            Assert(before.SequenceEqual(State(world)) && world.TempleProgress == null);
            Assert(world.CommitTempleSpeech(plan,false) == TempleWorldStatus.Ready && world.TempleProgress!.Departure.Phase == NpcDeparturePhase.ToGate
                && world.Current.Doors.Get(profile.GateKey)!.IsOpen && world.TempleProgress.InitialGateOpen && world.Experience == 0);
            var scripts = world.TempleProgress!.Scripts; var state = profile.Speech.CreateInitial(); state.Restore(scripts);
            Assert(state.TryRead(ScriptVariableScope.ScriptLocal,profile.ActorKey,14,out int phase) && phase == 0
                && state.TryRead(ScriptVariableScope.Map,profile.Speech.MapKey,20,out int quest) && quest == 2
                && !state.TryRead(ScriptVariableScope.ScriptLocal,profile.ActorKey,16,out _));
            Assert(map.AllObjects.Single(o => o.Key == profile.ActorKey).Header.Tile == 13728 && map.AllObjects.Single(o => o.Key == profile.GateKey).State["openFlags"] == 0);
        });
        check("world Speech refuses unsupported profiles incomplete choices distance movement and duplicate acceptance", () =>
        {
            var (_,profile,world) = Fixture(); byte[] before = State(world);
            Assert(world.PrepareTempleSpeech(profile,Accepted(profile),true,5,true).Status == TempleWorldStatus.Busy
                && world.PrepareTempleSpeech(profile,Accepted(profile),false,3,true).Status == TempleWorldStatus.Unsupported
                && world.PrepareTempleSpeech(profile,Accepted(profile),false,5,false).Status == TempleWorldStatus.Unsupported
                && world.PrepareTempleSpeech(profile,profile.Speech.CreateInitial().Capture(),false,5,true).Status == TempleWorldStatus.Unsupported);
            Assert(before.SequenceEqual(State(world)));
            var plan = Plan(world,profile); world.CommitTempleSpeech(plan,false); var saved = State(world);
            Assert(world.CommitTempleSpeech(plan,false) != TempleWorldStatus.Ready && saved.SequenceEqual(State(world)));
            world.Reset(); Assert(world.PrepareTempleSpeech(profile,Accepted(profile),false,5,true).Status == TempleWorldStatus.Unavailable);
        });
        check("world Speech proposals reject changed doors position resets foreign sessions and forged accepted variables", () =>
        {
            var (_,profile,world) = Fixture(); var plan = Plan(world,profile); var location = world.Location;
            world.Current.Travel.Arrive(location with { Rotation = 1 }); byte[] before = State(world);
            Assert(world.CommitTempleSpeech(plan,false) == TempleWorldStatus.Stale && before.SequenceEqual(State(world)));
            world.Current.Travel.Arrive(location); plan = Plan(world,profile);
            world.Current.Doors.Toggle(profile.GateKey,FalloutGrid.Neighbor(19928,2)!.Value,2,false);
            Assert(world.CommitTempleSpeech(plan,false) == TempleWorldStatus.Stale);
            plan = Plan(world,profile); plan.Accepted.Blocks[0].Values[8] = 1;
            before = State(world); Assert(world.CommitTempleSpeech(plan,false) == TempleWorldStatus.Unsupported && before.SequenceEqual(State(world)));
            plan = Plan(world,profile); world.Reset(); world.Current.Travel.Arrive(location);
            Assert(world.CommitTempleSpeech(plan,false) == TempleWorldStatus.Stale);
            var other = Fixture().World; Assert(other.CommitTempleSpeech(plan,false) == TempleWorldStatus.Stale);
        });
        check("world NPC steps relocate one blocker and reject player occupancy busy and old actions atomically", () =>
        {
            var (_,profile,world) = Fixture(); world.CommitTempleSpeech(Plan(world,profile),false);
            var session = profile.Restore(world.TempleProgress!.Departure); int next = session.NextHex!.Value;
            var location = world.Location; world.Current.Travel.Arrive(location with { Hex = next }); byte[] before = State(world);
            Assert(world.StepTempleNpc(world.TempleToken,world.TempleRevision,false) == TempleWorldStatus.Occupied && before.SequenceEqual(State(world)));
            world.Current.Travel.Arrive(location); before = State(world);
            Assert(world.StepTempleNpc(world.TempleToken,world.TempleRevision,true) == TempleWorldStatus.Busy && before.SequenceEqual(State(world)));
            long revision = world.TempleRevision;
            Assert(world.StepTempleNpc(world.TempleToken,revision,false) == TempleWorldStatus.Ready);
            var nav = world.Current.Doors.Navigation(2);
            Assert(!nav.BlockersAt(13728).Any(b => b.Key == profile.ActorKey) && nav.BlockersAt(next).Single(b => b.Key == profile.ActorKey).Id == 126
                && world.StepTempleNpc(world.TempleToken,revision,false) == TempleWorldStatus.Stale);
            Assert(world.Current.Doors.CanToggle(profile.GateKey,19928,2,false) == DoorUseStatus.ScriptRequired);
        });
        check("world NPC pose script phase gate and timer round trip together after every action", () =>
        {
            var (map,profile,world) = Fixture(false); world.CommitTempleSpeech(Plan(world,profile),false);
            bool sawDelay = false; int guard = 0;
            while (world.TempleProgress!.Departure.Phase != NpcDeparturePhase.Hidden && guard++ < 500)
            {
                var saved = world.Capture("catalog"); byte[] bytes = CheckpointFile.Encode(saved);
                var loaded = WorldSession.Restore(_ => map,new[] { 3,126 },CheckpointFile.Decode(bytes),"catalog",templeProfile: _ => profile);
                Assert(bytes.SequenceEqual(State(loaded)) && loaded.TempleToken != world.TempleToken);
                var scripts = profile.Speech.CreateInitial(); scripts.Restore(loaded.TempleProgress!.Scripts);
                Assert(scripts.TryRead(ScriptVariableScope.ScriptLocal,profile.ActorKey,14,out int phase) && phase == (int)loaded.TempleProgress.Departure.Phase);
                world = loaded;
                if (world.TempleProgress.Departure.Phase == NpcDeparturePhase.GateDelay)
                {
                    sawDelay = true; Assert(world.Current.Doors.Get(profile.GateKey)!.IsOpen);
                    string before = JsonSerializer.Serialize(world.TempleProgress); world.AdvanceUseTime(500);
                    Assert(JsonSerializer.Serialize(world.TempleProgress) == before);
                    Assert(world.AdvanceTempleNpcTime(1,world.TempleToken,world.TempleRevision,false).Status == TempleWorldStatus.Ready);
                }
                else Assert(world.StepTempleNpc(world.TempleToken,world.TempleRevision,false) == TempleWorldStatus.Ready);
            }
            Assert(sawDelay && guard < 500 && !world.Current.Doors.Navigation(2).BlockersAt(28715).Any(b => b.Key == profile.ActorKey));
            Assert(world.StepTempleNpc(world.TempleToken,world.TempleRevision,false) == TempleWorldStatus.Complete && world.Experience == 0);
        });
        check("v6 rejects conflicting NPC script gate event source and unsupported resolver without touching the live world", () =>
        {
            var (map,profile,world) = Fixture(); world.CommitTempleSpeech(Plan(world,profile),false); var saved = world.Capture("catalog"); byte[] before = State(world);
            WorldSession Restore(WorldCheckpoint snapshot) => WorldSession.Restore(_ => map,new[] { 3,126 },snapshot,"catalog",templeProfile: _ => profile);
            Reject(() => WorldSession.Restore(_ => map,new[] { 3,126 },saved,"catalog"));
            Reject(() => Restore(saved with { Entries = null, Version = 5, Profile = "neutral-v5" }));
            Reject(() => Restore(saved with { Temple = saved.Temple! with { Profile = "wrong" } }));
            Reject(() => Restore(saved with { Temple = saved.Temple! with { Departure = saved.Temple.Departure with { RemainingTicks = 1 } } }));
            var forged = CheckpointFile.Decode(before); forged.Temple!.Scripts.Blocks[0].Values[14] = 4;
            Reject(() => Restore(forged));
            forged = CheckpointFile.Decode(before); var area = forged.Areas.Single(a => a.Map == 3);
            area.Session.Doors[Array.FindIndex(area.Session.Doors,d => d.Key == profile.GateKey)] = new(profile.GateKey,0,0);
            Reject(() => Restore(forged)); Assert(before.SequenceEqual(State(world)));
            var snapshot = world.TempleProgress!; snapshot.Scripts.Blocks[0].Values[14] = 99;
            Assert(before.SequenceEqual(State(world)));
        });
        check("legacy v1 through v5 migrate to v6 without inventing Speech or NPC movement", () =>
        {
            var (map,_,world) = Fixture(false); var saved = world.Capture("catalog");
            for (int version = 1; version <= 5; version++)
            {
                var legacy = saved with { Entries = null, Version = version, Profile = $"neutral-v{version}", Areas = saved.Areas.Select(a => a with
                    { Taken = version < 2 ? null : a.Taken, Uses = version < 3 ? null : a.Uses, Replacements = version < 4 ? null : a.Replacements }).ToArray() };
                var restored = WorldSession.Restore(_ => map,new[] { 3,126 },legacy,"catalog");
                Assert(restored.TempleProgress == null && restored.Capture("catalog").Version == 9 && restored.Current.Doors.Navigation(2).BlockersAt(13728).Any(b => b.Id == 126));
            }
        });
        check("NPC departure freezes outside its active elevation and resets invalidate all old actions", () =>
        {
            var (_,profile,world) = Fixture(); world.CommitTempleSpeech(Plan(world,profile),false);
            var token = world.TempleToken; long revision = world.TempleRevision;
            world.Current.Travel.Arrive(new(3,0,20100,0)); byte[] before = State(world);
            Assert(world.StepTempleNpc(token,revision,false) == TempleWorldStatus.Unavailable
                && world.AdvanceTempleNpcTime(20,token,revision,false).Status == TempleWorldStatus.Unavailable && before.SequenceEqual(State(world)));
            world.Reset(); Assert(world.TempleProgress == null && world.TempleToken != token && world.StepTempleNpc(token,revision,false) == TempleWorldStatus.Stale);
        });
        check("v6 file rejects missing Temple metadata duplicate JSON properties and incomplete progress fields", () =>
        {
            var (_,profile,world) = Fixture(); world.CommitTempleSpeech(Plan(world,profile),false); byte[] bytes = State(world);
            foreach (string name in new[] { "Profile", "Scripts", "Departure", "InitialGateOpen" })
            {
                var node = JsonNode.Parse(bytes)!; node["Temple"]!.AsObject().Remove(name);
                Reject(() => CheckpointFile.Decode(Encoding.UTF8.GetBytes(node.ToJsonString())));
            }
            var missing = JsonNode.Parse(bytes)!; missing.AsObject().Remove("Temple");
            Reject(() => CheckpointFile.Decode(Encoding.UTF8.GetBytes(missing.ToJsonString())));
            string json = Encoding.UTF8.GetString(bytes);
            Reject(() => CheckpointFile.Decode(Encoding.UTF8.GetBytes(json.Replace("\"Version\": 9", "\"Version\": 9, \"Version\": 9"))));
            Reject(() => CheckpointFile.Decode(Encoding.UTF8.GetBytes(json.Replace("\"RouteIndex\": 0", "\"RouteIndex\": 0, \"RouteIndex\": 0"))));
            var legacy = world.Capture("catalog") with { Entries = null, Version = 5, Profile = "neutral-v5", Temple = null };
            var old = JsonNode.Parse(CheckpointFile.Encode(legacy))!; old.AsObject().Remove("Temple");
            Assert(CheckpointFile.Decode(Encoding.UTF8.GetBytes(old.ToJsonString())).Version == 5);
        });
        check("active NPC state survives external area departure save load and return without ticking while away", () =>
        {
            var (map,profile,world) = Fixture(); world.CommitTempleSpeech(Plan(world,profile),false);
            world.StepTempleNpc(world.TempleToken,world.TempleRevision,false); var progress = JsonSerializer.Serialize(world.TempleProgress);
            world.Current.Travel.MoveTo(28716); Assert(world.UseExit(false).Status == ExitStatus.Ready && world.CurrentMap == 126);
            byte[] before = State(world);
            Assert(world.StepTempleNpc(world.TempleToken,world.TempleRevision,false) == TempleWorldStatus.Unavailable
                && world.AdvanceTempleNpcTime(100,world.TempleToken,world.TempleRevision,false).Status == TempleWorldStatus.Unavailable
                && before.SequenceEqual(State(world)));
            var saved = CheckpointFile.Decode(before);
            var restored = WorldSession.Restore(id => id == 3 ? map : WorldTests.Map(id,3),new[] { 3,126 },saved,"catalog",templeProfile: _ => profile);
            Assert(restored.CurrentMap == 126 && before.SequenceEqual(State(restored)) && JsonSerializer.Serialize(restored.TempleProgress) == progress);
            Assert(restored.UseExit(false).Status == ExitStatus.Ready && restored.CurrentMap == 3 && JsonSerializer.Serialize(restored.TempleProgress) == progress);
            int[] route = profile.Route(NpcDeparturePhase.ToGate); int next = route[2];
            int safe = Enumerable.Range(0,6).Select(d => FalloutGrid.Neighbor(13728,d)!.Value).First(t => !route.Contains(t) && t != next);
            restored.Current.Travel.Arrive(new(3,2,safe,0));
            Assert(restored.StepTempleNpc(restored.TempleToken,restored.TempleRevision,false) == TempleWorldStatus.Ready);
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("World Temple Speech assertion failed."); }
    private static void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Invalid Temple world checkpoint accepted."); }
}
