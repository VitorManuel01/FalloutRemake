using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using FalloutRemake.Geometry;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public enum NpcDeparturePhase { ToGate, GateDelay, ToMiddle, ToExit, Hidden }
public enum NpcDepartureStatus { Ready, Stale, Occupied, NotWalking, NotWaiting, Invalid, Complete, LimitReached }
public sealed record NpcDepartureSnapshot([property: JsonRequired] int Version, [property: JsonRequired] string Profile,
    [property: JsonRequired] NpcDeparturePhase Phase, [property: JsonRequired] int RouteIndex, [property: JsonRequired] int RemainingTicks);
public sealed record NpcDepartureAdvance(NpcDepartureStatus Status, int AppliedTicks = 0, int UnusedTicks = 0);

/// <summary>Source-bound, single-hex departure projection. Owns no active world or general INT execution.</summary>
public sealed class TempleNpcDepartureProfile
{
    private readonly MapObject[] _roots;
    private readonly int[][] _routes;
    private readonly MapObject _actor, _gate;
    public TempleSpeechProfile Speech { get; }
    public string Fingerprint { get; }
    public string ActorKey => _actor.Key;
    public string GateKey => _gate.Key;
    public const int DelayTicks = 20;
    public const string MapProgramHash = "37221604DFD8166E635795FEEF5A72D011F195C9613A9E0DBBCCED7C28DE3CC1";

    internal TempleNpcDepartureProfile(TempleSpeechProfile speech, MapObject[] roots, MapObject actor, MapObject gate)
    {
        Speech = speech; _roots = roots.ToArray(); _actor = actor; _gate = gate;
        int[] targets = { 19728, 22712, 28715 }; int from = actor.Header.Tile;
        _routes = new int[3][];
        for (int i = 0; i < 3; i++)
        {
            var navigation = BuildNavigation(actor, i > 0, true);
            var route = navigation.FindRoute(from, targets[i]);
            if (route.Status != RouteStatus.Ready || route.Tiles.Length < 2)
                throw new InvalidDataException("Verified NPC departure route is unavailable; no teleport fallback.");
            _routes[i] = route.Tiles; from = targets[i];
        }
        string identity = string.Join("|", speech.Fingerprint, MapProgramHash, actor.Key, actor.Header.Id, actor.Header.Tile,
            actor.Header.Flags, actor.Header.Rotation, gate.Key, gate.Header.Flags, gate.State["openFlags"],
            string.Join(";", _routes.Select(route => string.Join(",", route))), "npc-departure-v1");
        Fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }
    public static TempleNpcDepartureProfile Create(MapDocument map, Func<string, GameResource> read)
    {
        var speech = TempleSpeechProfile.Create(map, read);
        var actor = map.ObjectLayers.SelectMany(l => l.Objects).Single(o => o.Key == speech.ActorKey);
        var gate = map.ObjectLayers.SelectMany(l => l.Objects).Single(o => o.Key == speech.GateKey);
        if ((actor.Header.Flags & (HexNavigation.Hidden | HexNavigation.NoBlock | HexNavigation.Multihex)) != 0
            || ((actor.Header.Fid >> 24) & 15) != 1 || actor.Header.Rotation is < 0 or > 5)
            throw new InvalidDataException("Unsupported NPC geometry profile.");
        var program = IntProgram.Read(read("scripts/actemvil.int"));
        var mapProgram = IntProgram.Read(read("scripts/arcaves.int"));
        if (program.Source.Sha256 != TempleSpeechProfile.ActorProgramHash || mapProgram.Source.Sha256 != MapProgramHash
            || map.MapProgram?.Sha256 != MapProgramHash) throw new InvalidDataException("Unknown NPC departure programs.");
        // Full program fingerprints bind the audited movement, timer and visibility instructions.
        // The evidence report also checks their exact opcode spans independently.
        return new(speech, map.ObjectLayers.SelectMany(l => l.Objects).ToArray(), actor, gate);
    }
    internal int[] Route(NpcDeparturePhase phase) => _routes[phase switch
    { NpcDeparturePhase.ToGate => 0, NpcDeparturePhase.ToMiddle => 1, NpcDeparturePhase.ToExit => 2, _ => throw new InvalidDataException("Not a movement phase.") }];
    public int[] RouteSteps => _routes.Select(r => r.Length - 1).ToArray();
    internal void Validate(NpcDepartureSnapshot? state)
    {
        if (state == null || state.Version != 1 || state.Profile != Fingerprint || !Enum.IsDefined(state.Phase))
            throw new InvalidDataException("NPC departure source or version mismatch.");
        bool walking = state.Phase is NpcDeparturePhase.ToGate or NpcDeparturePhase.ToMiddle or NpcDeparturePhase.ToExit;
        if (walking ? state.RouteIndex < 0 || state.RouteIndex >= Route(state.Phase).Length - 1 || state.RemainingTicks != 0
            : state.RouteIndex != 0 || (state.Phase == NpcDeparturePhase.GateDelay ? state.RemainingTicks is < 1 or > DelayTicks : state.RemainingTicks != 0))
            throw new InvalidDataException("NPC departure phase, position or event is inconsistent.");
    }
    public ActorLocation Location(NpcDepartureSnapshot state)
    {
        Validate(state);
        int[] route; int index;
        if (state.Phase == NpcDeparturePhase.Hidden) { route = _routes[2]; index = route.Length - 1; }
        else if (state.Phase == NpcDeparturePhase.GateDelay) { route = _routes[0]; index = route.Length - 1; }
        else { route = Route(state.Phase); index = state.RouteIndex; }
        int rotation = _actor.Header.Rotation;
        if (index > 0) rotation = Direction(route[index - 1], route[index]);
        else if (state.Phase != NpcDeparturePhase.ToGate)
        {
            int[] previous = state.Phase == NpcDeparturePhase.ToMiddle ? _routes[0] : _routes[1];
            rotation = Direction(previous[^2], previous[^1]);
        }
        return new(3, 2, route[index], rotation);
    }
    private static int Direction(int from, int to) => Enumerable.Range(0, 6).Single(d => FalloutGrid.Neighbor(from, d) == to);
    public MapObject Actor(NpcDepartureSnapshot state)
    {
        var location = Location(state);
        return _actor with { Header = _actor.Header with { Tile = location.Hex, Rotation = location.Rotation,
            Flags = state.Phase == NpcDeparturePhase.Hidden ? _actor.Header.Flags | HexNavigation.Hidden | HexNavigation.NoBlock : _actor.Header.Flags } };
    }
    public DoorState Gate(NpcDepartureSnapshot state)
    {
        Validate(state); bool open = state.Phase != NpcDeparturePhase.ToGate;
        return new(_gate, open ? _gate.Header.Flags | DoorSession.OpenDoorMask : _gate.Header.Flags & ~DoorSession.OpenDoorMask,
            (_gate.State["openFlags"] & ~(DoorSession.Locked | 1)) | (open ? 1 : 0), true);
    }
    private HexNavigation BuildNavigation(MapObject actor, bool gateOpen, bool forNpc)
    {
        var roots = _roots.Where(o => o.Key != _actor.Key).Concat(forNpc ? Array.Empty<MapObject>() : new[] { actor });
        return new(roots, 2, o => o.Key == _gate.Key
            ? new(gateOpen ? _gate.Header.Flags | DoorSession.OpenDoorMask : _gate.Header.Flags & ~DoorSession.OpenDoorMask,
                (_gate.State["openFlags"] & ~(DoorSession.Locked | 1)) | (gateOpen ? 1 : 0))
            : new(o.Header.Flags, o.State.TryGetValue("openFlags", out int flags) ? flags : null),
            forNpc ? ExitTraversalPolicy.ContinueAcrossExits : ExitTraversalPolicy.StopAtExits);
    }
    public HexNavigation Navigation(NpcDepartureSnapshot state, bool forNpc = false)
        => BuildNavigation(Actor(state), Gate(state).IsOpen, forNpc);
    public TempleNpcDeparture Begin(ScriptStateSnapshot acceptedSpeech)
    {
        if (Speech.Classify(acceptedSpeech) != TempleSpeechState.Accepted)
            throw new InvalidDataException("NPC departure requires the verified completed Speech rehearsal.");
        return new(this, new(1, Fingerprint, NpcDeparturePhase.ToGate, 0, 0));
    }
    public TempleNpcDeparture Restore(NpcDepartureSnapshot state) { Validate(state); return new(this, state); }
}

/// <summary>Each manual action replaces one complete immutable snapshot, including pose, blocker, gate and one-shot event.</summary>
public sealed class TempleNpcDeparture
{
    public TempleNpcDepartureProfile Profile { get; }
    private NpcDepartureSnapshot _state;
    public Guid SessionToken { get; } = Guid.NewGuid();
    public long Revision { get; private set; }
    public ActorLocation Location => Profile.Location(_state);
    public bool Walking => _state.Phase is NpcDeparturePhase.ToGate or NpcDeparturePhase.ToMiddle or NpcDeparturePhase.ToExit;
    public int? NextHex => Walking ? Profile.Route(_state.Phase)[_state.RouteIndex + 1] : null;
    internal TempleNpcDeparture(TempleNpcDepartureProfile profile, NpcDepartureSnapshot state) { Profile = profile; _state = state; }
    public NpcDepartureSnapshot Capture() => _state;
    private NpcDepartureStatus Guard(Guid token, long revision) => token != SessionToken || revision != Revision
        ? NpcDepartureStatus.Stale : Revision == long.MaxValue ? NpcDepartureStatus.LimitReached : NpcDepartureStatus.Ready;
    public NpcDepartureStatus Step(Guid token, long revision, ActorLocation? occupied = null)
    {
        var guard = Guard(token, revision); if (guard != NpcDepartureStatus.Ready) return guard;
        if (!Walking) return _state.Phase == NpcDeparturePhase.Hidden ? NpcDepartureStatus.Complete : NpcDepartureStatus.NotWalking;
        int next = NextHex!.Value;
        if (occupied is { Map: 3, Elevation: 2 } && occupied.Hex == next) return NpcDepartureStatus.Occupied;
        int index = _state.RouteIndex + 1; int[] route = Profile.Route(_state.Phase);
        NpcDepartureSnapshot candidate = index < route.Length - 1 ? _state with { RouteIndex = index } : _state.Phase switch
        {
            NpcDeparturePhase.ToGate => _state with { Phase = NpcDeparturePhase.GateDelay, RouteIndex = 0, RemainingTicks = TempleNpcDepartureProfile.DelayTicks },
            NpcDeparturePhase.ToMiddle => _state with { Phase = NpcDeparturePhase.ToExit, RouteIndex = 0 },
            _ => _state with { Phase = NpcDeparturePhase.Hidden, RouteIndex = 0 }
        };
        Profile.Validate(candidate); _state = candidate; Revision++; return NpcDepartureStatus.Ready;
    }
    public NpcDepartureAdvance AdvanceDelay(int ticks, Guid token, long revision)
    {
        var guard = Guard(token, revision); if (guard != NpcDepartureStatus.Ready) return new(guard);
        if (ticks <= 0) return new(NpcDepartureStatus.Invalid);
        if (_state.Phase != NpcDeparturePhase.GateDelay) return new(NpcDepartureStatus.NotWaiting);
        int applied = Math.Min(ticks, _state.RemainingTicks);
        var candidate = _state with { RemainingTicks = _state.RemainingTicks - applied };
        if (candidate.RemainingTicks == 0) candidate = candidate with { Phase = NpcDeparturePhase.ToMiddle };
        Profile.Validate(candidate); _state = candidate; Revision++;
        return new(NpcDepartureStatus.Ready, applied, ticks - applied);
    }
}
