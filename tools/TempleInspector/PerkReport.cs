using System.Text;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

/// <summary>
/// The CE perk table (gPerkDescriptions, fallout2.exe 0x519DCC) checked row by row against <see cref="PerkCatalog"/>, with the
/// names of perk.msg (101 + perk) and, for the neutral profile and the premades at level 3, the perks CE would offer.
/// </summary>
internal static class PerkReport
{
    const uint Table = 0x519DCC; const int RowBytes = 19 * 4;

    public static object Create(GameData data, string installDirectory)
    {
        var exe = new CriticalReport.Executable(File.ReadAllBytes(Path.Combine(installDirectory, "fallout2.exe")));
        var differing = new List<string>();
        for (int perk = 0; perk < PerkCatalog.Count; perk++)
        {
            int[] original = Enumerable.Range(2, 17).Select(i => exe.Int(Table + (uint)(perk * RowBytes + 4 * i))).ToArray();
            PerkDefinition d = PerkCatalog.Get(perk);
            int[] runtime = new[] { d.Frm, d.MaxRank, d.MinLevel, d.Stat, d.StatModifier, d.Param1, d.Value1, d.ParamMode, d.Param2, d.Value2 }.Concat(d.Stats).ToArray();
            if (!original.SequenceEqual(runtime)) differing.Add($"{perk}: exe {string.Join(",", original)}");
        }
        if (differing.Count > 0) throw new InvalidDataException("Perk rows differ from fallout2.exe: " + string.Join("; ", differing));

        string text = Encoding.Latin1.GetString(new ResourceCatalog(data.Read).Read("text/english/game/perk.msg").Bytes);
        string Message(int id) => text.Split('\n').FirstOrDefault(l => l.StartsWith("{" + id + "}")) is { } line ? line.Split('{', '}')[^2].Trim() : "?";
        var characters = new[] { PlayerCharacter.Neutral }.Concat(new[] { "combat", "stealth", "diplomat" }
            .Select(name => PlayerCharacter.FromPremade(CharacterFile.Read(data.Read($"premade/{name}.gcd"))))).ToArray();
        return new
        {
            Status = "PERK_TABLE_OK",
            Selectable = Enumerable.Range(0, PerkCatalog.Count).Where(p => PerkCatalog.Get(p).Selectable)
                .Select(p => new { Id = p, Name = Message(101 + p), PerkCatalog.Get(p).MinLevel, PerkCatalog.Get(p).MaxRank, Effect = PerkCatalog.Effect(p).ToString() }),
            AtLevel3 = characters.Select(c => new
            {
                c.Name,
                Offered = Enumerable.Range(0, PerkCatalog.Count).Where(p => PerkRules.CanAdd(p, c, 3, PerkRanks.None) is PerkChoiceStatus.Ready or PerkChoiceStatus.NotModeled)
                    .Select(p => $"{Message(101 + p)}{(PerkCatalog.Effect(p) == PerkEffect.Applied ? "" : $" ({PerkCatalog.Effect(p)})")}")
            }),
            OriginalReconstruction = "verified"
        };
    }
}
