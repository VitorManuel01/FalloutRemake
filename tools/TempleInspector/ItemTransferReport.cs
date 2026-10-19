using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class ItemTransferReport
{
    public static object Create(GameData data)
    {
        MapDocument map = MapDocument.Read(data.Read("maps/arcaves.map"), data.Read); MapLayoutWriter.Verify(map);
        var items = new ItemTransferSession(map);
        var text = new PrototypeTextCatalog(data.Read, data.FindPaths("pro_"));
        string? Name(MapObject obj) => text.Resolve(map.Prototypes.Single(p => p.Pid == obj.Header.Pid))?.Name;
        var doors = DoorSession.FromMap(map, true);
        HexNavigation initial = doors.Navigation(0); HexReachability reach = initial.ReachableFrom(map.Prefix.Header.EnteringTile);
        MapObject first = map.AllObjects.Single(o => o.Offset == 244040);
        if (doors.Toggle(first.Key, 24508, 0, false) != DoorUseStatus.Ready) throw new InvalidDataException("Verified first-door approach unavailable.");
        HexReachability openedReach = doors.Navigation(0).ReachableFrom(map.Prefix.Header.EnteringTile);
        int Distance(MapObject owner, HexReachability r) => Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(owner.Header.Tile, d))
            .Append(owner.Header.Tile).Where(t => t.HasValue && r.Distance(t.Value) >= 0).Select(t => r.Distance(t!.Value)).DefaultIfEmpty(-1).Min();
        MapObject pot = items.Containers.Single(o => o.Offset == 206348);
        ContainerItem powder = items.Contents(pot.Key).Single();
        int target = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(pot.Header.Tile, d)).Where(t => t.HasValue && openedReach.Distance(t.Value) >= 0)
            .OrderBy(t => openedReach.Distance(t!.Value)).ThenBy(t => t).First()!.Value;
        var walker = new HexWalker(doors.Navigation(0), map.Prefix.Header.EnteringTile);
        HexRoute route = walker.WalkTo(target); walker.Advance(1000);
        var arrived = new ActorLocation(3, 0, walker.CurrentHex, 0);
        if (items.Take(pot.Key, powder.Source.Key, 1, arrived, doors.Navigation(0), walker.Moving) != ItemTakeStatus.Ready
            || items.Carried.Single().Source.Header.Id != 106 || items.Contents(pot.Key).Single().Quantity != 0
            || items.Take(pot.Key, powder.Source.Key, 1, arrived, doors.Navigation(0), false) != ItemTakeStatus.Empty)
            throw new InvalidDataException("Real temple transfer failed to conserve the Healing Powder source.");
        var restored = new ItemTransferSession(map); restored.Restore(items.Capture());
        if (restored.Carried.Single().Quantity != 1) throw new InvalidDataException("Real item transfer restore failed.");
        items.Reset(); MapLayoutWriter.Verify(map);
        return new
        {
            Map = map.Source, Status = "Direct-container transfer profile; no source mutation or general scripts", Containers = items.Containers.Select(owner =>
            {
                HexNavigation nav = doors.Navigation(owner.Header.Elevation);
                int? near = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(owner.Header.Tile, d))
                    .Append(owner.Header.Tile).FirstOrDefault(t => t.HasValue && nav.Walkable(t.Value) && !nav.IsExit(t.Value));
                var actor = new ActorLocation(3, owner.Header.Elevation, near ?? 0, 0);
                return new
                {
                    owner.Key, owner.Header.Id, owner.Header.Pid, owner.Header.Tile, owner.Header.Elevation, Name = Name(owner),
                    BaseWeight = map.Prototypes.Single(p => p.Pid == owner.Header.Pid).BaseWeight,
                    InitialApproachSteps = owner.Header.Elevation == 0 ? Distance(owner, reach) : -1,
                    FirstDoorOpenApproachSteps = owner.Header.Elevation == 0 ? Distance(owner, openedReach) : -1,
                    Items = items.Contents(owner.Key).Select(line => new { line.Source.Key, line.Source.Header.Id, line.Source.Header.Pid,
                        Name = Name(line.Source), line.Quantity, BaseWeight = map.Prototypes.Single(p => p.Pid == line.Source.Header.Pid).BaseWeight,
                        AtReachStatus = items.CanTake(owner.Key, line.Source.Key, line.Quantity, actor, nav, false).ToString() })
                };
            }).ToArray(), ScriptedContainers = items.Containers.Count(o => o.Header.Sid >= 0 || o.Header.ScriptIndex >= 0),
            RealTransfer = new { OwnerId = pot.Header.Id, ItemId = powder.Source.Header.Id, powder.Source.Header.Pid,
                Steps = route.Tiles.Length - 1, RestoredQuantity = restored.Carried.Single().Quantity, RepeatedTake = "Empty" },
            OriginalReconstruction = "verified"
        };
    }
}
