using System;
using System.Collections.Generic;
using System.Linq;

namespace FalloutRemake.Runtime.Combat;

/// <summary>
/// Turn order as CE _compare_faster: Sequence, then Luck, both descending. CE leaves later ties to qsort; the project
/// breaks them deterministically by Agility, Perception and finally the ordinal source id, so a fight replays exactly.
/// </summary>
public static class CombatInitiative
{
    public static string[] Order(IEnumerable<CombatantState> combatants, string? initiatorId = null)
    {
        var ordered = combatants.Where(c => c.Alive)
            .OrderByDescending(c => c.Stats.Sequence).ThenByDescending(c => c.Stats.Special.Luck)
            .ThenByDescending(c => c.Stats.Special.Agility).ThenByDescending(c => c.Stats.Special.Perception)
            .ThenBy(c => c.Id, StringComparer.Ordinal).Select(c => c.Id).ToList();
        // CE _combat_sequence_init: in the first round whoever started the fight acts first.
        if (initiatorId != null && ordered.Remove(initiatorId)) ordered.Insert(0, initiatorId);
        return ordered.ToArray();
    }
}
