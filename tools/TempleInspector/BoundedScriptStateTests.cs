using System.Text.Json;
using FalloutRemake.Runtime;

internal static class BoundedScriptStateTests
{
    private const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    internal static ScriptVariableBlock[] Blocks() => new[] {
        new ScriptVariableBlock(ScriptVariableScope.ScriptLocal, "actor", Hash, Enumerable.Range(0, 16).ToArray(), new int[16]),
        new ScriptVariableBlock(ScriptVariableScope.ScriptLocal, "gate", Hash, Enumerable.Range(0, 9).ToArray(), new int[9]),
        new ScriptVariableBlock(ScriptVariableScope.Map, "map", Hash, Enumerable.Range(0, 21).ToArray(), new int[21]),
        new ScriptVariableBlock(ScriptVariableScope.GameGlobal, "game", Hash, new[] { 10 }, new[] { 0 }) };
    private static string State(BoundedScriptState state) => JsonSerializer.Serialize(state.Capture());
    public static void Run(Action<string, Action> check)
    {
        check("bounded script variables keep actor gate map and sparse global indices independent", () =>
        {
            var state = new BoundedScriptState(Blocks());
            Assert(state.Apply(new[] { new ScriptVariableWrite(ScriptVariableScope.ScriptLocal, "actor", 12, 1),
                new ScriptVariableWrite(ScriptVariableScope.Map, "map", 12, 2), new ScriptVariableWrite(ScriptVariableScope.GameGlobal, "game", 10, 3) }, state.SessionToken, 0) == ScriptWriteStatus.Ready);
            Assert(Read(state, ScriptVariableScope.ScriptLocal, "actor", 12) == 1 && Read(state, ScriptVariableScope.Map, "map", 12) == 2
                && Read(state, ScriptVariableScope.GameGlobal, "game", 10) == 3 && Read(state, ScriptVariableScope.ScriptLocal, "gate", 0) == 0 && state.Revision == 1);
            Assert(!state.TryRead(ScriptVariableScope.ScriptLocal, "actor", 16, out _) && !state.TryRead(ScriptVariableScope.GameGlobal, "game", 9, out _));
        });
        check("bounded writes refuse local 16 and duplicate unknown or foreign-space targets without partial mutation", () =>
        {
            var state = new BoundedScriptState(Blocks()); string before = State(state);
            var good = new ScriptVariableWrite(ScriptVariableScope.ScriptLocal, "actor", 12, 1);
            foreach (var (bad, status) in new[] { (good with { Index = 16 }, ScriptWriteStatus.OutOfRange),
                (good with { Key = "foreign" }, ScriptWriteStatus.UnknownBlock), (good with { Scope = ScriptVariableScope.Map }, ScriptWriteStatus.UnknownBlock),
                (good, ScriptWriteStatus.Duplicate), (good with { Index = -1 }, ScriptWriteStatus.OutOfRange) })
                Assert(state.Apply(new[] { good, bad }, state.SessionToken, 0) == status && State(state) == before && state.Revision == 0);
        });
        check("bounded transactions reject stale revisions sessions and invalid input while no-op does not advance", () =>
        {
            var state = new BoundedScriptState(Blocks()); Guid token = state.SessionToken;
            var write = new ScriptVariableWrite(ScriptVariableScope.ScriptLocal, "actor", 12, 1);
            Assert(state.Apply(Array.Empty<ScriptVariableWrite>(), token, 0) == ScriptWriteStatus.NoChange);
            Assert(state.Apply(new[] { write }, token, 0) == ScriptWriteStatus.Ready);
            Assert(state.Apply(new[] { write }, token, 0) == ScriptWriteStatus.Stale && state.Apply(new[] { write }, token, 1) == ScriptWriteStatus.NoChange);
            Assert(state.Apply(null, token, 1) == ScriptWriteStatus.Invalid);
            Assert(state.Apply(Enumerable.Repeat(write, BoundedScriptState.MaxWrites + 1).ToArray(), token, 1) == ScriptWriteStatus.LimitReached);
            state.Reset(); Assert(state.SessionToken != token && state.Apply(new[] { write }, token, 0) == ScriptWriteStatus.Stale);
        });
        check("bounded script schemas and snapshots own independent arrays and reject malformed declarations", () =>
        {
            var blocks = Blocks(); var state = new BoundedScriptState(blocks); string before = State(state);
            blocks[0].InitialValues[0] = 99; blocks[0].Indices[0] = 99; var saved = state.Capture(); saved.Blocks[0].Values[0] = 88; saved.Blocks[0].Indices[0] = 88;
            Assert(State(state) == before);
            foreach (var invalid in new[] { Blocks()[0] with { Indices = new[] { 0, 0 }, InitialValues = new int[2] },
                Blocks()[0] with { Indices = new[] { 2, 1 }, InitialValues = new int[2] }, Blocks()[0] with { SourceHash = "foreign" } })
                Reject(() => new BoundedScriptState(new[] { invalid }));
            Reject(() => new BoundedScriptState(new[] { Blocks()[0], Blocks()[0] }));
        });
        check("bounded script restoration is atomic on wrong fingerprints scope indices and sizes and invalidates old plans", () =>
        {
            var state = new BoundedScriptState(Blocks()); Guid token = state.SessionToken;
            state.Apply(new[] { new ScriptVariableWrite(ScriptVariableScope.ScriptLocal, "actor", 12, 1) }, token, 0);
            string before = State(state); var snapshot = state.Capture();
            foreach (var bad in new[] { snapshot with { Version = 2 }, snapshot with { SchemaHash = new string('B', 64) },
                snapshot with { Blocks = snapshot.Blocks.Select((b, i) => i == 3 ? b with { Values = Array.Empty<int>() } : b).ToArray() },
                snapshot with { Blocks = snapshot.Blocks.Select((b, i) => i == 3 ? b with { Indices = new[] { 9 } } : b).ToArray() } })
            { Reject(() => state.Restore(bad)); Assert(State(state) == before && state.SessionToken == token && state.Revision == 1); }
            state.Restore(snapshot); snapshot.Blocks[0].Values[12] = 0;
            Assert(State(state) == before && state.SessionToken != token && state.Revision == 0);
        });
    }
    private static int Read(BoundedScriptState state, ScriptVariableScope scope, string key, int index)
    { if (!state.TryRead(scope, key, index, out int value)) throw new Exception("Missing variable."); return value; }
    private static void Assert(bool value) { if (!value) throw new Exception("Bounded script state assertion failed."); }
    private static void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Invalid state accepted."); }
}
