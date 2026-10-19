using System;
using System.Collections.Generic;
using System.Linq;
using FalloutRemake.Geometry;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public sealed record ItemOriginLeg(int Map, int Elevation, int[] Tiles, ActorLocation? Destination);
public sealed record ItemOriginPlan(ItemOriginLeg[] Legs)
{
    public int WalkingSteps => Legs.Sum(l => l.Tiles.Length - 1);
    public int Transitions => Legs.Count(l => l.Destination != null);
}

/// <summary>Read-only route through cached areas and explicit supported exits. Never loads, travels or transfers items.</summary>
public static class ItemOriginRoute
{
    private sealed record Node(ActorLocation Location, ItemOriginLeg[] Legs);
    public static ItemOriginPlan? Find(WorldSession world, CarriedItem requested, bool moving)
    {
        if (moving || !world.Carried.Any(i => i.Map == requested.Map && i.OwnerKey == requested.OwnerKey && i.Source.Key == requested.Source.Key)) return null;
        var areas = world.Areas.ToDictionary(a => a.Map.Prefix.Header.MapIndex);
        if (!areas.TryGetValue(requested.Map, out WorldArea? source)) return null;
        MapObject? owner = source.Items.Containers.SingleOrDefault(o => o.Key == requested.OwnerKey);
        if (owner == null || source.Items.ContainerStatus(owner.Key) != ItemTakeStatus.Ready) return null;
        var navigation = new Dictionary<(int Map, int Elevation), HexNavigation>();
        HexNavigation Nav(int map, int elevation)
        {
            var key = (map, elevation);
            if (!navigation.TryGetValue(key, out HexNavigation? value)) navigation[key] = value = areas[map].Doors.Navigation(elevation);
            return value;
        }
        var targets = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(owner.Header.Tile, d)).Append(owner.Header.Tile)
            .Where(t => t.HasValue && Nav(requested.Map, owner.Header.Elevation).Walkable(t.Value)
                && !Nav(requested.Map, owner.Header.Elevation).IsExit(t.Value)).Select(t => t!.Value).Distinct().ToArray();
        if (targets.Length == 0) return null;
        var queue = new PriorityQueue<Node, (int Cost, int Order)>(); int order = 0, bestCost = int.MaxValue;
        var costs = new Dictionary<(int Map, int Elevation, int Hex), int>(); ItemOriginPlan? best = null;
        static (int, int, int) Key(ActorLocation p) => (p.Map, p.Elevation, p.Hex);
        queue.Enqueue(new(world.Location, Array.Empty<ItemOriginLeg>()), (0, order++)); costs[Key(world.Location)] = 0;
        while (queue.TryDequeue(out Node? node, out var priority))
        {
            if (priority.Cost >= bestCost) break;
            ActorLocation position = node.Location;
            if (costs[Key(position)] != priority.Cost) continue;
            HexReachability reach = Nav(position.Map, position.Elevation).ReachableFrom(position.Hex);
            if (position.Map == requested.Map && position.Elevation == owner.Header.Elevation)
            {
                int? target = targets.Where(t => reach.Distance(t) >= 0).OrderBy(t => reach.Distance(t)).ThenBy(t => t).Cast<int?>().FirstOrDefault();
                if (target.HasValue && priority.Cost + reach.Distance(target.Value) < bestCost)
                {
                    bestCost = priority.Cost + reach.Distance(target.Value);
                    best = new(node.Legs.Append(new ItemOriginLeg(position.Map, position.Elevation, reach.PathTo(target.Value), null)).ToArray());
                }
            }
            WorldArea area = areas[position.Map];
            foreach (int hex in area.Map.ObjectLayers.SelectMany(l => l.Objects)
                .Where(o => o.Header.Elevation == position.Elevation && o.Header.Pid is >= 0x05000010 and <= 0x05000017)
                .Select(o => o.Header.Tile).Distinct().OrderBy(t => t))
            {
                int steps = reach.Distance(hex); if (steps < 0) continue;
                ExitOption exit = area.Travel.InspectExitAt(position.Elevation, hex);
                if (exit.Status is not (ExitStatus.Ready or ExitStatus.ExternalMap) || exit.Destination is not ActorLocation destination
                    || !areas.TryGetValue(destination.Map, out WorldArea? next) || !next.Travel.CanArrive(destination)) continue;
                // One cost per explicit transition prevents zero-distance cycles; walking steps remain separate in the result.
                int cost = priority.Cost + steps + 1;
                if (cost >= bestCost || costs.TryGetValue(Key(destination), out int previous) && previous <= cost) continue;
                costs[Key(destination)] = cost;
                var leg = new ItemOriginLeg(position.Map, position.Elevation, reach.PathTo(hex), destination);
                queue.Enqueue(new(destination, node.Legs.Append(leg).ToArray()), (cost, order++));
            }
        }
        return best;
    }
}
