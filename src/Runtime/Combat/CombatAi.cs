using System;
using System.Collections.Generic;
using System.Linq;
using FalloutRemake.Geometry;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime.Combat;

/// <summary>
/// CE _combat_ai for a creature with an ai.txt packet, through the session's own preview/execute contract:
/// - flee when crippled as hurt_too_much fears or below min_hp (_ai_run_away), and keep fleeing;
/// - pick the danger (_ai_danger_source: whoHitMe while it lives, then the attackers of itself and its friends);
/// - distance preferences, then _ai_try_attack: approach along the CE pathfinder route to the target's hex, called shots
///   (called_freq, IN 5 on normal difficulty), attack while AP last; flee when even point blank the chance is below min_to_hit;
/// - disengage when the danger stands beyond max_dist with no friend near; keep 10 hexes from a friend's corpse whose
///   killer it never saw.
/// Natural attacks only: no weapon switching, reloading, drugs, ground items or taunts (none in the supported fights).
/// </summary>
public static class CombatAi
{
    // HIT_MODE_PUNCH: _ai_called_shot passes the hit mode where _determine_to_hit expects the location (CE bug kept).
    const HitLocation PunchAsLocation = HitLocation.RightLeg;
    const int CalledShotIntelligence = 5;

    public static CombatEvent[] TakeTurn(CombatSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        CombatantState actor = session.Active ?? throw new InvalidOperationException("The fight is over.");
        if (actor.IsPlayer) throw new InvalidOperationException("The player's turn is never automated.");
        if (actor.Ai is not AiPacket ai) return ProvisionalMeleeBehavior.TakeTurn(session);
        int start = session.Log.Count;
        new Turn(session, actor, ai).Run();
        if (session.Active == actor) session.Execute(CombatActionProposal.EndTurn(actor.Id));
        return session.Log.Skip(start).ToArray();
    }

    /// <summary>_combatai_rating: the better of Melee Damage and a carried weapon's maximum, plus AC; 0 when dead or out.</summary>
    public static int Rating(CombatantState critter)
    {
        if (!critter.Alive || critter.KnockedOut) return 0;
        int damage = critter.Stats.MeleeDamage;
        if (critter.Weapon.IsItem && damage < critter.Weapon.MaxDamage) damage = critter.Weapon.MaxDamage;
        return damage + critter.EffectiveArmorClass;
    }

    private sealed class Turn(CombatSession session, CombatantState actor, AiPacket ai)
    {
        CombatMovement Movement => session.Movement;
        CombatantState? Player => session.Combatants.FirstOrDefault(c => c.IsPlayer);
        int Distance(CombatantState other) => FalloutGrid.Distance(actor.Hex, other.Hex);
        bool Perceives(CombatantState other) =>
            CritterSight.WithinPerception(actor.Hex, actor.Rotation, false, actor.Perception, other.Hex, inCombat: true);
        bool Blocked(int hex) => !Movement.Passable(hex, actor);
        // _curr_crit_list: the combatants still in the fight's list.
        IEnumerable<CombatantState> InList => session.Combatants.Where(c => c.InFight);

        public void Run()
        {
            if (actor.Fleeing || (actor.Conditions & (DamageFlags)ai.HurtTooMuch) != 0 || actor.HitPoints < ai.MinHitPoints)
            {
                RunAway(null, actor.Fleeing ? "keeps fleeing" : actor.HitPoints < ai.MinHitPoints ? "below min_hp" : "hurt_too_much");
                return;
            }
            CombatantState? target = DangerSource();
            DistancePreferences(target);
            if (target != null) TryAttack(target);
            if (target != null && target.Alive && actor.ActionPoints != 0 && Distance(target) > ai.MaxDistance)
            {
                if (Corpse() is { } corpse) { MoveAway(corpse, 10); actor.FriendlyDead = null; }
                else if (!FindFriend(actor.Perception * 2, 5)) actor.Disengaging = true;
            }
            if (target == null && actor.WhoHitMe is string danger && session.Get(danger).Alive && actor.DamageTaken > 0)
            {
                if (Corpse() is { } corpse) { MoveAway(corpse, 10); actor.FriendlyDead = null; }
                else RunAway(null, "hit by someone it cannot see");
            }
            if (Corpse() is { } dead)
            {
                MoveAway(dead, 10);
                if (Distance(dead) >= 10) actor.FriendlyDead = null;
            }
            CombatantState? mate = actor.Team != 0 ? Nearest(c => c.Team == actor.Team && c.WhoHitMe != null) : Player;
            if (target == null && mate != null && Distance(mate) > 5) MoveStepsCloser(mate, Distance(mate) - 5);
            else if (actor.ActionPoints > 0) DistancePreferences(target);
        }

        CombatantState? Corpse() => actor.FriendlyDead is string id ? session.Get(id) : null;

        // _ai_find_nearest_team / _in_combat: nearest other living member of the list that matches.
        CombatantState? Nearest(Func<CombatantState, bool> match) =>
            InList.Where(c => c != actor && c.Alive && match(c)).OrderBy(Distance).FirstOrDefault();

        CombatantState? DangerSource()
        {
            var targets = new CombatantState?[4];
            if (actor.WhoHitMe is string id && id != actor.Id)
            {
                CombatantState danger = session.Get(id);
                if (danger.Alive) return danger;
                if (danger.Team != actor.Team) targets[0] = Nearest(c => c.Team == danger.Team);
            }
            // aiFindAttackers: the nearest who holds it as its danger, the danger of a friend, an enemy a friend holds as its danger.
            foreach (CombatantState candidate in InList.Where(c => c != actor).OrderBy(Distance))
            {
                if (targets[1] == null && candidate.Alive && candidate.WhoHitMe == actor.Id) { targets[1] = candidate; continue; }
                if (targets[2] == null && candidate.Team == actor.Team && candidate.WhoHitMe is string w && w != actor.Id
                    && session.Get(w) is { Alive: true } enemy && enemy.Team != actor.Team) { targets[2] = enemy; continue; }
                if (targets[3] == null && candidate.Team != actor.Team && candidate.Alive && candidate.WhoHitMe is string v
                    && session.Get(v).Team == actor.Team) targets[3] = candidate;
            }
            foreach (CombatantState candidate in targets.Where(t => t != null).Select(t => t!).OrderBy(Distance))
                if (Perceives(candidate) && (PathTo(candidate.Hex) != null
                    || session.Preview(CombatActionProposal.Attack(actor.Id, candidate.Id)).Status == CombatActionStatus.Ready)) return candidate;
            return null;
        }

        void DistancePreferences(CombatantState? target)
        {
            if (actor.ActionPoints <= 0) return;
            switch (ai.Distance)
            {
                case AiPacket.StayClose:
                    if (Player is { } player && actor.WhoHitMe != player.Id && Distance(player) > 5) MoveStepsCloser(player, Distance(player) - 5);
                    break;
                case AiPacket.Charge:
                    if (target != null) MoveStepsCloser(target, actor.ActionPoints);
                    break;
                case AiPacket.Snipe:
                    if (target == null || Distance(target) >= 10) break;
                    int movement = actor.ActionPoints - actor.Weapon.ActionPoints;
                    if (movement <= 0 || movement + Distance(target) - 1 < 5 && Rating(actor) < Rating(target)) MoveAway(target, 10);
                    break;
            }
        }

        // _ai_try_attack for a natural attack: no weapon to switch to, so a missing reach means closing in.
        void TryAttack(CombatantState target)
        {
            if (actor.Team != target.Team) actor.WhoHitMe = target.Id;
            int actionPoints = actor.ActionPoints;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                if (!actor.Alive || (actor.Conditions & (DamageFlags.KnockedOut | DamageFlags.LoseTurn)) != 0) break;
                CombatActionStatus reason = session.Preview(CombatActionProposal.Attack(actor.Id, target.Id)).Status;
                switch (reason)
                {
                    case CombatActionStatus.OutOfRange:
                        if (Accuracy(target, useDistance: false) < ai.MinToHit) { RunAway(target, "cannot possibly hit"); return; }
                        if (MoveStepsCloser(target, actor.ActionPoints) == -1) return;
                        break;
                    case CombatActionStatus.Ready:
                        if (Accuracy(target) < ai.MinToHit)
                        {
                            if (Accuracy(target, useDistance: false) < ai.MinToHit) { RunAway(target, "cannot possibly hit"); return; }
                            int use = 0;
                            if (actionPoints > 0)
                            {
                                if (PathTo(target.Hex) is not { } path) use = actionPoints;
                                else
                                {
                                    int steps = Math.Min(path.Length, actionPoints), index;
                                    for (index = 0; index < steps; index++)
                                    {
                                        use++;
                                        if (Accuracy(target, fromHex: path[index]) >= ai.MinToHit) break;
                                    }
                                    if (index == steps) use = steps;
                                }
                            }
                            if (MoveStepsCloser(target, use) == -1) { RunAway(target, "cannot get closer"); return; }
                        }
                        if (!Attack(target) || actor.ActionPoints < actor.Weapon.ActionPoints) return;
                        break;
                    default:
                        // Not enough AP (the natural attack is the only one), or a target the rules refuse.
                        return;
                }
            }
        }

        int Accuracy(CombatantState target, bool useDistance = true, int? fromHex = null) =>
            Math.Min(CombatResolver.MaximumChance, CombatResolver.ToHit(actor, target, HitLocation.Uncalled, fromHex, useDistance).Raw);

        // _ai_attack: the plain blow names the torso; _ai_called_shot may aim it.
        bool Attack(CombatantState target) =>
            session.Execute(CombatActionProposal.Attack(actor.Id, target.Id, CalledShot(target))).Status == CombatActionStatus.Ready;

        HitLocation CalledShot(CombatantState target)
        {
            if (!target.IsPlayer || CombatSession.AttackCost(actor, HitLocation.Head) > actor.ActionPoints || !CanAim()) return HitLocation.Torso;
            if (session.Random.Between(1, ai.CalledFrequency) != 1 || actor.Stats.Special.Intelligence < CalledShotIntelligence) return HitLocation.Torso;
            var location = (HitLocation)session.Random.Between(0, (int)HitLocation.Uncalled);
            int chance = Math.Min(CombatResolver.MaximumChance, CombatResolver.ToHit(actor, target, PunchAsLocation).Raw);
            return chance < ai.MinToHit ? HitLocation.Torso : location;
        }

        // critterCanAim: no burst, and not explosive, fire or EMP damage.
        bool CanAim() => actor.Weapon.DamageType is not (DamageType.Explosion or DamageType.Fire or DamageType.Emp);

        /// <summary>The CE pathfinder's route (combat costs) toward a hex, the destination's own blocker ignored; null without one.</summary>
        int[]? PathTo(int hex, Func<int, bool>? blocked = null, bool checkDestination = false) =>
            OriginalPathfinder.FindPath(actor.Hex, hex, blocked ?? Blocked, inCombat: true, checkDestination) is { } rotations ? Hexes(rotations) : null;

        int[] Hexes(int[] rotations)
        {
            var hexes = new int[rotations.Length];
            int hex = actor.Hex;
            for (int i = 0; i < rotations.Length; i++) hexes[i] = hex = OriginalPathfinder.TileInDirection(hex, rotations[i], 1);
            return hexes;
        }

        // _ai_move_steps_closer: toward the target's hex; with no way through, the first critter in the way becomes the
        // goal when even stepping past it finds no route (_obj_ai_blocking_at).
        int MoveStepsCloser(CombatantState target, int actionPoints)
        {
            if (actionPoints <= 0 || ai.Distance == AiPacket.Stay) return -1;
            if (ai.Distance == AiPacket.StayClose && Player is { } player && target != player
                && Distance(player) > 5 && FalloutGrid.Distance(target.Hex, player.Hex) > 5 && Distance(player) + actionPoints > 5) return -1;
            if (Distance(target) <= 1) return -1;
            int goal = target.Hex;
            if (PathTo(goal) == null)
            {
                int? stepPast = null;
                bool AiBlocked(int hex)
                {
                    if (!Blocked(hex)) return false;
                    if (stepPast == null && Movement.CritterAt(hex, actor) && !NonCritterBlocks(hex)) { stepPast = hex; return false; }
                    return true;
                }
                if (PathTo(goal, AiBlocked) == null && stepPast is int blocker) goal = blocker;
            }
            Walk(goal, actionPoints);
            return 0;
        }

        // Anything but a critter on the hex: a wall, scenery, a closed door or an exit grid.
        bool NonCritterBlocks(int hex)
        {
            if (!Movement.CritterAt(hex, actor)) return Blocked(hex);
            return !Movement.PassableIgnoringCritters(hex, actor);
        }

        // animateMoveObjectToTile: the route to the hex, minus its last step when something stands there, at most the AP given
        // and as many hexes as the AP pay for (crippled legs).
        void Walk(int goal, int actionPoints)
        {
            if (PathTo(goal) is not { } path) return;
            int length = path.Length;
            if (Blocked(goal)) length--;
            length = Math.Min(Math.Min(length, actionPoints), actor.StepsAffordable(length));
            if (length > 0) session.Execute(CombatActionProposal.MoveAlong(actor.Id, path[..length]));
        }

        // _ai_run_away: from the danger (the player when none is given), as far as AP allow straight away or 60 degrees aside;
        // beyond max_dist it disengages instead.
        void RunAway(CombatantState? from, string reason)
        {
            from ??= Player;
            if (from == null) return;
            if (Distance(from) >= ai.MaxDistance) { actor.Disengaging = true; return; }
            if (!actor.Fleeing) { actor.Fleeing = true; session.Report(new CombatantFleeing(actor.Id, reason)); }
            if (Away(from, actor.ActionPoints) is int destination) Walk(destination, actor.ActionPoints);
        }

        // _ai_move_away: only when within the distance; walks at most that many hexes.
        void MoveAway(CombatantState from, int distance)
        {
            if (ai.Distance == AiPacket.Stay || Distance(from) > distance) return;
            int actionPoints = Math.Min(actor.ActionPoints, distance);
            if (actionPoints > 0 && Away(from, actionPoints, keepLastTried: true) is int destination) Walk(destination, actionPoints);
        }

        int? Away(CombatantState from, int actionPoints, bool keepLastTried = false)
        {
            int rotation = OriginalStraightPath.RotationTo(from.Hex, actor.Hex), last = -1;
            for (int left = actionPoints; left > 0; left--)
                foreach (int r in new[] { rotation, (rotation + 1) % 6, (rotation + 5) % 6 })
                {
                    last = OriginalPathfinder.TileInDirection(actor.Hex, r, left);
                    if (PathTo(last, checkDestination: true) is { Length: > 0 }) return last;
                }
            // _ai_move_away still walks toward the last hex it tried; the route then fails unless it happens to be open.
            return keepLastTried && last >= 0 ? last : null;
        }

        // _ai_find_friend: a teammate within reach counts; CE asks to close in by a negative distance, which never moves.
        bool FindFriend(int maxDistance, int keep)
        {
            CombatantState? friend = Nearest(c => c.Team == actor.Team);
            if (friend == null || Distance(friend) > maxDistance) return false;
            if (keep > Distance(friend)) MoveStepsCloser(friend, Distance(friend) - keep);
            return true;
        }
    }
}
