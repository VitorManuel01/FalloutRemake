using System;
using System.IO;
using System.Linq;

namespace FalloutRemake.Runtime;

/// <summary>
/// The player's perks (CE perkAdd from the character editor): one per perk the level earns (every third level, fourth
/// with Skilled), chosen among those CE perkCanAdd allows against the current sheet. Their stat and skill bonuses join
/// <see cref="Sheet"/>; the ones whose effect is not modeled yet are refused. Saved with the skills (optional, inside v13).
/// </summary>
public sealed partial class WorldSession
{
    private PerkRanks _perks = PerkRanks.None;
    public PerkRanks Perks => _perks;

    public PerkChoiceStatus CanChoosePerk(int perk)
    {
        if (InCombat) return PerkChoiceStatus.InCombat;
        if (PerksAvailable <= 0) return PerkChoiceStatus.NoPerkAvailable;
        return PerkRules.CanAdd(perk, Sheet, Level, _perks);
    }

    public PerkChoiceStatus ChoosePerk(int perk)
    {
        PerkChoiceStatus status = CanChoosePerk(perk);
        if (status != PerkChoiceStatus.Ready) return status;
        // The levels already reached grant their skill points before the perk (Educated only counts for later ones).
        _ = UnspentSkillPoints;
        _perks = _perks.With(new ChosenPerk(perk, Level)); _skillRevision++;
        return PerkChoiceStatus.Ready;
    }

    // Read with the character, before the level is known: identities, ranks and the order of the levels.
    private void RestorePerks(ChosenPerk[]? saved)
    {
        _perks = PerkRanks.None;
        if (saved == null) return;
        if (saved.Length == 0) throw new InvalidDataException("Checkpoint perks store their default explicitly.");
        var ranks = new int[PerkCatalog.Count]; int previous = 1;
        foreach (ChosenPerk row in saved)
        {
            if (row == null || row.Perk is < 0 or >= PerkCatalog.Count || row.Level < previous || row.Level > CharacterProgression.MaxLevel)
                throw new InvalidDataException("Checkpoint perk is malformed.");
            PerkDefinition d = PerkCatalog.Get(row.Perk);
            if (!d.Selectable || PerkCatalog.Effect(row.Perk) == PerkEffect.NotModeled || row.Level < d.MinLevel || ++ranks[row.Perk] > d.MaxRank)
                throw new InvalidDataException("Checkpoint perk could not have been chosen.");
            previous = row.Level;
        }
        _perks = new PerkRanks(saved);
    }

    // Once XP is restored: no perk from a level not reached, and never more perks by a level than it had earned.
    private void ValidatePerks()
    {
        if (_perks.Chosen.Any(c => c.Level > Level)) throw new InvalidDataException("Checkpoint perk was chosen at a level not reached.");
        foreach (ChosenPerk row in _perks.Chosen)
            if (_perks.Chosen.Count(c => c.Level <= row.Level) > CharacterProgression.PerksAvailable(Character, row.Level))
                throw new InvalidDataException("Checkpoint perks exceed what their levels earned.");
    }
}
