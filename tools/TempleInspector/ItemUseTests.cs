using System.Text.Json;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class ItemUseTests
{
    internal static ItemUseAllocation Use(int number = 1) => new(new Guid(number, 0, 0, new byte[8]), "owner", "item", 1, ItemUsePhase.Placed,
        new(3, 0, 20101, 0), ExplosiveTimer.Plan(60, SkillRollOutcome.Success), 600);
    private static ItemTransferSession Session(int taken = 2)
    { var session = new ItemTransferSession(ExplosiveProposalTests.Fixture()); session.Restore(new[] { new TakenItem("owner", "item", taken) }); return session; }
    private static string State(ItemTransferSession session) => JsonSerializer.Serialize(new { Taken = session.Capture(), Uses = session.CaptureUses(), session.Revision });
    private static void Conserved(ItemTransferSession session)
    { var b = session.Balance("owner", "item")!; Assert(b.Original == b.Remaining + b.Carried + b.Placed + b.Consumed && b.Remaining >= 0 && b.Carried >= 0); }
    public static void Run(Action<string, Action> check)
    {
        check("placed and consumed units leave the bag without returning to their original container", () =>
        {
            var s = Session(); var map = ExplosiveProposalTests.Fixture(); var actor = new ActorLocation(3, 0, FalloutGrid.Neighbor(20500, 0)!.Value, 0); var nav = HexNavigation.FromMap(map, 0);
            Assert(s.Place(Use()) == ItemUseStatus.Ready && s.Carried.Single().Quantity == 1 && s.Balance("owner", "item") == new ItemBalance(5, 3, 1, 1, 0)); Conserved(s);
            Assert(s.Consume(Use().Id) == ItemUseStatus.Ready && s.Balance("owner", "item") == new ItemBalance(5, 3, 1, 0, 1)); Conserved(s);
            Assert(s.Return("owner", "item", 2, actor, nav, false) == ItemTakeStatus.InvalidQuantity);
            Assert(s.Return("owner", "item", 1, actor, nav, false) == ItemTakeStatus.Ready && s.Carried.Length == 0 && s.Balance("owner", "item") == new ItemBalance(5, 4, 0, 0, 1)); Conserved(s);
            Assert(s.Return("owner", "item", 1, actor, nav, false) == ItemTakeStatus.Empty);
            Assert(s.Take("owner", "item", 4, actor, nav, false) == ItemTakeStatus.Ready && s.Balance("owner", "item") == new ItemBalance(5, 0, 4, 0, 1)); Conserved(s);
            Assert(map.ObjectLayers[0].Objects[0].Inventory[0].Quantity == 5);
        });
        check("allocation identities prevent duplicate placement and consumption without changing failed-operation revision", () =>
        {
            var s = Session(); Assert(s.Place(Use()) == ItemUseStatus.Ready && s.Place(Use(2)) == ItemUseStatus.Ready && s.Carried.Length == 0);
            string before = State(s); Assert(s.Place(Use()) == ItemUseStatus.Duplicate && s.Place(Use(3)) == ItemUseStatus.Empty && s.Consume(Guid.Empty) == ItemUseStatus.NoItem && State(s) == before);
            Assert(s.Consume(Use().Id) == ItemUseStatus.Ready); before = State(s);
            Assert(s.Consume(Use().Id) == ItemUseStatus.Empty && s.Place(Use()) == ItemUseStatus.Duplicate && State(s) == before); Conserved(s);
        });
        check("item use restore preserves mixed ownership and returns independent snapshot arrays", () =>
        {
            var s = Session(3); s.Restore(s.Capture(), new[] { Use(), Use(2) with { Phase = ItemUsePhase.Consumed, RemainingTicks = 0 } });
            Assert(s.Balance("owner", "item") == new ItemBalance(5, 2, 1, 1, 1)); Conserved(s);
            var restored = Session(); restored.Restore(s.Capture(), s.CaptureUses()); Assert(restored.Balance("owner", "item") == s.Balance("owner", "item"));
            var snapshot = restored.CaptureUses(); snapshot[0] = snapshot[0] with { Quantity = 999 }; Assert(restored.CaptureUses().All(u => u.Quantity == 1));
            restored.Reset(); Assert(restored.Carried.Length == 0 && restored.CaptureUses().Length == 0 && restored.Balance("owner", "item") == new ItemBalance(5, 5, 0, 0, 0));
            Assert(s.Balance("foreign", "item") == null && s.Balance("owner", "foreign") == null);
        });
        check("malformed use records are rejected atomically with source, counts and revision unchanged", () =>
        {
            var s = Session(); s.Place(Use()); string before = State(s);
            var invalid = new[] { Use(2) with { Id = Guid.Empty }, Use(2) with { OwnerKey = "foreign" }, Use(2) with { ItemKey = "foreign" },
                Use(2) with { Quantity = 2 }, Use(2) with { Phase = (ItemUsePhase)99 }, Use(2) with { Placement = new(3, 0, -1, 0) },
                Use(2) with { Placement = new(9999, 0, 20101, 0) }, Use(2) with { Placement = new(3, 3, 20101, 0) },
                Use(2) with { RemainingTicks = 601 }, Use(2) with { RemainingTicks = -1 }, Use(2) with { Phase = ItemUsePhase.Consumed },
                Use(2) with { Schedule = new(60, 601, ExplosiveExpiry.Normal) }, Use(2) with { Schedule = new(60, 600, (ExplosiveExpiry)99) },
                Use(2) with { Schedule = null! }, Use(2) with { Placement = null! }, null! };
            foreach (var use in invalid) { Reject(() => s.Restore(new[] { new TakenItem("owner", "item", 3) }, new[] { use })); Assert(State(s) == before); }
            Reject(() => s.Restore(new[] { new TakenItem("owner", "item", 2) }, new[] { Use(), Use() }));
            Reject(() => s.Restore(new[] { new TakenItem("owner", "item", 1) }, new[] { Use(), Use(2) })); Assert(State(s) == before);
            Assert(s.Place(Use(3) with { RemainingTicks = 300 }) == ItemUseStatus.InvalidState && State(s) == before);
        });
        check("uncollected, unsupported and modified source items cannot acquire use allocations", () =>
        {
            var s = new ItemTransferSession(ExplosiveProposalTests.Fixture()); Assert(s.Place(Use()) == ItemUseStatus.Empty);
            s = new ItemTransferSession(ItemTransferTests.Map()); s.Restore(new[] { new TakenItem("owner", "item", 1) }); Assert(s.Place(Use()) == ItemUseStatus.Unsupported && s.Carried.Single().Quantity == 1);
            var map = ExplosiveProposalTests.Fixture(); var p = map.Prototypes[1];
            s = new ItemTransferSession(map with { Prototypes = new[] { map.Prototypes[0], p with { Resource = p.Resource with { Sha256 = "changed" } } } });
            s.Restore(new[] { new TakenItem("owner", "item", 1) }); Assert(s.Place(Use()) == ItemUseStatus.Unsupported && s.CaptureUses().Length == 0);
        });
        check("bounded use allocation count is checked before creating or restoring excess records", () =>
        {
            var map = ExplosiveProposalTests.Fixture(); var owner = map.ObjectLayers[0].Objects[0];
            owner = owner with { Inventory = new[] { owner.Inventory[0] with { Quantity = 2000 } } };
            var s = new ItemTransferSession(map with { ObjectLayers = new[] { new ObjectLayer(0, 0, 1, new[] { owner }) } }); s.Restore(new[] { new TakenItem("owner", "item", 2000) });
            var uses = Enumerable.Range(1, ItemTransferSession.MaxUses).Select(Use).ToArray(); s.Restore(s.Capture(), uses);
            string before = State(s); Assert(s.Place(Use(2000)) == ItemUseStatus.LimitReached && State(s) == before);
            Reject(() => s.Restore(s.Capture(), uses.Append(Use(2000)).ToArray())); Assert(State(s) == before); Conserved(s);
        });
        check("legacy checkpoint schemas cannot accept new use allocations as carried inventory", () =>
        {
            var world = new WorldSession(_ => ExplosiveProposalTests.Fixture(), new[] { 3 }); world.Current.Items.Restore(new[] { new TakenItem("owner", "item", 1) });
            Assert(world.Current.Items.Place(Use()) == ItemUseStatus.Ready);
            var saved = world.Capture("catalog");
            Reject(() => WorldSession.Restore(_ => ExplosiveProposalTests.Fixture(), new[] { 3 }, saved with { Entries = null, Version = 2, Profile = "neutral-v2" }, "catalog"));
            Assert(world.CarriedCount == 0); world.Reset(); Assert(world.Capture("catalog").Version == 9);
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Item-use ownership assertion failed."); }
    private static void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Invalid item-use state accepted."); }
}
