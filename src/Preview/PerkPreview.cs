using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private PerkWindow? _perkWindow;

    private void ClosePerkWindow() { if (_perkWindow != null) { _perkWindow.Hide(); _perkWindow.QueueFree(); _perkWindow = null; } }

    // Y opens the perk choice (CE character editor's perk list), outside fights.
    private void OpenPerkWindow()
    {
        if (_perkWindow?.Visible == true) { ClosePerkWindow(); return; }
        if (_campaign == null || _characterTexts == null || CombatRefuses("a escolha de perks")) return;
        CloseBarrierEffectReview(); CloseUseClock(); CloseExplosivePreparation(); CloseInventory(); CloseLoot(); CloseBag(); ClearRouteHover();
        GetTree().Root.GuiEmbedSubwindows = true;
        var dialog = new PerkWindow { Size = new Vector2I(880, 680), Theme = _hud.GetChild<Control>(0).Theme };
        _perkWindow = dialog; _hud.AddChild(dialog);
        dialog.Confirmed += ClosePerkWindow; dialog.Canceled += ClosePerkWindow; dialog.CloseRequested += ClosePerkWindow;
        Guid sessionToken = _campaign.SessionToken;
        dialog.ChooseRequested = (perk, skill) => { if (_perkWindow == dialog && _campaign?.SessionToken == sessionToken) ChoosePerk(perk, skill); };
        RefreshPerkWindow();
        dialog.PopupCenteredClamped(new Vector2I(880, 680), .94f);
    }

    private void RefreshPerkWindow()
    {
        if (_perkWindow == null || _campaign == null || _characterTexts == null) return;
        int interval = _campaign.Character.PerkInterval, next = (_campaign.Level / interval + 1) * interval;
        // Each perk shows its own reason (level, SPECIAL, skills, not modeled); one it would allow waits for an earned perk.
        PlayerCharacter sheet = _campaign.Sheet; int level = _campaign.Level; PerkRanks ranks = _campaign.Perks;
        Func<int, int?, PerkChoiceStatus> status = _campaign.PerksAvailable > 0 ? _campaign.CanChoosePerk
            : (perk, _) => PerkRules.CanAdd(perk, sheet, level, ranks) is var s && s == PerkChoiceStatus.Ready ? PerkChoiceStatus.NoPerkAvailable : s;
        _perkWindow.Refresh(_campaign.PerksAvailable, next <= CharacterProgression.MaxLevel ? next : 0, _campaign.Perks, status,
            _characterTexts.Perk, _characterTexts.PerkDescription, _characterTexts.Stat, _characterTexts.Skill, sheet.IsTagged);
    }

    private void ChoosePerk(int perk, int? skill)
    {
        PerkChoiceStatus status = _campaign!.ChoosePerk(perk, skill);
        NavigationStatus(status == PerkChoiceStatus.Ready ? $"Perk escolhido: {_characterTexts!.Perk(perk)}{(skill is int s ? $" ({_characterTexts.Skill(s)})" : "")} · {PerkWindow.EffectText(PerkCatalog.Effect(perk))}"
            : $"Perk recusado · {PerkWindow.StatusText(status)}");
        RefreshPerkWindow(); RefreshBag(); RefreshDoorAction();
    }

    /// <summary>
    /// The perk window in the preview: Y opens it with the original perk.msg names and texts; at level 1 nothing can be chosen
    /// and it says the next perk comes at level 3; Quick Pockets waits for the level, Awareness is marked not modeled.
    /// </summary>
    private async Task CheckPerkWindow()
    {
        bool checking = _checkingNavigation; _checkingNavigation = true;
        try
        {
            ResetWalker();
            using (var y = new InputEventKey { Pressed = true, PhysicalKeycode = Key.Y }) _UnhandledInput(y);
            if (_perkWindow == null) throw new InvalidDataException("Y did not open the perk window.");
            if (!_perkWindow.SummaryText.Contains("nível 3") || _perkWindow.RowCount < 70 || _perkWindow.ChooseEnabled)
                throw new InvalidDataException("The level-1 perk window differs: " + _perkWindow.SummaryText);
            if (!_perkWindow.Select(Perk.QuickPockets) || !_perkWindow.DetailText.Contains("Quick Pockets") || !_perkWindow.DetailText.Contains("nível 3")
                || _perkWindow.DetailText.Split('\n').Length < 5)
                throw new InvalidDataException("Quick Pockets lacks its original text or requirement: " + _perkWindow.DetailText);
            if (!_perkWindow.Select(Perk.Awareness) || !_perkWindow.DetailText.Contains("não modelado") || !_perkWindow.DetailText.Contains("Agora: nível insuficiente"))
                throw new InvalidDataException("Awareness is not marked as not modeled, waiting for level 3: " + _perkWindow.DetailText);
            // Tag! asks for its fourth skill among those not tagged yet.
            if (!_perkWindow.Select(Perk.Tag) || !_perkWindow.TagSkillVisible || !_perkWindow.SelectTagSkill(CharacterSkill.Lockpick)
                || !_perkWindow.DetailText.Contains("Tag!") || _perkWindow.ChooseEnabled)
                throw new InvalidDataException("Tag! does not offer its skill choice: " + _perkWindow.DetailText);
            _perkWindow.Select(Perk.Awareness);
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "perks.png"));
            using (var y = new InputEventKey { Pressed = true, PhysicalKeycode = Key.Y }) _UnhandledInput(y);
            if (_perkWindow != null) throw new InvalidDataException("Y did not close the perk window.");
            GD.Print($"PERK_OK: Y opened the perk list with the original perk.msg texts; at level 1 nothing is chosen and the next perk comes at level 3; Quick Pockets shows its level requirement, Awareness is marked not modeled and Tag! offers its skill choice.");
        }
        finally { ClosePerkWindow(); _checkingNavigation = checking; }
    }
}
