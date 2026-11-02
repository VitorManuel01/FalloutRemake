using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;

/// <summary>
/// Evidence for CombatAi on original data: data/ai.txt parses as CE aiInit reads it; the packets the temple creatures get
/// (Rat 7 and Scorpion 8 from zclrat/zclscorp map_enter_p_proc, checked by --critter-sight-report, and Dumar 143 from
/// actemvil's map_enter_p_proc, checked here) with the numbers that steer them; Cameron's IN, which decides his called shots.
/// </summary>
internal static class CombatAiReport
{
    public static object Create(GameData data)
    {
        var packets = AiPacketCatalog.Read(data.Read("data/ai.txt").Bytes);
        // actemvil map_enter_p_proc: critter_add_trait(self_obj, TRAIT_OBJECT, OBJECT_TEAM_NUM, 1); critter_add_trait(self_obj, 1, OBJECT_AI_PACKET, 143).
        IntProgram cameron = IntProgram.Read(data.Read("scripts/actemvil.int"));
        IntProcedure enter = cameron.Procedures.Single(p => p.Name == "map_enter_p_proc");
        IntInstruction[] code = cameron.ProcedureInstructions(enter.Index).ToArray();
        bool Trait(int kind, int value) => Enumerable.Range(4, Math.Max(0, code.Length - 4)).Any(i => code[i].Opcode == 0x8102 && code[i - 4].Opcode == 0x80BC
            && code[i - 3].OperandBits == 1 && code[i - 2].OperandBits == kind && code[i - 1].OperandBits == value);
        if (enter.BodyOffset != 10942 || !Trait(6, 1) || !Trait(5, WorldSession.CameronPacket))
            throw new InvalidDataException("actemvil map_enter_p_proc no longer gives Cameron team 1 and AI packet 143.");
        var prototypes = new PrototypeCatalog(new ResourceCatalog(data.Read));
        var dumar = DerivedCombatStats.FromCritter(CritterPrototypeData.Read(prototypes.Get(0x01000112)));
        object Packet(int number, string name)
        {
            AiPacket p = packets.TryGetValue(number, out AiPacket? found) && found.Name == name ? found : throw new InvalidDataException($"ai.txt packet {number} is not [{name}].");
            return new { p.Name, p.Number, p.MaxDistance, p.MinToHit, p.MinHitPoints, HurtTooMuch = ((DamageFlags)p.HurtTooMuch).ToString(), p.CalledFrequency,
                p.SecondaryFrequency, p.Chance, p.Distance, p.AttackWho, p.RunAwayMode, p.AreaAttackMode, p.BestWeapon, p.ChemUse, p.Disposition };
        }
        var rat = packets[7]; var scorpion = packets[8]; var dumarPacket = packets[143];
        if (rat is not { MinHitPoints: 0, MinToHit: 0, MaxDistance: 15, HurtTooMuch: 0x40, Distance: -1 } || scorpion is not { MinHitPoints: 0, MinToHit: 0, MaxDistance: 15, Distance: -1 }
            || dumarPacket is not { MinHitPoints: 10, MinToHit: 10, MaxDistance: 10, CalledFrequency: 10, Distance: -1 } || dumar.Special.Intelligence != 5)
            throw new InvalidDataException("The temple packets or Cameron's IN changed.");
        return new
        {
            Status = "COMBAT_AI_OK",
            PacketsRead = packets.Count,
            Temple = new[] { Packet(7, "Rat"), Packet(8, "Scorpion"), Packet(143, "Dumar") },
            Cameron = new { Intelligence = dumar.Special.Intelligence, dumar.MaxHitPoints, YieldBelow = dumar.MaxHitPoints / 2, dumar.MaxActionPoints,
                CalledShotsPossible = dumar.Special.Intelligence >= 5, FleesBeforeYielding = dumarPacket.MinHitPoints > dumar.MaxHitPoints / 2 },
            OriginalReconstruction = "verified"
        };
    }
}
