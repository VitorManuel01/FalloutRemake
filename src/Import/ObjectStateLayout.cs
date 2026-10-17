using System.Collections.Generic;

namespace FalloutRemake.Import;

internal static class ObjectStateLayout
{
    public static IEnumerable<string> Names(int pid, int? subtype)
    {
        if ((pid >> 24) == 1)
        {
            foreach (string name in new[] { "unknown0", "damageLastTurn", "maneuver", "ap", "results", "aiPacket", "team", "whoHitMeCid", "hp", "radiation", "poison" }) yield return name;
            yield break;
        }
        yield return "flags";
        string[] tail = (pid >> 24, subtype) switch
        {
            (0, 3) => new[] { "ammoQuantity", "ammoTypePid" },
            (0, 4) => new[] { "ammoQuantity" },
            (0, 5) => new[] { "charges" },
            (0, 6) => new[] { "keyCode" },
            (2, 0) => new[] { "openFlags" },
            (2, 1) => new[] { "destinationBuiltTile", "destinationMap" },
            (2, 2) => new[] { "elevatorType", "elevatorLevel" },
            (2, 3) or (2, 4) => new[] { "destinationMap", "destinationBuiltTile" },
            _ when pid >= 0x05000010 && pid <= 0x05000017 => new[] { "destinationMap", "destinationTile", "destinationElevation", "destinationRotation" },
            _ => System.Array.Empty<string>()
        };
        foreach (string name in tail) yield return name;
    }
}
