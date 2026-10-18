using System;

namespace FalloutRemake.Runtime;

/// <summary>Explicit neutral test profile: normal difficulty, no training, tags, traits, perks or party.</summary>
public sealed class PrototypeCharacter
{
    public static PrototypeCharacter Neutral { get; } = new(5, 5, 5);
    public int Perception { get; }
    public int Agility { get; }
    public int Luck { get; }
    public int Lockpick => 10 + Perception + Agility;
    public int CriticalChance => Luck;
    public PrototypeCharacter(int perception, int agility, int luck)
    {
        if (perception is < 1 or > 10 || agility is < 1 or > 10 || luck is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(perception), "Test-profile attributes must be 1–10.");
        Perception = perception; Agility = agility; Luck = luck;
    }
}

public sealed record SkillCheckResult(SkillRollOutcome Outcome, int SkillValue, int Modifier,
    int Percentile, int Margin, int? CriticalPercentile);

/// <summary>CE percentile translation, with explicit critical-time eligibility. Does not reproduce its RNG sequence.</summary>
public static class PercentileSkillCheck
{
    public static SkillCheckResult Roll(int skillValue, int modifier, int criticalChance, bool criticalsEnabled, Func<int> nextPercentile)
    {
        ArgumentNullException.ThrowIfNull(nextPercentile);
        if (skillValue is < 0 or > 300 || modifier is < -300 or > 300 || criticalChance is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(skillValue), "Unsupported skill-check inputs.");
        int Die()
        {
            int value = nextPercentile();
            if (value is < 1 or > 100) throw new InvalidOperationException("Percentile source returned a value outside 1–100.");
            return value;
        }
        int percentile = Die(), margin = skillValue + modifier - percentile;
        SkillRollOutcome outcome = margin >= 0 ? SkillRollOutcome.Success : SkillRollOutcome.Failure;
        int? critical = criticalsEnabled ? Die() : null;
        if (critical.HasValue)
        {
            if (margin < 0 && critical.Value <= -margin / 10) outcome = SkillRollOutcome.CriticalFailure;
            else if (margin >= 0 && critical.Value <= margin / 10 + criticalChance) outcome = SkillRollOutcome.CriticalSuccess;
        }
        return new SkillCheckResult(outcome, skillValue, modifier, percentile, margin, critical);
    }
}

public sealed class PreviewSkillResolver
{
    private readonly Func<int> _nextPercentile;
    public PrototypeCharacter Character { get; }
    public SkillCheckResult? LastCheck { get; private set; }
    public PreviewSkillResolver(PrototypeCharacter character, Func<int>? nextPercentile = null)
    {
        ArgumentNullException.ThrowIfNull(character);
        Character = character; _nextPercentile = nextPercentile ?? (() => Random.Shared.Next(1, 101));
    }
    public SkillRollOutcome Resolve(LockpickRequest request)
    {
        if (request.Skill != 9) throw new NotSupportedException("The preview profile supports Lockpick only.");
        // The prototype has no running game clock: first day, default CE critical-time limit.
        SkillCheckResult check = PercentileSkillCheck.Roll(Character.Lockpick, request.Modifier,
            Character.CriticalChance, false, _nextPercentile);
        LastCheck = check; return check.Outcome;
    }
    public void Reset() => LastCheck = null;
}
