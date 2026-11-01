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
    static readonly (string Path, int Size, string Sha256, int Body, int MapEnter, int Team, int Packet, string PacketName)[] Scripts =
    {
        ("scripts/zclrat.int", 3142, "7BB3F264C24726C02637447D4C73218EE7314E5FA55589499FF289C6F8D94D0A", 2552, 2848, 3, 7, "Rat"),
        ("scripts/zclscorp.int", 3482, "D215E4A5DA87CCBC901CFE078560832B337D4AD71EE1C4991BBEBA7412D0ECD0", 2662, 3030, 4, 8, "Scorpion")
    };

    // data/ai.txt: one [section] per packet, key=value lines.
    static Dictionary<string, Dictionary<string, string>> AiPackets(GameData data)
    {
        var packets = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal); Dictionary<string, string>? current = null;
        foreach (string raw in System.Text.Encoding.Latin1.GetString(data.Read("data/ai.txt").Bytes).Split('\n'))
        {
            string line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']')) packets[line[1..^1]] = current = new(StringComparer.Ordinal);
            else if (current != null && line.IndexOf('=') is int eq and > 0) current[line[..eq].Trim()] = line[(eq + 1)..].Trim();
        }
        return packets;
    }

    public static object Create(GameData data)
    {
        MapDocument map = MapDocument.Read(data.Read("maps/arcaves.map"), data.Read); MapLayoutWriter.Verify(map);
        var prototypes = new PrototypeCatalog(new ResourceCatalog(data.Read));
        var packets = AiPackets(data);
        foreach (var (path, size, sha256, body, mapEnter, team, packet, packetName) in Scripts)
        {
            IntProgram program = IntProgram.Read(data.Read(path));
            IntProcedure? proc = program.Procedures.FirstOrDefault(p => p.Name == "critter_p_proc");
            // push_base; if (self_obj sees dude_obj) attack_complex(dude_obj, 0, 1, 0, 0, 30000, 0, 0)
            (ushort, int?)[] expected = { (0x802B, null), (0xC001, body + 62), (0x80BC, null), (0x80BF, null), (0x80DC, null), (0x802F, null), (0x80BF, null),
                (0xC001, 0), (0xC001, 1), (0xC001, 0), (0xC001, 0), (0xC001, 30000), (0xC001, 0), (0xC001, 0), (0x80D0, null) };
            if (program.Source.Size != size || program.Source.Sha256 != sha256 || proc?.BodyOffset != body
                || !program.ProcedureInstructions(proc.Index).Take(expected.Length).Select(i => (i.Opcode, i.OperandBits)).SequenceEqual(expected))
                throw new InvalidDataException($"{path} critter_p_proc is not the audited sight-then-attack test.");
            // map_enter_p_proc: critter_add_trait(self, TRAIT_OBJECT, OBJECT_TEAM_NUM, team); critter_add_trait(self, 1, OBJECT_AI_PACKET, packet).
            IntProcedure enter = program.Procedures.Single(p => p.Name == "map_enter_p_proc");
            (ushort, int?)[] traits = { (0x802B, null), (0x80BC, null), (0xC001, 1), (0xC001, 6), (0xC001, team), (0x8102, null), (0x801A, null),
                (0x80BC, null), (0xC001, 1), (0xC001, 5), (0xC001, packet), (0x8102, null), (0x801A, null) };
            if (enter.BodyOffset != mapEnter || !program.ProcedureInstructions(enter.Index).Take(traits.Length).Select(i => (i.Opcode, i.OperandBits)).SequenceEqual(traits))
                throw new InvalidDataException($"{path} map_enter_p_proc no longer sets team {team} and AI packet {packet}.");
            // _combat_ai flees only below min_hp (absolute), on hurt_too_much results, or when no hit can reach min_to_hit.
            if (!packets.TryGetValue(packetName, out var ai) || ai.GetValueOrDefault("packet_num") != $"{packet}" || ai.GetValueOrDefault("min_hp") != "0"
                || ai.GetValueOrDefault("min_to_hit") != "0" || ai.GetValueOrDefault("hurt_too_much") != "blind" || ai.ContainsKey("run_away_mode") || ai.ContainsKey("chem_use"))
                throw new InvalidDataException($"AI packet {packetName} is not the audited never-flee profile.");
        }
        var programs = map.ScriptBindings.ToDictionary(b => b.Sid, b => b.Program.Path);
        var critters = map.ObjectLayers.SelectMany(l => l.Objects).Where(o => WorldSession.EncounterPids.Contains(o.Header.Pid))
            .OrderBy(o => o.Header.Elevation).ThenBy(o => o.Offset).Select(o =>
            {
                string script = programs.GetValueOrDefault(o.Header.Sid, "none");
                string expected = o.Header.Pid == 0x0100017E ? "scripts/zclrat.int" : "scripts/zclscorp.int";
                if (script != expected) throw new InvalidDataException($"Encounter critter {o.Offset} runs {script}, not {expected}.");
                var prototype = CritterPrototypeData.Read(prototypes.Get(o.Header.Pid));
                // _ai_check_drugs ("I need DRUGS" flight) returns at once for any body type other than biped (0).
                if (prototype.BodyType == 0) throw new InvalidDataException($"Encounter critter {o.Offset} is a biped.");
                int perception = CritterSight.Perception(prototype);
                return new { o.Offset, Pid = $"0x{o.Header.Pid:X8}", prototype.BodyType, o.Header.Elevation, Hex = o.Header.Tile, o.Header.Rotation, Script = script, Perception = perception,
                    FrontRange = perception * CritterSight.FacingRangePerPerception, AroundRange = perception, Multihex = (o.Header.Flags & HexNavigation.Multihex) != 0 };
            }).ToArray();
        return new
        {
            Rule = "obj_can_see_obj: isWithinPerception (PE x 5 in the front cone of _can_see, PE all around; dude not sneaking) and a straight path whose first blocker is the dude",
            Scripts = Scripts.Select(s => new { s.Path, s.Size, s.Sha256, CritterProcBody = s.Body, MapEnterBody = s.MapEnter, s.Team, AiPacket = s.Packet,
                Packet = new { Name = s.PacketName, MinHp = packets[s.PacketName]["min_hp"], MinToHit = packets[s.PacketName]["min_to_hit"],
                    HurtTooMuch = packets[s.PacketName]["hurt_too_much"], MaxDist = packets[s.PacketName].GetValueOrDefault("max_dist") } }),
            Flight = "Rat and Scorpion never flee: min_hp 0 (HP is never below it), min_to_hit 0, blindness needs a critical hit (none before game day 1), drug flight is biped-only",
            Critters = critters,
            ByPid = critters.GroupBy(c => c.Pid).Select(g => new { Pid = g.Key, Count = g.Count(), Perception = g.Select(c => c.Perception).Distinct().ToArray(),
                Rotations = g.GroupBy(c => c.Rotation).OrderBy(r => r.Key).Select(r => $"{r.Key}x{r.Count()}").ToArray() })
        };
    }
}
