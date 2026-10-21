using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class TempleVillageRouteTests
{
    private static (WorldSession World,TempleNpcDepartureProfile Profile,Func<int> Loads) Fixture(bool ambiguous = false,bool allowVillage = true)
    {
        var (map,profile,old) = WorldTempleSpeechTests.Fixture();
        var exit = WorldTests.Map(4,4).ObjectLayers[0].Objects[0];
        exit = exit with { Key = "village-exit", ContainerElevation = 2, Header = exit.Header with { Elevation = 2,Tile = 13326 },
            State = new Dictionary<string,int> { ["destinationMap"] = 4,["destinationElevation"] = 0,["destinationTile"] = 20517,["destinationRotation"] = 0 } };
        MapObject[] exits = ambiguous ? new[] { exit,exit with { Key = "conflicting-exit",State = new Dictionary<string,int>(exit.State) { ["destinationMap"] = 126 } } } : new[] { exit };
        map = map with { ObjectLayers = map.ObjectLayers.Select(l => l.Elevation == 2 ? l with { Objects = l.Objects.Concat(exits).ToArray() } : l).ToArray() };
        var source = map; int loads = 0;
        var world = new WorldSession(id => { loads++; return id == 3 ? source : WorldTests.Map(id,3); },allowVillage ? new[] { 3,4,126 } : new[] { 3,126 },templeProfile: _ => profile);
        world.Current.Doors.Toggle(profile.GateKey,FalloutGrid.Neighbor(19928,2)!.Value,2,false); world.Current.Travel.Arrive(old.Location);
        return (world,profile,() => loads);
    }
    private static void Complete(WorldSession world,TempleNpcDepartureProfile profile)
    {
        Assert(world.CommitTempleSpeech(WorldTempleSpeechTests.Plan(world,profile),false) == TempleWorldStatus.Ready);
        for (int guard = 0; world.TempleProgress!.Departure.Phase != NpcDeparturePhase.Hidden && guard < 200; guard++)
            if (world.TempleProgress.Departure.Phase == NpcDeparturePhase.GateDelay) world.AdvanceTempleNpcTime(20,world.TempleToken,world.TempleRevision,false);
            else Assert(world.StepTempleNpc(world.TempleToken,world.TempleRevision,false) == TempleWorldStatus.Ready);
        Assert(world.TempleProgress!.Departure.Phase == NpcDeparturePhase.Hidden);
    }
    public static void Run(Action<string,Action> check)
    {
        check("Temple village guidance is read-only source-bound and does not load or travel to the destination", () =>
        {
            var (world,profile,loads) = Fixture(); Complete(world,profile);
            byte[] before = CheckpointFile.Encode(world.Capture("catalog")); int count = loads();
            var route = world.InspectTempleVillageRoute(false);
            Assert(route.Status == TempleWorldStatus.Ready && route.ExitHex == 13326 && route.Destination == new ActorLocation(4,0,20517,0)
                && route.Tiles![0] == world.Location.Hex && route.Tiles[^1] == route.ExitHex && route.SourceKeys!.SequenceEqual(new[] { "village-exit" }));
            route.Tiles![0] = -1; route.SourceKeys![0] = "foreign";
            Assert(loads() == count && world.CurrentMap == 3 && before.SequenceEqual(CheckpointFile.Encode(world.Capture("catalog")))
                && world.InspectTempleVillageRoute(false).Tiles![0] == world.Location.Hex && world.InspectTempleVillageRoute(false).SourceKeys![0] == "village-exit");
        });
        check("Temple village guidance requires completed departure and refuses busy forbidden or ambiguous exits", () =>
        {
            var (world,profile,_) = Fixture(); Assert(world.InspectTempleVillageRoute(false).Status == TempleWorldStatus.Unavailable);
            world.CommitTempleSpeech(WorldTempleSpeechTests.Plan(world,profile),false);
            Assert(world.InspectTempleVillageRoute(false).Status == TempleWorldStatus.NotWalking);
            var (ambiguous,other,_) = Fixture(true); Complete(ambiguous,other);
            Assert(ambiguous.InspectTempleVillageRoute(false).Status == TempleWorldStatus.Blocked);
            var (forbidden,last,_) = Fixture(allowVillage:false); Complete(forbidden,last);
            Assert(forbidden.InspectTempleVillageRoute(false).Status == TempleWorldStatus.Unavailable);
            var (ready,p,_) = Fixture(); Complete(ready,p); Assert(ready.InspectTempleVillageRoute(true).Status == TempleWorldStatus.Busy);
        });
        check("Temple village walking ends at the original exit and only explicit travel changes the active area", () =>
        {
            var (world,profile,loads) = Fixture(); Complete(world,profile); var route = world.InspectTempleVillageRoute(false); int count = loads();
            world.Current.Travel.MoveTo(route.ExitHex!.Value);
            Assert(world.CurrentMap == 3 && loads() == count && world.InspectTempleVillageRoute(false).Tiles!.Length == 1);
            string progress = System.Text.Json.JsonSerializer.Serialize(world.TempleProgress);
            Assert(world.UseExit(false).Status == ExitStatus.Ready && world.CurrentMap == 4 && world.Location == route.Destination
                && progress == System.Text.Json.JsonSerializer.Serialize(world.TempleProgress));
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Temple village guidance assertion failed."); }
}
