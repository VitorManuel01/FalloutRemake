using System.Text;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;

/// <summary>
/// First Aid and Doctor on original data: the skills' rows of gSkillDescriptions (fallout2.exe 0x51D118: default, stat
/// multiplier, stats, XP) and gHealableDamageFlags (0x4AA304) against <see cref="MedicalRules"/>, the skill.msg and proto.msg
/// lines the uses print, the four kits' PROs, where kits lie on the supported maps (3, 4, 126), and the premades' chances.
/// </summary>
internal static class MedicalReport
{
    const uint SkillTable = 0x51D118, HealableFlags = 0x4AA304; const int RowBytes = 11 * 4;

    public static object Create(GameData data, string installDirectory)
    {
        var exe = new CriticalReport.Executable(File.ReadAllBytes(Path.Combine(installDirectory, "fallout2.exe")));
        int[] Row(int skill) => Enumerable.Range(4, 7).Select(i => exe.Int(SkillTable + (uint)(skill * RowBytes + 4 * i))).ToArray();
        int[] firstAid = Row(CharacterSkill.FirstAid), doctor = Row(CharacterSkill.Doctor);
        // defaultValue, statModifier, stat1, stat2, baseValueMult, experience, field_28
        if (!firstAid.SequenceEqual(new[] { 0, 2, 1, 4, 1, MedicalRules.FirstAidExperience, 0 }) || !doctor.SequenceEqual(new[] { 5, 1, 1, 4, 1, MedicalRules.DoctorExperience, 0 }))
            throw new InvalidDataException($"Skill rows differ from fallout2.exe: First Aid {string.Join(",", firstAid)}; Doctor {string.Join(",", doctor)}");
        int[] healable = Enumerable.Range(0, 5).Select(i => exe.Int(HealableFlags + (uint)(4 * i))).ToArray();
        if (!healable.SequenceEqual(MedicalRules.Healable.Select(f => (int)f)))
            throw new InvalidDataException("gHealableDamageFlags differ from fallout2.exe: " + string.Join(",", healable.Select(f => $"0x{f:X}")));

        var resources = new ResourceCatalog(data.Read); var prototypes = new PrototypeCatalog(resources);
        string Message(string file, int id)
        {
            string text = Encoding.Latin1.GetString(resources.Read("text/english/game/" + file).Bytes);
            return text.Split('\n').FirstOrDefault(l => l.StartsWith("{" + id + "}")) is { } line ? line.Split('{', '}')[^2].Trim() : "?";
        }
        var catalog = new MapCatalog(data.Read("data/maps.txt"));
        int Count(MapObject o) => (MedicalKits.For(o.Header.Pid) != null ? 1 : 0) + o.Inventory.Sum(e => MedicalKits.For(e.Object.Header.Pid) != null ? e.Quantity : Count(e.Object));
        var characters = new[] { PlayerCharacter.Neutral }.Concat(new[] { "combat", "stealth", "diplomat" }
            .Select(name => PlayerCharacter.FromPremade(CharacterFile.Read(data.Read($"premade/{name}.gcd"))))).ToArray();
        return new
        {
            Status = "MEDICAL_RULES_OK",
            Executable = new { FirstAid = firstAid, Doctor = doctor, Healable = healable.Select(f => ((DamageFlags)f).ToString()) },
            Messages = new[] { 500, 501, 503, 512, 520, 525, 530, 531, 532, 533, 534, 590, 591, 592 }.ToDictionary(id => id, id => Message("skill.msg", id)),
            Kits = MedicalKits.All.Select(k =>
            {
                Prototype proto = prototypes.Get(k.Pid);
                return new { k.Pid, Name = Message("pro_item.msg", proto.MessageId), proto.Type, proto.Subtype, proto.ScriptId, Skill = k.Skill == CharacterSkill.FirstAid ? "First Aid" : "Doctor",
                    k.Bonus, RunsOut = Message("proto.msg", k.Message), Supported = MedicalKits.Supported(proto) };
            }),
            InCombat = Message("proto.msg", 902),
            KitsOnMaps = new[] { 3, 4, 126 }.ToDictionary(id => id, id => catalog.Load(data, id).ObjectLayers.SelectMany(l => l.Objects).Sum(Count)),
            Characters = characters.Select(c => new { c.Name, FirstAid = c.Skill(CharacterSkill.FirstAid), Doctor = c.Skill(CharacterSkill.Doctor), c.CriticalChance,
                FirstAidRoll = c.Skill(CharacterSkill.FirstAid) + c.CriticalChance }),
            OriginalReconstruction = "verified"
        };
    }
}
