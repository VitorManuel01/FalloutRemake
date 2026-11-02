using System;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;

namespace FalloutRemake.Runtime;

public enum SkillSpendStatus { Ready, UnknownSkill, NoPoints, AtMaximum, NotEnoughPoints, AtMinimum }
public enum SkillCommitStatus { Ready, Unchanged, Stale, InCombat }

/// <summary>Level-up skill spending (checkpoint v13): base points bought per skill and the unspent pool.</summary>
/// <summary>Perks (optional, inside v13): the perks chosen, in order, with the level each was chosen at; absent when none.</summary>
public sealed record SavedSkills([property: JsonRequired] int[] Raises, [property: JsonRequired] int Unspent, ChosenPerk[]? Perks = null);

/// <summary>CE skill.cc rules for spending skill points.</summary>
public static class SkillSpendingRules
{
    /// <summary>CE skillsGetCost: skill points one more point costs at this skill value.</summary>
    public static int Cost(int value) => value >= 201 ? 6 : value >= 176 ? 5 : value >= 151 ? 4 : value >= 126 ? 3 : value >= 101 ? 2 : 1;

    /// <summary>Points the raises cost when bought one by one from the creation sheet (each skill's cost depends only on its own value).</summary>
    public static int CostOf(PlayerCharacter creation, int[] raises)
    {
        int total = 0;
        int[] partial = new int[CharacterSkill.Count];
        for (int skill = 0; skill < CharacterSkill.Count; skill++)
            for (partial[skill] = 0; partial[skill] < raises[skill]; partial[skill]++)
                total += Cost(creation.WithSkillRaises(partial).Skill(skill));
        return total;
    }
}

/// <summary>
/// One opening of the character sheet outside creation (CE characterEditorHandleAdjustSkillButtonPressed). Plus spends
/// with skillAdd: refused without unspent points (editor msg 136), at 300 (msgs 132/133), or when the bracket costs more
/// than what is left. Minus refunds with skillSub (tagged skills taking back the extra point when the bracket changes),
/// only above the value the skill had when the sheet opened (msgs 134/135). Nothing reaches the world until committed.
/// </summary>
public sealed class SkillSpending
{
    private readonly int[] _opened;
    private readonly int[] _raises;
    internal Guid Session { get; }
    internal long Revision { get; }
    internal PlayerCharacter Start { get; }
    public PlayerCharacter Sheet { get; private set; }
    public int Unspent { get; private set; }
    public bool Changed => !_raises.SequenceEqual(Start.SkillRaises);

    internal SkillSpending(PlayerCharacter sheet, int unspent, Guid session, long revision)
    {
        Start = Sheet = sheet; Unspent = unspent; Session = session; Revision = revision;
        _opened = sheet.Skills; _raises = sheet.SkillRaises.ToArray();
    }

    public SkillSpendStatus CanAdd(int skill)
    {
        if (skill is < 0 or >= CharacterSkill.Count) return SkillSpendStatus.UnknownSkill;
        if (Unspent <= 0) return SkillSpendStatus.NoPoints;
        int value = Sheet.Skill(skill);
        if (value >= PlayerCharacter.MaxSkill) return SkillSpendStatus.AtMaximum;
        return Unspent < SkillSpendingRules.Cost(value) ? SkillSpendStatus.NotEnoughPoints : SkillSpendStatus.Ready;
    }

    public SkillSpendStatus Add(int skill)
    {
        SkillSpendStatus status = CanAdd(skill);
        if (status != SkillSpendStatus.Ready) return status;
        Unspent -= SkillSpendingRules.Cost(Sheet.Skill(skill));
        _raises[skill]++; Sheet = Sheet.WithSkillRaises(_raises);
        return status;
    }

    public SkillSpendStatus CanSubtract(int skill)
    {
        if (skill is < 0 or >= CharacterSkill.Count) return SkillSpendStatus.UnknownSkill;
        return Sheet.Skill(skill) <= _opened[skill] || _raises[skill] <= 0 ? SkillSpendStatus.AtMinimum : SkillSpendStatus.Ready;
    }

    public SkillSpendStatus Subtract(int skill)
    {
        SkillSpendStatus status = CanSubtract(skill);
        if (status != SkillSpendStatus.Ready) return status;
        int lower = Sheet.Skill(skill) - 1, refund = SkillSpendingRules.Cost(lower);
        _raises[skill]--; Sheet = Sheet.WithSkillRaises(_raises);
        if (Sheet.IsTagged(skill) && SkillSpendingRules.Cost(lower) != SkillSpendingRules.Cost(Sheet.Skill(skill))) refund--;
        Unspent += refund;
        return status;
    }
}

public sealed partial class WorldSession
{
    private int[] _skillRaises = new int[CharacterSkill.Count];
    private int _unspentSkillPoints, _skillPointLevel = 1;
    private long _skillRevision;

    /// <summary>
    /// The character with the skill points spent on level-ups and the worn armor's perk: what combat, lockpicking and the
    /// sheet use.
    /// </summary>
    public PlayerCharacter Sheet => Character.WithSkillRaises(_skillRaises).Wearing(WornPerk()).WithPerks(_perks);

    /// <summary>
    /// CE pcAddExperience: each new level adds its skill points to the unspent pool, which never exceeds 99. XP only grows
    /// between restores, so the levels are granted when the pool is next read, one by one as the original does.
    /// </summary>
    public int UnspentSkillPoints
    {
        get
        {
            for (int level = Level; _skillPointLevel < level; _skillPointLevel++)
                _unspentSkillPoints = Math.Min(CharacterProgression.MaxUnspentSkillPoints,
                    _unspentSkillPoints + Character.WithPerks(_perks).SkillPointsForLevel(_skillPointLevel + 1));
            return _unspentSkillPoints;
        }
    }

    /// <summary>Opens the sheet for spending; the result is applied only by <see cref="CommitSkillSpending"/>.</summary>
    public SkillSpending BeginSkillSpending() => new(Sheet, UnspentSkillPoints, SessionToken, _skillRevision);

    public SkillCommitStatus CommitSkillSpending(SkillSpending spending)
    {
        ArgumentNullException.ThrowIfNull(spending);
        if (spending.Session != SessionToken || spending.Revision != _skillRevision || !ReferenceEquals(spending.Start.Creation, Character) || !spending.Start.SkillRaises.SequenceEqual(_skillRaises))
            return SkillCommitStatus.Stale;
        if (!spending.Changed) return SkillCommitStatus.Unchanged;
        // A fight snapshots the sheet when it opens; the project keeps the sheet fixed until it ends.
        if (InCombat) return SkillCommitStatus.InCombat;
        _skillRaises = spending.Sheet.SkillRaises.ToArray(); _unspentSkillPoints = spending.Unspent; _skillRevision++;
        return SkillCommitStatus.Ready;
    }

    private void ResetSkills() { _skillRaises = new int[CharacterSkill.Count]; _unspentSkillPoints = 0; _skillPointLevel = 1; _skillRevision++; _perks = PerkRanks.None; }

    private SavedSkills CaptureSkills() => new(_skillRaises.ToArray(), UnspentSkillPoints, _perks.Count == 0 ? null : _perks.Chosen.ToArray());

    // Runs after the XP sources are restored. The unspent pool plus what the raises cost can never exceed what the levels
    // granted; less is possible, since the 99 cap drops points that were not spent in time.
    private void RestoreSkills(SavedSkills? saved)
    {
        PerkRanks perks = _perks;
        ResetSkills(); _perks = perks;
        if (saved == null) return;
        if (saved.Raises is not { Length: CharacterSkill.Count } raises || raises.Any(r => r is < 0 or > PlayerCharacter.MaxSkill)
            || saved.Unspent is < 0 or > CharacterProgression.MaxUnspentSkillPoints)
            throw new InvalidDataException("Checkpoint skill spending is malformed.");
        int granted = Enumerable.Range(2, Math.Max(0, Level - 1)).Sum(Character.WithPerks(_perks).SkillPointsForLevel) + 2 * _perks.Rank(Perk.Educated);
        if (SkillSpendingRules.CostOf(Character, raises) + saved.Unspent > granted)
            throw new InvalidDataException("Checkpoint skill spending exceeds the points its levels grant.");
        _skillRaises = raises.ToArray(); _unspentSkillPoints = saved.Unspent; _skillPointLevel = Level;
    }
}
