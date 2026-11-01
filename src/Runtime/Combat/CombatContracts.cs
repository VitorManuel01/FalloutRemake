using System;

namespace FalloutRemake.Runtime.Combat;

/// <summary>CE damage-type order, shared by weapon PRO damageType and critter DT/DR stat blocks.</summary>
public enum DamageType { Normal, Laser, Fire, Plasma, Electrical, Emp, Explosion }
public enum LifeStatus { Active, Dead }
public enum CombatActionKind { Move, Attack, EndTurn, UseHealing, LeaveCombat }
public enum CombatActionStatus
{
    Ready, CombatOver, NotActive, UnknownCombatant, InvalidTarget, AlreadyThere, Unreachable,
    NotEnoughActionPoints, OutOfRange, OtherElevation, NoItem, EnemiesAware
}

/// <summary>What a combatant wants to do; previewed and executed through the same rules.</summary>
public sealed record CombatActionProposal(string ActorId, CombatActionKind Kind, int? TargetHex = null, string? TargetId = null)
{
    public static CombatActionProposal Move(string actor, int hex) => new(actor, CombatActionKind.Move, TargetHex: hex);
    public static CombatActionProposal Attack(string actor, string target) => new(actor, CombatActionKind.Attack, TargetId: target);
    public static CombatActionProposal EndTurn(string actor) => new(actor, CombatActionKind.EndTurn);
    /// <summary>Takes one carried healing dose (Healing Powder) through the inventory.</summary>
    public static CombatActionProposal UseHealing(string actor) => new(actor, CombatActionKind.UseHealing);
    /// <summary>The player's Enter (CE combatAttemptEnd): ends the fight when no enemy still perceives the player.</summary>
    public static CombatActionProposal LeaveCombat(string actor) => new(actor, CombatActionKind.LeaveCombat);
}

/// <summary>Every term of the hit chance. Raw is the CE value; Chance is the project's 5–95 clamp (roadmap COM-003).</summary>
public sealed record ToHitBreakdown(int Skill, int SkillIndex, int DistanceModifier, int StrengthPenalty, int TargetArmorClass, int Raw, int Chance);

/// <summary>Damage range of one hit after the target's threshold and resistance, using the exact resolution arithmetic.</summary>
public sealed record DamageEstimate(DamageType Type, int MinRoll, int MaxRoll, int Threshold, int Resistance, int MinDamage, int MaxDamage);

public sealed record CombatActionPreview(CombatActionStatus Status, int ActionPointCost, int ActionPointsAfter,
    int[] Path, ToHitBreakdown? ToHit, DamageEstimate? Damage)
{
    public static CombatActionPreview Refused(CombatActionStatus status, int actionPoints) =>
        new(status, 0, actionPoints, Array.Empty<int>(), null, null);
}

public abstract record CombatEvent;
public sealed record CombatStarted(string[] Order) : CombatEvent;
public sealed record TurnStarted(int Round, string CombatantId, int ActionPoints) : CombatEvent;
public sealed record CombatantMoved(string CombatantId, int From, int To, int[] Path, int ActionPointsSpent) : CombatEvent;
public sealed record AttackResolved(string AttackerId, string TargetId, int Chance, int Roll, bool Hit, int DamageRoll, int Damage,
    int TargetHitPoints, int ActionPointsSpent) : CombatEvent;
/// <summary>A healing dose taken: the rolled amount, the HP actually restored (capped at the maximum) and the HP after.</summary>
public sealed record HealingUsed(string CombatantId, int Rolled, int Healed, int HitPoints, int ActionPointsSpent) : CombatEvent;
/// <summary>A combatant that yields at the start of its turn (Cameron's combat_p_proc): it leaves the fight alive, awarding no XP.</summary>
public sealed record CombatantYielded(string CombatantId, int HitPoints) : CombatEvent;
public sealed record CombatantDied(string CombatantId, string KillerId, int Experience) : CombatEvent;
/// <summary>A radscorpion's hit on the player: Luck roll against LK - 1; on a failure, the dose rolled, what resistance let through and the new level.</summary>
public sealed record PoisonChecked(string AttackerId, string TargetId, int LuckRoll, int Needed, bool Poisoned, int Rolled, int Absorbed, int Level) : CombatEvent;
/// <summary>A poison event at a round's end: HP lost and the HP and level after.</summary>
public sealed record PoisonDamage(string CombatantId, int Lost, int HitPoints, int Level) : CombatEvent;
public sealed record TurnEnded(string CombatantId, int UnusedActionPoints, int BonusArmorClass) : CombatEvent;
/// <summary>The player ended the fight with enemies still standing (Enter); the fight then ends without a winner.</summary>
public sealed record CombatLeft(string CombatantId, string[] Survivors) : CombatEvent;
public sealed record CombatEnded(int? WinningTeam, int ExperienceAwarded) : CombatEvent;

public sealed record CombatActionResult(CombatActionStatus Status, CombatActionPreview Preview, CombatEvent[] Events);

public sealed record CombatTurnState(int Round, string? ActiveId, string[] Order, bool Over, int? WinningTeam, int ExperienceAwarded);
