using System;

namespace FalloutRemake.Runtime;

/// <summary>Fractional presentation time only. Whole steps and native ticks remain authoritative in the world snapshot.</summary>
public sealed class NpcPlaybackClock
{
    public const double StepSeconds = .2, TickSeconds = .1;
    private const double Epsilon = 1e-9;
    private double _seconds;
    public double PendingSeconds => _seconds;
    public void Add(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0 || !double.IsFinite(_seconds + seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        _seconds += seconds;
    }
    public bool TakeStep()
    {
        if (_seconds + Epsilon < StepSeconds) return false;
        _seconds = Math.Max(0,_seconds - StepSeconds); return true;
    }
    public int TakeTicks(int remaining)
    {
        if (remaining is < 1 or > TempleNpcDepartureProfile.DelayTicks) throw new ArgumentOutOfRangeException(nameof(remaining));
        int ticks = (int)Math.Min(remaining,Math.Floor((_seconds + Epsilon) / TickSeconds));
        _seconds = Math.Max(0,_seconds - ticks * TickSeconds); return ticks;
    }
    public void Reset() => _seconds = 0;
}
