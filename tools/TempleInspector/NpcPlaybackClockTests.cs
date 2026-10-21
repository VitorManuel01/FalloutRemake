using FalloutRemake.Runtime;

internal static class NpcPlaybackClockTests
{
    public static void Run(Action<string,Action> check)
    {
        check("NPC playback accumulates small frames without early steps and retains fractional presentation time", () =>
        {
            var clock = new NpcPlaybackClock(); clock.Add(.05); clock.Add(.05); clock.Add(.099);
            Assert(!clock.TakeStep()); clock.Add(.001); Assert(clock.TakeStep() && !clock.TakeStep());
            clock.Add(.27); Assert(clock.TakeStep() && Math.Abs(clock.PendingSeconds - .07) < 1e-9);
        });
        check("NPC playback native ticks stop at the event and retain overshoot for the following phase", () =>
        {
            var clock = new NpcPlaybackClock(); clock.Add(.03); clock.Add(.03); clock.Add(.04);
            Assert(clock.TakeTicks(20) == 1 && clock.TakeTicks(19) == 0);
            clock.Add(2.37); Assert(clock.TakeTicks(19) == 19 && Math.Abs(clock.PendingSeconds - .47) < 1e-9);
            Assert(clock.TakeStep() && clock.TakeStep() && !clock.TakeStep() && Math.Abs(clock.PendingSeconds - .07) < 1e-9);
        });
        check("NPC playback reset discards paused catch-up time without changing any logical snapshot", () =>
        {
            var clock = new NpcPlaybackClock(); clock.Add(100); int steps = 0;
            while (steps < 16 && clock.TakeStep()) steps++;
            Assert(steps == 16 && clock.PendingSeconds > 96); clock.Reset(); Assert(!clock.TakeStep() && clock.TakeTicks(20) == 0);
        });
        check("NPC playback rejects nonfinite negative overflowing time and invalid native event bounds atomically", () =>
        {
            var clock = new NpcPlaybackClock(); clock.Add(.17);
            foreach (double seconds in new[] { -1d,double.NaN,double.PositiveInfinity,double.NegativeInfinity })
            { Reject(() => clock.Add(seconds)); Assert(clock.PendingSeconds == .17); }
            Reject(() => clock.TakeTicks(0)); Reject(() => clock.TakeTicks(21));
            clock.Reset(); clock.Add(double.MaxValue); Reject(() => clock.Add(double.MaxValue)); Assert(clock.PendingSeconds == double.MaxValue);
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("NPC playback clock assertion failed."); }
    private static void Reject(Action action) { try { action(); } catch (ArgumentOutOfRangeException) { return; } throw new Exception("Invalid NPC clock input accepted."); }
}
