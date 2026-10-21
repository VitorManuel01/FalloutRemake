using System.Text;
using System.Text.Json.Nodes;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class ItemUseCheckpointTests
{
    private static readonly int[] Allowed = { 3, 126 };
    private static MapDocument Map(int id = 3)
    {
        var map = ExplosiveProposalTests.Fixture(); var exit = WorldTests.Map(id, id == 3 ? 126 : 3).ObjectLayers[0].Objects[0];
        return map with { Source = map.Source with { Path = $"maps/map{id}.map", Sha256 = $"hash{id}" }, Prefix = map.Prefix with { Header = map.Prefix.Header with { MapIndex = id } },
            ObjectLayers = new[] { new ObjectLayer(0, 0, 2, map.ObjectLayers[0].Objects.Append(exit).ToArray()) } };
    }
    private static WorldSession World() => new(id => Map(id), Allowed);
    private static WorldSession Restore(WorldCheckpoint saved, Func<int, MapDocument>? load = null) => WorldSession.Restore(load ?? (id => Map(id)), Allowed, saved, "catalog");
    private static WorldCheckpoint RoundTrip(WorldCheckpoint saved) => CheckpointFile.Decode(CheckpointFile.Encode(saved));
    public static void Run(Action<string, Action> check)
    {
        check("use checkpoint JSON requires explicit phase remaining ticks timing and placement coordinates", () =>
        {
            var world = World(); world.Current.Items.Restore(new[] { new TakenItem("owner", "item", 1) }); world.Current.Items.Place(ItemUseTests.Use());
            byte[] bytes = CheckpointFile.Encode(world.Capture("catalog"));
            foreach (string field in new[] { "Id", "OwnerKey", "ItemKey", "Quantity", "Phase", "Placement", "Schedule", "RemainingTicks" })
            {
                JsonNode node = JsonNode.Parse(bytes)!; node["Areas"]![0]!["Uses"]![0]!.AsObject().Remove(field);
                Reject(() => CheckpointFile.Decode(Encoding.UTF8.GetBytes(node.ToJsonString())));
            }
            foreach (string field in new[] { "SelectedSeconds", "DelayTicks", "Expiry" })
            {
                JsonNode node = JsonNode.Parse(bytes)!; node["Areas"]![0]!["Uses"]![0]!["Schedule"]!.AsObject().Remove(field);
                Reject(() => CheckpointFile.Decode(Encoding.UTF8.GetBytes(node.ToJsonString())));
            }
            foreach (string field in new[] { "Map", "Elevation", "Hex", "Rotation" })
            {
                JsonNode node = JsonNode.Parse(bytes)!; node["Areas"]![0]!["Uses"]![0]!["Placement"]!.AsObject().Remove(field);
                Reject(() => CheckpointFile.Decode(Encoding.UTF8.GetBytes(node.ToJsonString())));
            }
        });
        check("current checkpoint restores placed consumed and carried quantities without duplicating their original sources", () =>
        {
            var world = World(); var items = world.Current.Items; items.Restore(new[] { new TakenItem("owner", "item", 4) });
            items.Place(ItemUseTests.Use()); items.Place(ItemUseTests.Use(2)); items.Consume(ItemUseTests.Use(2).Id);
            var saved = RoundTrip(world.Capture("catalog")); var restored = Restore(saved);
            Assert(saved.Version == 8 && saved.Profile == "neutral-v8" && restored.CarriedCount == 2 && restored.CarriedWeight == 8 && restored.ItemUses.Length == 2);
            Assert(restored.Current.Items.Balance("owner", "item") == new ItemBalance(5, 1, 2, 1, 1));
            Assert(CheckpointFile.Encode(restored.Capture("catalog")).SequenceEqual(CheckpointFile.Encode(saved)));
            var before = world.ItemUses[0]; saved.Areas[0].Uses![0] = saved.Areas[0].Uses![0] with { RemainingTicks = 1 };
            Assert(world.ItemUses[0] == before); restored.Reset(); Assert(restored.ItemUses.Length == 0 && restored.CarriedCount == 0 && restored.Current.Items.Balance("owner", "item")!.Remaining == 5);
        });
        check("checkpoint keeps the source area separate from an explosive placement in another visited area", () =>
        {
            var world = World(); var source = world.Current.Items; source.Restore(new[] { new TakenItem("owner", "item", 2) }); world.UseExit(false);
            Assert(source.Place(ItemUseTests.Use() with { Placement = new(126, 0, 20101, 0) }) == ItemUseStatus.Ready);
            var restored = Restore(RoundTrip(world.Capture("catalog")));
            Assert(restored.CurrentMap == 126 && restored.Areas.Count == 2 && restored.CarriedCount == 1 && restored.CarriedWeight == 4 && restored.ItemUses.Single().SourceMap == 3
                && restored.ItemUses.Single().Allocation.Placement.Map == 126);
            Assert(restored.UseExit(false).Status == ExitStatus.Ready && restored.Current.Items.Balance("owner", "item") == new ItemBalance(5, 3, 1, 1, 0));
        });
        check("legacy v1 and v2 JSON without use fields is restored with neutral use state and upgraded on capture", () =>
        {
            var world = World(); world.Current.Items.Restore(new[] { new TakenItem("owner", "item", 2) });
            foreach (int version in new[] { 1, 2 })
            {
                JsonNode document = JsonNode.Parse(CheckpointFile.Encode(world.Capture("catalog")))!;
                document["Version"] = version; document["Profile"] = $"neutral-v{version}";
                document.AsObject().Remove("Entries");
                var area = document["Areas"]![0]!.AsObject(); area.Remove("Uses"); area.Remove("Replacements"); if (version == 1) area.Remove("Taken");
                var legacy = CheckpointFile.Decode(Encoding.UTF8.GetBytes(document.ToJsonString())); var restored = Restore(legacy);
                Assert(restored.ItemUses.Length == 0 && restored.CarriedCount == (version == 1 ? 0 : 2) && restored.Capture("catalog").Version == 8);
                Reject(() => Restore(legacy with { Areas = legacy.Areas.Select(a => a with { Uses = Array.Empty<ItemUseAllocation>() }).ToArray() }));
            }
        });
        check("current checkpoint refuses missing arrays global duplicate use identities and unknown placement maps without replacing the live world", () =>
        {
            var world = World(); world.Current.Items.Restore(new[] { new TakenItem("owner", "item", 1) }); world.Current.Items.Place(ItemUseTests.Use()); world.UseExit(false);
            world.Current.Items.Restore(new[] { new TakenItem("owner", "item", 1) }); world.Current.Items.Place(ItemUseTests.Use(2));
            WorldCheckpoint saved = world.Capture("catalog"); var source = saved.Areas[0]; var outside = saved.Areas[1];
            Reject(() => Restore(saved with { Areas = new[] { source with { Uses = null }, outside } }));
            Reject(() => Restore(saved with { Areas = new[] { source, outside with { Uses = new[] { outside.Uses![0] with { Id = source.Uses![0].Id } } } } }));
            int unknownLoads = 0;
            var foreign = saved with { Areas = new[] { source with { Uses = new[] { source.Uses![0] with { Placement = new(35, 0, 20101, 0) } } }, outside } };
            Reject(() => Restore(foreign, id => { if (id == 35) unknownLoads++; return Map(id); }));
            Assert(unknownLoads == 0 && world.CurrentMap == 126 && world.ItemUses.Length == 2 && world.CarriedCount == 0);
        });
        check("use placement rejects exit tiles permanent blockers absent elevations and changed explosive prototypes", () =>
        {
            var world = World(); world.Current.Items.Restore(new[] { new TakenItem("owner", "item", 1) }); world.Current.Items.Place(ItemUseTests.Use());
            var saved = world.Capture("catalog"); var source = saved.Areas[0];
            foreach (ActorLocation position in new[] { new ActorLocation(3, 0, 20100, 0), new(3, 1, 20101, 0) })
                Reject(() => Restore(saved with { Areas = new[] { source with { Uses = new[] { source.Uses![0] with { Placement = position } } } } }));
            var map = Map(); var block = WorldTests.Map(3, 126).ObjectLayers[0].Objects[1];
            block = block with { Key = "permanent-wall", Subtype = null, Header = block.Header with { Tile = 20101, Pid = 0x03000001, Fid = 0x03000001 } };
            Reject(() => Restore(saved, _ => map with { ObjectLayers = new[] { new ObjectLayer(0, 0, 3, map.ObjectLayers[0].Objects.Append(block).ToArray()) } }));
            var prototype = map.Prototypes[1]; Reject(() => Restore(saved, _ => map with { Prototypes = new[] { map.Prototypes[0], prototype with { Resource = prototype.Resource with { Sha256 = "changed" } } } }));
            Assert(world.ItemUses.Length == 1 && world.CarriedCount == 0);
        });
        check("an explosive can remain below a supported door that closed after placement", () =>
        {
            var map = ExplosiveProposalTests.Fixture(); var door = WorldTests.Map(3, 126).ObjectLayers[0].Objects[1];
            door = door with { Key = "placement-door", Header = door.Header with { Tile = 20101 } };
            map = map with { ObjectLayers = new[] { new ObjectLayer(0, 0, 2, map.ObjectLayers[0].Objects.Append(door).ToArray()) } };
            var world = new WorldSession(_ => map, new[] { 3 });
            Assert(world.Current.Doors.Toggle(door.Key, 20100, 0, false) == DoorUseStatus.Ready); world.Current.Travel.MoveTo(20101);
            world.Current.Items.Restore(new[] { new TakenItem("owner", "item", 1) }); world.Current.Items.Place(ItemUseTests.Use());
            world.Current.Travel.MoveTo(20100); Assert(world.Current.Doors.Toggle(door.Key, 20100, 0, false) == DoorUseStatus.Ready);
            var restored = WorldSession.Restore(_ => map, new[] { 3 }, RoundTrip(world.Capture("catalog")), "catalog");
            Assert(restored.ItemUses.Length == 1 && !restored.Current.Doors.Navigation(0).Walkable(20101) && restored.CarriedCount == 0);
            var bytes = CheckpointFile.Encode(restored.Capture("catalog")); string json = Encoding.UTF8.GetString(bytes);
            Assert(json.Contains("RemainingTicks") && !json.Contains("RawBytes") && !json.Contains("OriginalMap"));
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Item-use checkpoint assertion failed."); }
    private static void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Invalid item-use checkpoint accepted."); }
}
