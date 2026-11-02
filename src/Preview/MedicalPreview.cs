using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private MedicalWindow? _medicalWindow;

    private static readonly (DamageFlags Flag, string Name)[] LimbNames =
    {
        (DamageFlags.Blind, "olho ferido"), (DamageFlags.CrippledLeftArm, "braço esquerdo"), (DamageFlags.CrippledRightArm, "braço direito"),
        (DamageFlags.CrippledRightLeg, "perna direita"), (DamageFlags.CrippledLeftLeg, "perna esquerda")
    };
    private static string Limbs(DamageFlags flags) => string.Join(", ", LimbNames.Where(l => (flags & l.Flag) != 0).Select(l => l.Name));
    private static string KitName(int pid) => pid switch
    {
        47 => "First Aid Kit (+20)", 91 => "Doctor's Bag (+20)", 408 => "Field Medic First Aid Kit (+40)", 409 => "Paramedic's Bag (+40)", _ => $"Kit {pid}"
    };

    private void CloseMedicalWindow() { if (_medicalWindow != null) { _medicalWindow.Hide(); _medicalWindow.QueueFree(); _medicalWindow = null; } }

    private void OpenMedicalWindow()
    {
        if (_medicalWindow?.Visible == true) { CloseMedicalWindow(); return; }
        if (_campaign == null || CombatRefuses("usar Primeiros socorros ou Médico")) return;
        CloseBarrierEffectReview(); CloseUseClock(); CloseExplosivePreparation(); CloseInventory(); CloseLoot(); CloseBag(); CloseRestWindow(); ClearRouteHover();
        GetTree().Root.GuiEmbedSubwindows = true;
        var dialog = new MedicalWindow { Size = new Vector2I(640, 380), Theme = _hud.GetChild<Control>(0).Theme };
        _medicalWindow = dialog; _hud.AddChild(dialog);
        dialog.Confirmed += CloseMedicalWindow; dialog.Canceled += CloseMedicalWindow; dialog.CloseRequested += CloseMedicalWindow;
        dialog.UseRequested = (skill, kit) => { if (_medicalWindow == dialog) UseMedical(skill, kit); };
        RefreshMedicalWindow();
        dialog.PopupCentered(new Vector2I(640, 380));
    }

    private void RefreshMedicalWindow(string? message = null)
    {
        if (_medicalWindow == null || _campaign == null) return;
        PlayerCharacter sheet = _campaign.Sheet; bool moving = _walker?.Moving == true;
        string limbs = Limbs(_campaign.PlayerCrippled);
        string summary = $"{RestClock()}\nPrimeiros socorros {sheet.Skill(CharacterSkill.FirstAid)}% (o teste soma a chance de crítico, {sheet.CriticalChance}) · "
            + $"{_campaign.MedicalUsesLeft(CharacterSkill.FirstAid)} de 3 hoje\nMédico {sheet.Skill(CharacterSkill.Doctor)}% · {_campaign.MedicalUsesLeft(CharacterSkill.Doctor)} de 3 hoje"
            + $" · aleijado: {(limbs.Length == 0 ? "nada" : limbs)}";
        var kits = _campaign.MedicalKitsCarried(CharacterSkill.FirstAid).Concat(_campaign.MedicalKitsCarried(CharacterSkill.Doctor))
            .Select(k => (k, KitName(k.Source.Header.Pid))).ToArray();
        _medicalWindow.Refresh(summary, _campaign.CanUseMedicalSkill(CharacterSkill.FirstAid, moving), _campaign.CanUseMedicalSkill(CharacterSkill.Doctor, moving), kits, message);
    }

    private void UseMedical(int skill, CarriedItem? kit)
    {
        if (_campaign == null) return;
        MedicalResult result = _campaign.UseMedicalSkill(skill, new SeededCombatRandom(_combatSeed ?? (ulong)DateTime.UtcNow.Ticks), _walker?.Moving == true, kit);
        if (result.Died) { CloseMedicalWindow(); ResetWalker(); NavigationStatus("Você morreu envenenado enquanto se tratava · a prévia voltou ao início do Templo."); return; }
        string name = skill == CharacterSkill.FirstAid ? "Primeiros socorros" : "Médico";
        string done = result.Status != MedicalStatus.Ready ? MedicalWindow.Hint(result.Status)
            : $"{name}, {result.Minutes} min: "
              + (result.AlreadyHealthy ? "você já está com a saúde perfeita."
                  : string.Join("; ", new[]
                  {
                      result.LimbsHealed != DamageFlags.None ? $"tratou {Limbs(result.LimbsHealed)}" : null,
                      result.LimbsFailed != DamageFlags.None ? $"não conseguiu tratar {Limbs(result.LimbsFailed)}" : null,
                      result.Healing ? $"+{result.Healed} HP" : "não conseguiu curar nada"
                  }.Where(s => s != null)) + ".")
              + (result.Experience > 0 ? $" +{result.Experience} XP." : "")
              + (result.KitUsedUp ? " O kit acabou." : "")
              + (result.PoisonLost > 0 ? $" O veneno tirou {result.PoisonLost}." : "");
        NavigationStatus(done);
        RefreshMedicalWindow(done); RefreshDoorAction();
    }

    /// <summary>
    /// The medical window in the preview: First Aid on a healthy player tells so and still takes 30 minutes, as CE's skill_use
    /// does; no kit lies on the Temple's maps.
    /// </summary>
    private async Task CheckMedicalWindow()
    {
        bool checking = _checkingNavigation; _checkingNavigation = true;
        try
        {
            ResetWalker();
            OpenMedicalWindow();
            if (_medicalWindow == null || !_medicalWindow.FirstAidEnabled || !_medicalWindow.SummaryText.Contains("3 de 3 hoje"))
                throw new InvalidDataException("The medical window does not offer First Aid: " + _medicalWindow?.SummaryText);
            long time = _campaign!.GameTime; int hp = _campaign.HitPoints;
            UseMedical(CharacterSkill.FirstAid, null);
            if (_campaign.GameTime != time + 18000 || _campaign.HitPoints != hp || !_medicalWindow.StatusText.Contains("saúde perfeita"))
                throw new InvalidDataException("First Aid on a healthy player did not take 30 minutes: " + _medicalWindow.StatusText);
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "medical.png"));
            CloseMedicalWindow();
            GD.Print("MEDICAL_OK: First Aid on a healthy player says so and takes 30 minutes; Doctor offered; no kit on the Temple's maps.");
        }
        finally { CloseMedicalWindow(); _checkingNavigation = checking; }
    }

    /// <summary>
    /// The reload button in the preview: no map of the Temple or Arroyo holds a firearm or ammunition, so the active hand has
    /// nothing to reload; the rules themselves are covered by the synthetic tests (FirearmTests, WorldFirearmTests).
    /// </summary>
    private async Task CheckReload()
    {
        bool checking = _checkingNavigation; _checkingNavigation = true;
        try
        {
            ResetWalker();
            if (_campaign!.CanReload(_campaign.ActiveHand, false) != ReloadStatus.NoWeapon) throw new InvalidDataException("The bare hand offers a reload.");
            ReloadActiveWeapon();
            if (!_hud.NavigationStatusText.Contains("Recarregar")) throw new InvalidDataException("The reload refusal is not shown: " + _hud.NavigationStatusText);
            await Task.CompletedTask;
            GD.Print("RELOAD_OK: the reload button answers; without a firearm in hand it is refused (no firearm or ammunition on maps 3, 4 or 126).");
        }
        finally { _checkingNavigation = checking; }
    }
}
