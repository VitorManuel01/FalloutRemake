using System.Text.Json;
using FalloutRemake.Runtime;

internal static class TempleSpeechDraftTests
{
    internal static TempleSpeechProfile Profile() => new("actor", "gate", "map", "game", new int[21]);
    internal static void Complete(TempleSpeechDraft draft)
    { foreach (int offset in TempleSpeechProfile.ChoiceOffsets) Assert(draft.Choose(offset) == TempleSpeechStatus.Ready); }
    public static void Run(Action<string, Action> check)
    {
        check("bounded Speech draft traverses only the five verified choices without changing state before confirmation", () =>
        {
            var profile = Profile(); var state = profile.CreateInitial(); string before = JsonSerializer.Serialize(state.Capture());
            var draft = TempleSpeechDraft.Begin(profile, state, 5, true)!;
            Assert(draft.CommitDetached() == TempleSpeechStatus.Incomplete && draft.Choose(12966) == TempleSpeechStatus.UnsupportedChoice && draft.StepIndex == 0);
            Complete(draft); Assert(before == JsonSerializer.Serialize(state.Capture()) && draft.ChoicesComplete);
            Assert(draft.CommitDetached() == TempleSpeechStatus.Ready && state.Revision == 1 && profile.Classify(state.Capture()) == TempleSpeechState.Accepted);
            Assert(draft.CommitDetached() == TempleSpeechStatus.AlreadyCommitted && state.Revision == 1);
        });
        check("Speech rehearsal enforces tagged skill and effective intelligence and never makes the disputed local 16 available", () =>
        {
            var profile = Profile(); var state = profile.CreateInitial();
            Assert(TempleSpeechDraft.Begin(profile, state, 3, true) == null && TempleSpeechDraft.Begin(profile, state, 5, false) == null);
            Assert(!state.TryRead(ScriptVariableScope.ScriptLocal, "actor", 16, out _));
            var draft = TempleSpeechDraft.Begin(profile, state, 5, true)!; Complete(draft); draft.CommitDetached();
            Assert(TempleSpeechDraft.Begin(profile, state, 5, true) == null && !state.TryRead(ScriptVariableScope.ScriptLocal, "actor", 16, out _));
        });
        check("cancelled stale and reloaded Speech drafts cannot write or resume an unsupported state", () =>
        {
            var profile = Profile(); var state = profile.CreateInitial(); var draft = TempleSpeechDraft.Begin(profile, state, 5, true)!;
            Complete(draft); draft.Cancel(); Assert(draft.CommitDetached() == TempleSpeechStatus.Cancelled && state.Revision == 0);
            draft = TempleSpeechDraft.Begin(profile, state, 5, true)!; Complete(draft); state.Restore(state.Capture());
            Assert(draft.CommitDetached() == TempleSpeechStatus.Stale && state.Revision == 0);
            draft = TempleSpeechDraft.Begin(profile, state, 5, true)!; Complete(draft);
            state.Apply(new[] { new ScriptVariableWrite(ScriptVariableScope.ScriptLocal, "actor", 8, 1) }, state.SessionToken, 0);
            Assert(draft.CommitDetached() == TempleSpeechStatus.Stale && profile.Classify(state.Capture()) == TempleSpeechState.Unsupported
                && TempleSpeechDraft.Begin(profile, state, 5, true) == null);
        });
        check("Speech profile views own message arrays and state classification rejects a forged block identity", () =>
        {
            var profile = Profile(); var step = profile.Step(0); step.ReplyMessages[0] = 999;
            Assert(profile.Step(0).ReplyMessages[0] == 103);
            var snapshot = profile.CreateInitial().Capture(); snapshot.Blocks[0] = snapshot.Blocks[0] with { SourceHash = new string('B', 64) };
            Assert(profile.Classify(snapshot) == TempleSpeechState.Unsupported);
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Temple Speech draft assertion failed."); }
}
