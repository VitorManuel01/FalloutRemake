using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FalloutRemake.Geometry;

namespace FalloutRemake.Runtime.Combat;

/// <summary>
/// Turn-based fight on one elevation. Each turn restores the actor's AP; unspent AP become AC until its next turn.
/// Rounds re-sort the living by initiative. Kills by the player award the victim's PRO experience.
/// The turn is ended explicitly (EndTurn), never by running out of AP, so the presentation decides the pacing.
/// </summary>
public sealed class CombatSession : ICombatSession
{
    /// <summary>CE inventoryOpen in combat: 4 AP without Quick Pockets (the item used inside costs nothing more).</summary>
    public const int InventoryActionPoints = 4;
    private readonly List<CombatantState> _combatants;
    private readonly List<CombatEvent> _log = new();
    private readonly ICombatRandom _random;
    private string[] _order;
    private int _index, _round = 1, _experience;
    private bool _over;
    private int? _winner;
    internal CombatMovement Movement { get; }

    public CombatSession(IEnumerable<CombatantState> combatants, HexNavigation navigation, ICombatRandom random, string? initiatorId = null)
    {
        ArgumentNullException.ThrowIfNull(combatants); ArgumentNullException.ThrowIfNull(navigation);
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _combatants = combatants.ToList();
        if (_combatants.Count < 2 || _combatants.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != _combatants.Count)
            throw new InvalidDataException("A fight needs at least two combatants with distinct ids.");
        if (_combatants.Any(c => c.Elevation != navigation.Elevation || !c.Alive))
            throw new InvalidDataException("Every combatant must be alive and on the navigation elevation.");
        if (_combatants.GroupBy(c => c.Hex).Any(g => g.Count() > 1)) throw new InvalidDataException("Two combatants share a hex.");
        if (_combatants.Select(c => c.Team).Distinct().Count() < 2) throw new InvalidDataException("A fight needs two teams.");
        if (initiatorId != null && _combatants.All(c => c.Id != initiatorId)) throw new InvalidDataException("Unknown initiator.");
        Movement = new CombatMovement(navigation, _combatants);
        _order = CombatInitiative.Order(_combatants, initiatorId);
        _log.Add(new CombatStarted((string[])_order.Clone()));
        BeginNextTurn(first: true);
    }

    public IReadOnlyList<CombatantState> Combatants => _combatants.AsReadOnly();
    public IReadOnlyList<CombatEvent> Log => _log.AsReadOnly();
    public CombatantState? Active => _over ? null : Get(_order[_index]);
    public CombatTurnState Turn => new(_round, Active?.Id, (string[])_order.Clone(), _over, _winner, _experience);
    public CombatantState Get(string id) => _combatants.FirstOrDefault(c => c.Id == id)
        ?? throw new KeyNotFoundException($"Unknown combatant {id}.");

    public CombatActionPreview Preview(CombatActionProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        CombatantState? actor = _combatants.FirstOrDefault(c => c.Id == proposal.ActorId);
        if (actor == null) return CombatActionPreview.Refused(CombatActionStatus.UnknownCombatant, 0);
        int ap = actor.ActionPoints;
        if (_over) return CombatActionPreview.Refused(CombatActionStatus.CombatOver, ap);
        if (actor != Active) return CombatActionPreview.Refused(CombatActionStatus.NotActive, ap);
        switch (proposal.Kind)
        {
            case CombatActionKind.EndTurn:
                return new(CombatActionStatus.Ready, 0, ap, Array.Empty<int>(), null, null);
            case CombatActionKind.Move:
                if (proposal.TargetHex is not int hex || !HexNavigation.Valid(hex)) return CombatActionPreview.Refused(CombatActionStatus.InvalidTarget, ap);
                if (hex == actor.Hex) return CombatActionPreview.Refused(CombatActionStatus.AlreadyThere, ap);
                int[]? path = Movement.Path(actor, hex);
                if (path == null) return CombatActionPreview.Refused(CombatActionStatus.Unreachable, ap);
                int cost = path.Length * CombatMovement.ActionPointsPerHex;
                return new(cost > ap ? CombatActionStatus.NotEnoughActionPoints : CombatActionStatus.Ready, cost, ap - cost, path, null, null);
            case CombatActionKind.UseHealing:
                if (actor.HealingDoses <= 0) return CombatActionPreview.Refused(CombatActionStatus.NoItem, ap);
                return new(InventoryActionPoints > ap ? CombatActionStatus.NotEnoughActionPoints : CombatActionStatus.Ready,
                    InventoryActionPoints, ap - InventoryActionPoints, Array.Empty<int>(), null, null);
            case CombatActionKind.Attack:
                CombatantState? target = _combatants.FirstOrDefault(c => c.Id == proposal.TargetId);
                if (target == null || target == actor || !target.InFight) return CombatActionPreview.Refused(CombatActionStatus.InvalidTarget, ap);
                if (target.Elevation != actor.Elevation) return CombatActionPreview.Refused(CombatActionStatus.OtherElevation, ap);
                int attackCost = actor.Weapon.ActionPoints;
                CombatActionStatus status = CombatResolver.Reach(actor, target) > actor.Weapon.Range ? CombatActionStatus.OutOfRange
                    : attackCost > ap ? CombatActionStatus.NotEnoughActionPoints : CombatActionStatus.Ready;
                // The odds are shown even when the attack is not yet possible, so the HUD can explain the refusal.
                return new(status, attackCost, ap - attackCost, Array.Empty<int>(), CombatResolver.ToHit(actor, target), DamageCalculator.Estimate(actor, target));
            default:
                return CombatActionPreview.Refused(CombatActionStatus.InvalidTarget, ap);
        }
    }

    public CombatActionResult Execute(CombatActionProposal proposal)
    {
        CombatActionPreview preview = Preview(proposal);
        if (preview.Status != CombatActionStatus.Ready) return new(preview.Status, preview, Array.Empty<CombatEvent>());
        int start = _log.Count;
        CombatantState actor = Active!;
        switch (proposal.Kind)
        {
            case CombatActionKind.EndTurn:
                EndTurn(actor); break;
            case CombatActionKind.Move:
                int from = actor.Hex;
                actor.Hex = preview.Path[^1]; actor.ActionPoints -= preview.ActionPointCost;
                _log.Add(new CombatantMoved(actor.Id, from, actor.Hex, (int[])preview.Path.Clone(), preview.ActionPointCost));
                break;
            case CombatActionKind.UseHealing:
                actor.ActionPoints -= preview.ActionPointCost; actor.HealingDoses--;
                // CE critterAdjustHitPoints clamps at the maximum: the excess of the roll is lost.
                int rolled = HealingDrugProfile.RollHealing(_random), healed = Math.Min(rolled, actor.Stats.MaxHitPoints - actor.HitPoints);
                actor.HitPoints += healed;
                _log.Add(new HealingUsed(actor.Id, rolled, healed, actor.HitPoints, preview.ActionPointCost));
                break;
            case CombatActionKind.Attack:
                CombatantState target = Get(proposal.TargetId!);
                actor.ActionPoints -= preview.ActionPointCost;
                int roll = _random.Between(1, 100);
                bool hit = CombatResolver.Hits(preview.ToHit!.Chance, roll);
                int damageRoll = 0, damage = 0;
                if (hit)
                {
                    damageRoll = _random.Between(preview.Damage!.MinRoll, preview.Damage.MaxRoll);
                    damage = DamageCalculator.Apply(damageRoll, actor.Weapon, preview.Damage.Threshold, preview.Damage.Resistance);
                    target.HitPoints = Math.Max(0, target.HitPoints - damage);
                }
                _log.Add(new AttackResolved(actor.Id, target.Id, preview.ToHit.Chance, roll, hit, damageRoll, damage, target.HitPoints, preview.ActionPointCost));
                if (!target.Alive)
                {
                    int experience = actor.IsPlayer ? target.Experience : 0;
                    _experience += experience;
                    _log.Add(new CombatantDied(target.Id, actor.Id, experience));
                    CheckOver();
                }
                break;
        }
        return new(CombatActionStatus.Ready, preview, _log.Skip(start).ToArray());
    }

    private void StartTurn(CombatantState combatant)
    {
        combatant.ActionPoints = combatant.Stats.MaxActionPoints; combatant.BonusArmorClass = 0;
        _log.Add(new TurnStarted(_round, combatant.Id, combatant.ActionPoints));
    }

    private void EndTurn(CombatantState combatant)
    {
        combatant.BonusArmorClass = combatant.ActionPoints;
        _log.Add(new TurnEnded(combatant.Id, combatant.ActionPoints, combatant.BonusArmorClass));
        combatant.ActionPoints = 0;
        BeginNextTurn(first: false);
    }

    private void BeginNextTurn(bool first)
    {
        while (true)
        {
            if (!first)
                do
                {
                    if (++_index >= _order.Length) { _round++; _order = CombatInitiative.Order(_combatants); _index = 0; }
                } while (!Get(_order[_index]).InFight);
            first = false;
            CombatantState next = Get(_order[_index]);
            // actemvil combat_p_proc, COMBAT_SUBTYPE_TURN: below half of max HP, the duelist ends the fight.
            if (next.YieldsBelowHalf && next.HitPoints < next.Stats.MaxHitPoints / 2)
            {
                next.Yielded = true; _log.Add(new CombatantYielded(next.Id, next.HitPoints));
                CheckOver(); if (_over) return;
                continue;
            }
            StartTurn(next); return;
        }
    }

    private void CheckOver()
    {
        int[] teams = _combatants.Where(c => c.InFight).Select(c => c.Team).Distinct().ToArray();
        if (teams.Length > 1) return;
        _over = true; _winner = teams.Length == 1 ? teams[0] : null;
        _log.Add(new CombatEnded(_winner, _experience));
    }
}

/// <summary>
/// Placeholder turn for non-player combatants until the tactical AI (roadmap AI-001): close in on the nearest living
/// enemy and attack while AP last, then end the turn. Uses only the public preview/execute contract.
/// </summary>
public static class ProvisionalMeleeBehavior
{
    public static CombatEvent[] TakeTurn(CombatSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        CombatantState actor = session.Active ?? throw new InvalidOperationException("The fight is over.");
        if (actor.IsPlayer) throw new InvalidOperationException("The player's turn is never automated.");
        var events = new List<CombatEvent>();
        for (int guard = 0; guard < 64 && session.Active == actor; guard++)
        {
            CombatantState? enemy = session.Combatants.Where(c => c.InFight && c.Team != actor.Team)
                .OrderBy(c => CombatResolver.Reach(actor, c)).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
            CombatActionResult result;
            CombatActionPreview attack = enemy == null ? CombatActionPreview.Refused(CombatActionStatus.InvalidTarget, actor.ActionPoints)
                : session.Preview(CombatActionProposal.Attack(actor.Id, enemy.Id));
            if (attack.Status == CombatActionStatus.Ready) result = session.Execute(CombatActionProposal.Attack(actor.Id, enemy!.Id));
            else if (attack.Status == CombatActionStatus.OutOfRange
                && session.Movement.Path(actor, h => FalloutGrid.Distance(h, enemy!.Hex) <= actor.Weapon.Range) is { Length: > 0 } path
                && Math.Min(path.Length, actor.ActionPoints) is > 0 and int steps)
                result = session.Execute(CombatActionProposal.Move(actor.Id, path[steps - 1]));
            else result = session.Execute(CombatActionProposal.EndTurn(actor.Id));
            events.AddRange(result.Events);
            if (result.Status != CombatActionStatus.Ready) { events.AddRange(session.Execute(CombatActionProposal.EndTurn(actor.Id)).Events); break; }
        }
        return events.ToArray();
    }
}
