using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    // Same policy as the scene check below: strike Cameron while the attack is ready, else end the turn.
    private static void PlayDuelTurn(CombatSession fight)
    {
        if (!fight.Active!.IsPlayer) { ProvisionalMeleeBehavior.TakeTurn(fight); return; }
        CombatantState foe = fight.Combatants.Single(c => !c.IsPlayer);
        fight.Execute(fight.Preview(CombatActionProposal.Attack("player", foe.Id)).Status == CombatActionStatus.Ready
            ? CombatActionProposal.Attack("player", foe.Id) : CombatActionProposal.EndTurn("player"));
    }

    /// <summary>
    /// From beside Cameron (hex 13928), as Narg (the neutral profile wins none of 200 seeded challenges): the challenge
    /// through the HUD, a seeded fight Cameron yields, his departure and the chest. The world is restored afterwards.
    /// </summary>
    private async Task CheckTempleDuel()
    {
        string catalogHash = _mapCatalog!.Source.Sha256;
        WorldCheckpoint baseline = _campaign!.Capture(catalogHash);
        PlayerCharacter narg = _premadeChoices.Single(p => p.Character.Name == "Narg").Character;
        // The same world with Narg's original sheet; checkpoint v12 verifies it against premade/combat.gcd.
        WorldCheckpoint start = baseline with { Character = narg.Capture() };
        CheckpointFile.Write(CheckpointPath, start); LoadCheckpoint();
        if (_campaign!.Character.Name != "Narg" || _campaign.Location != baseline.Areas.Single(a => a.Map == baseline.CurrentMap).Location)
            throw new InvalidDataException("Narg did not take the neutral profile's place.");
        var data = new GameData(_directory!); var catalog = new MapCatalog(data.Read("data/maps.txt"));
        var prototypes = new PrototypeCatalog(new ResourceCatalog(data.Read));
        WorldSession Copy() => WorldSession.Restore(id => catalog.Load(data, id), new[] { 3, 4, 126 }, start, catalogHash, runtimePrototype: prototypes.Get,
            templeProfile: map => TempleNpcDepartureProfile.Create(map, data.Read), arroyoProfile: map => ArroyoRewardProfile.Create(map, data.Read),
            premade: path => CharacterFile.Read(data.Read(path)));
        // The first seed whose fight Cameron yields, found on a copy of this very world with the same turn policy.
        TempleDuelOutcome Simulate(ulong seed)
        {
            WorldSession copy = Copy();
            CombatSession fight = copy.BeginTempleDuel(TempleNpcDepartureProfile.Create(copy.Current.Map, data.Read), new SeededCombatRandom(seed));
            for (int guard = 0; guard < 400 && !fight.Turn.Over; guard++) PlayDuelTurn(fight);
            return fight.Turn.Over && copy.CommitCombat(fight) == CombatCommitStatus.Ready ? copy.TempleDuel : TempleDuelOutcome.None;
        }
        ulong seed = Enumerable.Range(1, 200).Select(i => (ulong)i).FirstOrDefault(s => Simulate(s) == TempleDuelOutcome.Yielded);
        if (seed == 0) throw new InvalidDataException("No seed among 200 lets Narg win the challenge.");
        bool checking = _checkingNavigation; _checkingNavigation = true; ulong? previousSeed = _combatSeed;
        try
        {
            var actor = _layout!.Objects.Single(o => o.Source.Offset == 425776); SelectObject(actor);
            var duel = (Button)_hud.FindChild("TempleDuelAction", true, false);
            if (!duel.Visible || duel.Disabled) throw new InvalidDataException("Challenge unavailable beside Cameron: " + duel.TooltipText);
            int carried = (int)_campaign.CarriedCount;
            _combatSeed = seed; duel.EmitSignal(Button.SignalName.Pressed);
            if (_combat == null || _combat.Active?.Id != actor.Source.Key || _campaign.CarriedCount != 0 || _campaign.TempleChestCustody.Sum(c => c.Quantity) != carried
                || _doors!.Get(_doors.InitialTempleGateKey!) is not { IsLocked: true, IsOpen: false } || _combatWeapon.AttackType != WeaponAttackType.Unarmed
                || _hud.FindChild("CombatBar", true, false) is not Control { Visible: true })
                throw new InvalidDataException("The challenge did not store the equipment, lock the gate and open the fight with Cameron first.");
            if (_capture != null) { _focus = Point(actor.Position, 0); _camera.Size = 14; UpdateCamera(); await Capture(Path.ChangeExtension(_capture, "duel-start.png")); }
            for (int guard = 0; guard < 400 && InCombat; guard++)
            {
                if (_combat!.Active is { IsPlayer: false }) { UpdateCombat(EnemyTurnDelay); continue; }
                CombatantState foe = _combat.Combatants.Single(c => !c.IsPlayer);
                if (_combat.Preview(CombatActionProposal.Attack("player", foe.Id)).Status == CombatActionStatus.Ready)
                    ApplyCombatResult(_combat.Execute(CombatActionProposal.Attack("player", foe.Id)));
                else { using var space = new InputEventKey { Pressed = true, PhysicalKeycode = Key.Space }; _UnhandledInput(space); }
            }
            if (InCombat || _campaign.TempleDuel != TempleDuelOutcome.Yielded || _campaign.TempleTrial != TempleTrialPath.Duel || _campaign.IsDefeated(3, actor.Source.Key)
                || _campaign.TempleChestCustody.Length == 0)
                throw new InvalidDataException($"Seed {seed} did not end with Cameron yielding in the scene ({_campaign.TempleDuel}).");
            // Cameron walks out as after Speech; the actor steps aside whenever it stands in his way.
            for (int guard = 0; guard < 600; guard++)
            {
                var status = _campaign.StepTempleNpc(_campaign.TempleToken, _campaign.TempleRevision, false);
                if (status == TempleWorldStatus.Complete) break;
                if (status == TempleWorldStatus.NotWalking) { _campaign.AdvanceTempleNpcTime(20, _campaign.TempleToken, _campaign.TempleRevision, false); continue; }
                if (status is TempleWorldStatus.Occupied or TempleWorldStatus.Blocked)
                {
                    int aside = Enumerable.Range(0, 40000).Where(t => _reachable!.Distance(t) is >= 3 and <= 6 && !_navigation!.IsExit(t)).OrderBy(_reachable!.Distance).ThenBy(t => t).First();
                    RequestWalk(aside); CompleteReadabilityWalk(); continue;
                }
                if (status != TempleWorldStatus.Ready) throw new InvalidDataException("Cameron's departure stopped: " + status);
            }
            BuildWorld();
            // J beside the challenge chest gives everything back.
            var chest = _layout!.Objects.Single(o => o.Source.Offset == WorldSession.TempleChestOffset);
            RequestWalk(ContainerApproach(chest.Source) ?? throw new InvalidDataException("No route to the challenge chest after the departure."));
            CompleteReadabilityWalk(); SelectObject(chest);
            using (var loot = new InputEventKey { Pressed = true, PhysicalKeycode = Key.J }) _UnhandledInput(loot);
            if (_campaign.CarriedCount != carried || _campaign.TempleChestCustody.Length != 0 || _loot != null)
                throw new InvalidDataException("J at the challenge chest did not return the equipment.");
            if (_capture != null) { _focus = Point(chest.Position, 0); _camera.Size = 14; UpdateCamera(); await Capture(Path.ChangeExtension(_capture, "duel-chest.png")); }
            GD.Print($"TEMPLE_DUEL_OK: Narg beside Cameron, challenge button, {carried} unit(s) into the chest, gate closed and locked, Cameron first, seeded fight {seed} yielded below half HP (G10=1), departure completed, J at the chest returned the equipment; source bytes verified.");
            foreach (WorldArea area in _campaign.Areas) MapLayoutWriter.Verify(area.Map);
        }
        finally
        {
            _combatSeed = previousSeed; DiscardCombat(); _checkingNavigation = checking;
            CheckpointFile.Write(CheckpointPath, baseline); LoadCheckpoint();
            // The following checks start with Cameron selected, as before this one.
            SelectObject(_layout!.Objects.Single(o => o.Source.Offset == 425776));
        }
    }
}
