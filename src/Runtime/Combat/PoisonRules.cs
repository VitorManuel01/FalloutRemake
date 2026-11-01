using System;

namespace FalloutRemake.Runtime.Combat;

/// <summary>
/// CE poison on the dude (critter.cc). critterAdjustPoison: a dose loses its share of Poison Resistance (amount - amount * PR / 100);
/// the new level re-queues the poison event 10 * (505 - 5 * level) ticks ahead. poisonEventProcess: the level drops by 2
/// (re-queued the same way, or cured at 0) and the dude loses 1 HP, which can kill. Game time: 10 ticks a second, 1 tick per
/// 100 ms outside combat (_script_chk_timed_events), 5 s (50 ticks) per combat round (_combat_sequence).
/// </summary>
public static class PoisonRules
{
    public const int TicksPerSecond = 10, CombatRoundTicks = 50, DecayPerEvent = 2, DamagePerEvent = 1;

    /// <summary>Ticks until the next poison event at this level; past level 100 CE queues it at once (here: the next tick).</summary>
    public static int DelayTicks(int level) => Math.Max(1, 10 * (505 - 5 * level));

    public static int Absorb(int amount, int resistance) => amount - amount * Math.Clamp(resistance, 0, 100) / 100;

    /// <summary>
    /// zclscorp combat_p_proc after each hit on the dude: do_check(dude_obj, STAT_LUCK, -1) rolls 1..10 against LK - 1;
    /// a failure poisons for random(1, 6).
    /// </summary>
    public const int StingLuckModifier = -1, StingMin = 1, StingMax = 6;
}

/// <summary>The dude's poison: level and ticks until the next event (0 when not poisoned).</summary>
public readonly record struct PoisonState(int Level, int TicksLeft)
{
    public static PoisonState None => new(0, 0);
    public bool Active => Level > 0;
    public bool Valid => Level == 0 ? TicksLeft == 0 : Level > 0 && TicksLeft >= 1 && TicksLeft <= PoisonRules.DelayTicks(Level);

    /// <summary>critterAdjustPoison with a positive dose already reduced by resistance.</summary>
    public PoisonState Add(int absorbed)
    {
        int level = Level + absorbed;
        return level > 0 ? new(level, PoisonRules.DelayTicks(level)) : None;
    }

    /// <summary>
    /// Lets <paramref name="ticks"/> pass; returns the state after and the HP lost to poison events. An event that falls due
    /// is processed once, and the next one is queued from the time it is processed, as the CE queue does after a combat round.
    /// </summary>
    public PoisonState Advance(int ticks, out int hitPointsLost)
    {
        if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
        hitPointsLost = 0;
        PoisonState state = this;
        while (state.Active && ticks > 0)
        {
            if (ticks < state.TicksLeft) return state with { TicksLeft = state.TicksLeft - ticks };
            ticks -= state.TicksLeft;
            int level = state.Level - PoisonRules.DecayPerEvent;
            hitPointsLost += PoisonRules.DamagePerEvent;
            state = level > 0 ? new(level, PoisonRules.DelayTicks(level)) : None;
        }
        return state;
    }

    /// <summary>A combat round: 50 ticks at once, so a due event is processed at the round's end and re-queued from there.</summary>
    public PoisonState AdvanceRound(out int hitPointsLost)
    {
        hitPointsLost = 0;
        if (!Active) return this;
        if (TicksLeft > PoisonRules.CombatRoundTicks) return this with { TicksLeft = TicksLeft - PoisonRules.CombatRoundTicks };
        int level = Level - PoisonRules.DecayPerEvent;
        hitPointsLost = PoisonRules.DamagePerEvent;
        return level > 0 ? new(level, PoisonRules.DelayTicks(level)) : None;
    }
}
