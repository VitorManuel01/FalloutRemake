using System.Text;
using System.Text.Json.Nodes;
using FalloutRemake.Runtime;
using static TempleNpcDepartureTests;

internal static class TempleNpcDepartureFileTests
{
    public static void Run(Action<string, Action> check, string root)
    {
        var profile = Profile();
        void Reject(byte[] bytes)
        {
            try { TempleNpcDepartureFile.Decode(profile, bytes); }
            catch (InvalidDataException) { return; }
            throw new Exception("Invalid NPC departure file was accepted.");
        }
        check("NPC file round trips every movement hex timer remainder and hidden state with new action tokens", () =>
        {
            var session = Begin(profile); int count = 0;
            do
            {
                var loaded = TempleNpcDepartureFile.Decode(profile, TempleNpcDepartureFile.Encode(session)); count++;
                Assert(loaded.Capture() == session.Capture() && loaded.Location == session.Location && loaded.Revision == 0
                    && loaded.SessionToken != session.SessionToken && profile.Gate(loaded.Capture()) == profile.Gate(session.Capture()));
                if (session.Capture().Phase == NpcDeparturePhase.Hidden) break;
                if (session.Walking) session.Step(session.SessionToken, session.Revision);
                else session.AdvanceDelay(1, session.SessionToken, session.Revision);
            } while (count < 40000);
            Assert(count < 40000 && session.Capture().Phase == NpcDeparturePhase.Hidden);
        });
        check("NPC file refuses missing unknown duplicate null wrong-type and incompatible phase fields", () =>
        {
            byte[] initial = TempleNpcDepartureFile.Encode(Begin(profile));
            foreach (string name in new[] { "Version", "Profile", "Phase", "RouteIndex", "RemainingTicks" })
            {
                var node = JsonNode.Parse(initial)!; node.AsObject().Remove(name); Reject(Encoding.UTF8.GetBytes(node.ToJsonString()));
                node = JsonNode.Parse(initial)!; node[name] = null; Reject(Encoding.UTF8.GetBytes(node.ToJsonString()));
            }
            foreach (Action<JsonNode> change in new Action<JsonNode>[] {
                n => n["Extra"] = 1, n => n["Profile"] = "foreign", n => n["Version"] = 2, n => n["Phase"] = 99,
                n => n["Phase"] = "ToGate", n => n["RouteIndex"] = -1, n => n["RouteIndex"] = 40000,
                n => n["RemainingTicks"] = 1, n => n["RemainingTicks"] = 2147483648L })
            { var node = JsonNode.Parse(initial)!; change(node); Reject(Encoding.UTF8.GetBytes(node.ToJsonString())); }
            string json = Encoding.UTF8.GetString(initial);
            Reject(Encoding.UTF8.GetBytes(json.Replace("\"Version\": 1", "\"Version\": 1, \"Version\": 1")));
            Reject(Encoding.UTF8.GetBytes("null")); Reject(Encoding.UTF8.GetBytes("[]")); Reject(Encoding.UTF8.GetBytes("{"));
            Reject(Array.Empty<byte>()); Reject(new byte[TempleNpcDepartureFile.MaxBytes + 1]);
        });
        check("NPC file validates event and movement as one record and rejected loading preserves the live session", () =>
        {
            var live = Begin(profile); ToDelay(live); live.AdvanceDelay(9, live.SessionToken, live.Revision);
            string path = Path.Combine(root, "departure-invalid.json"); var before = live.Capture(); Guid token = live.SessionToken;
            foreach (Action<JsonNode> change in new Action<JsonNode>[] { n => n["RemainingTicks"] = 0, n => n["RemainingTicks"] = 21,
                n => n["RouteIndex"] = 1, n => n["Phase"] = (int)NpcDeparturePhase.Hidden, n => n["Phase"] = (int)NpcDeparturePhase.ToMiddle })
            {
                var node = JsonNode.Parse(TempleNpcDepartureFile.Encode(live))!; change(node); File.WriteAllText(path, node.ToJsonString());
                try { live = TempleNpcDepartureFile.Read(path, profile); throw new Exception("Invalid file loaded."); }
                catch (InvalidDataException) { }
                Assert(live.Capture() == before && live.SessionToken == token);
            }
            TempleNpcDepartureFile.Write(path, live); var candidate = TempleNpcDepartureFile.Read(path, profile);
            Assert(candidate.Capture() == before && candidate.SessionToken != token
                && candidate.AdvanceDelay(1, token, live.Revision).Status == NpcDepartureStatus.Stale);
        });
        check("NPC atomic save preserves a locked previous file and cleans only its own temporary file", () =>
        {
            string path = Path.Combine(root, "departure-locked.json"); var session = Begin(profile);
            TempleNpcDepartureFile.Write(path, session); byte[] before = File.ReadAllBytes(path);
            session.Step(session.SessionToken, 0);
            using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                try { TempleNpcDepartureFile.Write(path, session); throw new Exception("Locked file replaced."); }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            Assert(before.SequenceEqual(File.ReadAllBytes(path)) && !Directory.EnumerateFiles(root, "temple-departure-*.tmp").Any());
            TempleNpcDepartureFile.Write(path, session);
            Assert(TempleNpcDepartureFile.Read(path, profile).Capture() == session.Capture());
        });
    }
}
