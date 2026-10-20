using System.Text;
using System.Text.Json.Nodes;
using FalloutRemake.Runtime;

internal static class TempleSpeechStateFileTests
{
    public static void Run(Action<string, Action> check, string root)
    {
        var profile = TempleSpeechDraftTests.Profile();
        byte[] Initial() => TempleSpeechStateFile.Encode(profile, profile.CreateInitial());
        void Reject(byte[] bytes)
        {
            try { TempleSpeechStateFile.Decode(profile, bytes); }
            catch (InvalidDataException) { return; }
            throw new Exception("Invalid Speech state file was accepted.");
        }
        check("Speech rehearsal file round trips initial and accepted states with fresh session tokens", () =>
        {
            var state = profile.CreateInitial();
            var loaded = TempleSpeechStateFile.Decode(profile, Initial());
            Assert(profile.Classify(loaded.Capture()) == TempleSpeechState.Initial && loaded.SessionToken != state.SessionToken);
            var draft = TempleSpeechDraft.Begin(profile, state, 5, true)!;
            TempleSpeechDraftTests.Complete(draft); draft.CommitDetached();
            loaded = TempleSpeechStateFile.Decode(profile, TempleSpeechStateFile.Encode(profile, state));
            Assert(profile.Classify(loaded.Capture()) == TempleSpeechState.Accepted && loaded.Revision == 0);
            Assert(!loaded.TryRead(ScriptVariableScope.ScriptLocal, "actor", 16, out _));
        });
        check("Speech file rejects omitted unknown duplicate null and mistyped fields at every schema level", () =>
        {
            void Mutate(Action<JsonNode> change)
            { var node = JsonNode.Parse(Initial())!; change(node); Reject(Encoding.UTF8.GetBytes(node.ToJsonString())); }
            Mutate(n => n.AsObject().Remove("Version"));
            Mutate(n => n["Extra"] = 1);
            Mutate(n => n["State"]!["Blocks"]![0]!.AsObject().Remove("Scope"));
            Mutate(n => n["State"]!["Blocks"]![0]!["Extra"] = 1);
            Mutate(n => n["State"] = null);
            Mutate(n => n["State"]!["Blocks"]![0]!["Values"] = "bad");
            string json = Encoding.UTF8.GetString(Initial());
            Reject(Encoding.UTF8.GetBytes(json.Replace("\"Version\": 1", "\"Version\": 1, \"Version\": 1")));
            Reject(Encoding.UTF8.GetBytes(json.Replace("\"Scope\": 0", "\"Scope\": 0, \"Scope\": 0")));
        });
        check("Speech file rejects foreign sources disputed indices altered values and unsupported versions", () =>
        {
            foreach (Action<JsonNode> change in new Action<JsonNode>[] {
                n => n["Profile"] = new string('A',64), n => n["Version"] = 2,
                n => n["State"]!["Version"] = 2, n => n["State"]!["SchemaHash"] = new string('A',64),
                n => n["State"]!["Blocks"]![0]!["SourceHash"] = new string('A',64),
                n => n["State"]!["Blocks"]![0]!["Indices"]![15] = 16,
                n => n["State"]!["Blocks"]![0]!["Values"]![8] = 1 })
            { var node = JsonNode.Parse(Initial())!; change(node); Reject(Encoding.UTF8.GetBytes(node.ToJsonString())); }
            Reject(Array.Empty<byte>()); Reject(new byte[TempleSpeechStateFile.MaxBytes + 1]);
            Reject(Encoding.UTF8.GetBytes("{")); Reject(Encoding.UTF8.GetBytes(new string('[',20) + new string(']',20)));
        });
        check("Speech restore is atomic on invalid input and invalidates old drafts only after successful load", () =>
        {
            string path = Path.Combine(root, "speech-load.json"); var state = profile.CreateInitial();
            var draft = TempleSpeechDraft.Begin(profile, state, 5, true)!; var token = state.SessionToken;
            File.WriteAllText(path, "{}");
            try { TempleSpeechStateFile.RestoreInto(path, profile, state); throw new Exception("Invalid file loaded."); }
            catch (InvalidDataException) { }
            Assert(state.SessionToken == token && state.Revision == 0 && draft.Choose(profile.Step(0).ChoiceOffset) == TempleSpeechStatus.Ready);
            TempleSpeechStateFile.Write(path, profile, state); TempleSpeechStateFile.RestoreInto(path, profile, state);
            Assert(state.SessionToken != token && draft.Choose(profile.Step(1).ChoiceOffset) == TempleSpeechStatus.Stale);
        });
        check("Speech atomic replacement preserves a locked destination and removes only its own temporary file", () =>
        {
            string path = Path.Combine(root, "speech-lock.json"); var state = profile.CreateInitial();
            TempleSpeechStateFile.Write(path, profile, state); byte[] before = File.ReadAllBytes(path);
            var draft = TempleSpeechDraft.Begin(profile, state, 5, true)!; TempleSpeechDraftTests.Complete(draft); draft.CommitDetached();
            using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                try { TempleSpeechStateFile.Write(path, profile, state); throw new Exception("Locked file replaced."); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            Assert(before.SequenceEqual(File.ReadAllBytes(path)) && !Directory.EnumerateFiles(root, "temple-speech-*.tmp").Any());
            TempleSpeechStateFile.Write(path, profile, state);
            Assert(profile.Classify(TempleSpeechStateFile.Read(path, profile).Capture()) == TempleSpeechState.Accepted);
        });
    }
    private static void Assert(bool condition) { if (!condition) throw new Exception("Temple Speech file assertion failed."); }
}
