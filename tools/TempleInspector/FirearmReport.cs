using System.Text;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;

/// <summary>
/// Firearms on original data: _attack_skill and _attack_subtype (fallout2.exe 0x519160, 0x5191A8) against the runtime's
/// attack types and skills; every weapon PRO with a firing mode as <see cref="CombatWeapon"/> reads it (skill, AP, range,
/// burst, magazine, caliber, weapon perk); every ammunition PRO; the burst split CE gives each burst weapon; and the guns
/// and ammunition lying on the supported maps (3, 4, 126).
/// </summary>
internal static class FirearmReport
{
    const uint AttackSkill = 0x519160, AttackSubtype = 0x5191A8;

    public static object Create(GameData data, string installDirectory)
    {
        var exe = new CriticalReport.Executable(File.ReadAllBytes(Path.Combine(installDirectory, "fallout2.exe")));
        int[] skills = Enumerable.Range(0, 9).Select(i => exe.Int(AttackSkill + (uint)(4 * i))).ToArray();
        int[] subtypes = Enumerable.Range(0, 9).Select(i => exe.Int(AttackSubtype + (uint)(4 * i))).ToArray();
        int[] expectedSkills = { -1, DerivedCombatStats.Unarmed, DerivedCombatStats.Unarmed, DerivedCombatStats.MeleeWeapons, DerivedCombatStats.MeleeWeapons,
            DerivedCombatStats.Throwing, DerivedCombatStats.SmallGuns, DerivedCombatStats.SmallGuns, DerivedCombatStats.SmallGuns };
        int[] runtimeTypes = Enumerable.Range(0, 9).Select(i => (int)WeaponPrototypeData.TypeOf((WeaponAttackMode)i)).ToArray();
        if (!skills.SequenceEqual(expectedSkills) || !subtypes.SequenceEqual(runtimeTypes))
            throw new InvalidDataException($"Attack tables differ from fallout2.exe: skills {string.Join(",", skills)}; types {string.Join(",", subtypes)}");

        var resources = new ResourceCatalog(data.Read); var prototypes = new PrototypeCatalog(resources);
        string text = Encoding.Latin1.GetString(resources.Read("text/english/game/pro_item.msg").Bytes);
        string Name(int id) => text.Split('\n').FirstOrDefault(l => l.StartsWith("{" + id + "}")) is { } line ? line.Split('{', '}')[^2].Trim() : "?";
        string perkText = Encoding.Latin1.GetString(resources.Read("text/english/game/perk.msg").Bytes);
        string PerkName(int perk) => perk < 0 ? "" : perkText.Split('\n').FirstOrDefault(l => l.StartsWith("{" + (101 + perk) + "}")) is { } line ? line.Split('{', '}')[^2].Trim() : "?";
        var items = new List<Prototype>();
        for (int pid = 1; pid < 600; pid++)
        {
            try { items.Add(prototypes.Get(pid)); }
            catch (Exception error) when (error is KeyNotFoundException or InvalidDataException or IOException or ArgumentException) { }
        }
        string SkillName(int skill) => skill switch { 0 => "Small Guns", 1 => "Big Guns", 2 => "Energy Weapons", _ => skill.ToString() };
        var guns = items.Where(p => p.Type == 0 && p.Subtype == WeaponPrototypeData.Subtype).Select(p => (Proto: p, Data: WeaponPrototypeData.Read(p)))
            .Where(w => WeaponPrototypeData.TypeOf(w.Data.PrimaryMode) == WeaponAttackType.Ranged || WeaponPrototypeData.TypeOf(w.Data.SecondaryMode) == WeaponAttackType.Ranged)
            .Select(w =>
            {
                CombatWeapon? primary = w.Data.PrimaryMode == WeaponAttackMode.Flame ? null : CombatWeapon.Primary(Name(w.Proto.MessageId), w.Data);
                CombatWeapon? secondary = CombatWeapon.Secondary(Name(w.Proto.MessageId), w.Data);
                CombatWeapon shown = primary ?? secondary!;
                return new
                {
                    w.Data.Pid, Name = Name(w.Proto.MessageId), Modes = $"{w.Data.PrimaryMode}/{w.Data.SecondaryMode}", Skill = primary == null ? "continuous (not supported)" : SkillName(shown.SkillIndex),
                    ActionPoints = $"{w.Data.PrimaryActionPoints}/{w.Data.SecondaryActionPoints}", Range = $"{w.Data.PrimaryRange}/{w.Data.SecondaryRange}",
                    Damage = $"{w.Data.MinDamage}-{w.Data.MaxDamage} {(DamageType)w.Data.DamageType}", Burst = secondary?.BurstRounds ?? primary?.BurstRounds ?? 0,
                    Magazine = w.Data.AmmoCapacity, w.Data.Caliber, DefaultAmmo = w.Data.AmmoPid, w.Data.MinStrength, Perk = PerkName(w.Data.Perk), w.Data.BigGun, w.Data.TwoHanded
                };
            }).ToArray();
        var ammo = items.Where(p => p.Type == 0 && p.Subtype == AmmoPrototypeData.Subtype).Select(p => (Proto: p, Data: AmmoPrototypeData.Read(p)))
            .Select(a => new { a.Data.Pid, Name = Name(a.Proto.MessageId), a.Data.Caliber, Box = a.Data.Quantity, AC = a.Data.ArmorClassModifier, DR = a.Data.DamageResistanceModifier,
                Multiplier = a.Data.DamageMultiplier, Divisor = a.Data.DamageDivisor }).ToArray();
        var catalog = new MapCatalog(data.Read("data/maps.txt"));
        var gunPids = guns.Select(g => g.Pid).ToHashSet(); var ammoPids = ammo.Select(a => a.Pid).ToHashSet();
        int Count(MapObject o, HashSet<int> pids) => (pids.Contains(o.Header.Pid) ? 1 : 0) + o.Inventory.Sum(e => pids.Contains(e.Object.Header.Pid) ? e.Quantity : Count(e.Object, pids));
        return new
        {
            Status = "FIREARMS_OK",
            Executable = new { AttackSkill = skills, AttackSubtype = subtypes },
            Guns = guns,
            Ammunition = ammo,
            // _compute_spray: center = n/3 (at least 1), left = n/3, right = the rest; main = center/2 (at least 1, taken from the center).
            BurstSplits = guns.Where(g => g.Burst > 0).Select(g => g.Burst).Distinct().OrderBy(n => n).Select(n =>
            {
                int center = Math.Max(1, n / 3), left = n / 3, right = n - center - left, main = center / 2;
                if (main == 0) { main = 1; center--; }
                return new { Rounds = n, Main = main, CenterLine = center, Left = left, Right = right };
            }),
            OnMaps = new[] { 3, 4, 126 }.ToDictionary(id => id, id =>
            {
                MapObject[] objects = catalog.Load(data, id).ObjectLayers.SelectMany(l => l.Objects).ToArray();
                return new { Guns = objects.Sum(o => Count(o, gunPids)), Ammunition = objects.Sum(o => Count(o, ammoPids)) };
            }),
            OriginalReconstruction = "verified"
        };
    }
}
