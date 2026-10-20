using System.Text;
using FalloutRemake.Import;

internal static class ScriptMetadataTests
{
    private static GameResource Source(string text) => new("scripts/scripts.lst", "synthetic", Encoding.ASCII.GetBytes(text));
    public static void Run(Action<string, Action> check)
    {
        check("script metadata preserves exact physical indices program identities and local counts without changing source bytes", () =>
        {
            var source = Source("first.int # local_vars=17\r\nsecond.int # local_vars=8\r\n"); byte[] before = source.Bytes.ToArray();
            var first = ScriptListMetadata.Read(source, 0); var second = ScriptListMetadata.Read(source, 1);
            Assert(first.Index == 0 && first.ProgramPath == "scripts/first.int" && first.DeclaredLocals == 17
                && second.Index == 1 && second.ProgramPath == "scripts/second.int" && second.DeclaredLocals == 8
                && first.Source == second.Source && source.Bytes.SequenceEqual(before));
        });
        check("script metadata does not compact blank or comment entries and rejects foreign paths and out-of-range indices", () =>
        {
            var source = Source("first.int\n\n# comment\nlast.int # local_vars=1\n");
            Reject(() => ScriptListMetadata.Read(source, 1)); Reject(() => ScriptListMetadata.Read(source, 2));
            Assert(ScriptListMetadata.Read(source, 3).ProgramPath == "scripts/last.int");
            foreach (int index in new[] { -1, 4, int.MaxValue }) Reject(() => ScriptListMetadata.Read(source, index));
            Reject(() => ScriptListMetadata.Read(source with { Path = "other.lst" }, 0));
            Reject(() => ScriptListMetadata.Read(Source("../escape.int # local_vars=1"), 0));
        });
        check("script metadata follows the native hash-comment gate and supports explicit zero and bounded declarations", () =>
        {
            Assert(ScriptListMetadata.Read(Source("entry.int"), 0).DeclaredLocals == 0);
            Assert(ScriptListMetadata.Read(Source("entry.int local_vars=8"), 0).DeclaredLocals == 0);
            Assert(ScriptListMetadata.Read(Source("entry.int # local_vars=0"), 0).DeclaredLocals == 0);
            Assert(ScriptListMetadata.Read(Source("entry.int # local_vars=\t4096 ; metadata"), 0).DeclaredLocals == 4096);
        });
        check("script metadata refuses ambiguous negative overflowing and malformed counts instead of silently coercing them", () =>
        {
            foreach (string count in new[] { "", "-1", "+1", "4097", "2147483648", "8oops", "8 local_vars=2" })
                Reject(() => ScriptListMetadata.Read(Source("entry.int # local_vars=" + count), 0));
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Script metadata assertion failed."); }
    private static void Reject(Action action)
    { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Unsupported script metadata accepted."); }
}
