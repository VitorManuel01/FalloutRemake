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

    /// <param name="gameTime">The game clock (CE ticks) when the fight opens; it gates criticals and runs 5 s per round.</param>
    public CombatSession(IEnumerable<CombatantState> combatants, HexNavigation navigation, ICombatRandom random, string? initiatorId = null,
        long gameTime = CriticalRules.GameStartTicks)
    {
        ArgumentNullException.ThrowIfNull(combatants); ArgumentNullException.ThrowIfNull(navigation);
        _random = random ?? throw new ArgumentNullException(nameof(random));
        if (gameTime is < CriticalRules.GameStartTicks or >= CriticalRules.GameEndTicks) throw new ArgumentOutOfRangeException(nameof(gameTime));
        GameTime = gameTime;
        _combatants = combatants.ToList();
        if (_combatants.Count < 2 || _combatants.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != _combatants.Count)
            throw new InvalidDataException("A fight needs at least two combatants with distinct ids.");
        if (_combatants.Any(c => c.Elevation != navigation.Elevation || !c.Alive))
            throw new InvalidDataException("Every combatant must be alive and on the navigation elevation.");
        // Only crippled limbs outlast a fight; nobody enters one knocked down, out or without its turn.
        if (_combatants.Any(c => (c.Conditions & ~DamageFlags.Crippled) != 0)) throw new InvalidDataException("A combatant enters the fight prone or without its turn.");
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
    /// <summary>The player left the fight with enemies standing (CombatLeft); Turn.WinningTeam is then null.</summary>
    public bool Left { get; private set; }
    /// <summary>Game clock in ticks: the opening time plus 5 s for every round that ended (CE _combat_sequence).</summary>
    public long GameTime { get; private set; }

    /// <summary>
    /// Enemies of <paramref name="player"/> that still perceive it (CE _combatai_want_to_stop is false while the danger source
    /// is within perception): PE x 5 in their front cone, PE x 2 around, with no line of sight involved. The knocked out
    /// are out of the fight's list; one that woke and waits to rejoin always wants to (_combatai_want_to_join).
    /// </summary>
    public IEnumerable<CombatantState> AwareEnemies(CombatantState player) => _combatants.Where(c => c.InFight && c.Team != player.Team && !c.KnockedOut
        && (c.Rejoining || CritterSight.WithinPerception(c.Hex, c.Rotation, false, c.Stats.Special.Perception, player.Hex, inCombat: true)));

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
                int cost = path.Length * actor.ActionPointsPerHex;
                return new(cost > ap ? CombatActionStatus.NotEnoughActionPoints : CombatActionStatus.Ready, cost, ap - cost, path, null, null);
            case CombatActionKind.LeaveCombat:
                if (!actor.IsPlayer) return CombatActionPreview.Refused(CombatActionStatus.InvalidTarget, ap);
                return new(AwareEnemies(actor).Any() ? CombatActionStatus.EnemiesAware : CombatActionStatus.Ready, 0, ap, Array.Empty<int>(), null, null);
            case CombatActionKind.UseHealing:
                if (actor.HealingDoses <= 0) return CombatActionPreview.Refused(CombatActionStatus.NoItem, ap);
                return new(InventoryActionPoints > ap ? CombatActionStatus.NotEnoughActionPoints : CombatActionStatus.Ready,
                    InventoryActionPoints, ap - InventoryActionPoints, Array.Empty<int>(), null, null);
            case CombatActionKind.Attack:
                CombatantState? target = _combatants.FirstOrDefault(c => c.Id == proposal.TargetId);
                if (target == null || target == actor || !target.InFight) return CombatActionPreview.Refused(CombatActionStatus.InvalidTarget, ap);
                if (target.Elevation != actor.Elevation) return CombatActionPreview.Refused(CombatActionStatus.OtherElevation, ap);
                int attackCost = actor.Weapon.ActionPoints;
                DamageFlags arms = actor.Conditions & DamageFlags.CrippledArms;
                CombatActionStatus status = actor.Weapon.IsItem && (arms == DamageFlags.CrippledArms || arms != 0 && actor.Weapon.TwoHanded) ? CombatActionStatus.ArmCrippled
                    : CombatResolver.Reach(actor, target) > actor.Weapon.Range ? CombatActionStatus.OutOfRange
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
            case CombatActionKind.LeaveCombat:
                string[] survivors = _combatants.Where(c => c.InFight && c.Team != actor.Team).Select(c => c.Id).ToArray();
                _log.Add(new CombatLeft(actor.Id, survivors));
                _over = true; _winner = null; Left = true;
                EndConditions();
                _log.Add(new CombatEnded(null, _experience));
                break;
            case CombatActionKind.Move:
                int from = actor.Hex;
                // The walk animation leaves the mover facing along its last step.
                actor.Rotation = OriginalStraightPath.RotationTo(preview.Path.Length > 1 ? preview.Path[^2] : from, preview.Path[^1]);
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
                // _ai_attack and the dude's attack both turn toward the target first (animationRegisterRotateToTile).
                actor.Rotation = OriginalStraightPath.RotationTo(actor.Hex, target.Hex);
                actor.ActionPoints -= preview.ActionPointCost;
                int chance = preview.ToHit!.Chance, roll = _random.Between(1, 100), margin = chance - roll;
                bool hit = CombatResolver.Hits(chance, roll), critical = false, failure = false;
                // randomTranslateRoll: from game day 1 a second d100 may upgrade the hit (margin / 10 + Critical Chance - the
                // torso's 0 penalty) or the miss (miss margin / 10). The margin is taken from the clamped chance that decided the hit.
                int criticalRoll = 0, needed = 0;
                if (CriticalRules.CriticalsAllowed(GameTime))
                {
                    criticalRoll = _random.Between(1, 100);
                    needed = hit ? margin / 10 + actor.Stats.CriticalChance - CriticalRules.TorsoPenalty : -margin / 10;
                    (critical, failure) = (hit && criticalRoll <= needed, !hit && criticalRoll <= needed);
                }
                int damageRoll = 0, damage = 0;
                if (hit)
                {
                    var (multiplier, flags) = critical ? CriticalStrike(actor, target, criticalRoll, needed) : (DamageCalculator.NormalHitMultiplier, DamageFlags.None);
                    bool bypass = (flags & DamageFlags.Bypass) != 0;
                    int threshold = bypass ? DamageCalculator.Threshold(target, actor.Weapon, true) : preview.Damage!.Threshold;
                    int resistance = bypass ? DamageCalculator.Resistance(target, actor.Weapon, true) : preview.Damage!.Resistance;
                    damageRoll = _random.Between(preview.Damage!.MinRoll, preview.Damage.MaxRoll);
                    damage = DamageCalculator.Apply(damageRoll, actor.Weapon, threshold, resistance, multiplier);
                    target.HitPoints = Math.Max(0, target.HitPoints - damage);
                    // DAM_DEAD from the table kills whatever HP are left (_damage_object then critterKill).
                    if ((flags & DamageFlags.Dead) != 0) target.HitPoints = 0;
                    _log.Add(new AttackResolved(actor.Id, target.Id, chance, roll, hit, damageRoll, damage, target.HitPoints, preview.ActionPointCost));
                    if (target.Alive && flags != DamageFlags.None) ApplyResults(target, flags);
                }
                else _log.Add(new AttackResolved(actor.Id, target.Id, chance, roll, hit, 0, 0, target.HitPoints, preview.ActionPointCost));
                // CE runs the attacker's combat_p_proc (fixed_param 2) after every hit on a critter that stood before it.
                if (hit && actor.PoisonsOnHit && target.IsPlayer) Sting(actor, target);
                if (!target.Alive)
                {
                    int experience = actor.IsPlayer ? target.Experience : 0;
                    _experience += experience;
                    _log.Add(new CombatantDied(target.Id, actor.Id, experience));
                    CheckOver();
                }
                if (failure) CriticalMiss(actor, criticalRoll, needed);
                break;
        }
        return new(CombatActionStatus.Ready, preview, _log.Skip(start).ToArray());
    }

    /// <summary>
    /// attackComputeCriticalHit on the torso row: d100 + Better Criticals picks the effect; a row with a massive check rolls
    /// d10 against the defender's stat + modifier and adds its flags on a failure (statRoll). Returns the multiplier and flags.
    /// </summary>
    private (int Multiplier, DamageFlags Flags) CriticalStrike(CombatantState attacker, CombatantState target, int roll, int needed)
    {
        int effectRoll = _random.Between(1, 100) + attacker.Stats.BetterCriticals, effect = CriticalRules.Effect(effectRoll);
        CriticalEffect row = CriticalRules.Torso(target.IsPlayer, target.KillType, effect);
        DamageFlags flags = row.Flags;
        int message = row.Message, statRoll = 0;
        bool massive = false;
        if (row.MassiveStat != -1)
        {
            statRoll = _random.Between(1, 10);
            if (statRoll > target.Stats.Special.ToArray()[row.MassiveStat] + row.MassiveModifier) { flags |= row.MassiveFlags; message = row.MassiveMessage; massive = true; }
        }
        if ((flags & DamageFlags.CrippleRandom) != 0) flags = flags & ~DamageFlags.CrippleRandom | CriticalRules.RandomCripple(_random.Between(0, 3));
        _log.Add(new CriticalHit(attacker.Id, target.Id, roll, needed, effectRoll, effect, row.Multiplier, statRoll, massive, flags, message));
        return (row.Multiplier, flags);
    }

    /// <summary>
    /// attackComputeCriticalFailure: the player's wait for game day 6 (a plain miss before). d100 - 5 x (LK - 5) picks the
    /// weapon's failure column. Applied: lost turn (no AP left now, the next turn lost), knockdown, random cripple and the
    /// self hit (x2, the attacker's own DT/DR). HurtSelf has no effect in CE. Drop, RandomHit, LoseAmmo, Destroy, Explode,
    /// Dud and OnFire are reported but not applied: no natural attack or bare hands can draw them.
    /// </summary>
    private void CriticalMiss(CombatantState attacker, int roll, int needed)
    {
        if (attacker.IsPlayer && !CriticalRules.PlayerFailuresAllowed(GameTime)) return;
        int effectRoll = _random.Between(1, 100) - 5 * (attacker.Stats.Special.Luck - 5), effect = CriticalRules.FailureEffect(effectRoll);
        DamageFlags flags = CriticalRules.Failure(attacker.Weapon.CriticalFailureType, effect);
        int selfRoll = 0, selfDamage = 0;
        if ((flags & DamageFlags.HitSelf) != 0)
        {
            var (min, max) = DamageCalculator.RollRange(attacker, attacker.Weapon);
            selfRoll = _random.Between(min, max);
            selfDamage = DamageCalculator.Apply(selfRoll, attacker.Weapon, DamageCalculator.Threshold(attacker, attacker.Weapon),
                DamageCalculator.Resistance(attacker, attacker.Weapon, finesse: DamageCalculator.HasFinesse(attacker)));
            attacker.HitPoints = Math.Max(0, attacker.HitPoints - selfDamage);
        }
        if ((flags & DamageFlags.LoseTurn) != 0) attacker.ActionPoints = 0;
        if ((flags & DamageFlags.CrippleRandom) != 0) flags = flags & ~DamageFlags.CrippleRandom | CriticalRules.RandomCripple(_random.Between(0, 3));
        _log.Add(new CriticalFailure(attacker.Id, roll, needed, effectRoll, effect, flags, selfRoll, selfDamage, attacker.HitPoints));
        if (!attacker.Alive)
        {
            // Killed by its own blow: no XP, and its turn ends here.
            _log.Add(new CombatantDied(attacker.Id, attacker.Id, 0));
            CheckOver();
            if (!_over) EndTurn(attacker);
            return;
        }
        ApplyResults(attacker, flags);
        // _combat_attack: an attacker its own failure knocked down gets up at once, unless the turn is lost.
        if ((attacker.Conditions & DamageFlags.KnockedDown) != 0 && (attacker.Conditions & DamageFlags.LoseTurn) == 0) StandUp(attacker);
    }

    // _set_new_results: the lasting flags stay on the critter; a knockout (re)schedules the wake-up event.
    private void ApplyResults(CombatantState critter, DamageFlags flags)
    {
        if ((flags & DamageFlags.KnockedOut) != 0) critter.WakeAt = GameTime + CriticalRules.KnockoutTicks(critter.Stats.Special.Endurance);
        critter.SetConditions(critter.Conditions | flags & DamageFlags.Results & ~DamageFlags.Dead);
    }

    private void StandUp(CombatantState combatant)
    {
        int spent = Math.Min(CriticalRules.StandUpActionPoints, combatant.ActionPoints);
        combatant.ActionPoints -= spent;
        combatant.SetConditions(combatant.Conditions & ~DamageFlags.KnockedDown);
        _log.Add(new StoodUp(combatant.Id, spent));
    }

    // _combat_over gets the prone dude up; the project lets every survivor's knockdown, knockout and lost turn end with the fight.
    private void EndConditions()
    {
        foreach (CombatantState combatant in _combatants) { combatant.SetConditions(combatant.Conditions & DamageFlags.Crippled); combatant.Rejoining = false; }
    }

    private void Sting(CombatantState attacker, CombatantState target)
    {
        int needed = target.Stats.Special.Luck + PoisonRules.StingLuckModifier, luck = _random.Between(1, 10);
        if (luck <= needed) { _log.Add(new PoisonChecked(attacker.Id, target.Id, luck, needed, false, 0, 0, target.Poison.Level)); return; }
        int rolled = _random.Between(PoisonRules.StingMin, PoisonRules.StingMax), absorbed = PoisonRules.Absorb(rolled, target.PoisonResistance);
        target.SetPoison(target.Poison.Add(absorbed));
        _log.Add(new PoisonChecked(attacker.Id, target.Id, luck, needed, true, rolled, absorbed, target.Poison.Level));
    }

    // _combat_sequence adds 5 s of game time after every round; due poison events then cost HP, which can kill, and due
    // knockout events leave the sleeper knocked down (knockoutEventProcess).
    private void RoundElapsed()
    {
        GameTime += PoisonRules.CombatRoundTicks;
        foreach (CombatantState combatant in _combatants.Where(c => c.Alive && c.Poison.Active))
        {
            combatant.SetPoison(combatant.Poison.AdvanceRound(out int lost));
            if (lost == 0) continue;
            combatant.HitPoints = Math.Max(0, combatant.HitPoints - lost);
            _log.Add(new PoisonDamage(combatant.Id, lost, combatant.HitPoints, combatant.Poison.Level));
            if (!combatant.Alive) { _log.Add(new CombatantDied(combatant.Id, PoisonKiller, 0)); CheckOver(); }
        }
        if (_over) return;
        foreach (CombatantState sleeper in _combatants.Where(c => c.InFight && c.KnockedOut && c.WakeAt <= GameTime))
        {
            sleeper.SetConditions(sleeper.Conditions & ~DamageFlags.KnockedOut | DamageFlags.KnockedDown);
            // The dude never left the list; a critter sits out as a non-combatant until _combat_add_noncoms takes it back.
            sleeper.Rejoining = !sleeper.IsPlayer;
            _log.Add(new WokeUp(sleeper.Id));
        }
    }
    public const string PoisonKiller = "poison";

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
                    if (++_index >= _order.Length) { NextRound(); if (_over) return; }
                } while (!Get(_order[_index]).InFight);
            first = false;
            CombatantState next = Get(_order[_index]);
            // _combat_turn: knocked out, or its turn lost, it does nothing at all (no script either); the lost turn is used up.
            // _combat_set_move_all refilled its AP and none get spent, so they all count as AC until its next turn.
            if ((next.Conditions & (DamageFlags.KnockedOut | DamageFlags.LoseTurn)) is var reason and not DamageFlags.None)
            {
                next.SetConditions(next.Conditions & ~DamageFlags.LoseTurn);
                next.ActionPoints = 0; next.BonusArmorClass = next.Stats.MaxActionPoints;
                _log.Add(new TurnLost(next.Id, reason, next.BonusArmorClass));
                continue;
            }
            // actemvil combat_p_proc, COMBAT_SUBTYPE_TURN: below half of max HP, the duelist ends the fight.
            if (next.YieldsBelowHalf && next.HitPoints < next.Stats.MaxHitPoints / 2)
            {
                next.Yielded = true; _log.Add(new CombatantYielded(next.Id, next.HitPoints));
                CheckOver(); if (_over) return;
                continue;
            }
            StartTurn(next);
            if (next.Prone) StandUp(next);
            return;
        }
    }

    // _combat_sequence: last round's late joiners take their sorted place; the round's time passes; whoever woke now waits
    // outside the order and acts after everyone else this round (_combat_add_noncoms at the next sequence).
    private void NextRound()
    {
        foreach (CombatantState combatant in _combatants) combatant.Rejoining = false;
        RoundElapsed();
        if (_over) return;
        _round++;
        _order = CombatInitiative.Order(_combatants.Where(c => !c.Rejoining)).Concat(CombatInitiative.Order(_combatants.Where(c => c.Rejoining))).ToArray();
        _index = 0;
    }

    private void CheckOver()
    {
        int[] teams = _combatants.Where(c => c.InFight).Select(c => c.Team).Distinct().ToArray();
        if (teams.Length > 1) return;
        _over = true; _winner = teams.Length == 1 ? teams[0] : null;
        EndConditions();
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
                && actor.StepsAffordable(path.Length) is > 0 and int steps)
                result = session.Execute(CombatActionProposal.Move(actor.Id, path[steps - 1]));
            else result = session.Execute(CombatActionProposal.EndTurn(actor.Id));
            events.AddRange(result.Events);
            if (result.Status != CombatActionStatus.Ready) { events.AddRange(session.Execute(CombatActionProposal.EndTurn(actor.Id)).Events); break; }
        }
        return events.ToArray();
    }
}
