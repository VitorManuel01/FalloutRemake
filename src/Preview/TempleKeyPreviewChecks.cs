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
    /// <summary>
    /// The lethal outcome of Cameron's challenge, from beside him as Narg: a seeded fight kills him, J on his body takes the
    /// Temple Key, the key unlocks the gate and the exit door (G369 = 1 keeps both locked otherwise), J at the chest returns
    /// the equipment and the exit leads to Arroyo for 300 XP with karma 5. The world is restored afterwards.
    /// </summary>
    private async Task CheckTempleKey()
    {
        string catalogHash = _mapCatalog!.Source.Sha256;
        WorldCheckpoint baseline = _campaign!.Capture(catalogHash);
        PlayerCharacter narg = _premadeChoices.Single(p => p.Character.Name == "Narg").Character;
        CheckpointFile.Write(CheckpointPath, baseline with { Character = narg.Capture() }); LoadCheckpoint();
        TempleNpcDepartureProfile profile = TempleProfile() ?? throw new InvalidDataException("Temple profile unavailable.");
        // The first seed whose fight kills Cameron, simulated on clones of the very combatants the challenge opens with.
        var data = new GameData(_directory!); var catalog = new MapCatalog(data.Read("data/maps.txt"));
        var prototypes = new PrototypeCatalog(new ResourceCatalog(data.Read));
        WorldSession copy = WorldSession.Restore(id => catalog.Load(data, id), new[] { 3, 4, 126 }, CheckpointFile.Read(CheckpointPath), catalogHash,
            runtimePrototype: prototypes.Get, templeProfile: map => TempleNpcDepartureProfile.Create(map, data.Read),
            arroyoProfile: map => ArroyoRewardProfile.Create(map, data.Read), premade: path => CharacterFile.Read(data.Read(path)), aiPackets: _aiPackets);
        CombatSession opening = copy.BeginTempleDuel(TempleNpcDepartureProfile.Create(copy.Current.Map, data.Read), new SeededCombatRandom(1));
        CombatantState[] start = opening.Combatants.Select(c => new CombatantState(c.Id, c.Name, c.Pid, c.Team, c.IsPlayer, c.Hex, c.Elevation, c.Stats, c.Weapon,
            c.HitPoints, c.Experience, c.HealingDoses) { YieldsBelowHalf = c.YieldsBelowHalf, Ai = c.Ai, KillType = c.KillType, Rotation = c.Rotation }).ToArray();
        HexNavigation arena = copy.Current.Doors.Navigation(2);
        bool Kills(ulong candidate)
        {
            var fight = new CombatSession(start.Select(c => new CombatantState(c.Id, c.Name, c.Pid, c.Team, c.IsPlayer, c.Hex, c.Elevation, c.Stats, c.Weapon,
                c.HitPoints, c.Experience, c.HealingDoses) { YieldsBelowHalf = c.YieldsBelowHalf, Ai = c.Ai, KillType = c.KillType, Rotation = c.Rotation }), arena, new SeededCombatRandom(candidate), profile.ActorKey);
            for (int guard = 0; guard < 400 && !fight.Turn.Over; guard++) PlayDuelTurn(fight);
            return fight.Turn.Over && !fight.Get(profile.ActorKey).Alive && fight.Get("player").Alive;
        }
        ulong seed = Enumerable.Range(1, 50000).Select(i => (ulong)i).FirstOrDefault(Kills);
        if (seed == 0) throw new InvalidDataException("No seed among 50000 lets Narg kill Cameron.");
        bool checking = _checkingNavigation; _checkingNavigation = true; ulong? previousSeed = _combatSeed;
        void Walk(int target) { RequestWalk(target); CompleteReadabilityWalk(); if (_walker!.CurrentHex != target) throw new InvalidDataException($"Walk to {target} failed."); }
        int Beside(int tile) => Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(tile, d))
            .Where(t => t.HasValue && _reachable!.Distance(t.Value) >= 0 && !_navigation!.IsExit(t.Value)).Select(t => t!.Value)
            .OrderBy(_reachable!.Distance).ThenBy(t => t).First();
        void Unlock(string door)
        {
            SelectObject(_layout!.Objects.Single(o => o.Source.Key == door));
            var button = (Button)_hud.FindChild("TempleKeyAction", true, false);
            if (!button.Visible || button.Disabled) throw new InvalidDataException("Temple Key unavailable beside the door: " + button.TooltipText);
            button.EmitSignal(Button.SignalName.Pressed);
            if (!_campaign.KeyUnlocked(door) || _doors!.Get(door)!.IsLocked || UseDoor(door) != DoorUseStatus.Ready || !_doors.Get(door)!.IsOpen)
                throw new InvalidDataException("The Temple Key did not unlock the door for U.");
        }
        try
        {
            int xp = _campaign.Experience;
            var actor = _layout!.Objects.Single(o => o.Source.Offset == 425776); SelectObject(actor);
            _combatSeed = seed; ((Button)_hud.FindChild("TempleDuelAction", true, false)).EmitSignal(Button.SignalName.Pressed);
            for (int guard = 0; guard < 400 && InCombat; guard++)
            {
                if (_combat!.Active is { IsPlayer: false }) { UpdateCombat(EnemyTurnDelay); continue; }
                if (_combat.Preview(CombatActionProposal.Attack("player", profile.ActorKey)).Status == CombatActionStatus.Ready)
                    ApplyCombatResult(_combat.Execute(CombatActionProposal.Attack("player", profile.ActorKey)));
                else { using var space = new InputEventKey { Pressed = true, PhysicalKeycode = Key.Space }; _UnhandledInput(space); }
            }
            if (InCombat || _campaign.TempleDuel != TempleDuelOutcome.Killed || !_campaign.IsDefeated(3, profile.ActorKey) || _campaign.Experience != xp + 100)
                throw new InvalidDataException($"Seed {seed} did not kill Cameron in the scene ({_campaign.TempleDuel}).");
            // G369 = 1: the exit door refuses U until the key unlocks it.
            string exit = profile.ExitDoor!.Key, gate = profile.GateKey;
            if (_doors!.CanToggle(exit, Beside(_layout.Objects.Single(o => o.Source.Key == exit).Source.Header.Tile), 2, false) != DoorUseStatus.ScriptRequired)
                throw new InvalidDataException("The exit door opened without the Temple Key.");
            // J beside his body: the loot window lists the Temple Key.
            BuildWorld(); SelectObject(_layout.Objects.Single(o => o.Source.Key == profile.ActorKey));
            using (var loot = new InputEventKey { Pressed = true, PhysicalKeycode = Key.J }) _UnhandledInput(loot);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_loot == null || _loot.Remaining != 1) throw new InvalidDataException("Cameron's body did not open with the Temple Key.");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "temple-key-body.png"));
            TakeAllFromContainer(); CloseLoot();
            if (!_campaign.TempleKeyCarried) throw new InvalidDataException("The Temple Key was not taken from the body.");
            Walk(Beside(_layout.Objects.Single(o => o.Source.Key == exit).Source.Header.Tile)); Unlock(exit);
            Walk(Beside(_layout.Objects.Single(o => o.Source.Key == gate).Source.Header.Tile)); Unlock(gate);
            // The equipment waits in the chest beyond the gate.
            var chest = _layout.Objects.Single(o => o.Source.Offset == WorldSession.TempleChestOffset);
            Walk(ContainerApproach(chest.Source) ?? throw new InvalidDataException("No route to the challenge chest through the unlocked gate."));
            SelectObject(chest);
            using (var loot = new InputEventKey { Pressed = true, PhysicalKeycode = Key.J }) _UnhandledInput(loot);
            if (_campaign.TempleChestCustody.Length != 0) throw new InvalidDataException("J at the chest did not return the equipment after the kill.");
            SaveCheckpoint(); WorldCheckpoint saved = WorldCheckpoint(); ResetWalker(); LoadCheckpoint();
            if (saved.Duel?.KeyUnlocked?.Length != 2 || !_campaign.TempleKeyCarried || !_doors.Get(exit)!.IsOpen || !_doors.Get(gate)!.IsOpen)
                throw new InvalidDataException("The checkpoint lost the key or the unlocked doors.");
            // Out through the exit door to Arroyo: 300 XP (G10 = 1) and the karma Cameron's death cost.
            ExitOption? route = null;
            foreach (int hex in Enumerable.Range(0, 40000).Where(t => _navigation!.IsExit(t) && _reachable!.Distance(t) >= 0).OrderBy(_reachable!.Distance))
                if (_campaign.PreviewExitAt(2, hex) is { Status: ExitStatus.Ready or ExitStatus.ExternalMap, Destination.Map: 4 } option) { route = option; Walk(hex); break; }
            if (route == null) throw new InvalidDataException("No reachable exit to Arroyo beyond the unlocked door.");
            int beforeArroyo = _campaign.Experience; UseExit();
            if (_campaign.CurrentMap != 4 || _campaign.Experience != beforeArroyo + ArroyoRewardProfile.OtherTrialExperience
                || _campaign.ArroyoGlobals is not { Karma: 5, ArroyoReputation: 60 })
                throw new InvalidDataException($"Arroyo after the kill gave {_campaign.Experience - beforeArroyo} XP, karma {_campaign.ArroyoGlobals?.Karma}.");
            foreach (WorldArea area in _campaign.Areas) MapLayoutWriter.Verify(area.Map);
            GD.Print($"TEMPLE_KEY_OK: seed {seed} killed Cameron (+100 XP), exit refused under G369 = 1, J on his body took the Temple Key, key unlocked exit and gate, J at the chest returned the equipment, v13 kept both doors, Arroyo +300 XP with karma 5 and reputation 60; source bytes verified.");
        }
        finally
        {
            _combatSeed = previousSeed; DiscardCombat(); CloseLoot(); _checkingNavigation = checking;
            CheckpointFile.Write(CheckpointPath, baseline); LoadCheckpoint();
            SelectObject(_layout!.Objects.Single(o => o.Source.Offset == 425776));
        }
    }
}
