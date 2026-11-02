using System;
using System.IO;
using System.Linq;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;
using Godot;

namespace FalloutRemake.Preview;

/// <summary>
/// Cameron's unarmed challenge in the scene: the path of characters without a tagged Speech. Starting it plays actemvil
/// Node009 (equipment to the challenge chest, gate locked) and opens the usual combat view with Cameron striking first;
/// J beside the chest takes the equipment back afterwards.
/// </summary>
public partial class TemplePreview
{
    private TempleNpcDepartureProfile? _templeProfileCache;
    private string? _templeProfileSource;
    private CombatSession? _duelFightView;

    private TempleNpcDepartureProfile? TempleProfile()
    {
        if (_directory == null || _map == null || _map.Source.Sha256 != TempleBarrierEffectProposal.MapHash) return null;
        string source = _directory + "|" + _map.Source.Sha256;
        if (_templeProfileSource == source) return _templeProfileCache;
        try { _templeProfileCache = TempleNpcDepartureProfile.Create(_map, new GameData(_directory).Read); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        { _templeProfileCache = null; GD.Print("TEMPLE_PROFILE_UNAVAILABLE: " + error.Message); }
        _templeProfileSource = source; return _templeProfileCache;
    }

    private static string DuelHint(TempleWorldStatus status) => status switch
    {
        TempleWorldStatus.Ready => "Aceitar o desafio desarmado de Cameron: o equipamento vai para o baú fora da sala e o portão é trancado.",
        TempleWorldStatus.AlreadyAccepted => "O desafio final já foi resolvido nesta partida.",
        TempleWorldStatus.Busy => "Termine a caminhada ou a luta antes do desafio.",
        TempleWorldStatus.TooFar => "Aproxime o personagem a um hex de Cameron.",
        _ => "O desafio requer Cameron na elevação 2 do templo."
    };

    private void RefreshDuelAction(bool actor)
    {
        if (_campaign == null) { _hud.DuelAction(false, false, ""); return; }
        TempleWorldStatus status = actor ? _campaign.PrepareTempleDuel(TempleProfile(), _walker?.Moving == true) : TempleWorldStatus.Unavailable;
        _hud.DuelAction(actor, status == TempleWorldStatus.Ready && !InspectorOpen, DuelHint(status));
    }

    private string CameronLine(int id, int? second = null)
    {
        try
        {
            var messages = MessageTextFile.Read(new GameData(_directory!).Read($"text/{_objectText?.Language ?? "english"}/dialog/actemvil.msg"));
            string Text(int key) => messages.Entries.TryGetValue(key, out MessageText? row) ? row.Text : "";
            return second == null ? Text(id) : $"{Text(id)}{_campaign!.Character.Name}{Text(second.Value)}";
        }
        catch (Exception error) when (error is IOException or InvalidDataException) { GD.Print("CAMERON_TEXT_UNAVAILABLE: " + error.Message); return ""; }
    }

    private void StartTempleDuel()
    {
        if (_campaign == null || InspectorOpen || InCombat) return;
        TempleNpcDepartureProfile? profile = TempleProfile();
        TempleWorldStatus status = _campaign.PrepareTempleDuel(profile, _walker?.Moving == true);
        if (status != TempleWorldStatus.Ready) { NavigationStatus(DuelHint(status)); return; }
        ClearRouteHover(); ClearRouteVisual(); ClearSelection();
        _combat = _campaign.BeginTempleDuel(profile!, new SeededCombatRandom(_combatSeed ?? (ulong)DateTime.UtcNow.Ticks));
        // Bare hands: the chosen hand's blow (Strong Punch, Strong Kick…), named as CE names it.
        _combatWeapon = _combat.Get("player").Weapon;
        _duelFightView = _combat;
        // The gate now projects closed and locked; rebuild before showing the fight.
        BuildWorld();
        OpenCombatView($"Cameron: \"{CameronLine(145, 146)}\" · equipamento no baú do desafio");
        GD.Print($"TEMPLE_DUEL_STARTED: {_campaign.Character.Name} vs Cameron, {_campaign.TempleChestCustody.Length} stack(s) in the chest");
    }

    // FinishCombat calls this after a challenge fight was applied, before the generic victory text.
    private string? DuelOutcomeText(CombatSession fight)
    {
        if (!ReferenceEquals(fight, _duelFightView) || _campaign == null) return null;
        _duelFightView = null;
        return _campaign.TempleDuel switch
        {
            TempleDuelOutcome.Yielded => $"Cameron: \"{CameronLine(161)}\" · seu equipamento está no baú do desafio (J ao lado do baú)",
            TempleDuelOutcome.Killed => $"Cameron morreu · +{fight.Turn.ExperienceAwarded} XP · o portão e a porta final ficam trancados (G369 = 1); a Temple Key está no corpo dele (J ao lado do corpo)",
            _ => null
        };
    }

    // The Temple Key on the selected gate or exit door, after Cameron's death (aitemdor use_obj_on_p_proc).
    private string? SelectedTempleKeyDoor()
    {
        TempleNpcDepartureProfile? profile = _campaign?.TempleDuel == TempleDuelOutcome.Killed ? TempleProfile() : null;
        string? key = _selected?.Source.Key;
        return profile != null && key != null && (key == profile.GateKey || key == profile.ExitDoor?.Key) ? key : null;
    }
    private static string TempleKeyHint(TempleWorldStatus status) => status switch
    {
        TempleWorldStatus.Ready => "Usar a Temple Key do corpo de Cameron nesta porta: ela fica destrancada de vez.",
        TempleWorldStatus.AlreadyAccepted => "A Temple Key já destrancou esta porta.",
        TempleWorldStatus.DoorRequired => "Falta a Temple Key: ela está no corpo de Cameron (J ao lado do corpo).",
        TempleWorldStatus.TooFar => "Aproxime o personagem a um hex da porta.",
        TempleWorldStatus.Busy => "Termine a caminhada ou a luta antes.",
        _ => "A Temple Key serve no portão do desafio e na porta final."
    };
    private void RefreshTempleKeyAction()
    {
        string? door = SelectedTempleKeyDoor();
        TempleWorldStatus status = door == null ? TempleWorldStatus.Unavailable : _campaign!.CanUseTempleKey(door, _walker?.Moving == true);
        _hud.TempleKeyAction(door != null, status == TempleWorldStatus.Ready && !InspectorOpen, TempleKeyHint(status));
    }
    private void UseTempleKeyOnSelection()
    {
        string? door = SelectedTempleKeyDoor();
        if (door == null || _campaign == null || InspectorOpen) return;
        TempleWorldStatus status = _campaign.UseTempleKey(door, _walker?.Moving == true);
        NavigationStatus(status == TempleWorldStatus.Ready ? "Temple Key usada · a porta está destrancada (U abre) · K para salvar" : TempleKeyHint(status));
        if (status == TempleWorldStatus.Ready) { BuildWorld(); SelectObject(_layout!.Objects.Single(o => o.Source.Key == door)); GD.Print($"TEMPLE_KEY_USED: {door}"); }
        RefreshTempleKeyAction(); RefreshDoorAction();
    }

    private void RetrieveFromTempleChest()
    {
        if (_campaign == null) return;
        int units = _campaign.TempleChestCustody.Length;
        TempleChestStatus status = _campaign.RetrieveTempleChest(_walker?.Moving == true);
        NavigationStatus(status switch
        {
            TempleChestStatus.Ready => $"Equipamento recuperado do baú do desafio · bolsa com {_campaign.CarriedCount} unidades · K para salvar",
            TempleChestStatus.TooFar => "Aproxime-se do baú do desafio para recuperar o equipamento.",
            TempleChestStatus.Busy => "Pare de andar para recuperar o equipamento.",
            TempleChestStatus.InCombat => "Em combate · recupere o equipamento depois da luta.",
            _ => "O baú do desafio não guarda nada seu."
        });
        if (status == TempleChestStatus.Ready) { RefreshContainerVisuals(); GD.Print($"TEMPLE_CHEST_RETRIEVED: {units} stack(s)"); }
    }
}
