using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;
using Godot;

namespace FalloutRemake.Preview;

/// <summary>
/// New game with a character: the premade GCDs of the original (Narg, Mingan, Chitsa, in the order of the original
/// selection screen), a character built with the editor rules or the neutral test profile. Starting resets the world.
/// </summary>
public partial class TemplePreview
{
    private static readonly string[] PremadeFiles = { "combat", "stealth", "diplomat" };
    private CharacterCreationWindow? _characterWindow;
    private IReadOnlyList<PremadeChoice> _premadeChoices = Array.Empty<PremadeChoice>();
    private CharacterTexts? _characterTexts;
    private Func<string, CharacterFile>? _premadeSource;

    private void LoadCharacters(GameData data)
    {
        _premadeSource = path => CharacterFile.Read(data.Read(path));
        var choices = new List<PremadeChoice>();
        foreach (string name in PremadeFiles)
            try
            {
                // The .bio text is hard-wrapped for the narrow original box: rejoin each paragraph.
                string bio = string.Join("\n\n", Encoding.Latin1.GetString(data.Read($"premade/{name}.bio").Bytes).Replace("\r", "")
                    .Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Select(p => string.Join(" ", p.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0))));
                choices.Add(new PremadeChoice(PlayerCharacter.FromPremade(_premadeSource($"premade/{name}.gcd")), bio));
            }
            catch (Exception error) when (error is IOException or InvalidDataException)
            { GD.Print($"PREMADE_UNAVAILABLE: {name}: {error.Message}"); }
        _premadeChoices = choices;
        string language = _objectText?.Language ?? "english";
        Func<int, string> Lookup(string file, int offset, Func<int, string> fallback)
        {
            try
            {
                MessageTextFile messages = MessageTextFile.Read(data.Read($"text/{language}/game/{file}.msg"));
                return id => messages.Entries.TryGetValue(offset + id, out MessageText? row) && !string.IsNullOrWhiteSpace(row.Text) ? row.Text : fallback(id);
            }
            catch (Exception error) when (error is IOException or InvalidDataException) { GD.Print($"CHARACTER_TEXT_UNAVAILABLE: {file}: {error.Message}"); return fallback; }
        }
        _characterTexts = new CharacterTexts(Lookup("stat", 100, i => $"Atributo {i}"), Lookup("trait", 100, i => ((CharacterTrait)i).ToString()),
            Lookup("trait", 200, _ => ""), Lookup("skill", 100, i => $"Perícia {i}"), Lookup("skill", 200, _ => ""));
    }

    private void CloseCharacterWindow() { if (_characterWindow != null) { _characterWindow.Hide(); _characterWindow.QueueFree(); _characterWindow = null; } }

    private void OpenCharacterWindow()
    {
        if (_characterWindow?.Visible == true) { CloseCharacterWindow(); return; }
        if (_campaign == null || _characterTexts == null || CombatRefuses("a troca de personagem")) return;
        CloseBarrierEffectReview(); CloseUseClock(); CloseExplosivePreparation(); CloseInventory(); CloseLoot(); CloseBag(); ClearRouteHover();
        GetTree().Root.GuiEmbedSubwindows = true;
        var dialog = new CharacterCreationWindow { Size = new Vector2I(1040, 720), Theme = _hud.GetChild<Control>(0).Theme };
        _characterWindow = dialog; _hud.AddChild(dialog);
        dialog.Canceled += CloseCharacterWindow; dialog.CloseRequested += CloseCharacterWindow;
        Guid token = _campaign.SessionToken;
        dialog.StartRequested = character => { if (_characterWindow == dialog && _campaign?.SessionToken == token) StartWithCharacter(character); };
        dialog.Configure(_premadeChoices, _characterTexts, _campaign.Character);
        dialog.PopupCenteredClamped(new Vector2I(1040, 720), .94f);
    }

    private void StartWithCharacter(PlayerCharacter character)
    {
        if (_campaign == null) return;
        CloseCharacterWindow(); DiscardCombat(); SetCameraFollow(false);
        _campaign.StartNewGame(character); BindCharacter(); BindCurrentArea();
        ChangeElevation(_travel!.Location.Elevation); FocusEntry();
        NavigationStatus($"Novo jogo com {character.Name} · {_campaign.HitPoints}/{_campaign.MaxHitPoints} HP · K para salvar");
        GD.Print($"CHARACTER_STARTED: {character.Name} ({character.Kind})");
    }

    // The lockpick resolver and the HUD follow the character of the current world.
    private void BindCharacter()
    {
        if (_campaign == null) return;
        _skills = new PreviewSkillResolver(PrototypeCharacter.From(_campaign.Sheet));
        RefreshDoorAction();
    }

    private SkillSheetWindow? _skillSheet;

    private void CloseSkillSheet() { if (_skillSheet != null) { _skillSheet.Hide(); _skillSheet.QueueFree(); _skillSheet = null; } }

    /// <summary>H: the sheet of the character in play, with level-up skill points to spend.</summary>
    private void OpenSkillSheet()
    {
        if (_skillSheet?.Visible == true) { CloseSkillSheet(); return; }
        if (_campaign == null || _characterTexts == null || CombatRefuses("a ficha (a luta fixa a ficha até acabar)")) return;
        CloseBarrierEffectReview(); CloseUseClock(); CloseExplosivePreparation(); CloseInventory(); CloseLoot(); CloseBag(); ClearRouteHover();
        GetTree().Root.GuiEmbedSubwindows = true;
        var dialog = new SkillSheetWindow { Size = new Vector2I(860, 680), Theme = _hud.GetChild<Control>(0).Theme };
        _skillSheet = dialog; _hud.AddChild(dialog);
        dialog.Canceled += CloseSkillSheet; dialog.CloseRequested += CloseSkillSheet;
        dialog.DoneRequested = spending => { if (_skillSheet == dialog) CommitSkillSheet(spending); };
        PlayerCharacter c = _campaign.Sheet; Special e = c.Effective;
        int next = _campaign.Level < CharacterProgression.MaxLevel ? CharacterProgression.ExperienceForLevel(_campaign.Level + 1) : 0;
        dialog.Configure(_campaign.BeginSkillSpending(), _characterTexts,
            $"{(c.Kind == CharacterKind.TestNeutral ? "Perfil de teste" : c.Name)} · Nível {_campaign.Level} · XP {_campaign.Experience}{(next > 0 ? $" (próximo nível em {next})" : "")} · HP {_campaign.HitPoints}/{_campaign.MaxHitPoints}\n"
            + string.Join("  ", Enumerable.Range(0, 7).Select(i => $"{_characterTexts.Stat(i)} {e.ToArray()[i]}"))
            + $"\nPor nível: +{c.HitPointsPerLevel} HP e +{c.SkillPointsPerLevel} pontos de perícia (o total guardado não passa de {CharacterProgression.MaxUnspentSkillPoints})");
        dialog.PopupCenteredClamped(new Vector2I(860, 680), .94f);
    }

    private void CommitSkillSheet(SkillSpending spending)
    {
        SkillCommitStatus status = _campaign!.CommitSkillSpending(spending);
        CloseSkillSheet();
        if (status == SkillCommitStatus.Ready) BindCharacter();
        NavigationStatus(status switch
        {
            SkillCommitStatus.Ready => $"Ficha atualizada · {_campaign.UnspentSkillPoints} ponto(s) de perícia sobrando · K para salvar",
            SkillCommitStatus.Unchanged => "Ficha sem mudanças",
            SkillCommitStatus.InCombat => "A ficha não muda durante a luta",
            _ => "A ficha mudou desde que abriu; abra de novo"
        });
        RefreshDoorAction();
    }

    private string CharacterLine()
    {
        if (_campaign == null) return "";
        PlayerCharacter c = _campaign.Sheet; Special e = c.Effective;
        string profile = c.Kind == CharacterKind.TestNeutral ? "Perfil de teste" : c.Name;
        return $"{profile} · FOR {e.Strength} PER {e.Perception} RES {e.Endurance} CAR {e.Charisma} INT {e.Intelligence} AGI {e.Agility} SOR {e.Luck}"
            + $" · Arrombamento {_skills.Character.Lockpick}";
    }
}
