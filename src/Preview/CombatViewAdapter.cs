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
/// The outcome lives in WorldSession memory: saving is refused until the checkpoint schema can hold it (CONT-003).
/// </summary>
public partial class TemplePreview
{
    private CombatSession? _combat;
    private CombatWeapon _combatWeapon = CombatWeapon.Unarmed();
    private bool _combatEncounters = true;
    private ulong? _combatSeed;
    private double _combatAiTimer;
    private int _encounterCheckedHex = -1;
    private string _combatHoverKey = "";
    private readonly Dictionary<string, Label3D> _combatLabels = new();
    private const double EnemyTurnDelay = .45;
    private bool InCombat => _combat != null;

    private void LoadCombatWeapon(PrototypeCatalog prototypes)
    {
        // The Chosen One starts with the spear (PID 7); the original inventory/equipment model is not wired yet.
        try { _combatWeapon = CombatWeapon.Primary("Lança", WeaponPrototypeData.Read(prototypes.Get(7))); }
        catch (Exception error) when (error is IOException or InvalidDataException) { _combatWeapon = CombatWeapon.Unarmed(); GD.Print("COMBAT_WEAPON_UNAVAILABLE: " + error.Message); }
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
        _combat = _campaign!.BeginCombat(encounter, _combatWeapon, new SeededCombatRandom(_combatSeed ?? (ulong)DateTime.UtcNow.Ticks));
        _combatAiTimer = 0; _combatHoverKey = "";
        foreach (CombatantState combatant in _combat.Combatants)
        {
            Node3D? host = combatant.IsPlayer ? _walkerNode : _bodies.GetValueOrDefault(combatant.Id);
            if (host == null) continue;
            var label = new Label3D { Name = "CombatHitPoints", Position = new Vector3(0, combatant.IsPlayer ? 1.55f : 1.15f, 0), FontSize = 34, PixelSize = .012f,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, Modulate = new Color(combatant.IsPlayer ? "8fe3f0" : "f08f7a") };
            host.AddChild(label); _combatLabels[combatant.Id] = label;
        }
        RefreshCombatView();
        NavigationStatus($"{CombatName(encounter.InitiatorKey)} percebeu você · combate iniciado");
        GD.Print($"COMBAT_STARTED: {string.Join(",", _combat.Turn.Order)} at hex {_campaign.Location.Hex}");
    }

    private string CombatName(string id)
    {
        if (id == "player") return "Você";
        PreviewObject? obj = _layout?.Objects.FirstOrDefault(o => o.Source.Key == id);
        return obj != null && OriginalText(obj.Source)?.Name is { } name ? ShortName(name) : "Criatura";
    }

    private static string Refusal(CombatActionStatus status) => status switch
    {
        CombatActionStatus.NotEnoughActionPoints => "AP insuficientes", CombatActionStatus.OutOfRange => "fora de alcance",
        CombatActionStatus.Unreachable => "sem caminho livre", CombatActionStatus.NotActive => "não é o seu turno",
        CombatActionStatus.AlreadyThere => "você já está aqui", CombatActionStatus.CombatOver => "o combate terminou",
        CombatActionStatus.OtherElevation => "outra elevação", _ => "alvo inválido"
    };

    // Screen proximity rather than a physics ray: tall kit walls must not swallow a click on a creature behind them.
    private CombatantState? EnemyAtScreen(Vector2 screen) => _combat?.Combatants
        .Where(c => !c.IsPlayer && c.Alive && _bodies.ContainsKey(c.Id) && !_camera.IsPositionBehind(_bodies[c.Id].GlobalPosition))
        .Select(c => (Foe: c, Distance: _camera.UnprojectPosition(_bodies[c.Id].GlobalPosition).DistanceTo(screen)))
        .Where(p => p.Distance <= 40).OrderBy(p => p.Distance).Select(p => p.Foe).FirstOrDefault();

    private string AttackSummary(CombatActionPreview preview, CombatantState target) =>
        $"{_combatWeapon.Name} → {CombatName(target.Id)}: {preview.ToHit!.Chance}% de acerto · dano {preview.Damage!.MinDamage}–{preview.Damage.MaxDamage} · {preview.ActionPointCost} AP"
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
                + $"\nAcerto: perícia {attack.ToHit!.Skill} − AC {attack.ToHit.TargetArmorClass}{(attack.ToHit.DistanceModifier != 0 ? $" {attack.ToHit.DistanceModifier:+0;−0} distância" : "")}{(attack.ToHit.StrengthPenalty > 0 ? $" − {attack.ToHit.StrengthPenalty} força" : "")} = {attack.ToHit.Raw}"
                + $" · DT {attack.Damage!.Threshold} · DR {attack.Damage.Resistance}%";
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
        else { key = "none"; if (key == _combatHoverKey) return; text = "Clique no chão para mover (1 AP por hex) ou na criatura para atacar."; }
        _combatHoverKey = key; _hud.CombatPreview(text);
    }

    private bool HandleCombatInput(InputEvent input)
    {
        if (_combat == null) return false;
        if (input is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.PhysicalKeycode == Key.Space) { EndCombatTurn(); return true; }
            // Camera, view and inspection keys stay available; anything that would act in the world waits.
            if (key.PhysicalKeycode is Key.Q or Key.E or Key.C or Key.Home or Key.V or Key.G or Key.O or Key.F or Key.R) return false;
            if (key.PhysicalKeycode is Key.M or Key.U or Key.L or Key.Enter or Key.KpEnter or Key.K or Key.J or Key.P or Key.Escape or Key.T)
            { CombatRefuses("essa ação"); return true; }
            return false;
        }
        if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouse && !_hud.MouseOverPanel)
        { CombatClickAtScreen(mouse.Position); return true; }
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

    private void EndCombatTurn()
    {
        if (_combat?.Active is not { IsPlayer: true } player) return;
        ApplyCombatResult(_combat.Execute(CombatActionProposal.EndTurn(player.Id)));
    }

    private void RunEnemyTurn()
    {
        if (_combat?.Active is not { IsPlayer: false }) return;
        ApplyCombatEvents(ProvisionalMeleeBehavior.TakeTurn(_combat));
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
        foreach (CombatEvent e in events)
            switch (e)
            {
                case CombatantMoved moved:
                    PlaceCombatant(_combat.Get(moved.CombatantId));
                    lines.Add($"{CombatName(moved.CombatantId)} andou {moved.Path.Length} hex(es) · {moved.ActionPointsSpent} AP"); break;
                case AttackResolved attack:
                    lines.Add($"{CombatName(attack.AttackerId)} ataca {CombatName(attack.TargetId)}: {attack.Roll} em {attack.Chance}% · "
                        + (attack.Hit ? $"acerto, {attack.Damage} de dano ({attack.TargetHitPoints} HP)" : "erro")); break;
                case CombatantDied died:
                    lines.Add($"{CombatName(died.CombatantId)} morreu" + (died.Experience > 0 ? $" · +{died.Experience} XP" : "")); break;
                case TurnStarted started when started.CombatantId != "player":
                    _combatAiTimer = 0; break;
                case CombatEnded:
                    ended = true; break;
            }
        _combatHoverKey = "";
        if (lines.Count > 0) _hud.CombatLog(string.Join("\n", lines));
        if (ended) FinishCombat(); else RefreshCombatView();
    }

    private void PlaceCombatant(CombatantState combatant)
    {
        Vector3 position = Point(FalloutGrid.HexCenter(combatant.Hex) - _layout!.Origin, 0);
        if (combatant.IsPlayer)
        {
            _travel?.MoveTo(combatant.Hex);
            if (_walkerNode != null) _walkerNode.Position = position;
            RefreshNpcRouteView();
        }
        else if (_bodies.TryGetValue(combatant.Id, out StaticBody3D? body)) body.Position = position + Vector3.Up * Dimensions(PreviewKind.Critter).Y / 2;
    }

    private void RefreshCombatView()
    {
        if (_combat == null) { _hud.Combat(false, "", "", false); return; }
        foreach (CombatantState c in _combat.Combatants)
            if (_combatLabels.TryGetValue(c.Id, out Label3D? label)) label.Text = c.Alive ? $"{c.HitPoints}/{c.Stats.MaxHitPoints} HP" : "morto";
        CombatantState player = _combat.Get("player");
        CombatTurnState turn = _combat.Turn;
        string foes = string.Join("   ", _combat.Combatants.Where(c => !c.IsPlayer).Select(c => $"{CombatName(c.Id)}: {(c.Alive ? $"{c.HitPoints}/{c.Stats.MaxHitPoints} HP · AC {c.EffectiveArmorClass}" : "morto")}"));
        bool mine = _combat.Active?.IsPlayer == true;
        _hud.Combat(true, $"COMBATE · Rodada {turn.Round} · {(mine ? "seu turno" : $"turno de {CombatName(turn.ActiveId!)}")}",
            $"Você: {player.HitPoints}/{player.Stats.MaxHitPoints} HP · {player.ActionPoints}/{player.Stats.MaxActionPoints} AP · AC {player.EffectiveArmorClass} · {_combatWeapon.Name}\n{foes}", mine);
        if (mine && _combatHoverKey.Length == 0) _hud.CombatPreview("Clique no chão para mover (1 AP por hex) ou na criatura para atacar. Espaço encerra o turno.");
    }

    private void FinishCombat()
    {
        CombatSession fight = _combat!;
        _combat = null; _combatLabels.Clear(); _encounterCheckedHex = -1;
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
        NavigationStatus($"Vitória · +{fight.Turn.ExperienceAwarded} XP · {player.HitPoints}/{player.Stats.MaxHitPoints} HP · este resultado ainda não pode ser salvo (checkpoint v10 pendente)");
        GD.Print($"COMBAT_WON: {fight.Turn.ExperienceAwarded} XP, player {player.HitPoints} HP, round {fight.Turn.Round}");
    }

    private void DiscardCombat()
    {
        if (_combat == null) return;
        _combat = null; _combatLabels.Clear(); _encounterCheckedHex = -1; _hud.Combat(false, "", "", false);
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
            var ants = _layout.Objects.Where(o => o.Source.Header.Pid == 0x0100017E && o.Source.Header.Elevation == 0).ToArray();
            // Walk toward the first reachable ant with encounters on: the actor stops on a whole hex once noticed.
            int target = -1;
            foreach (PreviewObject candidate in ants.OrderBy(o => o.Source.Offset))
            {
                target = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(candidate.Source.Header.Tile, d) ?? -1)
                    .Where(t => t >= 0 && _reachable!.Distance(t) > WorldSession.EncounterDistance).OrderBy(t => _reachable!.Distance(t)).FirstOrDefault(-1);
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
            if (_walker!.Moving || ant.Source.Header.Pid != 0x0100017E || FalloutGrid.Distance(_campaign.Location.Hex, ant.Source.Header.Tile) > WorldSession.EncounterDistance
                || _combat.Get("player").Hex != _campaign.Location.Hex) throw new InvalidDataException("Combat started mid-step, out of range or with the wrong initiator.");
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
            if (RequestWalk(28906).Status != RouteStatus.Busy || TryInspectionElevation(1) || _campaign.Location.Hex != hexBefore || !InCombat)
                throw new InvalidDataException("World actions leaked into combat.");
            int rounds = 0;
            for (int guard = 0; guard < 300 && InCombat; guard++)
            {
                if (_combat!.Active is { IsPlayer: false }) { UpdateCombat(EnemyTurnDelay); continue; }
                CombatantState player = _combat.Get("player"), foe = _combat.Combatants.First(c => !c.IsPlayer && c.Alive);
                Vector3 foePoint = _bodies[foe.Id].GlobalPosition + Vector3.Up * .2f;
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                CombatActionPreview attack = _combat.Preview(CombatActionProposal.Attack("player", foe.Id));
                if (attack.Status == CombatActionStatus.Ready)
                {
                    _combatHoverKey = ""; CombatHoverAtScreen(_camera.UnprojectPosition(foePoint));
                    if (!_hud.CombatPreviewText.Contains($"{attack.ToHit!.Chance}%")) throw new InvalidDataException("Hover preview differs from the rules: " + _hud.CombatPreviewText);
                    CombatClickAtScreen(_camera.UnprojectPosition(foePoint));
                }
                else if (attack.Status == CombatActionStatus.OutOfRange && player.ActionPoints > 0
                    && _combat.Movement.Path(player, h => FalloutGrid.Distance(h, foe.Hex) <= _combatWeapon.Range) is { Length: > 0 } path)
                    ApplyCombatResult(_combat.Execute(CombatActionProposal.Move("player", path[Math.Min(path.Length, player.ActionPoints) - 1])));
                else { using var space = new InputEventKey { Pressed = true, PhysicalKeycode = Key.Space }; _UnhandledInput(space); rounds++; }
                if (_combat != null && _walkerNode!.Position.DistanceTo(Point(FalloutGrid.HexCenter(_combat.Get("player").Hex) - _layout!.Origin, 0)) > .001f)
                    throw new InvalidDataException("Actor marker does not follow the combat position.");
            }
            if (InCombat) throw new InvalidDataException("Seeded fight did not end.");
            if (!_campaign.IsDefeated(_campaign.CurrentMap, ant.Source.Key)) throw new InvalidDataException("Seeded fight was lost; expected the documented victory.");
            if (_campaign.Experience != xpBefore + 25 || _campaign.CombatExperience != 25) throw new InvalidDataException("Ant XP was not awarded once.");
            MapObject corpse = _campaign.EffectiveObject(_campaign.CurrentMap, _campaign.Current.Map.ObjectLayers.SelectMany(l => l.Objects).Single(o => o.Key == ant.Source.Key));
            if (!_navigation!.Walkable(corpse.Header.Tile) || !_bodies[ant.Source.Key].HasMeta("defeated")
                || _bodies[ant.Source.Key].Position.DistanceTo(Point(FalloutGrid.HexCenter(corpse.Header.Tile) - _layout!.Origin, Dimensions(PreviewKind.Critter).Y / 2)) > .001f)
                throw new InvalidDataException("Corpse is not shown where it fell or still blocks.");
            if (_hud.FindChild("CombatBar", true, false) is Control { Visible: true }) throw new InvalidDataException("Combat HUD stayed open.");
            if (_capture != null) { _focus = _walkerNode!.Position; _camera.Size = 18; UpdateCamera(); await Capture(Path.ChangeExtension(_capture, "combat-victory.png")); }
            SaveCheckpoint();
            string checkpointAfter = File.Exists(CheckpointPath) ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(CheckpointPath))) : "";
            if (checkpointBefore != checkpointAfter || !_campaign.CombatOutcomeUnsaved) throw new InvalidDataException("An unsavable combat outcome was written to the checkpoint.");
            ResetWalker();
            if (_campaign.CombatOutcomeUnsaved || _campaign.IsDefeated(_campaign.CurrentMap, ant.Source.Key) || InCombat) throw new InvalidDataException("Reset kept the combat outcome.");
            MapLayoutWriter.Verify(_map!);
            GD.Print($"COMBAT_SLICE_OK: Giant Ant {ant.Source.Key} noticed the actor, seeded fight won in {rounds + 1}+ player turns, 25 XP once, corpse freed its hex, world actions and save refused, reset restored the ant; source bytes verified.");
        }
        finally { _combatEncounters = encounters; _combatSeed = null; DiscardCombat(); SetNavigationGrid(grid); _checkingNavigation = false; }
    }
}
