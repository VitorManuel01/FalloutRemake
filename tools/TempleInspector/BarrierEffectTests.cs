using System.Buffers.Binary;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class BarrierEffectTests
{
    // Metadata-only fixtures; no original MAP, INT or PRO bytes are embedded here.
    internal static Prototype Replacement() => Proto(TempleBarrierEffectProposal.ReplacementPid, TempleBarrierEffectProposal.ReplacementFid,
        "proto/scenery/00000934.pro", TempleBarrierEffectProposal.ReplacementPrototypeHash, DoorSession.OpenDoorMask);
    private static Prototype Proto(int pid, int fid, string path, string hash, int flags)
    {
        var raw = new byte[45]; BinaryPrimitives.WriteInt32BigEndian(raw.AsSpan(20), flags);
        return new(pid, 2, 5, fid, -1, new(path, "synthetic trusted-identity fixture", 45, hash), raw);
    }
    internal static MapDocument Fixture()
    {
        var map = ExplosiveProposalTests.Fixture(); var template = map.ObjectLayers[0].Objects[0];
        var barrier = template with { Key = TempleBarrierEffectProposal.MapHash + ":303324", Offset = 303324, ContainerElevation = 1, Subtype = 5,
            Header = template.Header with { Id = 44, Tile = 14322, Elevation = 1, Pid = 33555364, Fid = 33555375, Sid = 50331649, ScriptIndex = 34, Flags = 0, Frame = 0 },
            Inventory = Array.Empty<InventoryEntry>(), InventoryCount = 0 };
        var fields = new int[14]; fields[1] = 34; fields[3] = 44; fields[4] = -1;
        var script = new MapScript(0, 64, 50331649, -1, Array.Empty<int>(), fields);
        return map with { Source = new("maps/arcaves.map", "synthetic trusted-identity fixture", 526972, TempleBarrierEffectProposal.MapHash),
            Prefix = map.Prefix with { Header = map.Prefix.Header with { EnteringTile = 14521, EnteringElevation = 1 },
                Layers = new[] { new MapLayer(0, Array.Empty<SquareTile>()), new MapLayer(1, Array.Empty<SquareTile>()) } },
            ObjectLayers = new[] { map.ObjectLayers[0], new ObjectLayer(1, 0, 1, new[] { barrier }) }, DeclaredRootObjects = 2,
            ScriptGroups = new[] { new ScriptGroup(3, 0, 1, new[] { new ScriptBlock(0, 0, 1, 0, new[] { script }) }) },
            ScriptBindings = new[] { new ScriptBinding(50331649, 34, new("scripts/aibkdor.int", "synthetic", 2470, TempleBarrierEffectProposal.ProgramHash)) },
            Prototypes = map.Prototypes.Append(Proto(33555364, 33555375, "proto/scenery/00000933.pro", TempleBarrierEffectProposal.OriginalPrototypeHash, 0)).ToArray() };
    }
    private static WorldSession World(MapDocument? map = null)
    { var world = new WorldSession(_ => map ?? Fixture(), new[] { 3 }); world.Current.Items.Restore(new[] { new TakenItem("owner", "item", 1) }); return world; }
    private static Guid Place(WorldSession world)
    { var plan = ExplosiveUseProposal.Create(world, world.Carried.Single(), 60, SkillRollOutcome.Success, false).Plan; return world.CommitExplosiveUse(plan, false).Use!.Allocation.Id; }
    private static string State(WorldSession world) => System.Text.Json.JsonSerializer.Serialize(world.Capture("catalog"));
    public static void Run(Action<string, Action> check)
    {
        check("due explosive proposes the source-bound barrier replacement without modifying any world state", () =>
        {
            var world = World(); Guid id = Place(world); world.AdvanceUseTime(600); string before = State(world); long revision = world.Current.Items.Revision;
            var result = TempleBarrierEffectProposal.Create(world, id, Replacement());
            Assert(result.Status == BarrierEffectStatus.Ready && result.Plan is { ReplacementPid: 33555365, ReplacementFid: 33555376, Distance: 1, SourceMap: 3 });
            Assert(result.Plan!.UseId == id && result.Plan.SessionToken == world.SessionToken && result.Plan.SourceRevision == revision
                && result.Plan.Center == world.Location && result.Plan.TargetKey == TempleBarrierEffectProposal.MapHash + ":303324");
            Assert(State(world) == before && world.Current.Items.Revision == revision && !world.Current.Doors.Navigation(1).Walkable(14322));
            Assert(TempleBarrierEffectProposal.Create(world, id, Replacement()) == result && State(world) == before);
        });
        check("missing, early and already consumed uses never produce barrier effects", () =>
        {
            var world = World(); Guid id = Place(world); string before = State(world);
            Assert(TempleBarrierEffectProposal.Create(world, Guid.Empty, Replacement()).Status == BarrierEffectStatus.NoUse);
            Assert(TempleBarrierEffectProposal.Create(world, id, Replacement()).Status == BarrierEffectStatus.NotDue && State(world) == before);
            world.AdvanceUseTime(600); world.CompleteDueUse(id); before = State(world);
            Assert(TempleBarrierEffectProposal.Create(world, id, Replacement()).Status == BarrierEffectStatus.Consumed && State(world) == before);
        });
        check("barrier targeting includes distance three and excludes distance four and another elevation", () =>
        {
            foreach (int distance in new[] { 1, 2, 3, 4 })
            {
                var world = World(); int tile = Enumerable.Range(1, 39999).First(t => FalloutGrid.Distance(t, 14322) == distance && world.Current.Travel.CanArrive(new(3, 1, t, 0)));
                world.Current.Travel.Arrive(new(3, 1, tile, 0)); Guid id = Place(world); world.AdvanceUseTime(600); string before = State(world);
                var result = TempleBarrierEffectProposal.Create(world, id, Replacement());
                Assert(result.Status == (distance <= 3 ? BarrierEffectStatus.Ready : BarrierEffectStatus.NoTarget) && State(world) == before);
                if (result.Plan != null) Assert(result.Plan.Distance == distance);
            }
            var other = World(); other.Current.Travel.Arrive(new(3, 0, 14521, 0)); Guid otherId = Place(other); other.AdvanceUseTime(600);
            Assert(TempleBarrierEffectProposal.Create(other, otherId, Replacement()).Status == BarrierEffectStatus.NoTarget);
        });
        check("barrier profile refuses changed map, instance, script, active binding and original prototype", () =>
        {
            var map = Fixture(); var barrier = map.ObjectLayers[1].Objects.Single();
            MapDocument Changed(MapObject obj) => map with { ObjectLayers = new[] { map.ObjectLayers[0], map.ObjectLayers[1] with { Objects = new[] { obj } } } };
            foreach (var invalid in new[] { map with { Source = map.Source with { Sha256 = "changed" } }, map with { Source = map.Source with { Path = "maps/other.map" } },
                Changed(barrier with { Key = "wrong" }), Changed(barrier with { Header = barrier.Header with { Id = 45 } }), Changed(barrier with { Header = barrier.Header with { Flags = HexNavigation.NoBlock } }),
                Changed(barrier with { Header = barrier.Header with { ScriptIndex = 35 } }),
                map with { ScriptBindings = new[] { map.ScriptBindings[0] with { Program = map.ScriptBindings[0].Program with { Sha256 = "changed" } } } },
                map with { ScriptBindings = new[] { map.ScriptBindings[0], map.ScriptBindings[0] } }, map with { ScriptGroups = Array.Empty<ScriptGroup>() },
                map with { Prototypes = map.Prototypes.Select(p => p.Pid == 33555364 ? p with { Resource = p.Resource with { Sha256 = "changed" } } : p).ToArray() } })
                Assert(TempleBarrierEffectProposal.Match(invalid, Replacement()) == null);
            Assert(TempleBarrierEffectProposal.Match(map, Replacement()) != null);
        });
        check("replacement requires verified identity, nonblocking flags, subtype and absence of scripts", () =>
        {
            var map = Fixture(); var replacement = Replacement();
            foreach (var invalid in new Prototype?[] { null, replacement with { Resource = replacement.Resource with { Sha256 = "changed" } },
                replacement with { Resource = replacement.Resource with { Path = "other.pro" } }, replacement with { RawBytes = new byte[45] },
                replacement with { RawBytes = Array.Empty<byte>() }, replacement with { ScriptId = 34 }, replacement with { Subtype = 0 }, replacement with { Fid = 33555375 } })
                Assert(TempleBarrierEffectProposal.Match(map, invalid) == null);
        });
        check("restored due use proposes against its new session without consuming or mutating the source", () =>
        {
            var world = World(); Guid id = Place(world); world.AdvanceUseTime(600);
            var restored = WorldSession.Restore(_ => Fixture(), new[] { 3 }, CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog"))), "catalog");
            string before = State(restored); var result = TempleBarrierEffectProposal.Create(restored, id, Replacement());
            Assert(result.Status == BarrierEffectStatus.Ready && result.Plan!.SessionToken == restored.SessionToken && result.Plan.SessionToken != world.SessionToken && State(restored) == before);
        });
        check("targeting uses the placement area while another area is active and never loads additional maps", () =>
        {
            var map = Fixture(); var exit = WorldTests.Map(3, 126).ObjectLayers[0].Objects[0];
            exit = exit with { State = exit.State.ToDictionary(p => p.Key, p => p.Key == "destinationTile" ? 20101 : p.Value) };
            map = map with { ObjectLayers = new[] { map.ObjectLayers[0] with { Objects = map.ObjectLayers[0].Objects.Append(exit).ToArray() }, map.ObjectLayers[1] } };
            int loads = 0; var world = new WorldSession(id => { loads++; return id == 3 ? map : WorldTests.Map(126, 3); }, new[] { 3, 126 });
            world.Current.Items.Restore(new[] { new TakenItem("owner", "item", 2) }); Guid id = Place(world); world.AdvanceUseTime(600);
            world.Current.Travel.Arrive(new(3, 0, 20100, 0)); Assert(world.UseExit(false).Status == ExitStatus.Ready);
            string before = State(world); var result = TempleBarrierEffectProposal.Create(world, id, Replacement());
            Assert(result.Status == BarrierEffectStatus.Ready && result.Plan!.Center.Map == 3 && world.CurrentMap == 126 && loads == 2 && State(world) == before);
            world.Current.Travel.MoveTo(20101);
            var outside = world.CommitExplosiveUse(ExplosiveUseProposal.Create(world, world.Carried.Single(), 60, SkillRollOutcome.CriticalFailure, false).Plan, false);
            Assert(outside.Status == ExplosiveCommitStatus.Ready); before = State(world);
            Assert(TempleBarrierEffectProposal.Create(world, outside.Use!.Allocation.Id, Replacement()).Status == BarrierEffectStatus.UnsupportedArea
                && world.ItemUses.All(u => u.SourceMap == 3) && loads == 2 && State(world) == before);
        });
    }
    private static void Assert(bool condition) { if (!condition) throw new Exception("Barrier effect proposal assertion failed."); }
}
