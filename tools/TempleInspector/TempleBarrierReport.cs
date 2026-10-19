using System.Buffers.Binary;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class TempleBarrierReport
{
    public static object Create(GameData data)
    {
        MapDocument map = MapDocument.Read(data.Read("maps/arcaves.map"), data.Read); MapLayoutWriter.Verify(map);
        if (map.Source.Sha256 != "BA35474B8EA67AEC50B6A962A1BC767A87366449D4B631FD1B4CE241847B5BFE") throw new InvalidDataException("Unsupported barrier MAP profile.");
        MapObject barrier = map.ObjectLayers.SelectMany(l => l.Objects).Single(o => o.Offset == 303324 && o.Header.Id == 44
            && o.Header.Pid == 33555364 && o.Header.Tile == 14322 && o.Header.Elevation == 1 && o.Subtype == 5);
        ScriptBinding binding = map.ScriptBindings.Single(b => b.Sid == barrier.Header.Sid && b.Index == 34);
        IntProgram program = IntProgram.Read(data.Read(binding.Program.Path));
        if (program.Source.Sha256 != "11749956A873B8B7EAC7CCC4D5B3C286F2C4B2E2F5E24EB17C9CDEBEC34426EA" || program.Source.Size != 2470)
            throw new InvalidDataException("Unsupported barrier script profile.");
        var expected = new (int Offset, ushort Opcode, int? Operand)[] {
            (2296, 0xC001, 2358), (2302, 0xC001, 49), (2308, 0x80BE, null), (2310, 0x810B, null), (2312, 0xC001, 6), (2318, 0x8033, null), (2320, 0x802F, null),
            (2322, 0xC001, 33555365), (2328, 0x80BC, null), (2330, 0x80D4, null), (2332, 0x80BC, null), (2334, 0x80EC, null),
            (2336, 0xC001, 1), (2342, 0x8046, null), (2344, 0x80B7, null), (2346, 0xC001, 0), (2352, 0x8031, null), (2354, 0x80BC, null), (2356, 0x80F4, null) };
        var observed = program.ProcedureInstructions(program.Procedures.Single(p => p.Name == "damage_p_proc").Index)
            .Where(i => i.Offset >= 2296 && i.Offset < 2358).Select(i => (i.Offset, i.Opcode, i.OperandBits)).ToArray();
        if (!observed.SequenceEqual(expected)) throw new InvalidDataException("Barrier damage sequence changed.");
        var resources = new ResourceCatalog(data.Read); Prototype replacement = new PrototypeCatalog(resources).Get(33555365);
        int flags = BinaryPrimitives.ReadInt32BigEndian(replacement.RawBytes.AsSpan(20, 4));
        if (replacement.Type != 2 || replacement.Subtype != 5 || replacement.ScriptId != -1 || (flags & HexNavigation.NoBlock) == 0)
            throw new InvalidDataException("Replacement profile is not the expected nonblocking scenery.");
        var doors = DoorSession.FromMap(map, true); int entry = 29097;
        var beforeNavigation = doors.Navigation(1); var before = beforeNavigation.ReachableFrom(entry);
        var after = new HexNavigation(map.ObjectLayers.SelectMany(l => l.Objects), 1, o => o.Key == barrier.Key
            ? new(o.Header.Flags | HexNavigation.Hidden, null)
            : doors.Get(o.Key) is DoorState door ? new(door.Flags, door.OpenFlags) : new(o.Header.Flags, o.State.GetValueOrDefault("openFlags"))).ReachableFrom(entry);
        int[] exits = map.ObjectLayers.SelectMany(l => l.Objects).Where(o => o.Header.Elevation == 1 && o.Header.Pid is >= 0x05000010 and <= 0x05000017
            && o.State.GetValueOrDefault("destinationMap", -1) == 3 && o.State.GetValueOrDefault("destinationElevation", -1) == 2)
            .Select(o => o.Header.Tile).Distinct().OrderBy(t => t).ToArray();
        int beforeExits = exits.Count(t => before.Distance(t) >= 0), afterExits = exits.Count(t => after.Distance(t) >= 0);
        if (beforeExits != 0 || afterExits == 0) throw new InvalidDataException("Hypothetical barrier removal no longer explains the next-elevation boundary.");
        MapLayoutWriter.Verify(map);
        return new { Status = "TEMPLE_BARRIER_ANALYSIS_OK", Map = map.Source, Barrier = new { barrier.Key, barrier.Offset, barrier.Header, barrier.Subtype, Binding = binding },
            Program = program.Source, Procedure = "damage_p_proc",
            Condition = new { Metarule = 49, Argument = "target_obj", EqualTo = 6, Meaning = "weapon/explosion damage type equals explosion; otherwise skip to 2358" },
            Effect = new { ReplacementPid = replacement.Pid, Tile = "self tile", Elevation = "self elevation", ScriptId = -1,
                NegativeOne = "push 1 then OPCODE_NEGATE, not an elevation adjustment", CreateOffset = 2344, DestroySelfOffset = 2356, ExperienceAwardedByThisProcedure = 0 },
            Replacement = new { replacement.Resource, replacement.Fid, replacement.Subtype, Flags = flags, Nonblocking = true },
            GeometryExperiment = new { Mode = "read-only hypothetical removal; replacement is nonblocking", Entry = entry,
                BeforeReachable = before.Count, AfterReachable = after.Count, BeforeExitsToElevation2 = beforeExits, AfterExitsToElevation2 = afterExits },
            ExplosionRangeReference = new { Mode = "CE default unmodified scenery-dispatch radius; no explosive placed or consumed", Radius = 3, ConfigurationOverridesEvaluated = false,
                ReachablePlacementHexes = Enumerable.Range(0, 40000).Where(t => before.Distance(t) >= 0 && !beforeNavigation.IsExit(t)
                    && FalloutGrid.Distance(t, barrier.Header.Tile) <= 3).Select(t => new { Hex = t, Distance = FalloutGrid.Distance(t, barrier.Header.Tile), ApproachSteps = before.Distance(t) }).ToArray() },
            AdditionalDependencies = resources.Identities, RuntimeAdapterEnabled = false,
            RequiredBeforeEnabling = new[] { "validated explosion event/radius and damage source", "consumption and ownership of used explosive", "runtime replacement identity and rendering", "source-bound checkpoint and navigation overlay" },
            OriginalReconstruction = "verified" };
    }
}
