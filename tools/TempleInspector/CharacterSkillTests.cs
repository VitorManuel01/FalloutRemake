using FalloutRemake.Runtime;

internal static class CharacterSkillTests
{
    public static void Run(Action<string, Action> check)
    {
        check("neutral character derives untrained normal-difficulty Lockpick and critical chance from attributes", () =>
        {
            PrototypeCharacter profile = PrototypeCharacter.Neutral;
            Assert(profile.Perception == 5 && profile.Agility == 5 && profile.Luck == 5 && profile.Lockpick == 20 && profile.CriticalChance == 5);
            Assert(new PrototypeCharacter(1, 1, 1).Lockpick == 12 && new PrototypeCharacter(10, 10, 10).Lockpick == 30);
            Reject<ArgumentOutOfRangeException>(() => new PrototypeCharacter(0, 5, 5));
            Reject<ArgumentOutOfRangeException>(() => new PrototypeCharacter(5, 11, 5));
            Reject<ArgumentOutOfRangeException>(() => new PrototypeCharacter(5, 5, -1));
        });
        check("percentile success includes equality and has no invented automatic failure at 100", () =>
        {
            Assert(Roll(50, 0, 0, false, 50).Outcome == SkillRollOutcome.Success);
            Assert(Roll(50, 0, 0, false, 51).Outcome == SkillRollOutcome.Failure);
            Assert(Roll(20, 100, 5, false, 100) is { Outcome: SkillRollOutcome.Success, Margin: 20 });
            Assert(Roll(0, 0, 0, false, 1).Outcome == SkillRollOutcome.Failure);
        });
        check("first-day profile succeeds on all 100 dice and never consumes a critical die", () =>
        {
            for (int die = 1; die <= 100; die++)
            {
                int calls = 0;
                var resolver = new PreviewSkillResolver(PrototypeCharacter.Neutral, () => { calls++; return die; });
                Assert(resolver.Resolve(new LockpickRequest("door", 9, 100, LockpickAction.Unlock)) == SkillRollOutcome.Success);
                Assert(calls == 1 && resolver.LastCheck is { CriticalPercentile: null, SkillValue: 20, Modifier: 100 });
            }
        });
        check("eligible critical failure uses CE margin division and exact percentile boundary", () =>
        {
            Assert(Roll(10, 0, 5, true, 29, 1).Outcome == SkillRollOutcome.CriticalFailure); // -19 => 1.
            Assert(Roll(10, 0, 5, true, 29, 2).Outcome == SkillRollOutcome.Failure);
            Assert(Roll(10, 0, 100, true, 19, 1).Outcome == SkillRollOutcome.Failure); // Luck does not increase failure criticals.
            Assert(Roll(0, -300, 0, true, 100, 40).Outcome == SkillRollOutcome.CriticalFailure);
        });
        check("eligible critical success includes luck and truncates margin before adding it", () =>
        {
            Assert(Roll(50, 0, 5, true, 31, 6).Outcome == SkillRollOutcome.CriticalSuccess); // 19 / 10 + 5 => 6.
            Assert(Roll(50, 0, 5, true, 31, 7).Outcome == SkillRollOutcome.Success);
            Assert(Roll(50, 0, 5, true, 50, 5).Outcome == SkillRollOutcome.CriticalSuccess);
            Assert(Roll(50, 0, 5, false, 50).Outcome == SkillRollOutcome.Success);
        });
        check("invalid skill inputs or percentile values fail before yielding a result", () =>
        {
            Reject<ArgumentOutOfRangeException>(() => Roll(-1, 0, 0, false, 1));
            Reject<ArgumentOutOfRangeException>(() => Roll(301, 0, 0, false, 1));
            Reject<ArgumentOutOfRangeException>(() => Roll(20, 301, 0, false, 1));
            Reject<ArgumentOutOfRangeException>(() => Roll(20, 0, 101, false, 1));
            Reject<InvalidOperationException>(() => Roll(20, 0, 5, false, 0));
            Reject<InvalidOperationException>(() => Roll(20, 0, 5, true, 50, 101));
        });
        check("profile resolver rejects unknown skills without a die and clears only transient check history", () =>
        {
            int calls = 0;
            var resolver = new PreviewSkillResolver(PrototypeCharacter.Neutral, () => { calls++; return 70; });
            Reject<NotSupportedException>(() => resolver.Resolve(new LockpickRequest("door", 11, 0, LockpickAction.Unlock)));
            Assert(calls == 0 && resolver.LastCheck == null);
            resolver.Resolve(new LockpickRequest("door", 9, 100, LockpickAction.Unlock));
            Assert(calls == 1 && resolver.LastCheck!.Percentile == 70);
            resolver.Reset(); Assert(resolver.LastCheck == null && resolver.Character.Lockpick == 20);
        });
    }
    private static SkillCheckResult Roll(int skill, int modifier, int critical, bool enabled, params int[] dice)
    {
        var queue = new Queue<int>(dice);
        SkillCheckResult result = PercentileSkillCheck.Roll(skill, modifier, critical, enabled, () => queue.Dequeue());
        Assert(queue.Count == 0); return result;
    }
    private static void Reject<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception("Invalid character/check input accepted.");
    }
    private static void Assert(bool condition) { if (!condition) throw new Exception("Character skill-check assertion failed."); }
}
