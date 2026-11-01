using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

/// <summary>
/// Evidence for CritterSight on original data: the temple's encounter critters run zclrat (Giant Ant) or zclscorp
/// (Radscorpion), whose critter_p_proc is exactly "if obj_can_see_obj(self_obj, dude_obj) then attack_complex(dude_obj, ...)";
/// per critter: hex, facing, PE and the two perception ranges.
/// </summary>
internal static class CritterSightReport
{
    static readonly (string Path, int Size, string Sha256, int Body)[] Scripts =
    {
        ("scripts/zclrat.int", 3142, "7BB3F264C24726C02637447D4C73218EE7314E5FA55589499FF289C6F8D94D0A", 2552),
        ("scripts/zclscorp.int", 3482, "D215E4A5DA87CCBC901CFE078560832B337D4AD71EE1C4991BBEBA7412D0ECD0", 2662)
    };

    public static object Create(GameData data)
    {
        MapDocument map = MapDocument.Read(data.Read("maps/arcaves.map"), data.Read); MapLayoutWriter.Verify(map);
        var prototypes = new PrototypeCatalog(new ResourceCatalog(data.Read));
        foreach (var (path, size, sha256, body) in Scripts)
        {
            IntProgram program = IntProgram.Read(data.Read(path));
            IntProcedure? proc = program.Procedures.FirstOrDefault(p => p.Name == "critter_p_proc");
            // push_base; if (self_obj sees dude_obj) attack_complex(dude_obj, 0, 1, 0, 0, 30000, 0, 0)
            (ushort, int?)[] expected = { (0x802B, null), (0xC001, body + 62), (0x80BC, null), (0x80BF, null), (0x80DC, null), (0x802F, null), (0x80BF, null),
                (0xC001, 0), (0xC001, 1), (0xC001, 0), (0xC001, 0), (0xC001, 30000), (0xC001, 0), (0xC001, 0), (0x80D0, null) };
            if (program.Source.Size != size || program.Source.Sha256 != sha256 || proc?.BodyOffset != body
                || !program.ProcedureInstructions(proc.Index).Take(expected.Length).Select(i => (i.Opcode, i.OperandBits)).SequenceEqual(expected))
                throw new InvalidDataException($"{path} critter_p_proc is not the audited sight-then-attack test.");
        }
        var programs = map.ScriptBindings.ToDictionary(b => b.Sid, b => b.Program.Path);
        var critters = map.ObjectLayers.SelectMany(l => l.Objects).Where(o => WorldSession.EncounterPids.Contains(o.Header.Pid))
            .OrderBy(o => o.Header.Elevation).ThenBy(o => o.Offset).Select(o =>
            {
                string script = programs.GetValueOrDefault(o.Header.Sid, "none");
                string expected = o.Header.Pid == 0x0100017E ? "scripts/zclrat.int" : "scripts/zclscorp.int";
                if (script != expected) throw new InvalidDataException($"Encounter critter {o.Offset} runs {script}, not {expected}.");
                int perception = CritterSight.Perception(CritterPrototypeData.Read(prototypes.Get(o.Header.Pid)));
                return new { o.Offset, Pid = $"0x{o.Header.Pid:X8}", o.Header.Elevation, Hex = o.Header.Tile, o.Header.Rotation, Script = script, Perception = perception,
                    FrontRange = perception * CritterSight.FacingRangePerPerception, AroundRange = perception, Multihex = (o.Header.Flags & HexNavigation.Multihex) != 0 };
            }).ToArray();
        return new
        {
            Rule = "obj_can_see_obj: isWithinPerception (PE x 5 in the front cone of _can_see, PE all around; dude not sneaking) and a straight path whose first blocker is the dude",
            Scripts = Scripts.Select(s => new { s.Path, s.Size, s.Sha256, CritterProcBody = s.Body }),
            Critters = critters,
            ByPid = critters.GroupBy(c => c.Pid).Select(g => new { Pid = g.Key, Count = g.Count(), Perception = g.Select(c => c.Perception).Distinct().ToArray(),
                Rotations = g.GroupBy(c => c.Rotation).OrderBy(r => r.Key).Select(r => $"{r.Key}x{r.Count()}").ToArray() })
        };
    }
}
