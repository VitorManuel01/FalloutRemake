using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private async Task CheckActiveTempleSpeech()
    {
        WorldCheckpoint baseline = _campaign!.Capture(_mapCatalog!.Source.Sha256);
        byte[] State() => CheckpointFile.Encode(_campaign!.Capture(_mapCatalog!.Source.Sha256));
        byte[] before = State(); bool hidden = _hud.Filters["hidden"];
        var data = new GameData(_directory!); var profile = TempleNpcDepartureProfile.Create(_map!,data.Read);
        void Press(string name) => ((Button)_hud.FindChild(name,true,false)).EmitSignal(Button.SignalName.Pressed);
        void CheckPose()
        {
            var state = _campaign!.TempleProgress!.Departure; var actor = profile.Actor(state);
            var obj = _layout!.Objects.Single(o => o.Source.Key == profile.ActorKey); var body = _bodies[profile.ActorKey];
            if (obj.Source.Header != actor.Header || !body.Position.IsEqualApprox(Point(obj.Position,Dimensions(obj.Kind).Y / 2))
                || body.GetMeta("hex").AsInt32() != actor.Header.Tile || body.GetMeta("source_id").AsInt32() != 126
                || _lookup[body.GetInstanceId()].Source.Header.Tile != actor.Header.Tile
                || body.Visible == (state.Phase == NpcDeparturePhase.Hidden)
                || _campaign.Experience != 25 || _campaign.CarriedCount != 2)
                throw new InvalidDataException("Active Temple pose, picking, visibility, items or XP disagree.");
            if (state.Phase != NpcDeparturePhase.Hidden && !_navigation!.BlockersAt(actor.Header.Tile).Any(b => b.Key == profile.ActorKey))
                throw new InvalidDataException("Active NPC mesh moved without its blocker.");
            if (_map!.AllObjects.Single(o => o.Key == profile.ActorKey).Header.Tile != 13728)
                throw new InvalidDataException("Active NPC movement rewrote the original MAP record.");
        }
        try
        {
            SelectObject(_layout!.Objects.Single(o => o.Source.Key == profile.ActorKey)); OpenSpeechConsultation();
            for (int i = 0; i < 5; i++) _speechWindow!.ChooseRequested!();
            var apply = (Button)_speechWindow!.FindChild("SpeechApplyWorld",true,false); var oldApply = _speechWindow.ApplyRequested!;
            if (!apply.Visible || apply.Disabled || !before.SequenceEqual(State())) throw new InvalidDataException("Speech application was offered before its detached result or mutated early.");
            apply.EmitSignal(Button.SignalName.Pressed);
            if (InspectorOpen || _speechWindow != null || _campaign.TempleProgress == null || !_doors!.Get(profile.GateKey)!.IsOpen)
                throw new InvalidDataException("Speech did not start the active manual departure while preserving the open gate.");
            CheckPose(); byte[] accepted = State(); oldApply();
            if (!accepted.SequenceEqual(State())) throw new InvalidDataException("Old conversation callback reapplied Speech.");
            Press("TempleProgressAction"); Press("TempleWorldFocus");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture,"active-speech.png"));
            int next = profile.Restore(_campaign.TempleProgress.Departure).NextHex!.Value;
            if (_walker!.CurrentHex != next) { RequestWalk(next); CompleteReadabilityWalk(); }
            byte[] occupied = State(); Press("TempleWorldStep");
            if (!occupied.SequenceEqual(State())) throw new InvalidDataException("Active NPC entered the player's hex.");
            var nav = _doors!.Navigation(2); var reach = nav.ReachableFrom(_walker.CurrentHex);
            int[] routes = new[] { NpcDeparturePhase.ToGate,NpcDeparturePhase.ToMiddle,NpcDeparturePhase.ToExit }.SelectMany(profile.Route).ToArray();
            int safe = Enumerable.Range(0,40000).Where(t => reach.Distance(t) > 0 && !nav.IsExit(t) && !routes.Contains(t))
                .OrderBy(reach.Distance).ThenBy(t => t).First();
            RequestWalk(safe); _walker.Advance(.05); byte[] busy = State(); _hud.TempleStepRequested!();
            if (!busy.SequenceEqual(State())) throw new InvalidDataException("NPC advanced while player walking.");
            CompleteReadabilityWalk(); RefreshSpeechAction();
            var oldStep = _hud.TempleStepRequested!; Press("TempleWorldStep"); CheckPose(); byte[] stepped = State(); oldStep();
            if (!stepped.SequenceEqual(State())) throw new InvalidDataException("Stale active NPC callback advanced a second hex.");
            SaveCheckpoint(); var savedPose = _campaign.TempleProgress!.Departure; ResetWalker(); LoadCheckpoint(); CheckPose();
            if (_campaign.TempleProgress!.Departure != savedPose) throw new InvalidDataException("Active NPC pose failed K/R restoration.");
            SelectObject(_layout.Objects.Single(o => o.Source.Key == profile.ActorKey));
            if (_selected!.Source.Header.Tile != profile.Location(savedPose).Hex || TempleNpcApproachTarget() == 13728)
                throw new InvalidDataException("Active NPC inspection or approach still uses its original position.");
            Press("TempleWorldLeg"); CheckPose();
            if (_campaign.TempleProgress.Departure.Phase != NpcDeparturePhase.GateDelay) throw new InvalidDataException("Active NPC skipped its gate delay.");
            Press("TempleWorldTick"); SaveCheckpoint();
            byte[] waiting = State();
            if (_campaign.TempleProgress.Departure.RemainingTicks != 10) throw new InvalidDataException("Active NPC second did not preserve its half-wait.");
            Press("TempleWorldFocus");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture,"active-npc-wait.png"));
            var oldWait = _hud.TempleWaitRequested!; Press("TempleWorldWait"); byte[] fired = State(); oldWait();
            if (!fired.SequenceEqual(State()) || _campaign.TempleProgress.Departure.Phase != NpcDeparturePhase.ToMiddle)
                throw new InvalidDataException("Active NPC timer was replayed or moved before its next step.");
            LoadCheckpoint(); CheckPose();
            if (!waiting.SequenceEqual(State())) throw new InvalidDataException("Active partial wait K/R lost part of the world.");
            var invalid = _campaign.Capture(_mapCatalog.Source.Sha256);
            invalid.Temple!.Scripts.Blocks[0].Values[14] = 4;
            CheckpointFile.Write(CheckpointPath,invalid); var live = _campaign; LoadCheckpoint();
            if (_campaign != live || !waiting.SequenceEqual(State())) throw new InvalidDataException("Conflicting Speech/NPC file replaced the live world.");
            SaveCheckpoint(); Press("TempleWorldWait"); Press("TempleWorldLeg"); CheckPose();
            if (_campaign.TempleProgress!.Departure.Phase != NpcDeparturePhase.ToExit) throw new InvalidDataException("Active NPC skipped its intermediate destination.");
            Press("TempleWorldLeg"); CheckPose(); _hud.Filters["hidden"] = true; ApplyFilters();
            if (_campaign.TempleProgress.Departure.Phase != NpcDeparturePhase.Hidden || _bodies[profile.ActorKey].Visible || _bodies[profile.ActorKey].CollisionLayer != 0
                || _navigation!.BlockersAt(28715).Any(b => b.Key == profile.ActorKey))
                throw new InvalidDataException("Completed NPC remained selectable or blocking, including the hidden-object filter.");
            SaveCheckpoint(); LoadCheckpoint(); CheckPose();
            if (_capture != null) await Capture(Path.ChangeExtension(_capture,"active-npc-hidden.png"));
            foreach (var area in _campaign.Areas) MapLayoutWriter.Verify(area.Map);
            GD.Print("TEMPLE_ACTIVE_SPEECH_OK: explicit world acceptance, v6 K/R, projected mesh/picking/blocker/approach, open gate retained, occupied/busy/stale action guards, partial event restoration and corrupt-file rollback, three route legs, hidden final state, original MAP/items/25 XP preserved.");
        }
        finally
        {
            _hud.Filters["hidden"] = hidden;
            CheckpointFile.Write(CheckpointPath,baseline); LoadCheckpoint();
            if (!before.SequenceEqual(State())) throw new InvalidDataException("Active Speech fixture did not restore its baseline.");
        }
    }
}
