using System;
using System.IO;
using System.Linq;

namespace FalloutRemake.Runtime;

/// <summary>
/// The player's perks (CE perkAdd from the character editor). Reaching a level that divides by 3 (4 with Skilled then)
/// raises characterEditorUpdateLevel's single free-perk flag, which a choice clears: levels crossed before choosing never
/// stack a second perk. The choice is among those CE perkCanAdd allows against the current sheet; Tag! names a skill,
/// Mutate! a trait to lose and one to gain, Here and Now grants the experience left to the next level. Their bonuses join
/// <see cref="Sheet"/>; perks whose effect is not modeled yet are refused. Saved with the skills (optional, inside v13).
/// </summary>
public sealed partial class WorldSession
{
    private PerkRanks _perks = PerkRanks.None;
    private bool _freePerk;
    private int _swiftExperience;
    /// <summary>The XP Swift Learner added: 5% per rank of every gain after it was chosen, rounded down per gain (pcAddExperience).</summary>
    public int SwiftLearnerExperience => _swiftExperience;

    // pcAddExperienceWithOptions: each gain adds rank x 5 x xp / 100 more (combat once per fight, scripts, locks, Here and Now).
    private void GainExperience(int xp) { if (xp > 0) _swiftExperience += _perks.Rank(Perk.SwiftLearner) * 5 * xp / 100; }
    private int _perkLevel = 1;
    public PerkRanks Perks => _perks;

    /// <summary>1 while the free-perk flag is up, else 0. Levels reached since the last read are processed one by one, as the editor does.</summary>
    public int PerksAvailable
    {
        get
        {
            PlayerCharacter sheet = Character.WithPerks(_perks);
            // CE stops granting perks once 37 different ones have ranks.
            for (int level = Level; _perkLevel < level; _perkLevel++)
                if (sheet.PerkLevel(_perkLevel + 1) && _perks.Chosen.Select(c => c.Perk).Distinct().Count() < 37) _freePerk = true;
            return _freePerk ? 1 : 0;
        }
    }

    /// <param name="skill">Tag!'s fourth skill (perkDialogHandleTagPerk: one not tagged yet); null for every other perk.</param>
    /// <param name="lose">Mutate!'s trait to lose: one the character has (null when it has none).</param>
    /// <param name="gain">Mutate!'s new trait: none the character had before the swap.</param>
    public PerkChoiceStatus CanChoosePerk(int perk, int? skill = null, CharacterTrait? lose = null, CharacterTrait? gain = null)
    {
        if (InCombat) return PerkChoiceStatus.InCombat;
        if (PerksAvailable <= 0) return PerkChoiceStatus.NoPerkAvailable;
        PerkChoiceStatus status = PerkRules.CanAdd(perk, Sheet, Level, _perks);
        if (status != PerkChoiceStatus.Ready) return status;
        if (perk != Perk.Mutate && (lose != null || gain != null)) return PerkChoiceStatus.TraitChoice;
        if (perk == Perk.Mutate)
        {
            var traits = Sheet.CurrentTraits;
            bool losing = traits.Count == 0 ? lose == null : lose is CharacterTrait l && traits.Contains(l);
            if (!losing || gain is not CharacterTrait g || !Enum.IsDefined(g) || traits.Contains(g)) return PerkChoiceStatus.TraitChoice;
        }
        return perk == Perk.Tag ? skill is int tag && tag is >= 0 and < CharacterSkill.Count && !Sheet.IsTagged(tag) ? status : PerkChoiceStatus.SkillChoice
            : skill == null ? status : PerkChoiceStatus.SkillChoice;
    }

    public PerkChoiceStatus ChoosePerk(int perk, int? skill = null, CharacterTrait? lose = null, CharacterTrait? gain = null)
    {
        PerkChoiceStatus status = CanChoosePerk(perk, skill, lose, gain);
        if (status != PerkChoiceStatus.Ready) return status;
        // The levels already reached grant their skill points before the perk; Educated then adds 2 at once (character
        // editor), and 2 more at each later level.
        int unspent = UnspentSkillPoints, level = Level;
        // perkAddEffect: Here and Now gives what the next level still needs (pcGetExperienceForLevel(level + 1) - XP).
        int? experience = perk == Perk.HereAndNow && level < CharacterProgression.MaxLevel ? CharacterProgression.ExperienceForLevel(level + 1) - Experience : null;
        _perks = _perks.With(new ChosenPerk(perk, level, skill, lose is CharacterTrait l ? (int)l : null, gain is CharacterTrait g ? (int)g : null, experience));
        _freePerk = false; _skillRevision++;
        // Here and Now's experience goes through pcAddExperienceWithOptions too, so Swift Learner adds its share.
        if (experience is int granted) GainExperience(granted);
        if (perk == Perk.Educated) _unspentSkillPoints = Math.Min(CharacterProgression.MaxUnspentSkillPoints, unspent + 2);
        return PerkChoiceStatus.Ready;
    }

    private void ResetFreePerk() { _freePerk = false; _perkLevel = 1; }
    private void ResetSwiftExperience() => _swiftExperience = 0;

    // With the perks, before the level is needed: Swift Learner's XP, only with the perk; its bound is checked once XP is known.
    private void RestoreSwiftExperience(int? saved)
    {
        _swiftExperience = 0;
        if (saved == null) return;
        if (saved <= 0 || !_perks.Has(Perk.SwiftLearner)) throw new InvalidDataException("Checkpoint Swift Learner XP is malformed.");
        _swiftExperience = saved.Value;
    }

    // Read with the character, before the level is known: identities, ranks, what each perk decided and the level order.
    private void RestorePerks(ChosenPerk[]? saved)
    {
        _perks = PerkRanks.None;
        if (saved == null) return;
        if (saved.Length == 0) throw new InvalidDataException("Checkpoint perks store their default explicitly.");
        var ranks = new int[PerkCatalog.Count]; int previous = 1;
        foreach (ChosenPerk row in saved)
        {
            if (row == null || row.Perk is < 0 or >= PerkCatalog.Count || row.Level <= previous && previous > 1 || row.Level < 2 || row.Level > CharacterProgression.MaxLevel)
                throw new InvalidDataException("Checkpoint perk is malformed.");
            PerkDefinition d = PerkCatalog.Get(row.Perk);
            bool tag = row.Perk == Perk.Tag ? row.Skill is >= 0 and < CharacterSkill.Count && !Character.IsCreationTag(row.Skill.Value) : row.Skill == null;
            bool mutate = row.Perk == Perk.Mutate
                ? (Character.Traits.Count == 0 ? row.TraitLost == null : row.TraitLost is int lost && Character.Traits.Contains((CharacterTrait)lost))
                    && row.TraitGained is >= 0 and < 16 && !Character.Traits.Contains((CharacterTrait)row.TraitGained.Value)
                : row.TraitLost == null && row.TraitGained == null;
            bool now = row.Perk == Perk.HereAndNow
                ? row.Level < CharacterProgression.MaxLevel && row.Experience is int xp && xp > 0
                    && xp <= CharacterProgression.ExperienceForLevel(row.Level + 1) - CharacterProgression.ExperienceForLevel(row.Level)
                : row.Experience == null;
            if (!d.Selectable || PerkCatalog.Effect(row.Perk) == PerkEffect.NotModeled || row.Level < d.MinLevel || ++ranks[row.Perk] > d.MaxRank || !tag || !mutate || !now)
                throw new InvalidDataException("Checkpoint perk could not have been chosen.");
            previous = row.Level;
        }
        _perks = new PerkRanks(saved);
    }

    // Once XP is restored: every choice used a flag raised by a perk level since the previous one, none past the level reached.
    private void ValidatePerks()
    {
        // At most 5% per rank of all the other XP (it only counts gains after the perk).
        if (_swiftExperience > _perks.Rank(Perk.SwiftLearner) * 5 * BaseExperience / 100)
            throw new InvalidDataException("Checkpoint Swift Learner XP exceeds its share.");
        PlayerCharacter sheet = Character.WithPerks(_perks);
        int previous = 1;
        foreach (ChosenPerk row in _perks.Chosen)
        {
            if (row.Level > Level || !Enumerable.Range(previous + 1, Math.Max(0, row.Level - previous)).Any(sheet.PerkLevel))
                throw new InvalidDataException("Checkpoint perk was chosen without a perk level to earn it.");
            previous = row.Level;
        }
    }

    // After the level is known: the saved flag, which needs a perk level since the last choice. Saves made before the flag
    // was kept had every earned and unchosen perk waiting.
    private void RestoreFreePerk(bool? saved)
    {
        PlayerCharacter sheet = Character.WithPerks(_perks);
        int last = _perks.Chosen.Count == 0 ? 1 : _perks.Chosen[^1].Level;
        bool earned = Enumerable.Range(last + 1, Math.Max(0, Level - last)).Any(sheet.PerkLevel);
        if (saved == true && !earned) throw new InvalidDataException("Checkpoint keeps a free perk no level earned.");
        _freePerk = saved ?? earned; _perkLevel = Level;
    }
}
