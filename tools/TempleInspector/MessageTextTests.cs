using System.Buffers.Binary;
using System.Text;
using FalloutRemake.Import;

internal static class MessageTextTests
{
    private static GameResource Resource(string text) => new("synthetic.msg", "synthetic", Encoding.Latin1.GetBytes(text));
    internal static string[] Paths(string language) => new[] { "item", "crit", "scen", "wall", "tile", "misc" }
        .Select(type => $"text/{language}/game/pro_{type}.msg").ToArray();
    public static void Run(Action<string, Action> check)
    {
        check("MSG retains numeric IDs, empty/audio fields, legacy characters and multiline text", () =>
        {
            var file = MessageTextFile.Read(Resource("comment\r\n{+7}{VOICE}{café\r\nnext}\n{-2}{}{}\nend"));
            Assert(file.Entries.Count == 2 && file.Entries[7].Audio == "VOICE" && file.Entries[7].Text == "cafénext" && file.Entries[-2].Text == "");
        });
        check("MSG duplicate IDs replace earlier entries like CE and input bytes remain untouched", () =>
        {
            GameResource input = Resource("{1}{}{first}{1}{voice}{last}"); byte[] original = (byte[])input.Bytes.Clone();
            var file = MessageTextFile.Read(input);
            Assert(file.Replacements == 1 && file.Entries.Count == 1 && file.Entries[1].Text == "last" && input.Bytes.SequenceEqual(original));
            input.Bytes[0] = 0; Assert(file.Entries[1].Text == "last" && file.Source.Sha256.Length == 64);
        });
        check("MSG corruption, partial records, overflowing IDs, NUL and limits are rejected", () =>
        {
            foreach (string text in new[] { "}", "{1", "{1}", "{1}{}", "{x}{}{}", "{2147483648}{}{}", "{ 1}{}{}", "{+}{}{}", "{1}{}{a\0b}" })
                Reject(() => MessageTextFile.Read(Resource(text)));
            Reject(() => MessageTextFile.Read(Resource("{1}{}{out" + new string('x', 1024) + "}")));
            Reject(() => MessageTextFile.Read(new("large", "synthetic", new byte[MessageTextFile.MaxBytes + 1])));
            Reject(() => MessageTextFile.Read(Resource(string.Concat(Enumerable.Repeat("{1}{}{}", MessageTextFile.MaxRecords + 1)))));
        });
        check("prototype text uses the PRO message ID rather than PID arithmetic, with optional description", () =>
        {
            int reads = 0;
            var catalog = new PrototypeTextCatalog(path => { reads++; return Resource("{37}{}{Synthetic name}{38}{}{Synthetic description}{99}{}{}"); }, Paths("test"));
            Prototype proto = Proto(7, 37);
            PrototypeText text = catalog.Resolve(proto)!;
            Assert(text.MessageId == 37 && text.Name == "Synthetic name" && text.Description == "Synthetic description" && reads == 1);
            Assert(catalog.Resolve(Proto(8, 99))!.Name == null && reads == 1);
            Assert(catalog.Resolve(Proto(9, -1)) == null && catalog.Dependencies.Length == 1);
            Assert(catalog.Resolve(Proto(10, int.MaxValue))!.Description == null);
        });
        check("prototype text requires one complete discovered language and never mixes language sets", () =>
        {
            Reject(() => new PrototypeTextCatalog(_ => Resource(""), Array.Empty<string>()));
            Reject(() => new PrototypeTextCatalog(_ => Resource(""), Paths("one").Take(5)));
            Reject(() => new PrototypeTextCatalog(_ => Resource(""), Paths("one").Concat(Paths("two"))));
            var catalog = new PrototypeTextCatalog(_ => Resource(""), Paths("one").Concat(Paths("two").Take(2)));
            Assert(catalog.Language == "one");
            Reject(() => catalog.FileForType(-1)); Reject(() => catalog.FileForType(6));
        });
        check("prototype MSG type order distinguishes walls and tiles and keeps list fingerprints", () =>
        {
            var seen = new List<string>();
            var catalog = new PrototypeTextCatalog(path => { seen.Add(path); return new(path, "synthetic", Array.Empty<byte>()); }, Paths("test"));
            for (int i = 0; i < 6; i++) catalog.FileForType(i);
            Assert(seen[3].EndsWith("pro_wall.msg") && seen[4].EndsWith("pro_tile.msg") && catalog.Dependencies.Length == 6);
        });
    }
    private static Prototype Proto(int pid, int message)
    {
        byte[] bytes = new byte[28]; BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(4), message);
        return new(pid, 0, null, 0, null, ResourceIdentity.From(new("test.pro", "synthetic", bytes)), bytes);
    }
    private static void Assert(bool value) { if (!value) throw new Exception("MSG text assertion failed."); }
    private static void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Invalid MSG accepted."); }
}
