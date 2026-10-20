using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class TempleSpeechAftermathReport
{
    public static object Create(GameData data)
    {
        var map = MapDocument.Read(data.Read("maps/arcaves.map"), data.Read); MapLayoutWriter.Verify(map);
        var profile = TempleSpeechProfile.Create(map, data.Read);
        var actor = map.AllObjects.Single(o => o.Key == profile.ActorKey);
        var gate = map.AllObjects.Single(o => o.Key == profile.GateKey);
        var program = IntProgram.Read(data.Read("scripts/actemvil.int"));
        var mapProgram = IntProgram.Read(data.Read("scripts/arcaves.int"));
        if (mapProgram.Source.Sha256 != "37221604DFD8166E635795FEEF5A72D011F195C9613A9E0DBBCCED7C28DE3CC1"
            || map.MapProgram?.Sha256 != mapProgram.Source.Sha256)
            throw new InvalidDataException("Unknown Temple map program.");
        void Span(int start, params (ushort Op, int? Value)[] pattern)
        {
            var actual = program.Instructions.Where(i => i.Offset >= start).Take(pattern.Length).Select(i => (i.Opcode, i.OperandBits));
            if (!actual.SequenceEqual(pattern)) throw new InvalidDataException($"NPC aftermath pattern changed at {start}.");
        }
        // L13==2 enables the departure block. L14 is the movement phase.
        Span(4396, (0xC001,4882),(0xC001,13),(0x80C1,null),(0xC001,2),(0x8033,null),(0x802F,null));
        Span(4420, (0xC001,4520),(0xC001,14),(0x80C1,null),(0xC001,0),(0x8033,null),
            (0x80BC,null),(0x80D4,null),(0xC001,19728),(0x8034,null),(0x803E,null),(0x802F,null));
        Span(4458, (0xC001,19928),(0xC001,2),(0xC001,33555366),(0x80A7,null),(0xC001,0),(0x8031,null),
            (0xC001,0),(0x8032,null),(0x812F,null),(0x80BC,null),(0xC001,19728),(0xC001,1),(0x80CE,null));
        Span(4520, (0xC001,4636),(0xC001,14),(0x80C1,null),(0xC001,0),(0x8033,null),
            (0x80BC,null),(0x80D4,null),(0xC001,19728),(0x8033,null),(0x803E,null),(0x802F,null));
        Span(4558, (0xC001,14),(0xC001,1),(0x80C2,null),(0xC001,19928),(0xC001,2),(0xC001,33555366),
            (0x80A7,null),(0xC001,0),(0x8031,null),(0xC001,0),(0x8032,null),(0x8131,null),
            (0x80BC,null),(0xC001,2),(0x80F2,null),(0xC001,1),(0x80F0,null));
        Span(11398, (0xC001,11430),(0x80F7,null),(0xC001,1),(0x8033,null),(0x802F,null),
            (0xC001,14),(0xC001,2),(0x80C2,null));
        Span(4636, (0xC001,4698),(0xC001,14),(0x80C1,null),(0xC001,2),(0x8033,null),
            (0x80BC,null),(0x80D4,null),(0xC001,22712),(0x8034,null),(0x803E,null),(0x802F,null),
            (0x80BC,null),(0xC001,22712),(0xC001,1),(0x80CE,null));
        Span(4698, (0xC001,4758),(0xC001,14),(0x80C1,null),(0xC001,2),(0x8033,null),
            (0x80BC,null),(0x80D4,null),(0xC001,22712),(0x8033,null),(0x803E,null),(0x802F,null),
            (0xC001,14),(0xC001,3),(0x80C2,null));
        Span(4758, (0xC001,4820),(0xC001,14),(0x80C1,null),(0xC001,3),(0x8033,null),
            (0x80BC,null),(0x80D4,null),(0xC001,28715),(0x8034,null),(0x803E,null),(0x802F,null),
            (0x80BC,null),(0xC001,28715),(0xC001,1),(0x80CE,null));
        Span(4820, (0xC001,4882),(0xC001,14),(0x80C1,null),(0xC001,3),(0x8033,null),
            (0x80BC,null),(0x80D4,null),(0xC001,28715),(0x8033,null),(0x803E,null),(0x802F,null),
            (0xC001,14),(0xC001,4),(0x80C2,null),(0x80BC,null),(0xC001,1),(0x80E3,null));
        int xpCalls = program.Instructions.Count(i => i.Opcode == 0x80A1) + mapProgram.Instructions.Count(i => i.Opcode == 0x80A1);
        if (xpCalls != 0) throw new InvalidDataException("Direct XP call census changed in actor/map programs.");
        var roots = map.ObjectLayers.SelectMany(l => l.Objects).Where(o => o.Key != actor.Key).ToArray();
        HexNavigation Navigation(bool open, ExitTraversalPolicy policy = ExitTraversalPolicy.StopAtExits) => new(roots, 2, o => o.Key == gate.Key && open
            ? new ObjectTraversalState(o.Header.Flags | DoorSession.OpenDoorMask, o.State["openFlags"] | 1)
            : new ObjectTraversalState(o.Header.Flags, o.State.TryGetValue("openFlags", out int flags) ? flags : null), policy);
        var legs = new[] { (From: 13728, To: 19728, Open: false), (From: 19728, To: 22712, Open: true), (From: 22712, To: 28715, Open: true) }
            .Select(leg => { var navigation = Navigation(leg.Open); var route = navigation.FindRoute(leg.From, leg.To);
                return new { leg.From, leg.To, GateOpen = leg.Open, Status = route.Status.ToString(), Steps = route.Tiles.Length > 0 ? (int?)(route.Tiles.Length - 1) : null,
                    CrossesGate = route.Tiles.Contains(gate.Header.Tile), ContainsExit = route.Tiles.Any(navigation.IsExit) }; }).ToArray();
        var npcNavigation = Navigation(true, ExitTraversalPolicy.ContinueAcrossExits);
        var npcRoute = npcNavigation.FindRoute(22712, 28715);
        var playerNavigation = Navigation(true);
        if (npcRoute.Status != RouteStatus.Ready || npcRoute.Tiles.Length - 1 != 31 || !npcRoute.Tiles.Any(npcNavigation.IsExit)
            || npcNavigation.ExitHexCount != playerNavigation.ExitHexCount || npcNavigation.BlockedHexCount != playerNavigation.BlockedHexCount)
            throw new InvalidDataException("Source-preserving NPC exit traversal differs from inspected geometry.");
        MapLayoutWriter.Verify(map);
        return new { Status = "TEMPLE_SPEECH_AFTERMATH_OK", Map = map.Source, ActorProgram = program.Source, MapProgram = mapProgram.Source,
            actor.Key, OriginalActorHex = actor.Header.Tile,
            Phases = new[] {
                "L13=2/L14=0: unlock final gate, request running movement to 19728",
                "At 19728/L14=0: set L14=1, request gate open, add event parameter 1 after game_ticks(2)",
                "Event parameter 1: set L14=2; no elapsed-time simulation performed",
                "L14=2: request running movement to 22712; at destination set L14=3",
                "L14=3: request running movement to 28715; at destination set L14=4 and hide actor" },
            Timer = new { Seconds = 2, NativeTicks = 20, Parameter = 1, SeparateFromExplosiveRehearsalClock = true },
            GeometryAudit = new { Scope = "Single-hex prototype geometry only, source NPC blocker excluded for its own path; not CE animation/path execution", Legs = legs,
                NpcExitTraversal = new { Scope = "Explicit NPC-only traversal graph preserves exit markers and all blockers; no travel or NPC world mutation",
                    Status = npcRoute.Status.ToString(), Steps = npcRoute.Tiles.Length - 1, CrossesExit = true, PreservedExits = npcNavigation.ExitHexCount,
                    BlockedHexes = npcNavigation.BlockedHexCount } },
            RewardAudit = new { DirectXpCallsInActorAndMapPrograms = xpCalls, Scope = "Does not prove absence of rewards in other programs or on later transitions; no XP amount invented" },
            FinalVisibility = "CE set_obj_visibility(self,1) hides the critter and adds NO_BLOCK; this is not destroy_object",
            RequiredBeforeWorldIntegration = new[] { "typed persisted NPC pose/phase and source blocker relocation", "one-shot timer persistence and event guards", "gate transition and animation failure semantics", "complete world checkpoint rollback", "explicit supported character profile" },
            RuntimeAdapterEnabled = false, OriginalReconstruction = "verified" };
    }
}
