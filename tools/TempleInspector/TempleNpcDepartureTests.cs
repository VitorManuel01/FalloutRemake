using System.Text.Json;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class TempleNpcDepartureTests
{
    internal static TempleNpcDepartureProfile Profile()
    {
        MapObject Obj(string key, int hex, int type) => new(key, 0, 0, 0, 2,
            new(126, hex, 0, 0, 0, 0, 0, 0, type << 24, 0, 2, type << 24, 0, 0, 0, 0, -1, -1),
            0, 0, 0, type == 2 ? 0 : null, new Dictionary<string, int> { ["openFlags"] = 0 }, Array.Empty<InventoryEntry>());
        var actor = Obj("actor", 13728, 1); var gate = Obj("gate", 19928, 2);
        var exit = Obj("exit", 24712, 5); exit = exit with { Header = exit.Header with { Pid = 0x05000010 } };
        return new(TempleSpeechDraftTests.Profile(), new[] { actor, gate, exit }, actor, gate);
    }
    internal static TempleNpcDeparture Begin(TempleNpcDepartureProfile profile)
    {
        var speech = profile.Speech.CreateInitial(); var draft = TempleSpeechDraft.Begin(profile.Speech, speech, 5, true)!;
        TempleSpeechDraftTests.Complete(draft); draft.CommitDetached(); return profile.Begin(speech.Capture());
    }
    internal static void ToDelay(TempleNpcDeparture session)
    {
        int limit = 40000;
        while (session.Capture().Phase == NpcDeparturePhase.ToGate && limit-- > 0)
            Assert(session.Step(session.SessionToken, session.Revision) == NpcDepartureStatus.Ready);
        Assert(session.Capture().Phase == NpcDeparturePhase.GateDelay);
    }
    public static void Run(Action<string, Action> check)
    {
        check("NPC departure requires completed supported Speech and preserves the original actor identity", () =>
        {
            var profile = Profile(); Reject(() => profile.Begin(profile.Speech.CreateInitial().Capture()));
            var session = Begin(profile); var actor = profile.Actor(session.Capture());
            Assert(session.Location.Hex == 13728 && session.Location.Elevation == 2 && session.Location.Map == 3
                && actor.Key == "actor" && actor.Header.Id == 126 && !profile.Gate(session.Capture()).IsOpen);
        });
        check("NPC hex step atomically relocates the same blocker and never mutates an earlier snapshot", () =>
        {
            var profile = Profile(); var session = Begin(profile); var before = session.Capture(); string frozen = JsonSerializer.Serialize(before);
            int next = session.NextHex!.Value; int original = session.Location.Hex;
            Assert(session.Step(session.SessionToken, 0) == NpcDepartureStatus.Ready && session.Location.Hex == next && session.Revision == 1);
            var nav = profile.Navigation(session.Capture());
            Assert(nav.BlockersAt(next).Single(b => b.Key == "actor").Id == 126 && nav.Walkable(original)
                && profile.Navigation(session.Capture(), true).Walkable(next) && JsonSerializer.Serialize(before) == frozen);
            Assert(profile.Navigation(before).BlockersAt(original).Any(b => b.Key == "actor"));
        });
        check("NPC step refuses occupied destinations foreign tokens and stale revisions with complete rollback", () =>
        {
            var session = Begin(Profile()); var before = session.Capture();
            Assert(session.Step(Guid.NewGuid(), 0) == NpcDepartureStatus.Stale && session.Step(session.SessionToken, 1) == NpcDepartureStatus.Stale);
            Assert(session.Step(session.SessionToken, 0, new(3, 2, session.NextHex!.Value, 0)) == NpcDepartureStatus.Occupied
                && session.Capture() == before && session.Revision == 0);
            Assert(session.Step(session.SessionToken, 0, new(3, 1, session.NextHex.Value, 0)) == NpcDepartureStatus.Ready);
            Assert(session.Step(session.SessionToken, 0) == NpcDepartureStatus.Stale && session.Revision == 1);
        });
        check("NPC gate arrival opens the gate and schedules exactly twenty native ticks independently of movement", () =>
        {
            var profile = Profile(); var session = Begin(profile);
            Assert(session.AdvanceDelay(20, session.SessionToken, 0).Status == NpcDepartureStatus.NotWaiting);
            ToDelay(session); var before = session.Capture();
            Assert(session.Location.Hex == 19728 && before.RemainingTicks == 20 && profile.Gate(before).IsOpen
                && !profile.Gate(before).IsLocked && profile.Navigation(before).DoorFlagDisagreements == 0);
            Assert(session.Step(session.SessionToken, session.Revision) == NpcDepartureStatus.NotWalking && session.Capture() == before);
            Assert(session.AdvanceDelay(0, session.SessionToken, session.Revision).Status == NpcDepartureStatus.Invalid && session.Capture() == before);
            Assert(session.AdvanceDelay(7, session.SessionToken, session.Revision) == new NpcDepartureAdvance(NpcDepartureStatus.Ready, 7, 0));
            Assert(session.Capture().RemainingTicks == 13 && session.Location.Hex == 19728);
            long revision = session.Revision;
            Assert(session.AdvanceDelay(99, session.SessionToken, revision) == new NpcDepartureAdvance(NpcDepartureStatus.Ready, 13, 86));
            Assert(session.Capture().Phase == NpcDeparturePhase.ToMiddle && session.Capture().RemainingTicks == 0
                && session.AdvanceDelay(20, session.SessionToken, revision).Status == NpcDepartureStatus.Stale
                && session.AdvanceDelay(20, session.SessionToken, session.Revision).Status == NpcDepartureStatus.NotWaiting);
        });
        check("NPC departure reaches all three targets by adjacent hexes and hides without destroying its source object", () =>
        {
            var profile = Profile(); var session = Begin(profile); var visited = new List<int> { session.Location.Hex };
            for (int guard = 0; session.Capture().Phase != NpcDeparturePhase.Hidden && guard < 40000; guard++)
            {
                int from = session.Location.Hex;
                if (session.Walking)
                {
                    Assert(session.Step(session.SessionToken, session.Revision) == NpcDepartureStatus.Ready);
                    Assert(FalloutGrid.Distance(from, session.Location.Hex) == 1); visited.Add(session.Location.Hex);
                }
                else Assert(session.AdvanceDelay(20, session.SessionToken, session.Revision).Status == NpcDepartureStatus.Ready);
            }
            var final = session.Capture(); var actor = profile.Actor(final);
            Assert(final.Phase == NpcDeparturePhase.Hidden && session.Location.Hex == 28715 && visited.Contains(19728) && visited.Contains(22712)
                && (actor.Header.Flags & (HexNavigation.Hidden | HexNavigation.NoBlock)) == (HexNavigation.Hidden | HexNavigation.NoBlock)
                && actor.Key == "actor" && actor.Header.Id == 126 && profile.Navigation(final).Walkable(28715));
            Assert(session.Step(session.SessionToken, session.Revision) == NpcDepartureStatus.Complete && session.Capture() == final);
            Assert(profile.Navigation(final, true).ExitPolicy == ExitTraversalPolicy.ContinueAcrossExits
                && profile.Navigation(final).ExitPolicy == ExitTraversalPolicy.StopAtExits
                && profile.Navigation(final).ExitHexCount == profile.Navigation(final, true).ExitHexCount);
        });
        check("NPC restoration validates whole phase pose and timer together and gives old actions a stale token", () =>
        {
            var profile = Profile(); var session = Begin(profile); ToDelay(session);
            session.AdvanceDelay(9, session.SessionToken, session.Revision); var saved = session.Capture();
            var restored = profile.Restore(saved);
            Assert(restored.Capture() == saved && restored.Location == session.Location && restored.SessionToken != session.SessionToken
                && restored.Step(session.SessionToken, session.Revision) == NpcDepartureStatus.Stale);
            foreach (var invalid in new[] { saved with { Version = 2 }, saved with { Profile = "foreign" }, saved with { Phase = (NpcDeparturePhase)99 },
                saved with { RemainingTicks = 0 }, saved with { RemainingTicks = 21 }, saved with { RouteIndex = 1 },
                saved with { Phase = NpcDeparturePhase.Hidden }, saved with { Phase = NpcDeparturePhase.ToGate, RemainingTicks = 0, RouteIndex = -1 },
                saved with { Phase = NpcDeparturePhase.ToExit, RemainingTicks = 0, RouteIndex = 40000 } }) Reject(() => profile.Restore(invalid));
            Assert(session.Capture() == saved);
        });
    }
    private static void Reject(Action action)
    { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Invalid departure accepted."); }
    internal static void Assert(bool value) { if (!value) throw new Exception("NPC departure assertion failed."); }
}
