using System;
using System.Linq;
using FalloutRemake.Geometry;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public sealed record ArroyoElderRoute(TempleWorldStatus Status,string? ActorKey=null,int? TargetHex=null,int[]? Tiles=null);

/// <summary>Original village NPC identity. Header Id alone is not unique in this MAP.</summary>
public static class ArroyoElderSource
{
    public const string ProgramHash="7B33E32FF41BC24504658C300D3ED8C068D6F1621CAC02F697321EE204A8D27F";
    public static MapObject? Match(MapDocument map)
    {
        if (map.Source is not {Path:"maps/arvillag.map",Size:216200,Sha256:ArroyoRewardProfile.MapHash}
            || map.Prefix.Header.MapIndex!=4) return null;
        var actors=map.ObjectLayers.SelectMany(l=>l.Objects).Where(o=>o.Offset==126588).ToArray();
        if (actors.Length!=1 || actors[0].Key!=ArroyoRewardProfile.MapHash+":126588"
            || actors[0].Header is not {Id:20,Pid:16777229,Fid:16777247,Tile:18915,Elevation:0,Rotation:2,Sid:67108867,ScriptIndex:10}
            || (actors[0].Header.Flags&HexNavigation.Hidden)!=0) return null;
        var bindings=map.ScriptBindings.Where(b=>b.Sid==actors[0].Header.Sid).ToArray();
        return bindings.Length==1 && bindings[0].Index==10
            && bindings[0].Program is {Path:"scripts/ahelder.int",Size:20658,Sha256:ProgramHash} ? actors[0] : null;
    }
}

public sealed partial class WorldSession
{
    // Reads only the current area's source and navigation. Does not load, talk, travel or grant rewards.
    public ArroyoElderRoute InspectArroyoElderApproach(bool moving)
    {
        if (CurrentMap!=4 || Location.Elevation!=0 || ArroyoElderSource.Match(Current.Map) is not { } actor)
            return new(TempleWorldStatus.Unavailable);
        if (moving) return new(TempleWorldStatus.Busy,actor.Key);
        var navigation=Current.Doors.Navigation(0);
        if (FalloutGrid.Distance(Location.Hex,actor.Header.Tile)==1 && !navigation.IsExit(Location.Hex))
            return new(TempleWorldStatus.Ready,actor.Key,Location.Hex,new[] {Location.Hex});
        var reach=navigation.ReachableFrom(Location.Hex);
        int? target=Enumerable.Range(0,6).Select(d=>FalloutGrid.Neighbor(actor.Header.Tile,d))
            .Where(h=>h.HasValue && reach.Distance(h.Value)>=0 && !navigation.IsExit(h.Value))
            .OrderBy(h=>reach.Distance(h!.Value)).ThenBy(h=>h).FirstOrDefault();
        return target.HasValue ? new(TempleWorldStatus.Ready,actor.Key,target,reach.PathTo(target.Value))
            : new(TempleWorldStatus.Blocked,actor.Key);
    }
}
