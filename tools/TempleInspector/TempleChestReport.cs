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
            RelatedResources = related, DialogueMessage100 = dialogue.Entries.GetValueOrDefault(100)?.Text,
            RequiredBeforeEnabling = new[] { "map external-variable lifecycle", "scripted dialogue/combat transition", "runtime item ownership beyond original source container", "validated storage/restoration checkpoint format" },
            OriginalReconstruction = "verified"
        };
    }
}
