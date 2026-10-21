using System.Text.Json;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class TempleNpcDepartureReport
{
    public static object Create(GameData data)
    {
        // Keep the independent physical-opcode audit as a prerequisite to this execution test.
        TempleSpeechAftermathReport.Create(data);
        var map = MapDocument.Read(data.Read("maps/arcaves.map"), data.Read); MapLayoutWriter.Verify(map);
        var profile = TempleNpcDepartureProfile.Create(map, data.Read);
        var catalog = new MapCatalog(data.Read("data/maps.txt"));
        var world = new WorldSession(id => catalog.Load(data, id), new[] { 3, 4, 126 });
        byte[] before = CheckpointFile.Encode(world.Capture(catalog.Source.Sha256));
        var speech = profile.Speech.CreateInitial(); var draft = TempleSpeechDraft.Begin(profile.Speech, speech, 5, true)!;
        foreach (int offset in TempleSpeechProfile.ChoiceOffsets)
            Require(draft.Choose(offset) == TempleSpeechStatus.Ready, "Original Speech choice failed.");
        Require(draft.CommitDetached() == TempleSpeechStatus.Ready, "Original Speech acceptance failed.");
        var session = profile.Begin(speech.Capture());
        var phases = new List<object>(); int steps = 0, restores = 0, crossedExits = 0;
        int[] expectedSteps = { 32, 28, 31 };
        Require(profile.RouteSteps.SequenceEqual(expectedSteps), "Original departure routes changed.");
        phases.Add(new { Phase = session.Capture().Phase.ToString(), session.Location.Hex, Ticks = 0 });
        int exits = profile.Navigation(session.Capture()).ExitHexCount;
        for (int guard = 0; session.Capture().Phase != NpcDeparturePhase.Hidden && guard < 200; guard++)
        {
            var saved = session.Capture(); var previous = session.Location;
            var restored = profile.Restore(saved);
            Require(restored.Capture() == saved && restored.Location == previous && restored.SessionToken != session.SessionToken,
                "Complete NPC pose/event did not restore."); restores++;
            if (session.Walking)
            {
                int next = session.NextHex!.Value;
                Require(session.Step(session.SessionToken, session.Revision, new(3, 2, next, 0)) == NpcDepartureStatus.Occupied
                    && session.Capture() == saved, "Occupied NPC step changed state.");
                long revision = session.Revision;
                Require(session.Step(session.SessionToken, revision) == NpcDepartureStatus.Ready && FalloutGrid.Distance(previous.Hex, session.Location.Hex) == 1,
                    "NPC moved without an adjacent hex step."); steps++;
                Require(session.Step(session.SessionToken, revision) == NpcDepartureStatus.Stale, "Repeated NPC action was replayed.");
                var nav = profile.Navigation(session.Capture());
                Require(!nav.BlockersAt(previous.Hex).Any(b => b.Key == profile.ActorKey), "NPC left its blocker behind.");
                if (session.Capture().Phase != NpcDeparturePhase.Hidden)
                    Require(nav.BlockersAt(session.Location.Hex).Any(b => b.Key == profile.ActorKey && b.Id == 126), "NPC blocker did not follow its pose.");
                if (nav.IsExit(session.Location.Hex)) crossedExits++;
            }
            else
            {
                Require(saved.Phase == NpcDeparturePhase.GateDelay && saved.RemainingTicks == 20 && previous.Hex == 19728
                    && profile.Gate(saved).IsOpen, "Gate arrival and timer were not a single transition.");
                Require(session.AdvanceDelay(7, session.SessionToken, session.Revision).AppliedTicks == 7, "Partial timer advance failed.");
                session = profile.Restore(session.Capture()); restores++;
                var advance = session.AdvanceDelay(99, session.SessionToken, session.Revision);
                Require(advance.AppliedTicks == 13 && advance.UnusedTicks == 86 && session.Location == previous,
                    "Timer overshoot moved the NPC or replayed time.");
            }
            if (session.Capture().Phase != saved.Phase)
                phases.Add(new { Phase = session.Capture().Phase.ToString(), session.Location.Hex, Ticks = session.Capture().RemainingTicks });
            Require(before.SequenceEqual(CheckpointFile.Encode(world.Capture(catalog.Source.Sha256))), "Departure rehearsal changed active world.");
            Require(profile.Navigation(session.Capture()).ExitHexCount == exits, "Departure removed original exit markers.");
        }
        Require(session.Capture().Phase == NpcDeparturePhase.Hidden && steps == 91 && crossedExits > 0 && session.Location.Hex == 28715
            && !profile.Navigation(session.Capture()).BlockersAt(28715).Any(b => b.Key == profile.ActorKey), "Final NPC pose/visibility is invalid.");
        MapLayoutWriter.Verify(map);
        return new { Status = "TEMPLE_NPC_DEPARTURE_OK", Profile = profile.Fingerprint, ActorKey = profile.ActorKey, GateKey = profile.GateKey,
            RouteSteps = profile.RouteSteps, TotalSteps = steps, RestoredStates = restores, CrossedExitHexes = crossedExits, PreservedExitHexes = exits,
            Phases = phases, TimerTicks = 20, FinalLocation = session.Location, Hidden = true, SourceObjectPreserved = true,
            ActiveWorldUnchanged = true, ActiveAdapterEnabled = false, OriginalReconstruction = "verified" };
    }
    private static void Require(bool value, string reason) { if (!value) throw new InvalidDataException(reason); }
}
