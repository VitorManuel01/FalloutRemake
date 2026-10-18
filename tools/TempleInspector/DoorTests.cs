using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class DoorTests
{
    public static void Run(Action<string, Action> check)
    {
        const int tile = 20100;
        int adjacent = FalloutGrid.Neighbor(tile, 2)!.Value;
        check("door opening/closing changes only runtime flags and restores source-independent navigation", () =>
        {
            MapObject source = Door(tile) with { Header = Door(tile).Header with { Flags = 0x4000 },
                State = new Dictionary<string, int> { ["openFlags"] = 0x100 } };
            var session = new DoorSession(new[] { source });
            Assert(!session.Navigation(0).Walkable(tile));
            Assert(session.Toggle(source.Key, adjacent, 0, false) == DoorUseStatus.Ready);
            DoorState opened = session.Get(source.Key)!;
            Assert(opened.IsOpen && opened.Flags == (0x4000 | DoorSession.OpenDoorMask) && opened.OpenFlags == 0x101);
            Assert(session.Navigation(0).Walkable(tile) && session.Navigation(0).DoorFlagDisagreements == 0);
            Assert(session.Toggle(source.Key, adjacent, 0, false) == DoorUseStatus.Ready);
            Assert(!session.Get(source.Key)!.IsOpen && session.Get(source.Key)!.Flags == 0x4000);
            Assert(source.Header.Flags == 0x4000 && source.State["openFlags"] == 0x100 && !session.Navigation(0).Walkable(tile));
        });
        check("door interactions require a stationary adjacent actor on the same elevation", () =>
        {
            MapObject door = Door(tile); var session = new DoorSession(new[] { door });
            Assert(session.Toggle(door.Key, 28906, 0, false) == DoorUseStatus.TooFar);
            Assert(session.Toggle(door.Key, adjacent, 0, true) == DoorUseStatus.Busy);
            Assert(session.Toggle(door.Key, adjacent, 1, false) == DoorUseStatus.Unavailable);
            Assert(session.Toggle(door.Key, -1, 0, false) == DoorUseStatus.Unavailable);
            Assert(session.Toggle("absent", adjacent, 0, false) == DoorUseStatus.NoDoor);
            Assert(!session.Get(door.Key)!.IsOpen);
        });
        check("scripted instance/prototype and locked doors are never bypassed", () =>
        {
            MapObject plain = Door(tile);
            foreach (MapObject scripted in new[] { plain with { Header = plain.Header with { Sid = 123 } },
                plain with { Header = plain.Header with { ScriptIndex = 25 } } })
                Assert(new DoorSession(new[] { scripted }).Toggle(scripted.Key, adjacent, 0, false) == DoorUseStatus.ScriptRequired);
            Assert(new DoorSession(new[] { plain }, new[] { plain.Header.Pid }).Toggle(plain.Key, adjacent, 0, false) == DoorUseStatus.ScriptRequired);
            MapObject locked = plain with { State = new Dictionary<string, int> { ["openFlags"] = 0x02000000 } };
            Assert(new DoorSession(new[] { locked }).Toggle(locked.Key, adjacent, 0, false) == DoorUseStatus.Locked);
        });
        check("open doors cannot close onto the marker or another blocker including multihex footprints", () =>
        {
            MapObject door = Door(tile) with { Header = Door(tile).Header with { Flags = DoorSession.OpenDoorMask },
                State = new Dictionary<string, int> { ["openFlags"] = 1 } };
            var session = new DoorSession(new[] { door });
            Assert(session.Toggle(door.Key, tile, 0, false) == DoorUseStatus.Occupied);
            MapObject blocker = Door(tile) with { Key = "blocker", Subtype = null, Header = door.Header with { Flags = 0, Pid = 0x01000001, Fid = 0x01000001 } };
            Assert(new DoorSession(new[] { door, blocker }).Toggle(door.Key, adjacent, 0, false) == DoorUseStatus.Occupied);
            blocker = blocker with { Header = blocker.Header with { Tile = adjacent, Flags = HexNavigation.Multihex } };
            Assert(new DoorSession(new[] { door, blocker }).Toggle(door.Key, FalloutGrid.Neighbor(tile, 0)!.Value, 0, false) == DoorUseStatus.Occupied);
        });
        check("overlapping IDs use source keys; opening a door never removes a different blocker", () =>
        {
            MapObject door = Door(tile);
            MapObject other = door with { Key = "other-record", Subtype = null, Header = door.Header with { Pid = 0x03000001, Fid = 0x03000001 } };
            var session = new DoorSession(new[] { door, other });
            Assert(session.Toggle(door.Key, adjacent, 0, false) == DoorUseStatus.Ready);
            Assert(!session.Navigation(0).Walkable(tile) && session.Navigation(0).BlockersAt(tile).Single().Key == other.Key);
        });
        check("hidden, multihex, malformed door state and terminal exits refuse use", () =>
        {
            MapObject plain = Door(tile);
            foreach (int flags in new[] { HexNavigation.Hidden, HexNavigation.Multihex })
            {
                MapObject unsupported = plain with { Header = plain.Header with { Flags = flags } };
                Assert(new DoorSession(new[] { unsupported }).Toggle(plain.Key, adjacent, 0, false) == DoorUseStatus.Unavailable);
            }
            MapObject inconsistent = plain with { State = new Dictionary<string, int> { ["openFlags"] = 1 } };
            Assert(new DoorSession(new[] { inconsistent }).Toggle(plain.Key, adjacent, 0, false) == DoorUseStatus.Inconsistent);
            MapObject exit = Door(adjacent) with { Subtype = null, Header = plain.Header with { Tile = adjacent, Pid = 0x05000010, Fid = 0x05000001 } };
            Assert(new DoorSession(new[] { plain, exit }).Toggle(plain.Key, adjacent, 0, false) == DoorUseStatus.AtExit);
        });
        check("session reset restores every original door flag without modifying source records", () =>
        {
            MapObject door = Door(tile); var session = new DoorSession(new[] { door });
            session.Toggle(door.Key, adjacent, 0, false); session.Reset();
            Assert(!session.Get(door.Key)!.IsOpen && !session.Navigation(0).Walkable(tile));
            Assert(session.Get(door.Key)!.Flags == door.Header.Flags && session.Get(door.Key)!.OpenFlags == door.State["openFlags"]);
        });
    }
    private static MapObject Door(int tile) => new($"fixture:{tile}", tile, 92, 92, 0,
        new ObjectHeader(42, tile, 0, 0, 0, 0, 0, 0, 0x02000001, 0, 0, 0x02000001, -1, 0, 0, 0, -1, -1),
        0, 0, 0, 0, new Dictionary<string, int> { ["openFlags"] = 0 }, Array.Empty<InventoryEntry>());
    private static void Assert(bool condition) { if (!condition) throw new Exception("Door interaction assertion failed."); }
}
