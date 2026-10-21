using System.Text.Json;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class WorldTempleSpeechAudit
{
    public static object Create(WorldSession original, MapCatalog catalog, GameData data)
    {
        var prototypes = new PrototypeCatalog(new ResourceCatalog(data.Read));
        byte[] State(WorldSession value) => CheckpointFile.Encode(value.Capture(catalog.Source.Sha256));
        WorldSession Restore(byte[] bytes) => WorldSession.Restore(id => catalog.Load(data,id),new[] { 3,4,126 },CheckpointFile.Decode(bytes),catalog.Source.Sha256,
            runtimePrototype: prototypes.Get,templeProfile: map => TempleNpcDepartureProfile.Create(map,data.Read));
        byte[] before = State(original); var world = Restore(before); var map = world.Current.Map;
        var profile = TempleNpcDepartureProfile.Create(map,data.Read);
        Require(world.Location is { Map:3, Elevation:2 } && world.CarriedCount == 2 && world.Experience == 25,"Expected collected upper-Temple fixture.");
        string Items(WorldSession value) => JsonSerializer.Serialize(new { Taken = value.Areas.Select(a => a.Items.Capture()),
            Uses = value.ItemUses, Replacements = value.SceneryReplacements });
        string items = Items(world); var speech = profile.Speech.CreateInitial(); var draft = TempleSpeechDraft.Begin(profile.Speech,speech,5,true)!;
        foreach (int offset in TempleSpeechProfile.ChoiceOffsets) Require(draft.Choose(offset) == TempleSpeechStatus.Ready,"Speech choice failed.");
        Require(draft.CommitDetached() == TempleSpeechStatus.Ready,"Speech result failed.");
        var proposal = world.PrepareTempleSpeech(profile,speech.Capture(),false,5,true);
        Require(proposal.Status == TempleWorldStatus.Ready && world.CommitTempleSpeech(proposal.Plan,false) == TempleWorldStatus.Ready,"World Speech acceptance failed.");
        int[] routeHexes = new[] { NpcDeparturePhase.ToGate,NpcDeparturePhase.ToMiddle,NpcDeparturePhase.ToExit }.SelectMany(profile.Route).ToArray();
        void SafePlayer(bool beforeDeparture = false)
        {
            var navigation = world.Current.Doors.Navigation(2); var reach = navigation.ReachableFrom(world.Location.Hex);
            var downstream = profile.Navigation(new(1,profile.Fingerprint,NpcDeparturePhase.GateDelay,0,20)).ReachableFrom(23914);
            int safe = Enumerable.Range(1,39999).Where(t => reach.Distance(t) >= 0 && !navigation.IsExit(t) && !routeHexes.Contains(t))
                .Where(t => !beforeDeparture || downstream.Distance(t) >= 0)
                .OrderBy(reach.Distance).ThenBy(t => t).First();
            world.Current.Travel.MoveTo(safe);
        }
        SafePlayer(true); int steps = 0, resumes = 0; bool pausedOnStairs = false;
        void Resume()
        {
            byte[] bytes = State(world); var token = world.TempleToken;
            world = Restore(bytes); resumes++;
            Require(bytes.SequenceEqual(State(world)) && token != world.TempleToken,"Whole-world v6 round trip failed.");
        }
        void TravelTo(int elevation)
        {
            var nav = world.Current.Doors.Navigation(world.Location.Elevation); var reach = nav.ReachableFrom(world.Location.Hex);
            var exit = map.ObjectLayers.SelectMany(l => l.Objects).Where(o => o.Header.Elevation == world.Location.Elevation
                && o.Header.Pid is >= 0x05000010 and <= 0x05000017 && o.State.GetValueOrDefault("destinationMap",-1) == 3
                && o.State.GetValueOrDefault("destinationElevation",-1) == elevation && reach.Distance(o.Header.Tile) >= 0)
                .OrderBy(o => reach.Distance(o.Header.Tile)).ThenBy(o => o.Offset).FirstOrDefault()
                ?? throw new InvalidDataException("No original staircase is reachable with the NPC's current blocker.");
            world.Current.Travel.MoveTo(exit.Header.Tile);
            Require(world.UseExit(false).Status == ExitStatus.Ready && world.Location.Elevation == elevation,"Real stair transition failed.");
        }
        for (int guard = 0; world.TempleProgress!.Departure.Phase != NpcDeparturePhase.Hidden && guard < 150; guard++)
        {
            var previous = world.TempleProgress.Departure;
            if (previous.Phase == NpcDeparturePhase.GateDelay)
            {
                Require(world.AdvanceTempleNpcTime(7,world.TempleToken,world.TempleRevision,false).AppliedTicks == 7,"Partial world NPC timer failed.");
                TravelTo(1); byte[] paused = State(world);
                Require(world.StepTempleNpc(world.TempleToken,world.TempleRevision,false) == TempleWorldStatus.Unavailable
                    && world.AdvanceTempleNpcTime(100,world.TempleToken,world.TempleRevision,false).Status == TempleWorldStatus.Unavailable
                    && paused.SequenceEqual(State(world)),"NPC changed while player used another elevation.");
                Resume(); TravelTo(2); SafePlayer(); pausedOnStairs = true;
                var advance = world.AdvanceTempleNpcTime(100,world.TempleToken,world.TempleRevision,false);
                Require(advance.AppliedTicks == 13 && advance.UnusedTicks == 87,"NPC delay overshoot was not preserved.");
            }
            else
            {
                var prior = profile.Location(previous);
                Require(world.StepTempleNpc(world.TempleToken,world.TempleRevision,false) == TempleWorldStatus.Ready,"Real NPC step failed."); steps++;
                var location = profile.Location(world.TempleProgress.Departure);
                Require(FalloutGrid.Distance(prior.Hex,location.Hex) == 1,"Real NPC skipped a hex.");
                Require(!world.Current.Doors.Navigation(2).BlockersAt(prior.Hex).Any(b => b.Key == profile.ActorKey),"Real NPC left its blocker behind.");
            }
            if (world.TempleProgress.Departure.Phase != previous.Phase) Resume();
            Require(Items(world) == items && world.Experience == 25 && world.CarriedCount == 2,"NPC departure changed items/scenery/XP.");
        }
        Require(steps == 91 && pausedOnStairs && world.TempleProgress!.Departure.Phase == NpcDeparturePhase.Hidden
            && !world.Current.Doors.Navigation(2).BlockersAt(28715).Any(b => b.Key == profile.ActorKey),"Real active NPC final state failed.");
        Require(before.SequenceEqual(State(original)),"Detached world audit mutated its caller.");
        foreach (var area in world.Areas) MapLayoutWriter.Verify(area.Map);
        return new { Status = "WORLD_TEMPLE_SPEECH_AUDIT_OK", WorldCheckpointVersion = 6, Steps = steps, WholeWorldResumes = resumes,
            PausedAcrossRealStairs = pausedOnStairs, CarriedUnits = world.CarriedCount, Experience = world.Experience,
            FinalNpc = profile.Location(world.TempleProgress.Departure), Hidden = true, ItemsAndSceneryPreserved = true, CallerUnchanged = true };
    }
    private static void Require(bool value,string reason) { if (!value) throw new InvalidDataException(reason); }
}
