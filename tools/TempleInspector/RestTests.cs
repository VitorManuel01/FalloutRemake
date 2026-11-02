using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;

/// <summary>Natural healing by resting (CE pipboyRest, _Check4Health, _partyMemberRestingHeal, _critter_can_obj_dude_rest).</summary>
internal static class RestTests
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }

    // Level 2 after the veteran ant's fight: bitten for 4, so 30 of 34 HP; healing rate 1 (EN 5).
    static WorldSession Hurt() => SkillSpendingTests.Leveled(SkillSpendingTests.Map(1000));

    public static void Run(Action<string, Action> check)
    {
        check("rest needs the Pip-Boy (the vault suit, or CE's start option), no fight, no walking and an elevation the map lets rest on", () =>
        {
            var world = Hurt();
            Assert(world.CanRest(false) == RestStatus.NoPipBoy && world.Rest(1, 0, false).Status == RestStatus.NoPipBoy, "No Pip-Boy before the vault suit.");
            world.PipBoyFromStart = true;
            Assert(world.CanRest(false) == RestStatus.Ready && world.CanRest(true) == RestStatus.Busy, "With CE's option: ready, not while walking.");
            world.CanRestOn = (map, elevation) => false;
            Assert(world.CanRest(false) == RestStatus.EnemiesNearby, "can_rest_here=No and a living ant of team 1 on the elevation.");
            world.CanRestOn = null;
            Assert(world.Rest(0, 0, false).Status == RestStatus.InvalidDuration && world.Rest(25, 0, false).Status == RestStatus.InvalidDuration, "Durations of the Pip-Boy.");
            var fight = world.BeginCombat(new CombatEncounter(world.Location, new[] { "map3:other-ant" }, "player"), world.PlayerWeapon(), new CombatTests.Scripted());
            Assert(world.CanRest(false) == RestStatus.InCombat, "Not during a fight.");
            world.AbandonCombat();
        });
        check("CE pipboyRest counts truncated minutes per animation step: 3 hours count 169, so the next hour heals the healing rate once", () =>
        {
            var world = Hurt(); world.PipBoyFromStart = true; world.OpenPipBoy();
            Assert(world.HitPoints == 30 && world.MaxHitPoints == 34 && world.Sheet.HealingRate == 1, "30 of 34 HP, rate 1.");
            long time = world.GameTime;
            RestResult three = world.Rest(3, 0, false);
            Assert(three is { Status: RestStatus.Ready, Minutes: 180, Healed: 0 } && world.GameTime == time + 3 * 36000 && world.HitPoints == 30,
                "3 hours: 13 steps of 13 minutes, 169 < 180, nothing healed; the clock moves 3 hours.");
            RestResult one = world.Rest(1, 0, false);
            Assert(one is { Minutes: 60, Healed: 1 } && world.HitPoints == 31, "One more hour crosses 180: +1 HP.");
            world.OpenPipBoy();
            Assert(world.Rest(0, 30, false) is { Minutes: 30, Healed: 0 }, "Opening the Pip-Boy again starts the count over.");
        });
        check("the Pip-Boy's 'until healed' rests HP / rate x 3 hours, then 3 hours at a time; Faster Healing raises the rate", () =>
        {
            var world = Hurt(); world.PipBoyFromStart = true; world.OpenPipBoy();
            long time = world.GameTime;
            RestResult healed = world.RestUntilHealed(false);
            Assert(healed is { Status: RestStatus.Ready, Minutes: 720, Healed: 4 } && world.HitPoints == 34 && world.GameTime == time + 12 * 36000,
                "4 HP at rate 1: 12 hours, 40 steps of 18 minutes, four heals.");
            Assert(world.RestUntilHealed(false).Status == RestStatus.Healthy, "Nothing to heal.");
            Assert(world.Sheet.WithPerks(new PerkRanks(new[] { new ChosenPerk(Perk.FasterHealing, 3) })).HealingRate == world.Sheet.HealingRate + 2,
                "Faster Healing: +2 per rank (perkAddEffect on STAT_HEALING_RATE).");
        });
        check("maps.txt can_rest_here is read per elevation; absent, every elevation may rest (CE wmMapSlotInit)", () =>
        {
            var text = "[Map 000]\nlookup_name=A\nmap_name=AAA\ncan_rest_here=No,yes,NO\n[Map 001]\nlookup_name=B\nmap_name=BBB\n";
            var catalog = new MapCatalog(new GameResource("data/maps.txt", "synthetic", System.Text.Encoding.Latin1.GetBytes(text)));
            Assert(!catalog.Get(0).CanRest(0) && catalog.Get(0).CanRest(1) && !catalog.Get(0).CanRest(2) && catalog.Get(1).CanRest(2), "Per elevation, default yes.");
            try { _ = new MapCatalog(new GameResource("data/maps.txt", "synthetic", System.Text.Encoding.Latin1.GetBytes(text.Replace("No,yes,NO", "maybe")))); throw new Exception("Bad value accepted."); }
            catch (InvalidDataException) { }
        });
    }
}
