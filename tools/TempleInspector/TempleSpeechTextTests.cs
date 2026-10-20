using System.Text;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class TempleSpeechTextTests
{
    public static void Run(Action<string, Action> check)
    {
        var profile = TempleSpeechDraftTests.Profile();
        int[] ids = Enumerable.Range(0, 5).SelectMany(i => profile.Step(i).ReplyMessages.Append(profile.Step(i).ChoiceMessage)).Append(100).Distinct().ToArray();
        MessageTextFile FileFor(string path, int? missing = null) => MessageTextFile.Read(new GameResource(path, "synthetic",
            Encoding.ASCII.GetBytes(string.Join("\n", ids.Where(i => i != missing).Select(i => $"{{{i}}}{{}}{{Synthetic message {i}}}")))));
        void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Invalid rehearsal text accepted."); }
        check("Speech text resolves every bounded choice and response from the selected local language", () =>
        {
            var text = new TempleSpeechText(profile, FileFor("text/english/dialog/actemvil.msg"), "english");
            Assert(text.Speaker == "Synthetic message 100" && text.Message(171) == "Synthetic message 171");
            foreach (int id in ids) Assert(text.Message(id) == $"Synthetic message {id}");
            Assert(text.Reply(profile.Step(0), "PLAYER") == "Synthetic message 103PLAYERSynthetic message 104");
            Assert(text.Reply(profile.Step(1), "PLAYER") == "Synthetic message 111");
            Reject(() => text.Reply(profile.Step(0), ""));
            Reject(() => text.Message(172));
        });
        check("Speech text refuses mixed language wrong actor missing messages and path-like language identifiers", () =>
        {
            Reject(() => new TempleSpeechText(profile, FileFor("text/french/dialog/actemvil.msg"), "english"));
            Reject(() => new TempleSpeechText(profile, FileFor("text/english/dialog/other.msg"), "english"));
            Reject(() => new TempleSpeechText(profile, FileFor("text/english/dialog/actemvil.msg", 165), "english"));
            Reject(() => new TempleSpeechText(profile, FileFor("text/english/dialog/actemvil.msg"), "../english"));
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Speech text assertion failed."); }
}
