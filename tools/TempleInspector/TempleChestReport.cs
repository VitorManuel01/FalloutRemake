using FalloutRemake.Import;

internal static class TempleChestReport
{
    public static object Create(GameData data)
    {
        MapDocument map = MapDocument.Read(data.Read("maps/arcaves.map"), data.Read); MapLayoutWriter.Verify(map);
        if (map.Source.Sha256 != "BA35474B8EA67AEC50B6A962A1BC767A87366449D4B631FD1B4CE241847B5BFE")
            throw new InvalidDataException("Chest analysis requires the verified temple source profile.");
        MapObject chest = map.AllObjects.Single(o => o.Offset == 465604 && o.Header.Id == 100 && o.Header.Tile == 20132 && o.Header.Elevation == 2);
        var binding = map.ScriptBindings.Single(b => b.Sid == chest.Header.Sid && b.Index == 758);
        IntProgram publisher = IntProgram.Read(data.Read(binding.Program.Path));
        if (publisher.Source.Sha256 != "D80BC3F24D0936D43CA030ED4A24332B4C34CB655982BEFC64402813E451963C")
            throw new InvalidDataException("Chest script differs from the inspected program.");
        IntExternalReference store = IntExternalReferences.Inspect(publisher).Single(r => r.Identifier == "temple_chest_ptr" && r.Operation == "store" && r.ValueSource == "self_obj");
        ScriptBinding consumerBinding = map.ScriptBindings.Single(b => b.Program.Path == "scripts/actemvil.int");
        IntProgram consumer = IntProgram.Read(data.Read(consumerBinding.Program.Path));
        if (consumer.Source.Sha256 != "D865DF15AA53995F69D9A211034622E34A5D9D9AC10E406AEBBCE6E3B19D61F5")
            throw new InvalidDataException("Consumer script differs from the inspected program.");
        IntExternalReference fetch = IntExternalReferences.Inspect(consumer).Single(r => r.Identifier == store.Identifier && r.Operation == "fetch");
        IntInstruction[] sequence = consumer.Instructions.Where(i => i.Offset >= 13346 && i.Offset <= 13356).ToArray();
        if (sequence.Length != 4 || sequence[0].Opcode != 0x80BF || sequence[1].Opcode != 0x9001
            || sequence[2].Offset != fetch.Offset || sequence[2].Opcode != 0x8014 || sequence[3].Opcode != 0x8147)
            throw new InvalidDataException("Expected player-inventory transfer sequence changed.");
        MapObject actor = map.AllObjects.Single(o => o.Header.Sid == consumerBinding.Sid);
        var text = new PrototypeTextCatalog(data.Read, data.FindPaths("pro_"));
        string[] related = data.FindPaths("actemvil").ToArray();
        string dialoguePath = related.Single(p => p.EndsWith("/actemvil.msg", StringComparison.OrdinalIgnoreCase));
        MessageTextFile dialogue = MessageTextFile.Read(data.Read(dialoguePath));
        // Count decoded instructions once: procedure aliases can share a physical body.
        int[] moves = consumer.Instructions.Where(i => i.Opcode == 0x8147).Select(i => i.Offset).ToArray();
        if (!moves.SequenceEqual(new[] { 13356 }) || publisher.Instructions.Any(i => i.Opcode == 0x8147))
            throw new InvalidDataException("Inspected whole-inventory transfer census changed.");
        var options = new List<object>();
        foreach (var site in new[] { ("Node006", 12966, 134), ("Node006", 13032, 136), ("Node008", 13214, 141), ("Node008", 13280, 143) })
        {
            IntProcedure procedure = consumer.Procedures.Single(p => p.Name == site.Item1);
            // giq_option pops reaction, procedure, message and message-list after IQ.
            // These literal arguments identify a possible target, not arrival at this node.
            Expect(consumer, (site.Item2 - 24, 0xC001, 749), (site.Item2 - 18, 0xC001, site.Item3),
                (site.Item2 - 12, 0xC001, 28), (site.Item2 - 6, 0xC001, 50), (site.Item2, 0x8121, null));
            if (!consumer.ProcedureInstructions(procedure.Index).Any(i => i.Offset == site.Item2)
                || consumer.Procedures[28].Name != "Node009") throw new InvalidDataException("Dialogue target changed.");
            options.Add(new { Procedure = site.Item1, Offset = site.Item2, MessageList = 749, Message = site.Item3,
                TargetIndex = 28, Target = "Node009", ReachabilityProven = false });
        }
        // The object closed and locked here is a tile/PID lookup, not temple_chest_ptr.
        Expect(consumer, (13358, 0xC001, 19928), (13364, 0xC001, 2), (13370, 0xC001, 33555366),
            (13376, 0x80A7, null), (13378, 0xC001, 0), (13384, 0x8031, null),
            (13386, 0xC001, 0), (13392, 0x8032, null), (13394, 0x8132, null),
            (13396, 0xC001, 0), (13402, 0x8032, null), (13404, 0x812E, null));
        return new
        {
            Status = "Verified source identities and physical reference sequence; no scripts executed", Map = map.Source,
            Chest = new { chest.Key, chest.Offset, chest.Header.Id, chest.Header.Pid, chest.Header.Tile, chest.Header.Elevation,
                SourceInventoryEntries = chest.Inventory.Length, Binding = binding, Program = publisher.Source },
            Publisher = new { Procedure = "map_update_p_proc", Reference = store },
            Consumer = new { actor.Key, actor.Header.Id, PrototypeName = text.Resolve(map.Prototypes.Single(p => p.Pid == actor.Header.Pid))?.Name,
                Program = consumer.Source, Procedure = "Node009", Reference = fetch, InventoryMoveOffset = 13356,
                Operation = "dude_obj -> fetched temple_chest_ptr -> move_obj_inven_to_obj" },
            RuntimeAdapterEnabled = false,
            StaticInventoryFlow = new
            {
                Scope = "Only the two hash-bound programs; decoded physical instructions, not execution or a global script search",
                ConsumerWholeInventoryMoveOffsets = moves, PublisherWholeInventoryMoveCount = 0,
                AutomaticReturnProven = false,
                Recovery = "No reverse whole-inventory move in these programs. Manual recovery and other scripts remain unproven.",
                PossibleDialogueOptions = options,
                SeparateGateOperation = new { Tile = 19928, Elevation = 2, Pid = 33555366,
                    LookupOffset = 13376, CloseOffset = 13394, LockOffset = 13404, IsChest = false },
                PublisherHasUseProcedure = publisher.Procedures.Any(p => p.Name == "use_p_proc"),
                PublisherHasPickupProcedure = publisher.Procedures.Any(p => p.Name == "pickup_p_proc")
            },
            RelatedResources = related, DialogueMessage100 = dialogue.Entries.GetValueOrDefault(100)?.Text,
            RequiredBeforeEnabling = new[] { "map external-variable lifecycle", "scripted dialogue/combat transition", "runtime item ownership beyond original source container", "validated storage/restoration checkpoint format" },
            OriginalReconstruction = "verified"
        };
    }

    private static void Expect(IntProgram program, params (int Offset, ushort Opcode, int? Operand)[] expected)
    {
        foreach (var item in expected)
        {
            IntInstruction? instruction = program.Instructions.SingleOrDefault(i => i.Offset == item.Offset);
            if (instruction == null || instruction.Opcode != item.Opcode || instruction.OperandBits != item.Operand)
                throw new InvalidDataException($"Chest flow evidence changed at byte {item.Offset}.");
        }
    }
}
