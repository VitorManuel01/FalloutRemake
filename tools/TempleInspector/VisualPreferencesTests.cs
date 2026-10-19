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
            for (int flags = 0; flags < 8; flags++)
            {
                var expected = new VisualPreferences((flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0);
                VisualPreferencesFile.Write(path, expected);
                Assert(VisualPreferencesFile.Read(path) == expected);
            }
            Assert(Directory.GetFiles(Path.GetDirectoryName(path)!).Length == 1);
            Assert(File.ReadAllText(path).Contains("\"Version\": 1"));
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
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Visual preference assertion failed."); }
    private static void Reject(Action action)
    { try { action(); } catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException) { return; } throw new Exception("Invalid visual preferences accepted."); }
}
