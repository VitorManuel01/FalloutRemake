using System.Text;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class ItemCheckpointTests
{
    private static WorldSession World() => new(id => id == 3 ? ItemTransferTests.Map() : WorldTests.Map(126, 3), new[] { 3, 126 });
    private static void Take(WorldSession w)
    {
        w.Current.Travel.Arrive(new(3, 0, FalloutGrid.Neighbor(20500, 0)!.Value, 0));
        if (w.TakeItem("owner", "item", 2, false) != ItemTakeStatus.Ready) throw new Exception("Cannot collect fixture.");
    }
    private static WorldSession Restore(WorldCheckpoint saved) => WorldSession.Restore(id => id == 3 ? ItemTransferTests.Map() : WorldTests.Map(126, 3), new[] { 3, 126 }, saved, "catalog");
    public static void Run(Action<string, Action> check)
    {
        check("current world checkpoint restores collected source identities and remaining container quantities", () =>
        {
            var w = World(); Take(w); WorldCheckpoint saved = CheckpointFile.Decode(CheckpointFile.Encode(w.Capture("catalog")));
            var fresh = Restore(saved);
            Assert(saved.Version == 5 && saved.Profile == "neutral-v5" && fresh.CarriedCount == 2 && fresh.Carried.Single().Source.Key == "item");
            Assert(fresh.Current.Items.Contents("owner").Single().Quantity == 3 && fresh.Location == w.Location);
            saved.Areas[0].Taken![0] = new("owner", "item", 5); Assert(w.CarriedCount == 2 && fresh.CarriedCount == 2);
        });
        check("collected items survive an exterior visit and return but global reset restores their source containers", () =>
        {
            MapDocument source = ItemTransferTests.Map();
            MapObject exit = WorldTests.Map(3, 126).ObjectLayers[0].Objects[0];
            MapDocument temple = source with { ObjectLayers = new[] { new ObjectLayer(0, 0, 2, new[] { source.ObjectLayers[0].Objects[0], exit }) } };
            var w = new WorldSession(id => id == 3 ? temple : WorldTests.Map(126, 3), new[] { 3, 126 }); Take(w);
            w.Current.Travel.Arrive(new(3, 0, 20100, 0)); Assert(w.UseExit(false).Status == ExitStatus.Ready && w.CarriedCount == 2);
            var restored = WorldSession.Restore(id => id == 3 ? temple : WorldTests.Map(126, 3), new[] { 3, 126 }, CheckpointFile.Decode(CheckpointFile.Encode(w.Capture("catalog"))), "catalog");
            Assert(restored.CurrentMap == 126 && restored.CarriedCount == 2 && restored.UseExit(false).Status == ExitStatus.Ready);
            Assert(restored.Current.Items.Contents("owner").Single().Quantity == 3);
            restored.Reset(); Assert(restored.CarriedCount == 0 && restored.Current.Items.Contents("owner").Single().Quantity == 5);
        });
        check("v1 checkpoints remain loadable as an empty carried inventory without accepting injected item data", () =>
        {
            var w = World(); WorldCheckpoint saved = w.Capture("catalog");
            var legacy = saved with { Version = 1, Profile = "neutral-v1", Areas = saved.Areas.Select(a => a with { Taken = null, Uses = null, Replacements = null }).ToArray() };
            Assert(Restore(CheckpointFile.Decode(CheckpointFile.Encode(legacy))).CarriedCount == 0);
            Reject(() => Restore(legacy with { Areas = new[] { legacy.Areas[0] with { Taken = new[] { new TakenItem("owner", "item", 1) } } } }));
            Reject(() => Restore(saved with { Areas = new[] { saved.Areas[0] with { Taken = null } } }));
        });
        check("forged checkpoint item keys, duplicates or quantities cannot alter a live world", () =>
        {
            var w = World(); Take(w); WorldCheckpoint saved = w.Capture("catalog");
            foreach (TakenItem[] rows in new[] { new[] { new TakenItem("owner", "foreign", 1) }, new[] { new TakenItem("owner", "item", 6) },
                new[] { new TakenItem("owner", "item", 1), new TakenItem("owner", "item", 1) } })
            { Reject(() => Restore(saved with { Areas = new[] { saved.Areas[0] with { Taken = rows } } })); Assert(w.CarriedCount == 2 && w.Current.Items.Contents("owner").Single().Quantity == 3); }
            string json = Encoding.UTF8.GetString(CheckpointFile.Encode(saved));
            Assert(!json.Contains("RawBytes") && !json.Contains("OriginalMap") && json.Contains("ItemKey"));
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Item checkpoint assertion failed."); }
    private static void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Forged item checkpoint accepted."); }
}
