using System.Buffers.Binary;
using System.Security.Cryptography;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;

/// <summary>
/// Evidence for CriticalRules on the original executable (read only): the torso rows of every kill type and of the player,
/// the critical failure table, the torso hit penalty, the clock's start value and the day 1 / day 6 divisions, at the
/// addresses CE documents for fallout2.exe 1.02. Then the temple's critical inputs: critter kill types, Critical Chance,
/// Better Criticals and knockout delays, the weapons' failure columns and the premades' critical traits.
/// </summary>
internal static class CriticalReport
{
    sealed class Executable
    {
        readonly byte[] _bytes; readonly uint _base; readonly (uint Va, uint Size, uint Raw)[] _sections;
        public Executable(byte[] bytes)
        {
            _bytes = bytes;
            int pe = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0x3C));
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(pe)) != 0x4550) throw new InvalidDataException("fallout2.exe is not a PE image.");
            int count = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(pe + 6)), optional = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(pe + 20));
            _base = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(pe + 52));
            _sections = Enumerable.Range(0, count).Select(i => pe + 24 + optional + 40 * i).Select(s => (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(s + 12)),
                Math.Max(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(s + 8)), BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(s + 16))),
                BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(s + 20)))).ToArray();
        }
        int Offset(uint va)
        {
            uint rva = va - _base;
            foreach (var (start, size, raw) in _sections) if (rva >= start && rva < start + size) return (int)(raw + rva - start);
            throw new InvalidDataException($"Address {va:X} lies outside fallout2.exe.");
        }
        public int Int(uint va) => BinaryPrimitives.ReadInt32LittleEndian(_bytes.AsSpan(Offset(va)));
        public ReadOnlySpan<byte> Bytes(uint va, int length) => _bytes.AsSpan(Offset(va), length);
    }

    // CE addresses (combat.cc, scripts.cc, random.cc comments): gCriticalHitTables, gPlayerCriticalHitTable, _cf_table,
    // hit_location_penalty_default, gGameTime, randomTranslateRoll, attackComputeCriticalFailure.
    const uint KillTable = 0x510978, PlayerTable = 0x5179B0, FailureTable = 0x517FA0, HitPenalty = 0x510954, GameTime = 0x51C720,
        TranslateRoll = 0x4A3030, CriticalFailure = 0x4240DC;
    const int Torso = 3, Locations = 9, Effects = 6, RowBytes = 28;

    static int[] Row(Executable exe, uint address) => Enumerable.Range(0, 7).Select(i => exe.Int(address + (uint)(4 * i))).ToArray();
    static int[] Row(CriticalEffect e) => new[] { e.Multiplier, (int)e.Flags, e.MassiveStat, e.MassiveModifier, (int)e.MassiveFlags, e.Message, e.MassiveMessage };

    public static object Create(GameData data, string installDirectory)
    {
        string path = Path.Combine(installDirectory, "fallout2.exe");
        byte[] bytes = File.ReadAllBytes(path);
        var exe = new Executable(bytes);
        for (int kind = 0; kind < CriticalRules.KillTypeCount; kind++)
            for (int effect = 0; effect < Effects; effect++)
                if (!Row(exe, KillTable + (uint)(((kind * Locations + Torso) * Effects + effect) * RowBytes)).SequenceEqual(Row(CriticalRules.Torso(false, kind, effect))))
                    throw new InvalidDataException($"Kill type {kind} torso row {effect} differs from fallout2.exe.");
        for (int effect = 0; effect < Effects; effect++)
            if (!Row(exe, PlayerTable + (uint)((Torso * Effects + effect) * RowBytes)).SequenceEqual(Row(CriticalRules.Torso(true, 0, effect))))
                throw new InvalidDataException($"Player torso row {effect} differs from fallout2.exe.");
        for (int type = 0; type < 7; type++)
            for (int effect = 0; effect < 5; effect++)
                if (exe.Int(FailureTable + (uint)(4 * (type * 5 + effect))) != (int)CriticalRules.Failure(type, effect))
                    throw new InvalidDataException($"Critical failure {type}/{effect} differs from fallout2.exe.");
        if (exe.Int(HitPenalty + 4 * Torso) != CriticalRules.TorsoPenalty || exe.Int(GameTime) != CriticalRules.GameStartTicks)
            throw new InvalidDataException("Torso penalty or start time differs from fallout2.exe.");
        // randomTranslateRoll divides the clock by 864000 on both branches and compares the day with 1;
        // attackComputeCriticalFailure divides it for the dude and compares with 6 (div ebx; cmp eax, 6).
        byte[] day = BitConverter.GetBytes((int)CriticalRules.TicksPerDay);
        ReadOnlySpan<byte> translate = exe.Bytes(TranslateRoll, 0x90), failure = exe.Bytes(CriticalFailure, 0x60);
        int Count(ReadOnlySpan<byte> code) { int n = 0; for (int i = 0; i + 4 <= code.Length; i++) if (code.Slice(i, 4).SequenceEqual(day)) n++; return n; }
        if (Count(translate) != 2 || translate.IndexOf(new byte[] { 0x83, 0xF8, 0x01 }) < 0 || Count(failure) != 1 || failure.IndexOf(new byte[] { 0xF7, 0xF3, 0x83, 0xF8, 0x06 }) < 0)
            throw new InvalidDataException("The day 1 / day 6 critical gates are not where CE places them.");

        var prototypes = new PrototypeCatalog(new ResourceCatalog(data.Read));
        var critters = new[] { (0x0100017E, "Giant Ant"), (0x01000005, "Radscorpion"), (0x01000112, "Cameron") }.Select(c =>
        {
            var critter = CritterPrototypeData.Read(prototypes.Get(c.Item1)); var stats = DerivedCombatStats.FromCritter(critter);
            return new { Pid = $"0x{c.Item1:X8}", Name = c.Item2, critter.KillType, stats.CriticalChance, stats.BetterCriticals, stats.Special.Endurance, stats.Special.Agility,
                stats.Special.Luck, KnockoutTicks = CriticalRules.KnockoutTicks(stats.Special.Endurance), Torso = Enumerable.Range(0, Effects).Select(e => CriticalRules.Torso(false, critter.KillType, e)).ToArray() };
        }).ToArray();
        if (critters[0].KillType != 17 || critters[1].KillType != 6) throw new InvalidDataException("Temple creature kill types changed.");
        var weapons = new[] { 7, 4 }.Select(pid => WeaponPrototypeData.Read(prototypes.Get(pid))).Select(w => new { w.Pid, w.CriticalFailureType, w.TwoHanded,
            Failures = Enumerable.Range(0, 5).Select(e => CriticalRules.Failure(w.CriticalFailureType, e).ToString()).ToArray() }).ToArray();
        var premades = new[] { "combat", "stealth", "diplomat" }.Select(name => PlayerCharacter.FromPremade(CharacterFile.Read(data.Read($"premade/{name}.gcd")))).Select(p =>
        {
            var stats = DerivedCombatStats.ForPlayer(p);
            return new { p.Name, Traits = p.Traits.Select(t => t.ToString()).ToArray(), stats.CriticalChance, stats.BetterCriticals, stats.Finesse, p.Effective.Luck };
        }).ToArray();
        return new
        {
            Status = "CRITICAL_TABLES_OK",
            Executable = new { Size = bytes.Length, Sha256 = Convert.ToHexString(SHA256.HashData(bytes)) },
            Verified = new[] { "19 kill-type torso rows x 6 effects", "player torso rows x 6", "critical failure table 7 x 5", "torso hit penalty 0",
                "game clock starts at 302400 ticks", "criticals from day 1 (randomTranslateRoll)", "player failures from day 6 (attackComputeCriticalFailure)" },
            HoursUntilDay1 = (CriticalRules.TicksPerDay - CriticalRules.GameStartTicks) / 36000.0,
            Critters = critters, Weapons = weapons, Premades = premades,
            Player = new { Torso = Enumerable.Range(0, Effects).Select(e => CriticalRules.Torso(true, 0, e)).ToArray() },
            OriginalReconstruction = "verified"
        };
    }
}
