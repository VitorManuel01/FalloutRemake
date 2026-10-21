using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class ArroyoElderApproachTests
{
    private static MapDocument Fixture()
    {
        var map=WorldTests.Map(4,3); var template=map.ObjectLayers[0].Objects[0];
        var actor=template with {Key=ArroyoRewardProfile.MapHash+":126588",Offset=126588,State=new Dictionary<string,int>(),
            Header=template.Header with {Id=20,Pid=16777229,Fid=16777247,Tile=18915,Elevation=0,Rotation=2,Sid=67108867,ScriptIndex=10,Flags=536870912}};
        // A second NPC shares Id=20 in the real map. It must not become the guidance target.
        var other=actor with {Key="other-id20",Offset=126000,Header=actor.Header with {Tile=20101,Sid=67108868,ScriptIndex=11}};
        return map with {Source=map.Source with {Path="maps/arvillag.map",Size=216200,Sha256=ArroyoRewardProfile.MapHash},
            Prefix=map.Prefix with {Header=map.Prefix.Header with {EnteringTile=20102}},
            ObjectLayers=new[] {map.ObjectLayers[0] with {Objects=map.ObjectLayers[0].Objects.Append(actor).Append(other).ToArray()}},
            ScriptBindings=new[] {new ScriptBinding(67108867,10,new("scripts/ahelder.int","synthetic",20658,ArroyoElderSource.ProgramHash))}};
    }
    private static WorldSession World(MapDocument map)=>new(_=>map,new[] {4},initialMap:4);
    public static void Run(Action<string,Action> check)
    {
        check("Arroyo elder route uses source offset and binding despite duplicate Id20 and never mutates the world",()=>
        {
            var world=World(Fixture()); byte[] before=CheckpointFile.Encode(world.Capture("catalog"));
            var route=world.InspectArroyoElderApproach(false);
            if (route.Status!=TempleWorldStatus.Ready) throw new Exception($"Synthetic elder route: {route.Status}, source match {ArroyoElderSource.Match(world.Current.Map)!=null}.");
            Assert(route.Status==TempleWorldStatus.Ready && route.ActorKey==ArroyoRewardProfile.MapHash+":126588"
                && route.TargetHex.HasValue && FalloutGrid.Distance(route.TargetHex.Value,18915)==1
                && route.Tiles![0]==world.Location.Hex && route.Tiles[^1]==route.TargetHex && !route.Tiles.Contains(18915)
                && !route.Tiles.Skip(1).Any(world.Current.Doors.Navigation(0).IsExit));
            route.Tiles![0]=-1;
            Assert(world.InspectArroyoElderApproach(true).Status==TempleWorldStatus.Busy
                && world.InspectArroyoElderApproach(false).Tiles![0]==world.Location.Hex
                && before.SequenceEqual(CheckpointFile.Encode(world.Capture("catalog"))));
            world.Current.Travel.MoveTo(route.TargetHex ?? throw new Exception("Missing elder route target."));
            Assert(world.InspectArroyoElderApproach(false).Tiles!.SequenceEqual(new[] {world.Location.Hex}));
        });
        check("Arroyo elder guidance refuses changed source identity scripts duplicate offset and absent target",()=>
        {
            var map=Fixture(); var actor=ArroyoElderSource.Match(map)!;
            foreach (var invalid in new[] {
                map with {Source=map.Source with {Sha256="foreign"}},
                map with {ScriptBindings=Array.Empty<ScriptBinding>()},
                map with {ScriptBindings=new[] {map.ScriptBindings[0] with {Program=map.ScriptBindings[0].Program with {Sha256="foreign"}}}},
                map with {ObjectLayers=new[] {map.ObjectLayers[0] with {Objects=map.ObjectLayers[0].Objects.Where(o=>o!=actor).ToArray()}}},
                map with {ObjectLayers=new[] {map.ObjectLayers[0] with {Objects=map.ObjectLayers[0].Objects.Append(actor with {Key="duplicate"}).ToArray()}}}})
                Assert(World(invalid).InspectArroyoElderApproach(false).Status==TempleWorldStatus.Unavailable);
        });
        check("Arroyo elder guidance reports blocked neighboring geometry and refuses another elevation",()=>
        {
            var map=Fixture(); var actor=ArroyoElderSource.Match(map)!;
            var walls=Enumerable.Range(0,6).Select(d=>actor with {Key="wall"+d,Offset=100+d,
                Header=actor.Header with {Tile=FalloutGrid.Neighbor(18915,d)!.Value,Fid=0x03000001,Pid=0x03000001,Sid=-1,ScriptIndex=-1}});
            var blocked=map with {ObjectLayers=new[] {map.ObjectLayers[0] with {Objects=map.ObjectLayers[0].Objects.Concat(walls).ToArray()}}};
            Assert(World(blocked).InspectArroyoElderApproach(false).Status==TempleWorldStatus.Blocked);
            var other=map with {Prefix=map.Prefix with {Header=map.Prefix.Header with {EnteringElevation=1}}};
            Assert(World(other).InspectArroyoElderApproach(false).Status==TempleWorldStatus.Unavailable);
        });
    }
    private static void Assert(bool value) {if (!value) throw new Exception("Arroyo elder approach assertion failed.");}
}
