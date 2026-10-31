using System.Collections.Generic;

namespace FalloutRemake.Runtime.Combat;

/// <summary>
/// Contract frozen before the Godot slice (roadmap ARCH-001): pure C#, no Godot types. Preview and Execute share one
/// rule path, so what the HUD promises is what the dice resolve. Refused actions never change any state.
/// </summary>
public interface ICombatSession
{
    CombatTurnState Turn { get; }
    IReadOnlyList<CombatantState> Combatants { get; }
    IReadOnlyList<CombatEvent> Log { get; }
    CombatantState Get(string id);
    CombatActionPreview Preview(CombatActionProposal proposal);
    CombatActionResult Execute(CombatActionProposal proposal);
}

/// <summary>Uniform integer source. Sessions own one so a seed reproduces a whole fight.</summary>
public interface ICombatRandom
{
    int Between(int min, int max);
}

/// <summary>SplitMix64: deterministic, platform-independent and cheap to persist later (one 64-bit state).</summary>
public sealed class SeededCombatRandom : ICombatRandom
{
    public ulong State { get; private set; }
    public SeededCombatRandom(ulong seed) => State = seed;
    public int Between(int min, int max)
    {
        if (max < min) throw new System.ArgumentOutOfRangeException(nameof(max));
        ulong z = State += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        z ^= z >> 31;
        return min + (int)(z % (ulong)((long)max - min + 1));
    }
}
