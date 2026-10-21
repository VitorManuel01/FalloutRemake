using FalloutRemake.Import;

namespace FalloutRemake.Geometry;

public sealed record NatureModel(string Name, float Width, bool Tree = false);
public static class NaturePresentation
{
    // Explicit source PID/FID pairs only. No names-based substitution of benches, monuments or critters.
    public static NatureModel? Resolve(int map, MapObject obj)
    {
        if (map is not (4 or 126) || obj.Subtype != 5 || obj.Inventory.Length != 0
            || obj.Header.Sid != -1 || obj.Header.ScriptIndex != -1) return null;
        return (obj.Header.Pid,obj.Header.Fid) switch {
            (33554506,33554603) or (33554507,33554604) => new("RiverRock1",.72f),
            (33554510,33554605) or (33554522,33554609) or (33554523,33554610) => new("RockS01",1.05f),
            (33554524,33554611) or (33554525,33554612) => new("RiverRock1",.9f),
            (33555377,33555388) or (33555378,33555389) or (33555379,33555390) when map == 126
                => new("BlastedForestBurntTreeStump01",1.25f,true),
            _ => null };
    }
}
