using FalloutRemake.Runtime;

internal static class ExplosiveTimerTests
{
    public static void Run(Action<string, Action> check)
    {
        check("explosive timer preserves all eighteen legal selections and converts seconds into game ticks", () =>
        {
            for (int seconds = 10; seconds <= 180; seconds += 10)
            {
                foreach (var outcome in new[] { SkillRollOutcome.Success, SkillRollOutcome.CriticalSuccess })
                    Assert(ExplosiveTimer.Plan(seconds, outcome) == new ExplosiveSchedule(seconds, seconds * 10, ExplosiveExpiry.Normal));
            }
            Assert(ExplosiveTimer.DefaultSeconds == 60 && ExplosiveTimer.Plan(60, SkillRollOutcome.Success).DelayTicks == 600);
        });
        check("ordinary arming failure halves the timer and critical failure expires immediately", () =>
        {
            foreach (int seconds in new[] { 10, 60, 180 })
            {
                Assert(ExplosiveTimer.Plan(seconds, SkillRollOutcome.Failure) == new ExplosiveSchedule(seconds, seconds * 5, ExplosiveExpiry.Premature));
                Assert(ExplosiveTimer.Plan(seconds, SkillRollOutcome.CriticalFailure) == new ExplosiveSchedule(seconds, 0, ExplosiveExpiry.Premature));
            }
        });
        check("demolition expert uses the confirmed success branch without inventing another skill roll", () =>
        {
            foreach (SkillRollOutcome outcome in Enum.GetValues<SkillRollOutcome>())
                Assert(ExplosiveTimer.Plan(60, outcome, true) == new ExplosiveSchedule(60, 600, ExplosiveExpiry.Normal));
        });
        check("explosive scheduling refuses cancelled, fractional-step, out-of-range and unknown roll inputs", () =>
        {
            foreach (int seconds in new[] { int.MinValue, -1, 0, 1, 9, 11, 179, 181, 190, int.MaxValue }) Reject(() => ExplosiveTimer.Plan(seconds, SkillRollOutcome.Success));
            Reject(() => ExplosiveTimer.Plan(60, (SkillRollOutcome)99)); Reject(() => ExplosiveTimer.Plan(60, (SkillRollOutcome)99, true));
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Explosive timer assertion failed."); }
    private static void Reject(Action action) { try { action(); } catch (ArgumentOutOfRangeException) { return; } throw new Exception("Invalid explosive timer accepted."); }
}
