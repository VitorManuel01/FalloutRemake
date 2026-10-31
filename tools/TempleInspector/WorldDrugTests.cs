using System.Text;
using System.Text.Json.Nodes;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;

internal static class WorldDrugTests
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Reject(Action action, string message) { try { action(); } catch (InvalidDataException) { return; } throw new Exception(message); }

    // WorldCombatTests map plus a pot (away from the ant's path) holding three Healing Powder units whose PRO carries the trusted identity.
    internal static MapDocument Map(string hash = HealingDrugProfile.SourcePrototypeHash)
    {
        MapDocument combat = WorldCombatTests.Map(), items = ItemTransferTests.Map();
        MapObject owner = items.ObjectLayers[0].Objects[0];
        MapObject powder = owner.Inventory[0].Object with { Key = "powder", Subtype = DrugPrototypeData.Subtype, Header = owner.Inventory[0].Object.Header with { Pid = HealingDrugProfile.Pid } };
        owner = owner with { Header = owner.Header with { Tile = 100 * 200 + 199 - 80 }, Inventory = new[] { owner.Inventory[0] with { Quantity = 3, Object = powder } } };
        var prototype = new Prototype(HealingDrugProfile.Pid, 0, DrugPrototypeData.Subtype, 0, -1,
            new("proto/items/00000273.pro", "synthetic trusted-identity fixture", 125, hash), new byte[125]);
        var objects = combat.ObjectLayers[0].Objects.Append(owner).ToArray();
        return combat with { ObjectLayers = new[] { combat.ObjectLayers[0] with { Objects = objects, DeclaredCount = objects.Length } },
            Prototypes = combat.Prototypes.Concat(new[] { items.Prototypes[0], prototype }).ToArray() };
    }
    static WorldSession World(int carried = 2, string hash = HealingDrugProfile.SourcePrototypeHash)
    {
        var map = Map(hash); var world = new WorldSession(_ => map, new[] { 3, 126 });
        if (carried > 0) world.Current.Items.Restore(new[] { new TakenItem("owner", "powder", carried) });
        return world;
    }
    // The near ant walks in (no bite); the player waits two turns (it leads round 2 on Sequence); the ant bites for 4:
    // the player stands at 26/30 leading round 3.
    static CombatSession Hurt(WorldSession world, params int[] rest)
    {
        var fight = world.BeginCombat(world.FindEncounter(world.Location)!, WorldCombatTests.Spear(), new CombatTests.Scripted(new[] { 1, 4 }.Concat(rest).ToArray()));
        ProvisionalMeleeBehavior.TakeTurn(fight); fight.Execute(CombatActionProposal.EndTurn("player")); fight.Execute(CombatActionProposal.EndTurn("player"));
        ProvisionalMeleeBehavior.TakeTurn(fight);
        Assert(fight.Active!.IsPlayer && fight.Get("player").HitPoints == 26, "Hurt setup.");
        return fight;
    }
    // From Hurt: the player ends its turn, the ant misses (99), the spear kills it (1, 11).
    static void Win(CombatSession fight)
    {
        fight.Execute(CombatActionProposal.EndTurn("player")); ProvisionalMeleeBehavior.TakeTurn(fight); fight.Execute(CombatActionProposal.Attack("player", "map3:near-ant"));
        Assert(fight.Turn.Over, "Won.");
    }

    public static void Run(Action<string, Action> check)
    {
        check("Healing Powder in a fight costs 4 AP, rolls 8-18 clamped at the maximum and is used up only when the won fight is applied", () =>
        {
            var world = World(); var fight = Hurt(world, 9, 99, 1, 11);
            var hero = fight.Get("player");
            Assert(hero.HealingDoses == 2 && fight.Preview(CombatActionProposal.UseHealing("player")) is { Status: CombatActionStatus.Ready, ActionPointCost: 4, ActionPointsAfter: 3 }, "Preview.");
            var used = fight.Execute(CombatActionProposal.UseHealing("player")).Events.OfType<HealingUsed>().Single();
            Assert(used is { Rolled: 9, Healed: 4, HitPoints: 30, ActionPointsSpent: 4 } && hero.HitPoints == 30 && hero.HealingDoses == 1 && hero.ActionPoints == 3, "Dose taken.");
            Assert(fight.Preview(CombatActionProposal.UseHealing("player")).Status == CombatActionStatus.NotEnoughActionPoints, "Second dose needs 4 AP.");
            Assert(world.CarriedCount == 2 && world.UseDrug(world.Carried.Single(), new CombatTests.Scripted(), false).Status == DrugUseStatus.InCombat, "World untouched mid-fight.");
            Win(fight);
            Assert(world.CommitCombat(fight) == CombatCommitStatus.Ready && world.CarriedCount == 1 && world.HealingDoses == 1 && world.PlayerHitPoints == null
                && world.Current.Items.Balance("owner", "powder") is { Original: 3, Remaining: 1, Carried: 1, Consumed: 1 }, "Applied.");
            Assert(world.Current.Items.CanReturn("owner", "powder", 2, world.Location, world.Current.Doors.Navigation(0), false) == ItemTakeStatus.InvalidQuantity, "Used units cannot go back.");
        });
        check("a fight whose doses are no longer carried is refused whole, and a player without powder has no dose to take", () =>
        {
            var world = World(1); var fight = Hurt(world, 12, 99, 1, 11);
            fight.Execute(CombatActionProposal.UseHealing("player")); Win(fight);
            world.Current.Items.Restore(Array.Empty<TakenItem>());
            Assert(world.CommitCombat(fight) == CombatCommitStatus.InvalidFight && !world.HasCombatOutcome && world.CombatExperience == 0, "Refused whole.");
            var bare = World(0); var unarmed = Hurt(bare);
            Assert(unarmed.Get("player").HealingDoses == 0 && unarmed.Preview(CombatActionProposal.UseHealing("player")).Status == CombatActionStatus.NoItem, "No powder.");
        });
        check("Healing Powder outside combat heals without AP, wastes the excess and refuses moving, foreign and modified items", () =>
        {
            var world = World(); var fight = Hurt(world, 99, 1, 11); Win(fight);
            Assert(world.CommitCombat(fight) == CombatCommitStatus.Ready && world.HitPoints == 26, "Hurt.");
            CarriedItem powder = world.Carried.Single();
            Assert(world.UseDrug(powder, new CombatTests.Scripted(), true).Status == DrugUseStatus.Busy, "Moving.");
            Assert(world.UseDrug(powder with { Source = powder.Source with { Key = "ghost" } }, new CombatTests.Scripted(), false).Status == DrugUseStatus.NoItem, "Foreign key.");
            Assert(world.UseDrug(powder, new CombatTests.Scripted(18), false) is { Status: DrugUseStatus.Ready, Rolled: 18, Healed: 4, HitPoints: 30 } && world.CarriedCount == 1, "Heal capped.");
            Assert(world.UseDrug(world.Carried.Single(), new CombatTests.Scripted(8), false) is { Status: DrugUseStatus.Ready, Healed: 0 } && world.CarriedCount == 0, "Unhurt use wastes it.");
            Assert(world.UseDrug(powder, new CombatTests.Scripted(), false).Status == DrugUseStatus.NoItem, "Nothing left.");
            var modified = World(hash: new string('0', 64));
            Assert(modified.UseDrug(modified.Carried.Single(), new CombatTests.Scripted(), false).Status == DrugUseStatus.Unsupported && modified.CarriedCount == 2
                && modified.HealingDoses == 0, "Modified PRO identity.");
        });
        check("checkpoint v11 keeps used powder and healed HP, migrates v10 without consumption and refuses forged consumption", () =>
        {
            WorldSession Load(WorldCheckpoint checkpoint) => WorldSession.Restore(_ => Map(), new[] { 3, 126 }, checkpoint, "catalog");
            var world = World(); var fight = Hurt(world, 99, 1, 11); Win(fight);
            world.CommitCombat(fight); world.UseDrug(world.Carried.Single(), new CombatTests.Scripted(8), false);
            WorldCheckpoint saved = CheckpointFile.Decode(CheckpointFile.Encode(world.Capture("catalog")));
            ConsumedItem eaten = saved.Areas.Single(a => a.Map == 3).Consumed!.Single();
            Assert(saved.Version == 11 && saved.Profile == "neutral-v11" && eaten == new ConsumedItem("owner", "powder", 1) && saved.Combat!.PlayerHitPoints == null, "Captured.");
            var restored = Load(saved);
            Assert(restored.CarriedCount == 1 && restored.HitPoints == 30 && restored.Current.Items.Balance("owner", "powder") is { Consumed: 1, Remaining: 1 }, "Restored.");
            var legacy = Load(saved with { Version = 10, Profile = "neutral-v10", Areas = saved.Areas.Select(a => a with { Consumed = null }).ToArray() });
            Assert(legacy.CarriedCount == 2 && legacy.Capture("catalog").Version == 11, "v10 migrates without consumption.");
            WorldCheckpoint With(params ConsumedItem[] rows) => saved with { Areas = saved.Areas.Select(a => a.Map == 3 ? a with { Consumed = rows } : a).ToArray() };
            var forged = new[]
            {
                With(eaten with { Quantity = 3 }), With(eaten with { Quantity = 0 }), With(eaten, eaten), With(eaten with { ItemKey = "ghost" }),
                With(eaten with { OwnerKey = "map3:exit" }),
                saved with { Areas = saved.Areas.Select(a => a with { Consumed = null }).ToArray() },
                saved with { Version = 10, Profile = "neutral-v10" }
            };
            foreach (WorldCheckpoint bad in forged) Reject(() => Load(bad), "Forged consumption accepted.");
            Reject(() => WorldSession.Restore(_ => Map(new string('0', 64)), new[] { 3, 126 }, saved, "catalog"), "Consumption of a modified PRO accepted.");
            var node = JsonNode.Parse(CheckpointFile.Encode(saved))!; node["Areas"]![0]!.AsObject().Remove("Consumed");
            Reject(() => Load(CheckpointFile.Decode(Encoding.UTF8.GetBytes(node.ToJsonString()))), "v11 area without Consumed accepted.");
        });
    }
}
