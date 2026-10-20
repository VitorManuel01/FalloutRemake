using System.Text;
using FalloutRemake.Import;

internal static class GameGlobalTests
{
    private static GameResource Source(string text) => new("data/fixture.gam", "synthetic", Encoding.ASCII.GetBytes(text));
    public static void Run(Action<string, Action> check)
    {
        check("GAM entries use native section order signed values names and zero rows without compacting indices", () =>
        {
            var source = Source("// before\nGAME_GLOBAL_VARS:\nFIRST :=0;\n\n// skip\n   \n // indented counts\nSECOND :=-2; // note\n");
            byte[] before = source.Bytes.ToArray(); var file = GameGlobalFile.Read(source);
            Assert(file.Entries.Count == 4 && file.Entries[0].Name == "FIRST" && file.Entries[0].Value == 0
                && file.Entries[1].Name == null && file.Entries[2].Name == null && file.Entries[3].Name == "SECOND"
                && file.Entries[3].Value == -2 && file.Entries[3].Index == 3 && source.Bytes.SequenceEqual(before));
        });
        check("GAM lookup does not mistake the commented map section for the game section", () =>
        {
            var file = GameGlobalFile.Read(Source("//MAP_GLOBAL_VARS:\nIGNORED:=9;\nGAME_GLOBAL_VARS:\nVALUE:=+3;\n"));
            Assert(file.Entries.Single().Value == 3 && file.Entries.Single().Index == 0);
            Reject(() => GameGlobalFile.Read(Source("//GAME_GLOBAL_VARS:\nVALUE:=0;")));
            Reject(() => GameGlobalFile.Read(Source(" GAME_GLOBAL_VARS:\nVALUE:=0;")));
        });
        check("GAM malformed overflow long lines and missing entries fail rather than shifting indices or guessing", () =>
        {
            foreach (string text in new[] { "GAME_GLOBAL_VARS:\n", "GAME_GLOBAL_VARS:\nA:=oops;", "GAME_GLOBAL_VARS:\nA:=2147483648;",
                "GAME_GLOBAL_VARS:\n:=1;", "GAME_GLOBAL_VARS:\n" + new string('a', 258) }) Reject(() => GameGlobalFile.Read(Source(text)));
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("GAM metadata assertion failed."); }
    private static void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Invalid GAM accepted."); }
}
