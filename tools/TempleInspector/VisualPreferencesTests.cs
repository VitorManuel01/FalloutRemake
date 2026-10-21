using System.Text;
using FalloutRemake.Runtime;

internal static class VisualPreferencesTests
{
    public static void Run(Action<string, Action> check, string root)
    {
        string path = Path.Combine(root, "visual", "preferences.json");
        check("missing visual preferences use defaults without creating a file", () =>
        {
            Assert(VisualPreferencesFile.Read(path) == new VisualPreferences(true, false, true));
            Assert(!Directory.Exists(Path.GetDirectoryName(path)));
        });
        check("all visual choices survive independent reads and atomic replacement without carrying world data", () =>
        {
            for (int flags = 0; flags < 16; flags++)
            {
                var expected = new VisualPreferences((flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0,(flags & 8) != 0);
                VisualPreferencesFile.Write(path, expected);
                Assert(VisualPreferencesFile.Read(path) == expected);
            }
            Assert(Directory.GetFiles(Path.GetDirectoryName(path)!).Length == 1);
            Assert(File.ReadAllText(path).Contains("\"Version\": 2"));
        });
        check("legacy visual choices preserve their original file and enable Nature by default without rewriting on read",() =>
        {
            string original = "{\"Version\":1,\"ShowNavigationGrid\":false,\"UseOriginalFloor\":true,\"RevealActor\":false}";
            File.WriteAllText(path,original);
            Assert(VisualPreferencesFile.Read(path) == new VisualPreferences(false,true,false,true) && File.ReadAllText(path) == original);
        });
        check("visual v2 requires an explicit boolean Nature choice and rejects duplicate unknown and missing fields",() =>
        {
            string valid = "{\"Version\":2,\"ShowNavigationGrid\":true,\"UseOriginalFloor\":false,\"RevealActor\":true,\"ShowNatureModels\":false}";
            Assert(!VisualPreferencesFile.Decode(Encoding.UTF8.GetBytes(valid)).ShowNatureModels);
            foreach (string bad in new[] {valid.Replace(",\"ShowNatureModels\":false",""),valid.Replace("\"ShowNatureModels\":false","\"ShowNatureModels\":null"),
                valid.Replace("\"ShowNatureModels\":false","\"ShowNatureModels\":\"false\""),valid.Replace("\"ShowNatureModels\":false","\"RevealActor\":false"),
                valid.Replace("}",",\"Unexpected\":false}"),valid.Replace("\"Version\":2","\"Version\":3")})
                Reject(() => VisualPreferencesFile.Decode(Encoding.UTF8.GetBytes(bad)));
        });
        check("invalid or incompatible visual preferences are refused without rewriting the source", () =>
        {
            string valid = "{\"Version\":1,\"ShowNavigationGrid\":true,\"UseOriginalFloor\":false,\"RevealActor\":true}";
            foreach (string invalid in new[] { "", "{", "null", "[]", valid.Replace("\"Version\":1", "\"Version\":2"),
                valid.Replace("\"Version\":1", "\"Version\":\"1\""), valid.Replace("\"ShowNavigationGrid\":true", "\"ShowNavigationGrid\":null"),
                valid.Replace(",\"RevealActor\":true", ""), valid.Replace("\"RevealActor\":true", "\"ShowNavigationGrid\":false"),
                valid.Replace("}", ",\"GameDataDirectory\":\"ignored\"}"), new string(' ', VisualPreferencesFile.MaxBytes + 1) })
            {
                File.WriteAllText(path, invalid); Reject(() => VisualPreferencesFile.Read(path));
                Assert(File.ReadAllText(path) == invalid);
            }
            Reject(() => VisualPreferencesFile.Decode(Encoding.UTF8.GetBytes("{\"Version\":{\"nested\":{\"too\":{\"deep\":{}}}}}")));
        });
        check("failed visual preference replacement preserves the previous file and cleans temporary data", () =>
        {
            VisualPreferencesFile.Write(path, VisualPreferences.Defaults);
            byte[] before = File.ReadAllBytes(path);
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                Reject(() => VisualPreferencesFile.Write(path, new(false, true, false)));
            Assert(before.SequenceEqual(File.ReadAllBytes(path)));
            Assert(Directory.GetFiles(Path.GetDirectoryName(path)!).Length == 1);
        });
        check("transient Windows replacement denials retry only the rename and preserve the prior file until success", () =>
        {
            string source = Path.Combine(root, "transient.tmp"), target = Path.Combine(root, "transient.json");
            File.WriteAllText(source, "new"); File.WriteAllText(target, "old");
            var delays = new List<int>(); int attempts = 0;
            AtomicFileReplacement.Replace(source, target, (from, to, overwrite) => {
                attempts++; Assert(File.ReadAllText(to) == "old" && File.ReadAllText(from) == "new");
                if (attempts < 3) throw new IOException("Synthetic sharing violation", unchecked((int)0x80070020));
                File.Move(from, to, overwrite);
            }, delays.Add);
            Assert(attempts == 3 && delays.SequenceEqual(new[] { 10, 25 }) && File.ReadAllText(target) == "new" && !File.Exists(source));
        });
        check("replacement retry is bounded and unrelated filesystem errors fail immediately", () =>
        {
            foreach (int code in new[] { 5, 32, 33, 3 })
            {
                int attempts = 0; var delays = new List<int>();
                Reject(() => AtomicFileReplacement.Replace("unused", "unused", (_, _, _) => {
                    attempts++; throw new IOException("Synthetic filesystem denial", unchecked((int)0x80070000) | code);
                }, delays.Add));
                Assert(attempts == (code == 3 ? 1 : 4) && delays.Sum() == (code == 3 ? 0 : 85));
            }
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Visual preference assertion failed."); }
    private static void Reject(Action action)
    { try { action(); } catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException) { return; } throw new Exception("Invalid visual preferences accepted."); }
}
