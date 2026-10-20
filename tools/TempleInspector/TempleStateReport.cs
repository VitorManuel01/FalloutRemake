using FalloutRemake.Import;
using FalloutRemake.Runtime;
using System.Text.Json;

internal static class TempleStateReport
{
    public static object Create(GameData data)
    {
        string path = data.FindPaths("vault13.gam").Single(p => p == "data/vault13.gam");
        GameGlobalFile configuration = GameGlobalFile.Read(data.Read(path));
        MapDocument map = MapDocument.Read(data.Read("maps/arcaves.map"), data.Read); MapLayoutWriter.Verify(map);
        if (map.Source.Sha256 != TempleBarrierEffectProposal.MapHash || configuration.Source.Sha256 != "724464005C0B99AADA0E7808A2D13E7C67CDF7A3FC06EF50010DEC133BF42B12")
            throw new InvalidDataException("Unknown state source profile.");
        MapObject actor = map.AllObjects.Single(o => o.Offset == 425776), gate = map.AllObjects.Single(o => o.Offset == 462744);
        var actorBinding = map.ScriptBindings.Single(b => b.Sid == actor.Header.Sid); var gateBinding = map.ScriptBindings.Single(b => b.Sid == gate.Header.Sid);
        var list = data.Read("scripts/scripts.lst"); var actorMeta = ScriptListMetadata.Read(list, actorBinding.Index); var gateMeta = ScriptListMetadata.Read(list, gateBinding.Index);
        if (actorMeta.DeclaredLocals != 16 || gateMeta.DeclaredLocals != 9 || configuration.Entries[10].Value != 0)
            throw new InvalidDataException("Original variable declarations differ.");
        string actorKey = actor.Key, gateKey = gate.Key;
        var state = new BoundedScriptState(new[] {
            new ScriptVariableBlock(ScriptVariableScope.ScriptLocal, actorKey, actorBinding.Program.Sha256, Enumerable.Range(0, 16).ToArray(), new int[16]),
            new ScriptVariableBlock(ScriptVariableScope.ScriptLocal, gateKey, gateBinding.Program.Sha256, Enumerable.Range(0, 9).ToArray(), new int[9]),
            new ScriptVariableBlock(ScriptVariableScope.Map, map.Source.Sha256, map.Source.Sha256, Enumerable.Range(0, map.Prefix.GlobalVariables.Length).ToArray(), map.Prefix.GlobalVariables),
            new ScriptVariableBlock(ScriptVariableScope.GameGlobal, path, configuration.Source.Sha256, new[] { 10 }, new[] { configuration.Entries[10].Value }) });
        var initialized = state.Apply(new[] { new ScriptVariableWrite(ScriptVariableScope.ScriptLocal, gateKey, 3, 1),
            new ScriptVariableWrite(ScriptVariableScope.ScriptLocal, gateKey, 1, 1), new ScriptVariableWrite(ScriptVariableScope.ScriptLocal, gateKey, 0, 1) }, state.SessionToken, 0);
        string before = JsonSerializer.Serialize(state.Capture());
        var refused = state.Apply(new[] { new ScriptVariableWrite(ScriptVariableScope.ScriptLocal, actorKey, 12, 1),
            new ScriptVariableWrite(ScriptVariableScope.ScriptLocal, actorKey, 16, 1) }, state.SessionToken, state.Revision);
        if (initialized != ScriptWriteStatus.Ready || refused != ScriptWriteStatus.OutOfRange || before != JsonSerializer.Serialize(state.Capture()))
            throw new InvalidDataException("Real source-bounded storage failed isolation.");
        return new { Status = "TEMPLE_STATE_PROFILE_OK", Map = map.Source, configuration.Source, GlobalEntry = configuration.Entries[10],
            ActorMetadata = actorMeta, GateMetadata = gateMeta, State = state.Capture(), InitialGateWrites = "Verified entry subset applied to detached storage only",
            InvalidLocal16 = "refused atomically", RuntimeAdapterEnabled = false, OriginalReconstruction = "verified" };
    }
}
