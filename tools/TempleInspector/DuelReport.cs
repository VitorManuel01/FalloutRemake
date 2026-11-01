using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;

/// <summary>
/// Cameron's unarmed challenge measured on original data: each character's sheet and blows against Cameron's, and the
/// share of seeded duels won (Cameron yields or dies) with the audit's policy: strike while the blow is affordable,
/// otherwise end the turn. Duels open as WorldSession.BeginTempleDuel opens them, beside Cameron on elevation 2.
/// </summary>
internal static class DuelReport
{
    static readonly ulong Seeds = ulong.TryParse(Environment.GetEnvironmentVariable("DUEL_SEEDS"), out ulong seeds) && seeds is > 0 and <= 20000 ? seeds : 1000;

    public static object Create(GameData data)
    {
        var catalog = new MapCatalog(data.Read("data/maps.txt"));
        var prototypes = new PrototypeCatalog(new ResourceCatalog(data.Read));
        var packets = AiPacketCatalog.Read(data.Read("data/ai.txt").Bytes);
        MapDocument map = catalog.Load(data, 3);
        var world = new WorldSession(id => catalog.Load(data, id), new[] { 3, 4, 126 }, runtimePrototype: prototypes.Get, aiPackets: packets);
        HexNavigation arena = world.Current.Doors.Navigation(2);
        MapObject cameronSource = map.ObjectLayers.SelectMany(l => l.Objects).Single(o => o.Offset == 425776);
        CritterPrototypeData cameronProto = CritterPrototypeData.Read(prototypes.Get(cameronSource.Header.Pid));
        // The hex beside Cameron the preview's challenge starts from.
        const int PlayerHex = 13928;
        if (FalloutGrid.Distance(PlayerHex, cameronSource.Header.Tile) != 1 || !arena.Walkable(PlayerHex)) throw new InvalidDataException("The challenge hex is no longer beside Cameron.");

        var characters = new[] { PlayerCharacter.Neutral }.Concat(new[] { "combat", "stealth", "diplomat" }
            .Select(name => PlayerCharacter.FromPremade(CharacterFile.Read(data.Read($"premade/{name}.gcd"))))).ToArray();
        CombatantState Cameron()
        {
            CombatantState c = CombatantState.FromMapCritter(cameronSource, cameronProto, "Cameron");
            return new CombatantState(c.Id, c.Name, c.Pid, c.Team, false, c.Hex, c.Elevation, c.Stats, c.Weapon, c.HitPoints, c.Experience)
                { YieldsBelowHalf = true, KillType = c.KillType, Rotation = c.Rotation, Ai = packets[WorldSession.CameronPacket] };
        }
        // The left hand's punch (FO2's default) or the right hand's kick, the best each allows (CombatWeapon.UnarmedFor).
        CombatantState Player(PlayerCharacter character, bool kick = false)
        {
            var stats = DerivedCombatStats.ForPlayer(character);
            return new CombatantState("player", character.Name, null, 0, true, PlayerHex, 2, stats, CombatWeapon.UnarmedFor(stats, 1, kick), stats.MaxHitPoints);
        }
        object Duels(PlayerCharacter character, bool kick)
        {
            int won = 0, killed = 0, lost = 0, rounds = 0;
            for (ulong seed = 1; seed <= Seeds; seed++)
            {
                var fight = new CombatSession(new[] { Player(character, kick), Cameron() }, arena, new SeededCombatRandom(seed), cameronSource.Key);
                for (int guard = 0; guard < 600 && !fight.Turn.Over; guard++)
                {
                    if (!fight.Active!.IsPlayer) { CombatAi.TakeTurn(fight); continue; }
                    var strike = CombatActionProposal.Attack("player", cameronSource.Key);
                    fight.Execute(fight.Preview(strike).Status == CombatActionStatus.Ready ? strike : CombatActionProposal.EndTurn("player"));
                }
                CombatantState cameron = fight.Get(cameronSource.Key);
                if (!fight.Get("player").Alive) lost++; else if (!cameron.Alive) killed++; else if (cameron.Yielded) won++;
                rounds += fight.Turn.Round;
            }
            CombatantState striker = Player(character, kick);
            var (low, high) = DamageCalculator.RollRange(striker, striker.Weapon);
            return new { Blow = striker.Weapon.Name, striker.Weapon.ActionPoints, DamageRoll = new[] { low, high }, Yielded = won, Killed = killed, Lost = lost,
                WinRate = Math.Round(100.0 * (won + killed) / Seeds, 1), AverageRounds = Math.Round((double)rounds / Seeds, 2) };
        }
        object Measure(PlayerCharacter character)
        {
            CombatantState player = Player(character), foe = Cameron();
            var opening = new CombatSession(new[] { player, foe }, arena, new SeededCombatRandom(1), cameronSource.Key);
            CombatActionPreview hisBlow = opening.Preview(CombatActionProposal.Attack(foe.Id, "player"));
            return new
            {
                character.Name, Traits = character.Traits.Select(t => t.ToString()).ToArray(), Special = player.Stats.Special.ToArray(),
                player.Stats.MaxHitPoints, player.Stats.MaxActionPoints, player.Stats.ArmorClass, player.Stats.Sequence, player.Stats.MeleeDamage,
                Unarmed = player.Stats.Skill(DerivedCombatStats.Unarmed),
                CameronBlowAtStart = new { hisBlow.ToHit!.Chance, hisBlow.ToHit.TargetArmorClass, hisBlow.Damage!.MinDamage, hisBlow.Damage.MaxDamage },
                Duels = Seeds, Punch = Duels(character, false), Kick = Duels(character, true)
            };
        }
        var cameronState = Cameron();
        return new
        {
            Status = "DUEL_MEASURED",
            Cameron = new { Special = cameronState.Stats.Special.ToArray(), cameronState.Stats.MaxHitPoints, cameronState.Stats.MaxActionPoints, cameronState.Stats.ArmorClass,
                cameronState.Stats.Sequence, cameronState.Stats.MeleeDamage, Unarmed = cameronState.Stats.Skill(DerivedCombatStats.Unarmed),
                DamageRoll = new[] { DamageCalculator.RollRange(cameronState, cameronState.Weapon).Min, DamageCalculator.RollRange(cameronState, cameronState.Weapon).Max }, YieldsBelow = cameronState.Stats.MaxHitPoints / 2 },
            Characters = characters.Select(Measure).ToArray(),
            OriginalReconstruction = "verified"
        };
    }
}
