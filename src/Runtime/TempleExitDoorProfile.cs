using System;
using System.IO;
using System.Linq;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

/// <summary>
/// aitemdor instance 100 only; map_update unlocks it at M20=2 and G369=0. Killing Cameron writes G369=1, which keeps it
/// locked; then use_obj_on_p_proc with the Temple Key (PID 438, carried by Cameron) sets L0=1 and L7=1 and unlocks the door,
/// and map_update keeps it unlocked. The same script runs the challenge gate, so the key unlocks that one too.
/// </summary>
public sealed class TempleExitDoorProfile
{
    public const int TempleKeyPid = 438;
    public string Key { get; }
    public GameGlobalEntry InitialGlobal { get; }
    internal TempleExitDoorProfile(string key, GameGlobalEntry initialGlobal) { Key = key; InitialGlobal = initialGlobal; }
    public static MapObject? Match(MapDocument map)
    {
        if (TempleInitialGateProfile.Match(map) == null) return null;
        var objects = map.ObjectLayers.SelectMany(l => l.Objects).Where(o => o.Offset == 423748).ToArray();
        if (objects.Length != 1) return null;
        var door = objects[0];
        if (door.Key != map.Source.Sha256 + ":423748" || door.Subtype != 0 || door.ContainerElevation != 2 || door.Inventory.Length != 0
            || door.Header is not { Id: 100, Tile: 13528, Elevation: 2, Pid: 33555366, Fid: 33555377, Flags: 32768,
                Frame: 0, Sid: 50331651, ScriptIndex: 749 } || !door.State.TryGetValue("openFlags", out int open) || open != 0) return null;
        var binding = map.ScriptBindings.Where(b => b.Sid == door.Header.Sid).ToArray();
        var scripts = map.ActiveScripts.Where(s => s.Sid == door.Header.Sid).ToArray();
        return binding.Length == 1 && binding[0].Index == 749
            && binding[0].Program is { Path: "scripts/aitemdor.int", Size: 13372, Sha256: TempleInitialGateProfile.ProgramHash }
            && scripts.Length == 1 && scripts[0].ScriptIndex == 749 && scripts[0].OwnerId == 100
            && scripts[0].LocalVariablesCount == 0 && scripts[0].LocalVariablesOffset == -1 ? door : null;
    }
    public static TempleExitDoorProfile Create(MapDocument map, Func<string, GameResource> read)
    {
        var door = Match(map) ?? throw new InvalidDataException("Unknown Temple exit door instance.");
        var program = IntProgram.Read(read("scripts/aitemdor.int"));
        var metadata = ScriptListMetadata.Read(read("scripts/scripts.lst"), 749);
        var globals = GameGlobalFile.Read(read("data/vault13.gam"));
        if (program.Source.Sha256 != TempleInitialGateProfile.ProgramHash || metadata.Source.Sha256 != TempleInitialGateProfile.ListHash
            || metadata.ProgramPath != "scripts/aitemdor.int" || metadata.DeclaredLocals != 9
            || globals.Source.Sha256 != TempleSpeechProfile.GlobalHash || globals.Entries.Count <= 369 || globals.Entries[369].Value != 0)
            throw new InvalidDataException("Unsupported Temple exit door program or initial global 369.");
        (ushort, int?)[] expected = { (0x80BC,null),(0x80D4,null),(0xC001,13528),(0x8033,null),(0xC001,20),(0x80C3,null),
            (0xC001,2),(0x8033,null),(0x803E,null),(0xC001,369),(0x80C5,null),(0xC001,0),(0x8033,null),(0x803E,null),
            (0x802F,null),(0xC001,0),(0xC001,1),(0x80C2,null),(0xC001,6012),(0xC001,0),(0x80C1,null),(0xC001,0),
            (0x8033,null),(0x802F,null),(0x80BC,null),(0x812E,null),(0xC001,6016),(0x8004,null),(0x80BC,null),(0x812F,null) };
        if (!program.Instructions.Where(i => i.Offset >= 5912).Take(expected.Length).Select(i => (i.Opcode,i.OperandBits)).SequenceEqual(expected))
            throw new InvalidDataException("Temple exit door unlock instructions changed.");
        // use_obj_on_p_proc: obj_pid(obj_being_used_with) == 438 -> L0 = 1, obj_unlock(self), L7 = 1.
        (ushort, int?)[] key = { (0xC001,4692),(0xC001,0),(0x8032,null),(0xC001,TempleKeyPid),(0x8033,null),(0x802F,null),(0x80B9,null),(0xC001,13),
            (0xC001,400),(0x8105,null),(0x80B8,null),(0xC001,0),(0xC001,1),(0x80C2,null),(0x80BC,null),(0x812F,null),(0xC001,7),(0xC001,1),(0x80C2,null) };
        // map_update_p_proc: at hex 13528 with M20 != 2 and L7 == 0 -> L0 = 0; L7 = 1 keeps a key-unlocked door open to use.
        (ushort, int?)[] kept = { (0xC001,5906),(0x80BC,null),(0x80D4,null),(0xC001,13528),(0x8033,null),(0xC001,20),(0x80C3,null),(0xC001,2),
            (0x8034,null),(0x803E,null),(0xC001,7),(0x80C1,null),(0xC001,0),(0x8033,null),(0x803E,null),(0x802F,null),(0xC001,0),(0xC001,0),(0x80C2,null) };
        if (!program.Instructions.Where(i => i.Offset >= 4618).Take(key.Length).Select(i => (i.Opcode,i.OperandBits)).SequenceEqual(key)
            || !program.Instructions.Where(i => i.Offset >= 5828).Take(kept.Length).Select(i => (i.Opcode,i.OperandBits)).SequenceEqual(kept))
            throw new InvalidDataException("Temple Key door instructions changed.");
        return new(door.Key, globals.Entries[369]);
    }
}
