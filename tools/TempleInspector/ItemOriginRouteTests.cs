using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class ItemOriginRouteTests
{
    private static MapObject Exit(MapObject template, string key, int tile, int elevation, int map, int target, int targetElevation) => template with
    {
        Key = key, Header = template.Header with { Tile = tile, Elevation = elevation }, ContainerElevation = elevation,
        State = new Dictionary<string, int> { ["destinationMap"] = map, ["destinationTile"] = target, ["destinationElevation"] = targetElevation, ["destinationRotation"] = 0 }
    };
    private static MapDocument Source()
    {
        var map = ItemTransferTests.Map(); var owner = map.ObjectLayers[0].Objects[0];
        var template = WorldTests.Map(3, 126).ObjectLayers[0].Objects[0];
        return map with
        {
            Prefix = map.Prefix with { Header = map.Prefix.Header with { EnteringTile = 20101 }, Layers = new[] { new MapLayer(0, Array.Empty<SquareTile>()), new MapLayer(1, Array.Empty<SquareTile>()) } },
            ObjectLayers = new[] { new ObjectLayer(0, 0, 2, new[] { Exit(template, "outside", 20100, 0, 126, 20101, 0), Exit(template, "stairs", 20110, 0, 3, 20490, 1) }),
                new ObjectLayer(1, 0, 1, new[] { owner with { Header = owner.Header with { Elevation = 1 }, ContainerElevation = 1 } }) }
        };
    }
    private static MapDocument Outside(int destination = 3)
    {
        var map = WorldTests.Map(126, destination); var exit = map.ObjectLayers[0].Objects[0];
        return map with { Prefix = map.Prefix with { Header = map.Prefix.Header with { EnteringTile = 20101 } },
            ObjectLayers = new[] { new ObjectLayer(0, 0, 1, new[] { Exit(exit, "return", 20200, 0, destination, 20101, 0) }) } };
    }
    public static void Run(Action<string, Action> check)
    {
        check("item origin route crosses cached maps and elevations without travel, loads or transfer", () =>
        {
            int loads = 0;
            var world = new WorldSession(id => { loads++; return id == 3 ? Source() : Outside(); }, new[] { 3, 126, 4 });
            world.Current.Items.Restore(new[] { new TakenItem("owner", "item", 2) }); world.Current.Travel.Arrive(new(3, 0, 20100, 0)); world.UseExit(false);
            var before = world.Location; var item = world.Carried.Single(); var saved = world.Capture("catalog");
            var plan = ItemOriginRoute.Find(world, item, false)!;
            Assert(plan.Transitions == 2 && plan.Legs.Length == 3 && plan.Legs[0].Tiles.First() == 20101 && plan.Legs[0].Tiles.Last() == 20200
                && plan.Legs[0].Destination!.Map == 3 && plan.Legs[1].Destination!.Elevation == 1 && plan.Legs[2].Destination == null);
            Assert(world.Location == before && loads == 2 && world.Areas.Count == 2 && world.CarriedCount == 2);
            foreach (var leg in plan.Legs)
            {
                var area = world.Areas.Single(a => a.Map.Prefix.Header.MapIndex == leg.Map);
                var route = area.Doors.Navigation(leg.Elevation).FindRoute(leg.Tiles[0], leg.Tiles[^1]);
                Assert(route.Tiles.SequenceEqual(leg.Tiles));
            }
            var restored = WorldSession.Restore(id => id == 3 ? Source() : Outside(), new[] { 3, 126, 4 }, saved, "catalog");
            var replay = ItemOriginRoute.Find(restored, restored.Carried.Single(), false)!;
            Assert(replay.WalkingSteps == plan.WalkingSteps && replay.Legs[0].Tiles.SequenceEqual(plan.Legs[0].Tiles));
        });
        check("item origin planning never discovers or loads an unvisited intermediate area", () =>
        {
            int loads = 0;
            var world = new WorldSession(id => { loads++; return id == 3 ? Source() : id == 126 ? Outside(4) : throw new Exception("Unexpected route load"); }, new[] { 3, 126, 4 });
            world.Current.Items.Restore(new[] { new TakenItem("owner", "item", 2) }); world.Current.Travel.Arrive(new(3, 0, 20100, 0)); world.UseExit(false);
            Assert(ItemOriginRoute.Find(world, world.Carried.Single(), false) == null && loads == 2 && world.CurrentMap == 126);
        });
        check("item origin planning rejects moving actor and stale or invented source identities", () =>
        {
            var world = new WorldSession(_ => ItemTransferTests.Map(), new[] { 3 }); world.Current.Items.Restore(new[] { new TakenItem("owner", "item", 2) });
            var item = world.Carried.Single();
            Assert(ItemOriginRoute.Find(world, item, true) == null && ItemOriginRoute.Find(world, item with { OwnerKey = "invented" }, false) == null);
            var plan = ItemOriginRoute.Find(world, item, false)!; Assert(plan.Transitions == 0 && plan.WalkingSteps > 0);
            world.Current.Travel.Arrive(new(3, 0, plan.Legs[0].Tiles[^1], 0));
            Assert(ItemOriginRoute.Find(world, item, false)!.WalkingSteps == 0);
            world.Current.Items.Reset(); Assert(ItemOriginRoute.Find(world, item, false) == null);
        });
        check("ambiguous exits and same-elevation reloads cannot become item origin shortcuts", () =>
        {
            foreach (bool ambiguous in new[] { true, false })
            {
                var source = Source(); var stairs = source.ObjectLayers[0].Objects.Single(o => o.Key == "stairs");
                MapObject[] exits = ambiguous ? new[] { stairs, Exit(stairs, "conflict", 20110, 0, 3, 20491, 1) }
                    : new[] { Exit(stairs, "reload", 20110, 0, 3, 20490, 0) };
                source = source with { ObjectLayers = new[] { new ObjectLayer(0, 0, exits.Length, exits), source.ObjectLayers[1] } };
                var world = new WorldSession(_ => source, new[] { 3 }); world.Current.Items.Restore(new[] { new TakenItem("owner", "item", 1) });
                Assert(ItemOriginRoute.Find(world, world.Carried.Single(), false) == null && world.Location.Elevation == 0);
            }
        });
        check("unreachable inventory origin terminates cached cycles and rejects hidden or blocked transitions", () =>
        {
            foreach (string variant in new[] { "cycle", "hidden", "blocked" })
            {
                var source = Source(); var ground = source.ObjectLayers[0].Objects;
                var upper = source.ObjectLayers[1].Objects;
                if (variant == "cycle") ground = ground.Where(o => o.Key != "stairs").ToArray();
                if (variant == "hidden") ground = ground.Select(o => o.Key == "stairs" ? o with { Header = o.Header with { Flags = HexNavigation.Hidden } } : o).ToArray();
                if (variant == "blocked")
                {
                    var obstacle = WorldTests.Map(3, 126).ObjectLayers[0].Objects[1];
                    upper = upper.Append(obstacle with { Key = "blocked-arrival", ContainerElevation = 1,
                        Header = obstacle.Header with { Elevation = 1, Tile = 20490 } }).ToArray();
                }
                source = source with { ObjectLayers = new[] { new ObjectLayer(0, 0, ground.Length, ground), new ObjectLayer(1, 0, upper.Length, upper) } };
                var world = new WorldSession(id => id == 3 ? source : Outside(), new[] { 3, 126 });
                world.Current.Items.Restore(new[] { new TakenItem("owner", "item", 1) }); world.Current.Travel.Arrive(new(3, 0, 20100, 0)); world.UseExit(false);
                Assert(ItemOriginRoute.Find(world, world.Carried.Single(), false) == null && world.CurrentMap == 126 && world.CarriedCount == 1);
            }
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Item origin route assertion failed."); }
}
