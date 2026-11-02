using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;
using Godot;

namespace FalloutRemake.Preview;

/// <summary>
/// First playable fight (roadmap COM-004): exploration hands over to a CombatSession when a Giant Ant notices the
/// actor. Rules stay in Runtime.Combat; this layer only feeds clicks, shows the preview and mirrors the events.
/// The outcome lives in WorldSession and is saved by the checkpoint (v10+); saving waits only while a fight is open.
/// H takes a carried Healing Powder for 4 AP (CE inventory access); the powder is used up when the won fight is applied.
/// </summary>
public partial class TemplePreview
{
    private CombatSession? _combat;
    private CombatWeapon _combatWeapon = CombatWeapon.Unarmed();
    private bool _combatEncounters = true;
    private ulong? _combatSeed;
    // data/ai.txt, read with the map: every world the preview builds or restores gives its creatures their packets.
    private IReadOnlyDictionary<int, AiPacket>? _aiPackets;
    private double _combatAiTimer;
    private int _encounterCheckedHex = -1;
    private string _combatHoverKey = "";
    private readonly Dictionary<string, Label3D> _combatLabels = new();
    private const double EnemyTurnDelay = .45, StepSeconds = .12, LungeSeconds = .11;
    private ulong _combatBusyUntil;
    private bool _combatFinishPending;
    private readonly Dictionary<Node3D, Tween> _combatTweens = new();
    // Automated checks resolve visuals instantly so positions are exact on the same frame.
    private bool _forceCombatAnimation;
    private bool AnimateCombat => _forceCombatAnimation || !_checkingNavigation && !_test;
    private bool InCombat => _combat != null;

    // The carried weapon keeps its original prototype name; bare hands otherwise (WorldSession.PlayerWeapon).
    private CombatWeapon NamedWeapon(CombatWeapon weapon)
    {
        if (weapon.Pid is not int pid) return weapon;
        CarriedItem? item = _campaign?.Carried.FirstOrDefault(i => i.Source.Header.Pid == pid);
        string mode = (weapon.Throws ? " (arremesso)" : weapon.Burst ? " (rajada)" : "") + (weapon.UsesAmmo ? $" {weapon.Rounds}/{weapon.AmmoCapacity}" : "");
        return item != null && OriginalText(item.Source)?.Name is { } name ? weapon with { Name = ShortName(name) + mode } : weapon with { Name = weapon.Name + mode };
    }

    private bool CombatRefuses(string action)
    {
        if (!InCombat) return false;
        NavigationStatus($"Em combate · {action} indisponível até o fim da luta.");
        return true;
    }

    private void UpdateCombat(double delta)
    {
        if (_combat == null) { CheckEncounter(); return; }
        if (Time.GetTicksMsec() < _combatBusyUntil) return;
        if (_combatFinishPending) { FinishCombat(); return; }
        if (_combat.Active is { IsPlayer: false })
        {
            _combatAiTimer += delta;
            if (_combatAiTimer >= EnemyTurnDelay) { _combatAiTimer = 0; RunEnemyTurn(); }
        }
        else if (!_hud.MouseOverPanel) CombatHoverAtScreen(GetViewport().GetMousePosition());
    }

    private void CheckEncounter()
    {
        if (!_combatEncounters || _campaign == null || _walker == null || _travel == null || _elevation != _travel.Location.Elevation || InspectorOpen) return;
        int hex = _walker.CurrentHex;
        if (hex == _encounterCheckedHex && !_walker.Moving) return;
        if (_walker.Moving && hex == _encounterCheckedHex) return;
        _encounterCheckedHex = hex;
        CombatEncounter? encounter = _campaign.FindEncounter(_campaign.Location);
        if (encounter == null) return;
        // Finish the step in progress, then fight from a whole hex.
        if (_walker.Moving) _walker.RequestStop();
        if (_walker.Moving) { _encounterCheckedHex = -1; return; }
        StartCombat(encounter);
    }

    private void StartCombat(CombatEncounter encounter)
    {
        ClearRouteHover(); ClearRouteVisual(); ClearSelection();
        _combatWeapon = NamedWeapon(_campaign!.PlayerWeapon());
        _combat = _campaign.BeginCombat(encounter, _combatWeapon, new SeededCombatRandom(_combatSeed ?? (ulong)DateTime.UtcNow.Ticks));
        OpenCombatView($"{CombatName(encounter.InitiatorKey)} percebeu você · combate iniciado");
    }

    // Labels, HUD and the first status line of a fight opened by an encounter or by Cameron's challenge.
    private void OpenCombatView(string message)
    {
        _combatAiTimer = 0; _combatHoverKey = ""; _combatBusyUntil = 0; _combatFinishPending = false; _combatTweens.Clear();
        foreach (CombatantState combatant in _combat!.Combatants)
        {
            Node3D? host = combatant.IsPlayer ? _walkerNode : _bodies.GetValueOrDefault(combatant.Id);
            if (host == null) continue;
            var label = new Label3D { Name = "CombatHitPoints", Position = new Vector3(0, combatant.IsPlayer ? 1.55f : 1.15f, 0), FontSize = 34, PixelSize = .012f,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, Modulate = new Color(combatant.IsPlayer ? "8fe3f0" : "f08f7a") };
            host.AddChild(label); _combatLabels[combatant.Id] = label;
        }
        RefreshCombatView();
        NavigationStatus(message);
        GD.Print($"COMBAT_STARTED: {string.Join(",", _combat.Turn.Order)} at hex {_campaign!.Location.Hex}");
    }

    private string CombatName(string id)
    {
        if (id == "player") return "Você";
        // A fight that names its combatant (Cameron, whose PRO is called Dumar) keeps that name; encounters use the PRO text.
        if (_combat?.Combatants.FirstOrDefault(c => c.Id == id) is { } named && !named.Name.StartsWith("PID ", StringComparison.Ordinal)) return named.Name;
        PreviewObject? obj = _layout?.Objects.FirstOrDefault(o => o.Source.Key == id);
        return obj != null && OriginalText(obj.Source)?.Name is { } name ? ShortName(name) : "Criatura";
    }

    private static string Refusal(CombatActionStatus status) => status switch
    {
        CombatActionStatus.NotEnoughActionPoints => "AP insuficientes", CombatActionStatus.OutOfRange => "fora de alcance",
        CombatActionStatus.Unreachable => "sem caminho livre", CombatActionStatus.NotActive => "não é o seu turno",
        CombatActionStatus.AlreadyThere => "você já está aqui", CombatActionStatus.CombatOver => "o combate terminou",
        CombatActionStatus.OtherElevation => "outra elevação", CombatActionStatus.NoItem => "sem Healing Powder na bolsa",
        CombatActionStatus.EnemiesAware => "inimigos ainda percebem você",
        CombatActionStatus.ArmCrippled => "braço aleijado não segura a arma",
        CombatActionStatus.CannotAim => "este golpe não pode ser mirado",
        CombatActionStatus.AimBlocked => "a linha de tiro está bloqueada",
        CombatActionStatus.NoAmmo => "a arma está sem munição", CombatActionStatus.AlreadyLoaded => "a arma já está carregada", _ => "alvo inválido"
    };

    // CE combat results in the HUD's words; the critical tables only ever produce these on the torso.
    private static string Conditions(DamageFlags flags)
    {
        var words = new List<string>();
        if ((flags & DamageFlags.Dead) != 0) words.Add("morte instantânea");
        if ((flags & DamageFlags.KnockedOut) != 0) words.Add("nocauteado");
        else if ((flags & DamageFlags.KnockedDown) != 0) words.Add("caído");
        if ((flags & DamageFlags.LoseTurn) != 0) words.Add("perde o turno");
        if ((flags & DamageFlags.LoseAmmo) != 0) words.Add("perde a munição");
        if ((flags & DamageFlags.CrippledLegs) == DamageFlags.CrippledLegs) words.Add("pernas aleijadas");
        else if ((flags & DamageFlags.CrippledLegs) != 0) words.Add("perna aleijada");
        if ((flags & DamageFlags.CrippledArms) == DamageFlags.CrippledArms) words.Add("braços aleijados");
        else if ((flags & DamageFlags.CrippledArms) != 0) words.Add("braço aleijado");
        if ((flags & DamageFlags.Blind) != 0) words.Add("cego");
        if ((flags & DamageFlags.HitSelf) != 0) words.Add("acerta a si mesmo");
        return string.Join(", ", words);
    }
    // A creature's called shot names its target location; the plain blow (torso or uncalled) says nothing.
    // The player's plain blow is Uncalled, so a torso it names was aimed; a critter's plain blow names the torso.
    private static string Aimed(HitLocation location, bool player = false) => location switch
    {
        HitLocation.Torso when player => " mirando o tronco",
        HitLocation.Head => " mirando a cabeça", HitLocation.LeftArm => " mirando o braço esquerdo", HitLocation.RightArm => " mirando o braço direito",
        HitLocation.RightLeg => " mirando a perna direita", HitLocation.LeftLeg => " mirando a perna esquerda", HitLocation.Eyes => " mirando os olhos",
        HitLocation.Groin => " mirando a virilha", _ => ""
    };
    private static string Multiplier(int multiplier) => multiplier % 2 == 0 ? $"×{multiplier / 2}" : $"×{multiplier / 2},5";

    // Screen proximity rather than a physics ray: tall kit walls must not swallow a click on a creature behind them.
    private CombatantState? EnemyAtScreen(Vector2 screen) => _combat?.Combatants
        .Where(c => !c.IsPlayer && c.Alive && _bodies.ContainsKey(c.Id) && !_camera.IsPositionBehind(_bodies[c.Id].GlobalPosition))
        .Select(c => (Foe: c, Distance: _camera.UnprojectPosition(_bodies[c.Id].GlobalPosition).DistanceTo(screen)))
        .Where(p => p.Distance <= 40).OrderBy(p => p.Distance).Select(p => p.Foe).FirstOrDefault();

    private string AttackSummary(CombatActionPreview preview, CombatantState target) =>
        $"{_combatWeapon.Name} → {CombatName(target.Id)}: {preview.ToHit!.Chance}% de acerto · dano {preview.Damage!.MinDamage}–{preview.Damage.MaxDamage} · {preview.ActionPointCost} AP"
        + (preview.ToHit.StrengthPenalty > 0 ? $" · Força insuficiente −{preview.ToHit.StrengthPenalty}%" : "")
        + (preview.ToHit.OneHanderModifier != 0 ? $" · One Hander {preview.ToHit.OneHanderModifier:+0;−0}%" : "")
        + (preview.Status == CombatActionStatus.Ready ? "" : $" · {Refusal(preview.Status)}");

    private void CombatHoverAtScreen(Vector2 screen)
    {
        if (_combat?.Active is not { IsPlayer: true } player) return;
        string key; string text;
        if (EnemyAtScreen(screen) is { } foe)
        {
            key = "attack:" + foe.Id + ":" + player.ActionPoints + ":" + player.Hex;
            if (key == _combatHoverKey) return;
            CombatActionPreview attack = _combat.Preview(CombatActionProposal.Attack(player.Id, foe.Id));
            text = AttackSummary(attack, foe)
                + $"\nAcerto: perícia {attack.ToHit!.Skill} − AC {attack.ToHit.TargetArmorClass}{(attack.ToHit.DistanceModifier != 0 ? $" {attack.ToHit.DistanceModifier:+0;−0} distância" : "")}{(attack.ToHit.StrengthPenalty > 0 ? $" − {attack.ToHit.StrengthPenalty} força" : "")}{(attack.ToHit.ProneTarget > 0 ? $" + {attack.ToHit.ProneTarget} alvo caído" : "")} = {attack.ToHit.Raw}"
                + $" · DT {attack.Damage!.Threshold} · DR {attack.Damage.Resistance}%"
                + (CombatSession.CanAim(player) ? " · clique direito: mirar" : "");
        }
        else if (GroundHexAtScreen(screen) is int hex)
        {
            key = "move:" + hex + ":" + player.ActionPoints + ":" + player.Hex;
            if (key == _combatHoverKey) return;
            CombatActionPreview move = _combat.Preview(CombatActionProposal.Move(player.Id, hex));
            text = move.Status == CombatActionStatus.Ready ? $"Mover até hex {hex}: {move.ActionPointCost} AP · restam {move.ActionPointsAfter}"
                : move.Status == CombatActionStatus.NotEnoughActionPoints ? $"Mover até hex {hex}: {move.ActionPointCost} AP · você tem {player.ActionPoints}"
                : $"Hex {hex}: {Refusal(move.Status)}";
        }
        else { key = "none"; if (key == _combatHoverKey) return; text = $"Clique no chão para mover ({player.ActionPointsPerHex} AP por hex) ou na criatura para atacar."; }
        _combatHoverKey = key; _hud.CombatPreview(text);
    }

    private bool HandleCombatInput(InputEvent input)
    {
        if (_combat == null) return false;
        if (input is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.PhysicalKeycode == Key.Space) { EndCombatTurn(); return true; }
            if (key.PhysicalKeycode == Key.H) { UseCombatHealing(); return true; }
            if (key.PhysicalKeycode == Key.X) { SwitchCombatHand(); return true; }
            if (key.PhysicalKeycode == Key.Z) { ToggleAttackMode(); return true; }
            // J beside a thrown item picks it up on the player's turn; otherwise looting waits for the fight's end.
            if (key.PhysicalKeycode == Key.J && PickUpGroundItem()) return true;
            if (key.PhysicalKeycode is Key.Enter or Key.KpEnter) { LeaveCombat(); return true; }
            // Camera, view and inspection keys stay available; anything that would act in the world waits.
            if (key.PhysicalKeycode is Key.Q or Key.E or Key.C or Key.Home or Key.V or Key.G or Key.O or Key.F or Key.R) return false;
            if (key.PhysicalKeycode is Key.M or Key.U or Key.L or Key.K or Key.J or Key.P or Key.Escape or Key.T or Key.N)
            { CombatRefuses("essa ação"); return true; }
            return false;
        }
        if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouse && !_hud.MouseOverPanel)
        { CombatClickAtScreen(mouse.Position); return true; }
        // The right button still drags the view: only a click that ends where it started aims at the creature under it.
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Right } aim)
        {
            if (aim.Pressed) { _aimPress = aim.Position; return false; }
            bool click = _aimPress is { } press && press.DistanceTo(aim.Position) <= 6; _aimPress = null;
            if (!click || _hud.MouseOverPanelAt(aim.Position) || EnemyAtScreen(aim.Position) is not { } foe) return false;
            if (_combat.Active is not { IsPlayer: true }) NavigationStatus("Aguarde: é o turno da criatura.");
            else OpenCalledShot(foe);
            return true;
        }
        return false;
    }

    private void CombatClickAtScreen(Vector2 screen)
    {
        if (_combat?.Active is not { IsPlayer: true } player) { NavigationStatus("Aguarde: é o turno da criatura."); return; }
        CombatActionResult result = EnemyAtScreen(screen) is { } foe
            ? _combat.Execute(CombatActionProposal.Attack(player.Id, foe.Id))
            : GroundHexAtScreen(screen) is int hex ? _combat.Execute(CombatActionProposal.Move(player.Id, hex))
            : new(CombatActionStatus.InvalidTarget, CombatActionPreview.Refused(CombatActionStatus.InvalidTarget, player.ActionPoints), Array.Empty<CombatEvent>());
        ApplyCombatResult(result);
    }

    private CalledShotWindow? _calledShot;
    private Vector2? _aimPress;

    // CE aimed attack: the called-shot window lists the eight locations with their odds; choosing one attacks it.
    private void OpenCalledShot(CombatantState foe)
    {
        if (_combat?.Active is not { IsPlayer: true } player) return;
        if (!CombatSession.CanAim(player)) { NavigationStatus("Mira recusada · " + (player.Stats.FastShot ? "o trait Fast Shot impede ataques mirados" : "este golpe não pode ser mirado")); return; }
        CloseCalledShot();
        GetTree().Root.GuiEmbedSubwindows = true;
        var dialog = new CalledShotWindow { Theme = _hud.GetChild<Control>(0).Theme };
        _calledShot = dialog; _hud.AddChild(dialog);
        dialog.Confirmed += CloseCalledShot; dialog.Canceled += CloseCalledShot; dialog.CloseRequested += CloseCalledShot;
        dialog.Chosen = location =>
        {
            if (_calledShot != dialog || _combat?.Active is not { IsPlayer: true } shooter) return;
            CloseCalledShot();
            ApplyCombatResult(_combat.Execute(CombatActionProposal.Attack(shooter.Id, foe.Id, location)));
        };
        dialog.Refresh(CombatName(foe.Id), _combatWeapon.Name, location => _combat.Preview(CombatActionProposal.Attack(player.Id, foe.Id, location)), Refusal);
        dialog.PopupCentered(new Vector2I(560, 320));
    }

    private void CloseCalledShot()
    {
        if (_calledShot == null) return;
        CalledShotWindow dialog = _calledShot; _calledShot = null;
        dialog.Hide(); dialog.QueueFree();
    }

    private void EndCombatTurn()
    {
        if (_combat?.Active is not { IsPlayer: true } player) return;
        ApplyCombatResult(_combat.Execute(CombatActionProposal.EndTurn(player.Id)));
    }

    // CE combatAttemptEnd: refused (combat.msg 103) while any enemy still perceives the player; Cameron's challenge is fought out.
    private void LeaveCombat()
    {
        if (_combat?.Active is not { IsPlayer: true } player) { NavigationStatus("Aguarde: é o turno da criatura."); return; }
        if (_campaign!.TempleDuel == TempleDuelOutcome.InProgress) { NavigationStatus("Ação recusada · o desafio de Cameron vai até o fim"); return; }
        CombatActionResult result = _combat.Execute(CombatActionProposal.LeaveCombat(player.Id));
        if (result.Status == CombatActionStatus.EnemiesAware)
        {
            string aware = string.Join(", ", _combat.AwareEnemies(player).Select(c => $"{CombatName(c.Id)} a {FalloutGrid.Distance(c.Hex, player.Hex)} hex(es)"));
            NavigationStatus($"Ação recusada · inimigos ainda percebem você ({aware})"); return;
        }
        ApplyCombatResult(result);
    }

    // FO2's hand swap: bare hands punch with the left and kick with the right; no AP.
    private void SwitchCombatHand()
    {
        if (_combat?.Active is not { IsPlayer: true } player) { NavigationStatus("Aguarde: é o turno da criatura."); return; }
        CombatActionResult result = _combat.Execute(CombatActionProposal.SwitchHand(player.Id));
        if (result.Status != CombatActionStatus.Ready) { NavigationStatus("Ação recusada · a arma carregada não tem outra mão"); return; }
        _combatWeapon = player.Weapon;
        ApplyCombatResult(result);
    }

    // CE _intface_item_reload: 2 AP in a fight (1 with Fast Reload), free outside one.
    private void ReloadActiveWeapon()
    {
        if (_campaign == null) return;
        if (_combat != null)
        {
            if (_combat.Active is not { IsPlayer: true } player) { NavigationStatus("Aguarde: é o turno da criatura."); return; }
            CombatActionResult result = _combat.Execute(CombatActionProposal.Reload(player.Id));
            if (result.Status == CombatActionStatus.NoItem) { NavigationStatus("Ação recusada · nenhuma munição do calibre na bolsa"); return; }
            ApplyCombatResult(result); return;
        }
        ReloadStatus status = _campaign.Reload(_campaign.ActiveHand, _walker?.Moving == true);
        NavigationStatus(status switch
        {
            ReloadStatus.Ready => $"Arma recarregada · {HandsLine()}",
            ReloadStatus.NoWeapon => "Recarregar: a mão ativa não tem arma que use munição.",
            ReloadStatus.AlreadyLoaded => "Recarregar: a arma já está carregada.",
            ReloadStatus.NoAmmo => "Recarregar: nenhuma munição do calibre na bolsa.",
            ReloadStatus.Busy => "Recarregar: pare de andar primeiro.",
            _ => "Recarregar: indisponível agora."
        });
        RefreshBag(); RefreshDoorAction();
    }

    private void UseCombatHealing()
    {
        if (_combat?.Active is not { IsPlayer: true } player) { NavigationStatus("Aguarde: é o turno da criatura."); return; }
        ApplyCombatResult(_combat.Execute(CombatActionProposal.UseHealing(player.Id)));
    }

    private void RunEnemyTurn()
    {
        if (_combat?.Active is not { IsPlayer: false }) return;
        ApplyCombatEvents(CombatAi.TakeTurn(_combat));
    }

    private void ApplyCombatResult(CombatActionResult result)
    {
        if (result.Status != CombatActionStatus.Ready) { NavigationStatus($"Ação recusada · {Refusal(result.Status)}"); return; }
        ApplyCombatEvents(result.Events);
    }

    private void ApplyCombatEvents(IEnumerable<CombatEvent> events)
    {
        if (_combat == null || _layout == null) return;
        var lines = new List<string>();
        bool ended = false;
        _combatTweens.Clear();
        foreach (CombatEvent e in events)
            switch (e)
            {
                case CombatantMoved moved:
                    PlaceCombatant(_combat.Get(moved.CombatantId), moved.Path);
                    lines.Add($"{CombatName(moved.CombatantId)} andou {moved.Path.Length} hex(es) · {moved.ActionPointsSpent} AP"); break;
                case AttackResolved attack:
                    Lunge(_combat.Get(attack.AttackerId), _combat.Get(attack.TargetId));
                    if (attack.UnderStrength) lines.Add("Você não tem força suficiente para usar esta arma direito.");
                    lines.Add($"{CombatName(attack.AttackerId)} ataca {CombatName(attack.TargetId)}{Aimed(attack.Location, attack.AttackerId == "player")}: {attack.Roll} em {attack.Chance}% · "
                        + (attack.Hit ? $"acerto, {attack.Damage} de dano ({attack.TargetHitPoints} HP)" : "erro")); break;
                case InventoryOpened opened:
                    lines.Add($"Você abre a bolsa · {opened.ActionPointsSpent} AP"); break;
                case WeaponsChanged changed:
                    lines.Add($"Nas mãos: {changed.Blow} (ativa) · {changed.OtherHand}"); break;
                case WeaponThrown thrown:
                    if (thrown.AttackerId == "player") { _combatWeapon = NamedWeapon(_combat.Get("player").Weapon); RefreshGroundItems(); RefreshDoorAction(); }
                    lines.Add($"{CombatName(thrown.AttackerId)} arremessou {(thrown.AttackerId == "player" ? "a arma" : "")}: caiu no hex {thrown.LandingHex}"
                        + (thrown.AttackerId == "player" ? " · J ao lado para recolher" : "")); break;
                case StrayHit stray:
                    lines.Add($"O arremesso perdido atinge {CombatName(stray.TargetId)}: {stray.Damage} de dano ({stray.TargetHitPoints} HP)"); break;
                case AmmoSpent spent:
                    if (spent.CombatantId == "player") _combatWeapon = NamedWeapon(_combat.Get("player").Weapon);
                    lines.Add($"{CombatName(spent.CombatantId)} gastou {spent.Rounds} bala(s) · {spent.Left} no pente"); break;
                case WeaponReloaded reloaded:
                    if (reloaded.CombatantId == "player") _combatWeapon = NamedWeapon(_combat.Get("player").Weapon);
                    lines.Add($"{CombatName(reloaded.CombatantId)} recarregou {reloaded.RoundsLoaded} bala(s) · {reloaded.Rounds} no pente"
                        + (reloaded.ActionPointsSpent > 0 ? $" · {reloaded.ActionPointsSpent} AP" : "")); break;
                case BurstFired burst:
                    lines.Add($"Rajada de {burst.RoundsFired}: {burst.Roll} em {burst.Chance}% · {burst.MainHits} bala(s) em {CombatName(burst.TargetId)}"
                        + string.Concat(burst.Others.Select(o => $" · {o.Rounds} em {CombatName(o.TargetId)} ({o.Damage} de dano, {o.HitPoints} HP)"))); break;
                case AttackModeChanged mode:
                    lines.Add($"Modo de ataque: {NamedWeapon(_combat.Get(mode.CombatantId).Weapon).Name}"); break;
                case ArmorChanged armor:
                    lines.Add(armor.Armor == null ? $"Você tira a armadura · CA {armor.ArmorClass}" : $"Você veste a armadura · CA {armor.ArmorClass}"); break;
                case HandSwitched hand:
                    lines.Add($"Você troca para {hand.Blow} (0 AP)"); break;
                case CombatantFleeing fleeing:
                    lines.Add($"{CombatName(fleeing.CombatantId)} foge"); break;
                case CombatantDisengaged gone:
                    lines.Add($"{CombatName(gone.CombatantId)} deixa a luta com {gone.HitPoints} HP"); break;
                case HealingUsed used:
                    lines.Add($"{CombatName(used.CombatantId)} usou Healing Powder: +{used.Healed} HP ({used.HitPoints} HP)"
                        + (used.Healed < used.Rolled ? $" · {used.Rolled - used.Healed} além do máximo se perderam" : "") + $" · {used.ActionPointsSpent} AP"); break;
                case CombatantYielded yielded:
                    lines.Add($"{CombatName(yielded.CombatantId)} se rende com {yielded.HitPoints} HP"); break;
                case CombatantDied died:
                    lines.Add($"{CombatName(died.CombatantId)} morreu" + (died.KillerId == CombatSession.PoisonKiller ? " envenenado" : "")
                        + (died.Experience > 0 ? $" · +{died.Experience} XP" : "")); break;
                case PoisonChecked sting:
                    lines.Add(sting.Poisoned ? $"Ferroada: Sorte {sting.LuckRoll} acima de {sting.Needed} · veneno {sting.Rolled}, {sting.Absorbed} após resistência · nível {sting.Level}"
                        : $"Ferroada: Sorte {sting.LuckRoll} até {sting.Needed} · você resistiu ao veneno"); break;
                case PoisonDamage poisoned:
                    lines.Add($"Veneno: −{poisoned.Lost} HP ({poisoned.HitPoints} HP) · nível {poisoned.Level}"); break;
                case CriticalHit critical:
                    string effects = Conditions(critical.Flags);
                    lines.Add($"Crítico{(critical.Massive ? " maciço" : "")}! {critical.Roll} até {critical.Needed} · efeito {critical.EffectRoll} · dano {Multiplier(critical.Multiplier)}"
                        + ((critical.Flags & DamageFlags.Bypass) != 0 ? " · ignora 80% da armadura" : "") + (effects.Length > 0 ? $" · {CombatName(critical.TargetId)}: {effects}" : "")); break;
                case CriticalFailure failure when failure.Flags != DamageFlags.None:
                    string fumble = Conditions(failure.Flags);
                    lines.Add($"Falha crítica de {CombatName(failure.AttackerId)}: {failure.Roll} até {failure.Needed}" + (fumble.Length > 0 ? $" · {fumble}" : "")
                        + (failure.SelfDamage > 0 ? $" · −{failure.SelfDamage} HP ({failure.HitPoints} HP)" : "")); break;
                case TurnLost lost:
                    lines.Add($"{CombatName(lost.CombatantId)} perde o turno ({((lost.Reason & DamageFlags.KnockedOut) != 0 ? "nocauteado" : "turno perdido")})"); break;
                case StoodUp up:
                    lines.Add($"{CombatName(up.CombatantId)} se levanta · {up.ActionPointsSpent} AP"); break;
                case WokeUp woke:
                    lines.Add($"{CombatName(woke.CombatantId)} recobra a consciência"); break;
                case CombatLeft left:
                    lines.Add($"Você saiu do combate · {left.Survivors.Length} criatura(s) de pé"); break;
                case TurnStarted started when started.CombatantId != "player":
                    _combatAiTimer = 0; break;
                case CombatEnded:
                    ended = true; break;
            }
        _combatHoverKey = "";
        if (lines.Count > 0) _hud.CombatLog(string.Join("\n", lines));
        if (ended && AnimateCombat && Time.GetTicksMsec() < _combatBusyUntil) { _combatFinishPending = true; RefreshCombatView(); }
        else if (ended) FinishCombat(); else RefreshCombatView();
    }

    private Node3D? CombatNode(CombatantState combatant) => combatant.IsPlayer ? _walkerNode : _bodies.GetValueOrDefault(combatant.Id);
    private Vector3 CombatPoint(CombatantState combatant, int hex) =>
        Point(FalloutGrid.HexCenter(hex) - _layout!.Origin, combatant.IsPlayer ? 0 : Dimensions(PreviewKind.Critter).Y / 2);

    // One tween per node and event batch, so a creature walks its path and then strikes, in order.
    private Tween CombatTween(Node3D node)
    {
        if (!_combatTweens.TryGetValue(node, out Tween? tween)) _combatTweens[node] = tween = node.CreateTween();
        return tween;
    }
    private void Busy(double seconds) => _combatBusyUntil = Math.Max(_combatBusyUntil, Time.GetTicksMsec()) + (ulong)(seconds * 1000);

    private void PlaceCombatant(CombatantState combatant, int[] path)
    {
        if (combatant.IsPlayer) { _travel?.MoveTo(combatant.Hex); RefreshNpcRouteView(); }
        if (CombatNode(combatant) is not { } node) return;
        if (!AnimateCombat) { node.Position = CombatPoint(combatant, combatant.Hex); return; }
        Tween tween = CombatTween(node);
        foreach (int hex in path) tween.TweenProperty(node, "position", CombatPoint(combatant, hex), StepSeconds);
        Busy(path.Length * StepSeconds);
    }

    private void Lunge(CombatantState attacker, CombatantState target)
    {
        if (!AnimateCombat || CombatNode(attacker) is not { } node) return;
        Vector3 home = CombatPoint(attacker, attacker.Hex), toward = CombatPoint(attacker, target.Hex) - home;
        if (toward.LengthSquared() < 1e-6f) return;
        Tween tween = CombatTween(node);
        tween.TweenProperty(node, "position", home + toward.Normalized() * .35f, LungeSeconds);
        tween.TweenProperty(node, "position", home, LungeSeconds * 1.4);
        Busy(LungeSeconds * 2.4 + .15);
    }

    private void RefreshCombatView()
    {
        if (_combat == null) { _hud.Combat(false, "", "", false); return; }
        foreach (CombatantState c in _combat.Combatants)
            if (_combatLabels.TryGetValue(c.Id, out Label3D? label)) label.Text = c.Alive ? $"{c.HitPoints}/{c.Stats.MaxHitPoints} HP" : "morto";
        CombatantState player = _combat.Get("player");
        CombatTurnState turn = _combat.Turn;
        string State(CombatantState c) => Conditions(c.Conditions) is { Length: > 0 } words ? $" · {words}" : "";
        string foes = string.Join("   ", _combat.Combatants.Where(c => !c.IsPlayer).Select(c => $"{CombatName(c.Id)}: {(c.Alive ? $"{c.HitPoints}/{c.Stats.MaxHitPoints} HP · AC {c.EffectiveArmorClass}{State(c)}" : "morto")}"));
        bool mine = _combat.Active?.IsPlayer == true;
        _hud.Combat(true, $"COMBATE · Rodada {turn.Round} · {(mine ? "seu turno" : $"turno de {CombatName(turn.ActiveId!)}")}",
            $"Você: {player.HitPoints}/{player.Stats.MaxHitPoints} HP · {player.ActionPoints}/{player.Stats.MaxActionPoints} AP · AC {player.EffectiveArmorClass} · {_combatWeapon.Name}{(player.HealingDoses > 0 ? $" · Healing Powder ×{player.HealingDoses}" : "")}{(player.Poison.Active ? $" · envenenado ({player.Poison.Level})" : "")}{State(player)}\n{foes}", mine);
        if (mine && _combatHoverKey.Length == 0) _hud.CombatPreview($"Clique no chão para mover ({player.ActionPointsPerHex} AP por hex) ou na criatura para atacar. Espaço encerra o turno; Enter sai do combate longe da percepção das criaturas."
            + (player.HealingDoses > 0 ? $" H usa Healing Powder ({CombatSession.InventoryCost(player)} AP)." : "")
            + (player.AlternateWeapon != null ? $" X troca para {player.AlternateWeapon.Name}." : "")
            + (_combat.InventoryOpen ? " Bolsa aberta: trocar o que está nas mãos não custa mais AP." : $" B abre a bolsa ({CombatSession.InventoryCost(player)} AP)."));
    }

    private void FinishCombat()
    {
        CloseCalledShot();
        CombatSession fight = _combat!;
        int levelBefore = _campaign!.Level;
        _combat = null; _combatLabels.Clear(); _encounterCheckedHex = -1; _combatFinishPending = false; _combatTweens.Clear();
        CombatCommitStatus status = _campaign!.CommitCombat(fight);
        _hud.Combat(false, "", "", false);
        if (status == CombatCommitStatus.PlayerDefeated)
        {
            ResetWalker();
            NavigationStatus("Você foi derrotado · a prévia voltou ao início do Templo.");
            GD.Print("COMBAT_LOST");
            return;
        }
        if (status != CombatCommitStatus.Ready) { ResetWalker(); NavigationStatus($"Resultado do combate recusado ({status}) · prévia reiniciada."); return; }
        // The corpse and the freed hex come from WorldSession projections; rebuilding keeps every other system unchanged.
        BuildWorld();
        CombatantState player = fight.Get("player");
        if (fight.Left)
        {
            NavigationStatus($"Você saiu do combate · {fight.Combatants.Count(c => !c.IsPlayer && c.Alive)} criatura(s) de pé onde estão"
                + (fight.Turn.ExperienceAwarded > 0 ? $" · +{fight.Turn.ExperienceAwarded} XP" : "") + $" · {_campaign.HitPoints}/{_campaign.MaxHitPoints} HP{LevelUpText(levelBefore)}");
            GD.Print($"COMBAT_LEFT: {fight.Turn.ExperienceAwarded} XP, player {player.HitPoints} HP, round {fight.Turn.Round}");
            return;
        }
        string? duel = DuelOutcomeText(fight);
        NavigationStatus(duel != null ? $"{duel} · {_campaign.HitPoints}/{_campaign.MaxHitPoints} HP{LevelUpText(levelBefore)}"
            : $"Vitória · +{fight.Turn.ExperienceAwarded} XP · {_campaign.HitPoints}/{_campaign.MaxHitPoints} HP{LevelUpText(levelBefore)} · salve com K para guardar o resultado");
        GD.Print($"COMBAT_WON: {fight.Turn.ExperienceAwarded} XP, player {player.HitPoints} HP, round {fight.Turn.Round}");
    }

    // CE shows message 600 per level gained; unspent skill points and perks wait for a character screen.
    private string LevelUpText(int levelBefore)
    {
        if (_campaign == null || _campaign.Level <= levelBefore) return "";
        var gains = CharacterProgression.Gains(_campaign.Character, levelBefore, _campaign.Level);
        return $" · subiu para o nível {_campaign.Level}: +{gains.Sum(g => g.HitPoints)} HP, +{gains.Sum(g => g.SkillPoints)} pontos de perícia"
            + (gains.Any(g => g.PerkAvailable) ? ", perk disponível" : "");
    }

    private void DiscardCombat()
    {
        CloseCalledShot();
        if (_combat == null) return;
        _campaign?.AbandonCombat();
        _combat = null; _combatLabels.Clear(); _encounterCheckedHex = -1; _combatFinishPending = false; _combatTweens.Clear(); _hud.Combat(false, "", "", false);
    }

    // Presentation of fallen source critters: lying down, darker, still selectable; WorldSession frees their hex.
    private void ApplyCombatCorpses()
    {
        if (_campaign == null || _layout == null) return;
        foreach (PreviewObject obj in _layout.Objects.Where(o => o.Kind == PreviewKind.Critter && _campaign.IsDefeated(_campaign.CurrentMap, o.Source.Key)))
        {
            StaticBody3D body = _bodies[obj.Source.Key];
            var main = body.GetChild<MeshInstance3D>(0);
            main.RotationDegrees = new Vector3(0, 0, 90); main.Position = new Vector3(0, .25f - body.Position.Y, 0);
            main.MaterialOverride = EntityMaterial("5b3a33");
            body.SetMeta("defeated", true);
        }
    }

    private async Task CheckCombat()
    {
        _checkingNavigation = true;
        bool encounters = _combatEncounters, grid = _showNavigationGrid;
        try
        {
            ResetWalker(); ToggleWalkMode(); SetNavigationGrid(false);
            string checkpointBefore = File.Exists(CheckpointPath) ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(CheckpointPath))) : "";
            // The first door opens the way to the ants of the first chambers.
            RequestWalk(24508); CompleteReadabilityWalk(); UseDoor(_layout!.Objects.Single(o => o.Source.Offset == 244040).Source.Key);
            int xpBefore = _campaign!.Experience;
            if (_campaign.PlayerWeapon().Pid != null) throw new InvalidDataException("An empty bag must fight bare-handed.");
            // The only weapon of the temple is the spear in a container behind the locked door; carry it, and the
            // Healing Powder ID 106 of pot 206348, as if collected.
            var spear = _campaign.Current.Map.ObjectLayers.SelectMany(l => l.Objects)
                .SelectMany(o => o.Inventory.Select(e => (Owner: o, Item: e.Object))).Single(p => p.Item.Header.Pid == 7);
            MapObject pot = _campaign.Current.Items.Containers.Single(o => o.Offset == 206348);
            MapObject powder = pot.Inventory.Select(e => e.Object).Single(i => i.Header.Pid == HealingDrugProfile.Pid && i.Header.Id == 106);
            _campaign.Current.Items.Restore(new[] { new TakenItem(spear.Owner.Key, spear.Item.Key, 1), new TakenItem(pot.Key, powder.Key, 1) });
            if (_campaign.HealingDoses != 1) throw new InvalidDataException("The original Healing Powder is not a supported dose.");
            // Carrying is not wielding (FO2's hand slots): the bag puts the spear into the left hand.
            if (_campaign.PlayerWeapon().Pid != null) throw new InvalidDataException("A carried spear was wielded without the bag.");
            OpenBag();
            if (_bag?.Select(_campaign.Carried.Single(c => c.Source.Key == spear.Item.Key)) != true) throw new InvalidDataException("The spear is missing from the bag.");
            ((Button)_bag.FindChild("WieldLeftAction", true, false)).EmitSignal(Button.SignalName.Pressed);
            CloseBag();
            if (_campaign.Held(Hand.Left)?.ItemKey != spear.Item.Key || _campaign.PlayerWeapon().Pid != 7 || !HandsLine().Contains("(ativa)"))
                throw new InvalidDataException("The bag did not put the spear into the left hand.");
            var ants = _layout.Objects.Where(o => o.Source.Header.Pid == 0x0100017E && o.Source.Header.Elevation == 0).ToArray();
            // Walk toward the first reachable ant with encounters on: the actor stops on a whole hex once the ant sees it.
            int target = -1;
            foreach (PreviewObject candidate in ants.OrderBy(o => o.Source.Offset))
            {
                target = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(candidate.Source.Header.Tile, d) ?? -1)
                    .Where(t => t >= 0 && _reachable!.Distance(t) > 5).OrderBy(t => _reachable!.Distance(t)).FirstOrDefault(-1);
                if (target >= 0) break;
            }
            if (target < 0) throw new InvalidDataException("No reachable approach to an elevation-0 ant.");
            _combatEncounters = true; _combatSeed = 2026;
            if (RequestWalk(target).Status != RouteStatus.Ready) throw new InvalidDataException("Approach route unavailable.");
            for (int i = 0; i < 400 && !InCombat; i++)
            {
                if (_walker!.Moving) { _checkingNavigation = false; AdvanceWalker(.2); _checkingNavigation = true; }
                UpdateCombat(0);
                if (!InCombat && !_walker.Moving) throw new InvalidDataException("Walk ended without an encounter.");
            }
            if (_combat == null) throw new InvalidDataException("Approaching the ant did not start combat.");
            PreviewObject ant = _layout!.Objects.Single(o => o.Source.Key == _combat.Turn.Order[0]);
            int perception = CritterSight.Perception(CritterPrototypeData.Read(_campaign.Current.Map.Prototypes.Single(p => p.Pid == ant.Source.Header.Pid)));
            if (_walker!.Moving || !WorldSession.EncounterPids.Contains(ant.Source.Header.Pid) || _combat.Get("player").Hex != _campaign.Location.Hex
                || !CritterSight.Sees(ant.Source, perception, _campaign.Location.Hex, _campaign.Current.Doors.Navigation(_campaign.Location.Elevation)))
                throw new InvalidDataException("Combat started mid-step, unseen or with the wrong initiator.");
            // Every creature that saw the actor joins (each one's critter_p_proc attacks on sight).
            string[] foes = _combat.Combatants.Where(c => !c.IsPlayer).Select(c => c.Id).ToArray();
            // Their scripts' AI packets drive them (zclrat Rat 7, zclscorp Scorpion 8).
            if (_combat.Combatants.Where(c => !c.IsPlayer).Any(c => c.Ai?.Number != WorldSession.ScriptPackets[c.Pid!.Value]))
                throw new InvalidDataException("A creature in the fight lacks its script's ai.txt packet.");
            var hud = (Control)_hud.FindChild("CombatBar", true, false);
            if (!hud.Visible || _combatLabels.Count != _combat.Combatants.Count) throw new InvalidDataException("Combat HUD or HP labels missing.");
            if (_capture != null)
            {
                _focus = (_walkerNode!.Position + _bodies[ant.Source.Key].Position) / 2; _camera.Size = 16; UpdateCamera();
                await Capture(Path.ChangeExtension(_capture, "combat-start.png"));
            }
            // World actions wait for the fight; refused actions leave the session unchanged.
            int hexBefore = _campaign.Location.Hex;
            SaveCheckpoint(); UseExit(); ToggleWalkMode(); SetArchitecture(!_architectureEnabled);
            if (RequestWalk(28906).Status != RouteStatus.Busy || TryInspectionElevation(1) || _campaign.Location.Hex != hexBefore || !InCombat
                || checkpointBefore != (File.Exists(CheckpointPath) ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(CheckpointPath))) : ""))
                throw new InvalidDataException("World actions or a mid-fight save leaked into combat.");
            // One animated creature turn: the next action waits for the tweens, which end on the logical hex.
            if (_combat.Active is { IsPlayer: false } mover)
            {
                _forceCombatAnimation = true;
                Node3D node = _bodies[mover.Id];
                UpdateCombat(EnemyTurnDelay);
                bool waited = Time.GetTicksMsec() < _combatBusyUntil;
                CombatantState? nextActor = _combat?.Active;
                UpdateCombat(EnemyTurnDelay);
                if (!waited || _combat != null && _combat.Active != nextActor) throw new InvalidDataException("Creature turn did not wait for its animation.");
                for (int frame = 0; frame < 240 && Time.GetTicksMsec() < _combatBusyUntil; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                for (int frame = 0; frame < 3; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (node.Position.DistanceTo(CombatPoint(mover, mover.Hex)) > .01f) throw new InvalidDataException("Animated creature did not end on its logical hex.");
                _forceCombatAnimation = false;
                if (_combatFinishPending) UpdateCombat(0);
            }
            int rounds = 0; bool healed = false;
            for (int guard = 0; guard < 300 && InCombat; guard++)
            {
                if (_combat!.Active is { IsPlayer: false }) { UpdateCombat(EnemyTurnDelay); continue; }
                CombatantState player = _combat.Get("player"), foe = _combat.Combatants.First(c => !c.IsPlayer && c.Alive);
                if (!healed)
                {
                    // Enter is refused while the creatures beside the player still perceive it (combatAttemptEnd).
                    using (var enter = new InputEventKey { Pressed = true, PhysicalKeycode = Key.Enter }) _UnhandledInput(enter);
                    if (!InCombat || _combat.Left || !_combat.AwareEnemies(player).Any()) throw new InvalidDataException("Enter left a fight the creatures still perceive.");
                    // H takes the dose for 4 AP; a second dose this turn is refused for lack of AP and of powder.
                    int apBefore = player.ActionPoints; healed = true;
                    using var heal = new InputEventKey { Pressed = true, PhysicalKeycode = Key.H }; _UnhandledInput(heal);
                    if (player.ActionPoints != apBefore - CombatSession.InventoryActionPoints || player.HealingDoses != 0 || _combat.Log.OfType<HealingUsed>().Count() != 1
                        || _campaign.HealingDoses != 1) throw new InvalidDataException("H did not take one dose for 4 AP inside the fight only.");
                    _UnhandledInput(heal);
                    if (player.ActionPoints != apBefore - CombatSession.InventoryActionPoints || _combat.Log.OfType<HealingUsed>().Count() != 1)
                        throw new InvalidDataException("A refused second dose changed the fight.");
                    continue;
                }
                Vector3 foePoint = _bodies[foe.Id].GlobalPosition + Vector3.Up * .2f;
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                CombatActionPreview attack = _combat.Preview(CombatActionProposal.Attack("player", foe.Id));
                if (attack.Status == CombatActionStatus.Ready)
                {
                    _combatHoverKey = ""; CombatHoverAtScreen(_camera.UnprojectPosition(foePoint));
                    if (!_hud.CombatPreviewText.Contains($"{attack.ToHit!.Chance}%")) throw new InvalidDataException("Hover preview differs from the rules: " + _hud.CombatPreviewText);
                    CombatClickAtScreen(_camera.UnprojectPosition(foePoint));
                }
                else if (attack.Status == CombatActionStatus.OutOfRange && player.StepsAffordable(1) > 0
                    && _combat.Movement.Path(player, h => FalloutGrid.Distance(h, foe.Hex) <= _combatWeapon.Range) is { Length: > 0 } path)
                    ApplyCombatResult(_combat.Execute(CombatActionProposal.Move("player", path[player.StepsAffordable(path.Length) - 1])));
                else { using var space = new InputEventKey { Pressed = true, PhysicalKeycode = Key.Space }; _UnhandledInput(space); rounds++; }
                if (_combat != null && _walkerNode!.Position.DistanceTo(Point(FalloutGrid.HexCenter(_combat.Get("player").Hex) - _layout!.Origin, 0)) > .001f)
                    throw new InvalidDataException("Actor marker does not follow the combat position.");
            }
            if (InCombat) throw new InvalidDataException("Seeded fight did not end.");
            if (!foes.All(f => _campaign.IsDefeated(_campaign.CurrentMap, f))) throw new InvalidDataException("Seeded fight was lost; expected the documented victory.");
            if (_combatWeapon.Pid != 7 || !_combatWeapon.Name.Contains("Spear", StringComparison.OrdinalIgnoreCase) || _combatWeapon.Range != 2)
                throw new InvalidDataException($"The carried spear was not used ({_combatWeapon.Name}).");
            var sources = _campaign.Current.Map.ObjectLayers.SelectMany(l => l.Objects).Where(o => foes.Contains(o.Key)).ToArray();
            int reward = sources.Sum(o => CritterPrototypeData.Read(_campaign.Current.Map.Prototypes.Single(p => p.Pid == o.Header.Pid)).Experience);
            if (_campaign.Experience != xpBefore + reward || _campaign.CombatExperience != reward) throw new InvalidDataException("Creature XP was not awarded once.");
            if (_campaign.HealingDoses != 0 || _campaign.Current.Items.Balance(pot.Key, powder.Key) is not { Consumed: 1, Carried: 0 } || _campaign.PlayerWeapon().Pid != 7)
                throw new InvalidDataException("The dose taken in the won fight was not used up, or the spear went with it.");
            MapObject corpse = _campaign.EffectiveObject(_campaign.CurrentMap, _campaign.Current.Map.ObjectLayers.SelectMany(l => l.Objects).Single(o => o.Key == ant.Source.Key));
            if (!_navigation!.Walkable(corpse.Header.Tile) || !_bodies[ant.Source.Key].HasMeta("defeated")
                || _bodies[ant.Source.Key].Position.DistanceTo(Point(FalloutGrid.HexCenter(corpse.Header.Tile) - _layout!.Origin, Dimensions(PreviewKind.Critter).Y / 2)) > .001f)
                throw new InvalidDataException("Corpse is not shown where it fell or still blocks.");
            if (_hud.FindChild("CombatBar", true, false) is Control { Visible: true }) throw new InvalidDataException("Combat HUD stayed open.");
            if (_capture != null) { _focus = _walkerNode!.Position; _camera.Size = 18; UpdateCamera(); await Capture(Path.ChangeExtension(_capture, "combat-victory.png")); }
            // Checkpoint v11 keeps the outcome: save, reset (the ant returns), load (the corpse, XP and used powder return).
            int corpseHex = corpse.Header.Tile; int? hitPoints = _campaign.PlayerHitPoints;
            SaveCheckpoint();
            WorldCheckpoint saved = CheckpointFile.Read(CheckpointPath);
            if (saved.Version != 13 || saved.Combat is not { } combat || combat.Experience != reward || combat.PlayerHitPoints != hitPoints
                || !combat.Defeated.Select(d => d.Key).OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(foes.OrderBy(k => k, StringComparer.Ordinal))
                || combat.Defeated.Single(d => d.Key == ant.Source.Key).Hex != corpseHex
                || saved.Areas.Single(a => a.Map == _campaign.CurrentMap).Consumed?.SingleOrDefault() != new ConsumedItem(pot.Key, powder.Key, 1))
                throw new InvalidDataException("Checkpoint v11 did not record the won fight or the used powder.");
            ResetWalker();
            if (_campaign.HasCombatOutcome || foes.Any(f => _campaign.IsDefeated(_campaign.CurrentMap, f)) || InCombat) throw new InvalidDataException("Reset kept the combat outcome.");
            LoadCheckpoint();
            if (!foes.All(f => _campaign.IsDefeated(_campaign.CurrentMap, f)) || _campaign.Experience != xpBefore + reward || _campaign.PlayerHitPoints != hitPoints
                || !_bodies[ant.Source.Key].HasMeta("defeated") || !_navigation!.Walkable(corpseHex) || InCombat || _campaign.HealingDoses != 0 || _campaign.PlayerWeapon().Pid != 7)
                throw new InvalidDataException("Loading checkpoint v11 lost the corpse, XP, HP or the used powder.");
            ResetWalker();
            MapLayoutWriter.Verify(_map!);
            GD.Print($"COMBAT_SLICE_OK: PID 0x{ant.Source.Header.Pid:X8} {ant.Source.Key} saw the actor at hex {hexBefore} ({foes.Length} creature(s) joined, driven by their ai.txt packets), spear put into the left hand from the bag, Healing Powder taken with H for 4 AP and used up on commit, animated creature turn awaited, seeded fight won in {rounds + 1}+ player turns, {reward} XP once, corpse freed its hex, world actions and mid-fight save refused, checkpoint v11 save/reset/load round trip; source bytes verified.");
        }
        finally { _forceCombatAnimation = false; _combatEncounters = encounters; _combatSeed = null; DiscardCombat(); SetNavigationGrid(grid); _checkingNavigation = false; }
    }
}
