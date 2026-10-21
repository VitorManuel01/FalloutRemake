using FalloutRemake.Runtime;

internal static class TempleNpcRouteTests
{
    public static void Run(Action<string,Action> check)
    {
        check("NPC route inspection returns an owned current-leg path and never changes world state or loads areas", () =>
        {
            var (_,profile,world) = WorldTempleSpeechTests.Fixture(); world.CommitTempleSpeech(WorldTempleSpeechTests.Plan(world,profile),false);
            byte[] before = CheckpointFile.Encode(world.Capture("catalog")); int areas = world.Areas.Count;
            var route = world.InspectTempleNpcRoute();
            Assert(route.Status == TempleWorldStatus.Ready && route.Tiles[0] == 13728 && route.Tiles[^1] == 19728 && route.TargetKey == profile.GateKey);
            route.Tiles[0] = -1;
            Assert(world.InspectTempleNpcRoute().Tiles[0] == 13728 && before.SequenceEqual(CheckpointFile.Encode(world.Capture("catalog"))) && world.Areas.Count == areas);
            world.StepTempleNpc(world.TempleToken,world.TempleRevision,false);
            Assert(world.InspectTempleNpcRoute().Tiles.Length == route.Tiles.Length - 1 && world.InspectTempleNpcRoute().Tiles[0] != 13728);
        });
        check("NPC route inspection reports the first player-occupied hex and preserves source exit and blocker identities", () =>
        {
            var (_,profile,world) = WorldTempleSpeechTests.Fixture(); world.CommitTempleSpeech(WorldTempleSpeechTests.Plan(world,profile),false);
            var route = world.InspectTempleNpcRoute(); var nav = world.Current.Doors.Navigation(2); int blocked = route.Tiles[4];
            world.Current.Travel.Arrive(world.Location with { Hex = blocked }); byte[] before = CheckpointFile.Encode(world.Capture("catalog"));
            var occupied = world.InspectTempleNpcRoute(); var afterNav = world.Current.Doors.Navigation(2);
            Assert(occupied.Status == TempleWorldStatus.Occupied && occupied.BlockedHex == blocked && occupied.TargetKey == profile.GateKey
                && occupied.Tiles.SequenceEqual(route.Tiles) && before.SequenceEqual(CheckpointFile.Encode(world.Capture("catalog")))
                && nav.ExitHexCount == afterNav.ExitHexCount && nav.BlockedHexCount == afterNav.BlockedHexCount);
        });
        check("NPC route inspection stops at waiting and hidden phases and cannot preview from another elevation", () =>
        {
            var (_,profile,world) = WorldTempleSpeechTests.Fixture(); Assert(world.InspectTempleNpcRoute().Status == TempleWorldStatus.Unavailable);
            world.CommitTempleSpeech(WorldTempleSpeechTests.Plan(world,profile),false);
            for (int guard = 0; world.TempleProgress!.Departure.Phase != NpcDeparturePhase.GateDelay && guard < 100; guard++)
                world.StepTempleNpc(world.TempleToken,world.TempleRevision,false);
            Assert(world.InspectTempleNpcRoute().Status == TempleWorldStatus.NotWalking && world.InspectTempleNpcRoute().Tiles.Length == 0);
            world.AdvanceTempleNpcTime(20,world.TempleToken,world.TempleRevision,false);
            Assert(world.InspectTempleNpcRoute().Tiles[0] == 19728 && world.InspectTempleNpcRoute().Tiles[^1] == 22712);
            for (int guard = 0; world.TempleProgress!.Departure.Phase != NpcDeparturePhase.Hidden && guard < 100; guard++)
                world.StepTempleNpc(world.TempleToken,world.TempleRevision,false);
            Assert(world.InspectTempleNpcRoute().Status == TempleWorldStatus.Complete && world.InspectTempleNpcRoute().Tiles.Length == 0);
            world.Current.Travel.Arrive(new(3,0,20100,0)); Assert(world.InspectTempleNpcRoute().Status == TempleWorldStatus.Unavailable);
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("NPC route inspection assertion failed."); }
}
