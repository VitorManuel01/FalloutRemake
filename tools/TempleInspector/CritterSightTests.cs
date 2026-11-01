using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class CritterSightTests
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static int Tile(int column, int row) => row * 200 + 199 - column;
    static int Walk(int from, int direction, int steps) { for (int i = 0; i < steps; i++) from = FalloutGrid.Neighbor(from, direction)!.Value; return from; }

    public static void Run(Action<string, Action> check)
    {
        check("CE screen geometry: every hex maps back to itself through the hit mask and each neighbour lies in its own rotation", () =>
        {
            for (int tile = 0; tile < 40000; tile++)
            {
                (int x, int y) = OriginalStraightPath.Screen(tile);
                Assert(OriginalStraightPath.TileAt(x + 16, y + 8) == tile, $"Hex {tile} does not map back from its centre.");
                for (int direction = 0; direction < 6; direction++)
                    if (FalloutGrid.Neighbor(tile, direction) is int next)
                        Assert(OriginalStraightPath.RotationTo(tile, next) == direction, $"Neighbour {direction} of {tile} has another rotation.");
            }
            (int x0, int y0) = OriginalStraightPath.Screen(Tile(0, 0));
            Assert(OriginalStraightPath.TileAt(x0 + 16 - 64, y0 + 8) == -1 && OriginalStraightPath.Screen(Tile(1, 0)) == (x0 + 32, y0), "Grid edge and column step.");
        });
        check("critter sight follows isWithinPerception: PE x 5 in the front cone, PE all around, multihex one closer, PE clamped to 1..10", () =>
        {
            int centre = Tile(100, 100);
            var open = new HexNavigation(Array.Empty<MapObject>(), 0);
            MapObject Ant(int rotation, int flags = 0) => CombatTests.Critter(centre, 0x0100017E, 6, 1, "ant") with
                { Header = CombatTests.Critter(centre, 0x0100017E, 6, 1, "ant").Header with { Rotation = rotation, Flags = flags } };
            Assert(CritterSight.Perception(CritterPrototypeData.Read(CombatTests.AntPrototype())) == 2, "Fixture ant PE 2.");
            // Facing east (1): east and its two neighbouring directions see 10 hexes, the other three only 2.
            for (int direction = 0; direction < 6; direction++)
            {
                bool front = direction is 0 or 1 or 2;
                Assert(CritterSight.Facing(1, centre, Walk(centre, direction, 3)) == front, $"Cone for direction {direction}.");
                Assert(CritterSight.Sees(Ant(1), 2, Walk(centre, direction, front ? 10 : 2), open), $"Seen at range in direction {direction}.");
                Assert(!CritterSight.Sees(Ant(1), 2, Walk(centre, direction, front ? 11 : 3), open), $"Unseen beyond range in direction {direction}.");
            }
            Assert(CritterSight.Sees(Ant(4, HexNavigation.Multihex), 2, Walk(centre, 1, 3), open) && !CritterSight.Sees(Ant(4, HexNavigation.Multihex), 2, Walk(centre, 1, 4), open),
                "A multihex critter stands one hex closer.");
            Assert(!CritterSight.Sees(Ant(1), 2, centre, open), "Its own hex.");
            byte[] Stats(int perception, int bonus)
            {
                var bytes = CombatTests.AntPrototype().RawBytes.ToArray();
                System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(52, 4), perception);
                System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(192, 4), bonus);
                return bytes;
            }
            int Pe(int perception, int bonus) => CritterSight.Perception(CritterPrototypeData.Read(PrototypeCatalog.Read(
                new GameResource("proto/critters/00000382.pro", "synthetic", Stats(perception, bonus)), 0x0100017E)));
            Assert(Pe(0, 0) == 1 && Pe(9, 3) == 10 && Pe(3, 2) == 5, "Base plus bonus, clamped.");
        });
        check("critter sight needs a straight path to the player: walls, scenery, critters and closed doors block; open doors, corpses and hidden objects do not", () =>
        {
            int centre = Tile(100, 100), player = Walk(centre, 1, 6), between = Walk(centre, 1, 3);
            MapObject ant = CombatTests.Critter(centre, 0x0100017E, 6, 1, "ant") with { Header = CombatTests.Critter(centre, 0x0100017E, 6, 1, "ant").Header with { Rotation = 1 } };
            bool Sees(params MapObject[] objects) => CritterSight.Sees(ant, 2, player, new HexNavigation(objects.Append(ant), 0));
            MapObject With(MapObject o, int flags) => o with { Header = o.Header with { Flags = flags } };
            MapObject door = CombatTests.Blocker(between, 2, 0x02000001, "door") with { Subtype = 0 };
            Assert(Sees(), "Clear line.");
            Assert(!Sees(CombatTests.Blocker(between, 3, 0x03000001, "wall")) && !Sees(CombatTests.Blocker(between, 2, 0x02000099, "rock"))
                && !Sees(CombatTests.Critter(between, 0x01000005, 10, 1, "other")) && !Sees(door), "Wall, scenery, another critter and a closed door block.");
            Assert(Sees(With(door, HexNavigation.NoBlock)) && Sees(With(CombatTests.Critter(between, 0x0100017E, 0, 1, "corpse"), HexNavigation.NoBlock))
                && Sees(With(CombatTests.Blocker(between, 3, 0x03000001, "hidden"), HexNavigation.Hidden)) && Sees(CombatTests.Blocker(between, 0, 0x00000007, "item")),
                "Open door, corpse, hidden wall and an item on the floor do not.");
            Assert(Sees(CombatTests.Blocker(Walk(between, 0, 1), 3, 0x03000001, "aside")), "A wall beside the line does not.");
            Assert(OriginalStraightPath.FirstBlocked(centre, player, t => t == between) == between
                && OriginalStraightPath.FirstBlocked(centre, player, t => t == centre) == centre, "The walk tests the start hex, then the hexes in order.");
        });
    }
}
