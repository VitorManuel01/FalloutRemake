using System;
using System.IO;
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
        _combatWeapon = CombatWeapon.Unarmed();
        _combat = _campaign.BeginTempleDuel(profile!, new SeededCombatRandom(_combatSeed ?? (ulong)DateTime.UtcNow.Ticks));
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
            TempleDuelOutcome.Killed => $"Cameron morreu · +{fight.Turn.ExperienceAwarded} XP · no original isso grava G369, que a porta final consulta; essa saída ainda não está modelada",
            _ => null
        };
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
