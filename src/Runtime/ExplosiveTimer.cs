using System;

namespace FalloutRemake.Runtime;

public enum ExplosiveExpiry { Normal, Premature }
public sealed record ExplosiveSchedule(int SelectedSeconds, int DelayTicks, ExplosiveExpiry Expiry);

/// <summary>Confirmed timer/roll branch only. Does not activate, drop, consume or detonate an item.</summary>
public static class ExplosiveTimer
{
    public const int MinimumSeconds = 10, MaximumSeconds = 180, DefaultSeconds = 60, StepSeconds = 10, TicksPerSecond = 10;
    public static ExplosiveSchedule Plan(int seconds, SkillRollOutcome outcome, bool demolitionExpert = false)
    {
        if (seconds < MinimumSeconds || seconds > MaximumSeconds || seconds % StepSeconds != 0)
            throw new ArgumentOutOfRangeException(nameof(seconds), "Timer must be 10..180 seconds in steps of ten.");
        if (!Enum.IsDefined(outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));
        if (demolitionExpert) outcome = SkillRollOutcome.Success;
        int ticks = seconds * TicksPerSecond;
        return outcome switch
        {
            SkillRollOutcome.CriticalFailure => new(seconds, 0, ExplosiveExpiry.Premature),
            SkillRollOutcome.Failure => new(seconds, ticks / 2, ExplosiveExpiry.Premature),
            _ => new(seconds, ticks, ExplosiveExpiry.Normal)
        };
    }
}
