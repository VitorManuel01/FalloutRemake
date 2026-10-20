using System.Linq;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

/// <summary>Only initial aitemdor gate 104: map variable 20 is zero, local 0/1/3 initialize to one. No challenge, lockpick or NPC dispatch.</summary>
public static class TempleInitialGateProfile
{
    public const string ProgramHash = "3E8AE39C72E0347330FF967EE7DEE36DBBA31CA27087F26D037A08F5E6C1B9DB";
    public const string ListHash = "05AD814B71D80BD3884D80CA4F903EDD1E85CB9B882C89B096B8CF3CADC702F9";
    public static MapObject? Match(MapDocument map)
    {
        if (map.Source is not { Path: "maps/arcaves.map", Size: 526972, Sha256: TempleBarrierEffectProposal.MapHash }
            || map.Prefix.Header.MapIndex != 3 || map.Prefix.GlobalVariables is not { Length: 21 } vars || vars[20] != 0
            || map.Prefix.LocalVariables.Length != 0) return null;
        MapObject[] objects = map.ObjectLayers.SelectMany(l => l.Objects).Where(o => o.Offset == 462744).ToArray();
        if (objects.Length != 1) return null;
        MapObject gate = objects[0];
        if (gate.Key != TempleBarrierEffectProposal.MapHash + ":462744" || gate.Subtype != 0 || gate.ContainerElevation != 2
            || gate.Inventory.Length != 0 || gate.Header is not { Id: 104, Tile: 19928, Elevation: 2, Pid: 33555366, Fid: 33555377,
                Flags: 0, Frame: 0, Sid: 50331653, ScriptIndex: 749 }
            || !gate.State.TryGetValue("openFlags", out int open) || open != 0) return null;
        var bindings = map.ScriptBindings.Where(b => b.Sid == gate.Header.Sid).ToArray();
        if (bindings.Length != 1 || bindings[0].Index != 749 || bindings[0].Program is not { Path: "scripts/aitemdor.int", Size: 13372, Sha256: ProgramHash }) return null;
        var scripts = map.ActiveScripts.Where(s => s.Sid == gate.Header.Sid).ToArray();
        if (scripts.Length != 1 || scripts[0].ScriptIndex != 749 || scripts[0].OwnerId != 104 || scripts[0].LocalVariablesCount != 0 || scripts[0].LocalVariablesOffset != -1) return null;
        var prototypes = map.Prototypes.Where(p => p.Pid == gate.Header.Pid).ToArray();
        if (prototypes.Length != 1 || prototypes[0] is not { Type: 2, Subtype: 0, ScriptId: -1, Fid: 33555377 }
            || prototypes[0].Resource is not { Path: "proto/scenery/00000935.pro", Size: 49, Sha256: "7239F5F5A8A9A595C104E0E72024598E86DDB0B764BA2820F3A4F2DC689BD2C8" }) return null;
        return map.Dependencies.Any(d => d.Path == "scripts/scripts.lst" && d.Size == 104534 && d.Sha256 == ListHash) ? gate : null;
    }
}
