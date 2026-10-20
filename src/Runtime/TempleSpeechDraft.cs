using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public enum TempleSpeechState { Initial, Accepted, Unsupported }
public enum TempleSpeechStatus { Ready, Unavailable, UnsupportedChoice, Cancelled, Incomplete, Stale, AlreadyCommitted }
public sealed record TempleSpeechStep(string Procedure, int[] ReplyMessages, int ChoiceOffset, int ChoiceMessage);

/// <summary>Verified, deliberately incomplete speech route. Owns no world, character, inventory or NPC movement.</summary>
public sealed class TempleSpeechProfile
{
    public const string ActorProgramHash = "D865DF15AA53995F69D9A211034622E34A5D9D9AC10E406AEBBCE6E3B19D61F5";
    public const string GlobalHash = "724464005C0B99AADA0E7808A2D13E7C67CDF7A3FC06EF50010DEC133BF42B12";
    private readonly ScriptVariableBlock[] _blocks;
    internal static readonly int[] ChoiceOffsets = { 11698, 11956, 12650, 14192, 14328 };
    private static readonly string[] Procedures = { "Node001", "Node002", "Node003", "Node014", "Node015" };
    private static readonly int[][] Replies = { new[] { 103, 104 }, new[] { 111 }, new[] { 122, 123 }, new[] { 166, 167 }, new[] { 169 } };
    private static readonly int[] Messages = { 108, 117, 165, 168, 171 };
    public string ActorKey { get; }
    public string GateKey { get; }
    public string MapKey { get; }
    public string GameKey { get; }
    public string Fingerprint { get; }
    internal TempleSpeechProfile(string actorKey, string gateKey, string mapKey, string gameKey, int[] mapValues)
    {
        ActorKey = actorKey; GateKey = gateKey; MapKey = mapKey; GameKey = gameKey;
        var gate = new int[9]; gate[0] = gate[1] = gate[3] = 1;
        _blocks = new[] {
            new ScriptVariableBlock(ScriptVariableScope.ScriptLocal, actorKey, ActorProgramHash, Enumerable.Range(0,16).ToArray(), new int[16]),
            new ScriptVariableBlock(ScriptVariableScope.ScriptLocal, gateKey, TempleInitialGateProfile.ProgramHash, Enumerable.Range(0,9).ToArray(), gate),
            new ScriptVariableBlock(ScriptVariableScope.Map, mapKey, TempleBarrierEffectProposal.MapHash, Enumerable.Range(0,21).ToArray(), mapValues.ToArray()),
            new ScriptVariableBlock(ScriptVariableScope.GameGlobal, gameKey, GlobalHash, new[] { 10 }, new[] { 0 }) };
        Fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CreateInitial().SchemaHash + "|" + TempleInitialGateProfile.ListHash + "|speech-prefix-v1")));
    }
    public static TempleSpeechProfile Create(MapDocument map, Func<string, GameResource> read)
    {
        MapObject gate = TempleInitialGateProfile.Match(map) ?? throw new InvalidDataException("Unknown initial Temple gate profile.");
        var actors = map.AllObjects.Where(o => o.Offset == 425776).ToArray();
        if (actors.Length != 1 || actors[0].Header is not { Id: 126, Pid: 16777490, Tile: 13728, Elevation: 2, Sid: 67108869, ScriptIndex: 748 })
            throw new InvalidDataException("Unknown Temple actor profile.");
        var bindings = map.ScriptBindings.Where(b => b.Sid == actors[0].Header.Sid).ToArray();
        if (bindings.Length != 1 || bindings[0].Index != 748 || bindings[0].Program is not { Path: "scripts/actemvil.int", Size: 15276, Sha256: ActorProgramHash })
            throw new InvalidDataException("Actor binding differs from the inspected program.");
        var resources = new ResourceCatalog(read); var metadata = ScriptListMetadata.Read(resources.Read("scripts/scripts.lst"), 748);
        if (metadata.Source.Sha256 != TempleInitialGateProfile.ListHash || metadata.DeclaredLocals != 16 || metadata.ProgramPath != "scripts/actemvil.int")
            throw new InvalidDataException("Unknown actor local declaration.");
        var program = IntProgram.Read(resources.Read(metadata.ProgramPath)); var globals = GameGlobalFile.Read(resources.Read("data/vault13.gam"));
        if (program.Source.Sha256 != ActorProgramHash || globals.Source.Sha256 != GlobalHash || globals.Entries[10].Value != 0)
            throw new InvalidDataException("Unknown speech program or global initialization.");
        var choices = IntDialogueOptions.Inspect(program);
        for (int i = 0; i < ChoiceOffsets.Length; i++)
        {
            var choice = choices.Single(c => c.Offset == ChoiceOffsets[i]);
            if (choice.MessageList != 749 || choice.Message != Messages[i] || choice.Intelligence != 4
                || !choice.OwningProcedures.Any(p => program.Procedures[p].Name == Procedures[i]))
                throw new InvalidDataException("Verified Speech route changed.");
        }
        // The two message fragments are concatenated around obj_name(dude_obj), not alternatives.
        for (int i = 0; i < ChoiceOffsets.Length; i++)
        {
            int start = i switch { 0 => 11522, 1 => 11854, 2 => 12488, 3 => 14118, _ => 14284 };
            (ushort Op, int? Value)[] expected = Replies[i].Length == 2
                ? new (ushort, int?)[] { (0xC001,749), (0xC001,749), (0xC001,Replies[i][0]), (0x8105,null),
                    (0x80BF,null), (0x80A4,null), (0x8039,null), (0xC001,749), (0xC001,Replies[i][1]), (0x8105,null), (0x8039,null), (0x811E,null) }
                : new (ushort, int?)[] { (0xC001,749), (0xC001,Replies[i][0]), (0x811E,null) };
            var actual = program.Instructions.Where(instruction => instruction.Offset >= start).Take(expected.Length)
                .Select(instruction => (instruction.Opcode, instruction.OperandBits));
            if (!actual.SequenceEqual(expected)) throw new InvalidDataException("Verified Speech reply composition changed.");
        }
        return new(actors[0].Key, gate.Key, map.Source.Sha256, globals.Source.Path, map.Prefix.GlobalVariables);
    }
    public BoundedScriptState CreateInitial() => new(_blocks);
    public TempleSpeechStep Step(int step)
    {
        if (step < 0 || step >= ChoiceOffsets.Length) throw new ArgumentOutOfRangeException(nameof(step));
        return new(Procedures[step], Replies[step].ToArray(), ChoiceOffsets[step], Messages[step]);
    }
    internal ScriptVariableWrite[] AcceptanceWrites() => new[] {
        new ScriptVariableWrite(ScriptVariableScope.ScriptLocal, ActorKey, 7, 1), // Node002a
        new ScriptVariableWrite(ScriptVariableScope.ScriptLocal, ActorKey, 12, 1),
        new ScriptVariableWrite(ScriptVariableScope.ScriptLocal, ActorKey, 13, 2),
        new ScriptVariableWrite(ScriptVariableScope.Map, MapKey, 20, 2),
        new ScriptVariableWrite(ScriptVariableScope.GameGlobal, GameKey, 10, 2) };
    public TempleSpeechState Classify(ScriptStateSnapshot snapshot)
    {
        var candidate = CreateInitial();
        try { candidate.Restore(snapshot); }
        catch (InvalidDataException) { return TempleSpeechState.Unsupported; }
        string json = JsonSerializer.Serialize(candidate.Capture()); var initial = CreateInitial();
        if (json == JsonSerializer.Serialize(initial.Capture())) return TempleSpeechState.Initial;
        initial.Apply(AcceptanceWrites(), initial.SessionToken, 0);
        return json == JsonSerializer.Serialize(initial.Capture()) ? TempleSpeechState.Accepted : TempleSpeechState.Unsupported;
    }
}

public sealed class TempleSpeechDraft
{
    private readonly TempleSpeechProfile _profile;
    private readonly BoundedScriptState _state;
    private readonly Guid _token;
    private readonly long _revision;
    private bool _cancelled, _committed;
    public int StepIndex { get; private set; }
    public bool ChoicesComplete => StepIndex == TempleSpeechProfile.ChoiceOffsets.Length;
    private TempleSpeechDraft(TempleSpeechProfile profile, BoundedScriptState state)
    { _profile = profile; _state = state; _token = state.SessionToken; _revision = state.Revision; }
    public static TempleSpeechDraft? Begin(TempleSpeechProfile profile, BoundedScriptState state, int effectiveIntelligence, bool speechTagged)
        => effectiveIntelligence is >= 4 and <= 20 && speechTagged && profile.Classify(state.Capture()) == TempleSpeechState.Initial
            ? new(profile, state) : null;
    public TempleSpeechStatus Choose(int offset)
    {
        if (_cancelled) return TempleSpeechStatus.Cancelled;
        if (_committed) return TempleSpeechStatus.AlreadyCommitted;
        if (_state.SessionToken != _token || _state.Revision != _revision) return TempleSpeechStatus.Stale;
        if (ChoicesComplete || offset != TempleSpeechProfile.ChoiceOffsets[StepIndex]) return TempleSpeechStatus.UnsupportedChoice;
        StepIndex++; return TempleSpeechStatus.Ready;
    }
    public void Cancel() { if (!_committed) _cancelled = true; }
    public TempleSpeechStatus CommitDetached()
    {
        if (_cancelled) return TempleSpeechStatus.Cancelled;
        if (_committed) return TempleSpeechStatus.AlreadyCommitted;
        if (_state.SessionToken != _token || _state.Revision != _revision) return TempleSpeechStatus.Stale;
        if (!ChoicesComplete) return TempleSpeechStatus.Incomplete;
        if (_profile.Classify(_state.Capture()) != TempleSpeechState.Initial) return TempleSpeechStatus.Unavailable;
        if (_state.Apply(_profile.AcceptanceWrites(), _token, _revision) != ScriptWriteStatus.Ready) return TempleSpeechStatus.Stale;
        _committed = true; return TempleSpeechStatus.Ready;
    }
}
