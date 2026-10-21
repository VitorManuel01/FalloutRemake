using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using FalloutRemake.Geometry;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public sealed record SavedTempleProgress([property: JsonRequired] string Profile, [property: JsonRequired] ScriptStateSnapshot Scripts,
    [property: JsonRequired] NpcDepartureSnapshot Departure, [property: JsonRequired] bool InitialGateOpen);
public enum TempleWorldStatus { Ready, Unsupported, AlreadyAccepted, Busy, TooFar, Unavailable, Stale, Occupied, Blocked, NotWalking, NotWaiting, Complete, Invalid, DoorRequired }
public sealed record TempleSpeechCommitPlan(Guid WorldToken, Guid TempleToken, long Revision, string WorldStamp,
    TempleNpcDepartureProfile Profile, ScriptStateSnapshot Accepted, int Intelligence, bool SpeechTagged);
public sealed record TempleSpeechProposal(TempleWorldStatus Status, TempleSpeechCommitPlan? Plan = null);
public sealed record TempleWorldAdvance(TempleWorldStatus Status, int AppliedTicks = 0, int UnusedTicks = 0);
public sealed record TempleNpcRouteInfo(TempleWorldStatus Status, int[] Tiles, int? BlockedHex = null, string? TargetKey = null);

internal sealed class TempleWorldProgress
{
    internal TempleNpcDepartureProfile Profile { get; }
    internal NpcDepartureSnapshot Departure { get; }
    internal bool InitialGateOpen { get; }
    internal TempleWorldProgress(TempleNpcDepartureProfile profile, NpcDepartureSnapshot departure, bool initialGateOpen)
    { profile.Validate(departure); Profile = profile; Departure = departure; InitialGateOpen = initialGateOpen; }
    internal ScriptStateSnapshot Scripts()
    {
        var state = Profile.Speech.CreateInitial();
        var writes = Profile.Speech.AcceptanceWrites().Append(new ScriptVariableWrite(ScriptVariableScope.ScriptLocal,
            Profile.ActorKey, 14, (int)Departure.Phase)).ToArray();
        if (state.Apply(writes, state.SessionToken, 0) != ScriptWriteStatus.Ready) throw new InvalidDataException("Temple progress script projection failed.");
        return state.Capture();
    }
    internal DoorState Gate()
    {
        var gate = Profile.Gate(Departure);
        return Departure.Phase == NpcDeparturePhase.ToGate && InitialGateOpen ? gate with
        { Flags = gate.Flags | DoorSession.OpenDoorMask, OpenFlags = gate.OpenFlags | 1 } : gate;
    }
    internal SavedTempleProgress Capture() => new(Profile.Fingerprint, Scripts(), Departure, InitialGateOpen);
    internal static TempleWorldProgress Restore(TempleNpcDepartureProfile profile, SavedTempleProgress saved)
    {
        if (saved == null || saved.Profile != profile.Fingerprint) throw new InvalidDataException("Unsupported Temple world profile.");
        var candidate = new TempleWorldProgress(profile, saved.Departure, saved.InitialGateOpen);
        // Expected values are generated from the phase; L14 can never disagree with pose or event.
        if (saved.Scripts == null || JsonSerializer.Serialize(saved.Scripts) != JsonSerializer.Serialize(candidate.Scripts()))
            throw new InvalidDataException("Temple script variables disagree with NPC phase.");
        return candidate;
    }
}

public sealed partial class WorldSession
{
    private TempleWorldProgress? _templeProgress;
    private readonly Func<MapDocument, TempleNpcDepartureProfile>? _templeProfile;
    public Guid TempleToken { get; private set; } = Guid.NewGuid();
    public long TempleRevision { get; private set; }
    public SavedTempleProgress? TempleProgress => _templeProgress?.Capture();
    public TempleNpcRouteInfo InspectTempleNpcRoute()
    {
        if (_templeProgress == null || CurrentMap != 3 || Location.Elevation != 2) return new(TempleWorldStatus.Unavailable,Array.Empty<int>());
        var state = _templeProgress.Departure;
        if (state.Phase == NpcDeparturePhase.Hidden) return new(TempleWorldStatus.Complete,Array.Empty<int>());
        if (state.Phase == NpcDeparturePhase.GateDelay) return new(TempleWorldStatus.NotWalking,Array.Empty<int>());
        int[] tiles = _templeProgress.Profile.Route(state.Phase).Skip(state.RouteIndex).ToArray();
        string? target = state.Phase == NpcDeparturePhase.ToGate ? _templeProgress.Profile.GateKey : null;
        var nav = Current.Doors.NavigationForNpc(2,_templeProgress.Profile.ActorKey);
        foreach (int hex in tiles.Skip(1))
        {
            if (hex == Location.Hex) return new(TempleWorldStatus.Occupied,tiles,hex,target);
            if (!nav.Walkable(hex)) return new(TempleWorldStatus.Blocked,tiles,hex,target);
        }
        return new(TempleWorldStatus.Ready,tiles,TargetKey: target);
    }
    public MapObject EffectiveObject(int map, MapObject source) => map == 3 && _templeProgress?.Profile.ActorKey == source.Key
        ? _templeProgress.Profile.Actor(_templeProgress.Departure) : source;
    private DoorState? TempleGate(int map, string key) => map == 3 && _templeProgress?.Profile.GateKey == key ? _templeProgress.Gate() : null;
    private string TempleWorldStamp() => Convert.ToHexString(SHA256.HashData(CheckpointFile.Encode(Capture("temple-transaction"))));
    private TempleWorldStatus TempleSpeechPreflight(TempleNpcDepartureProfile? profile, ScriptStateSnapshot? accepted, bool moving, int intelligence, bool tagged)
    {
        if (_templeProgress != null) return TempleWorldStatus.AlreadyAccepted;
        if (moving) return TempleWorldStatus.Busy;
        if (profile == null || accepted == null || intelligence != 5 || !tagged
            || profile.Speech.Classify(accepted) != TempleSpeechState.Accepted) return TempleWorldStatus.Unsupported;
        if (CurrentMap != 3 || Location.Elevation != 2 || !profile.Matches(Current.Map)) return TempleWorldStatus.Unavailable;
        var initial = profile.Actor(new(1, profile.Fingerprint, NpcDeparturePhase.ToGate, 0, 0));
        if (FalloutGrid.Distance(Location.Hex, initial.Header.Tile) != 1) return TempleWorldStatus.TooFar;
        var gate = Current.Doors.Get(profile.GateKey);
        if (gate == null || gate.IsLocked || gate.IsJammed || !Current.Travel.CanArrive(Location)
            || Current.Doors.Navigation(2).IsExit(Location.Hex)) return TempleWorldStatus.Unavailable;
        return TempleWorldStatus.Ready;
    }
    public TempleSpeechProposal PrepareTempleSpeech(TempleNpcDepartureProfile profile, ScriptStateSnapshot accepted, bool moving, int intelligence, bool speechTagged)
    {
        var status = TempleSpeechPreflight(profile, accepted, moving, intelligence, speechTagged);
        if (status != TempleWorldStatus.Ready) return new(status);
        // Clone the variable arrays; the proposal must not share mutable snapshot arrays with its caller.
        var state = profile.Speech.CreateInitial(); state.Restore(accepted);
        return new(status, new(SessionToken, TempleToken, TempleRevision, TempleWorldStamp(), profile, state.Capture(), intelligence, speechTagged));
    }
    public TempleWorldStatus CommitTempleSpeech(TempleSpeechCommitPlan? plan, bool moving)
    {
        if (plan == null || plan.WorldToken != SessionToken || plan.TempleToken != TempleToken || plan.Revision != TempleRevision)
            return TempleWorldStatus.Stale;
        var status = TempleSpeechPreflight(plan.Profile, plan.Accepted, moving, plan.Intelligence, plan.SpeechTagged);
        if (status != TempleWorldStatus.Ready) return status;
        if (plan.WorldStamp != TempleWorldStamp()) return TempleWorldStatus.Stale;
        var departure = plan.Profile.Begin(plan.Accepted);
        var candidate = new TempleWorldProgress(plan.Profile, departure.Capture(), Current.Doors.Get(plan.Profile.GateKey)!.IsOpen);
        // All validation precedes this single aggregate replacement. Doors and blockers read its projection.
        _templeProgress = candidate; TempleRevision++; return TempleWorldStatus.Ready;
    }
    private TempleWorldStatus TempleActionGuard(Guid token, long revision, bool moving)
    {
        if (token != TempleToken || revision != TempleRevision) return TempleWorldStatus.Stale;
        if (_templeProgress == null || CurrentMap != 3 || Location.Elevation != 2) return TempleWorldStatus.Unavailable;
        return moving ? TempleWorldStatus.Busy : TempleWorldStatus.Ready;
    }
    public TempleWorldStatus StepTempleNpc(Guid token, long revision, bool moving)
    {
        var guard = TempleActionGuard(token, revision, moving); if (guard != TempleWorldStatus.Ready) return guard;
        var current = _templeProgress!; var candidate = current.Profile.Restore(current.Departure);
        if (candidate.NextHex is int next && !Current.Doors.NavigationForNpc(2, current.Profile.ActorKey).Walkable(next)) return TempleWorldStatus.Blocked;
        var result = candidate.Step(candidate.SessionToken, candidate.Revision, Location);
        if (result != NpcDepartureStatus.Ready) return result switch
        { NpcDepartureStatus.Occupied => TempleWorldStatus.Occupied, NpcDepartureStatus.Complete => TempleWorldStatus.Complete, _ => TempleWorldStatus.NotWalking };
        _templeProgress = new(current.Profile, candidate.Capture(), current.InitialGateOpen); TempleRevision++;
        return TempleWorldStatus.Ready;
    }
    public TempleWorldAdvance AdvanceTempleNpcTime(int ticks, Guid token, long revision, bool moving)
    {
        var guard = TempleActionGuard(token, revision, moving); if (guard != TempleWorldStatus.Ready) return new(guard);
        var current = _templeProgress!; var candidate = current.Profile.Restore(current.Departure);
        var result = candidate.AdvanceDelay(ticks, candidate.SessionToken, candidate.Revision);
        if (result.Status != NpcDepartureStatus.Ready) return new(result.Status == NpcDepartureStatus.Invalid ? TempleWorldStatus.Invalid : TempleWorldStatus.NotWaiting);
        _templeProgress = new(current.Profile, candidate.Capture(), current.InitialGateOpen); TempleRevision++;
        return new(TempleWorldStatus.Ready, result.AppliedTicks, result.UnusedTicks);
    }
    private void RestoreTempleProgress(SavedTempleProgress? saved)
    {
        if (saved == null) return;
        if (!_areas.TryGetValue(3, out var area) || _templeProfile == null) throw new InvalidDataException("Temple progress requires its source-bound profile resolver.");
        var profile = _templeProfile(area.Map);
        if (!profile.Matches(area.Map)) throw new InvalidDataException("Temple progress source MAP mismatch.");
        var candidate = TempleWorldProgress.Restore(profile, saved); var gate = area.Doors.Get(profile.GateKey); var expected = candidate.Gate();
        if (gate == null || gate.Flags != expected.Flags || gate.OpenFlags != expected.OpenFlags)
            throw new InvalidDataException("Saved gate disagrees with Temple progress.");
        _templeProgress = candidate;
    }
}
