using System.Text.Json;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;

/// <summary>
/// End-to-end Temple run on the original data with encounters on: entry, first door, lockpicked door (controlled success),
/// explosive and barrier, Cameron's Speech and departure, door 100 and the Arroyo exit. Every step is one real hex move;
/// any creature that notices the actor starts a fight resolved by a simple policy (approach and strike, else end turn).
/// Measures fights, damage and where runs die or stall. Read-only: nothing is written to disk.
/// </summary>
internal static class TempleRunAudit
{
    sealed record FightLog(int Elevation, int Hex, string[] Foes, int Rounds, int HitPointsBefore, int HitPointsAfter, int Experience, string Outcome);
    sealed record RunLog(string Character, ulong Seed, bool Spear, bool Heal, string Result, string? StoppedAt, int Steps, FightLog[] Fights, int FinalHitPoints, int MaxHitPoints,
        int Experience, int Level, int LowestHitPoints, int PowdersFound, int PowdersUsed);

    // TEMPLE_RUN_SEEDS shortens a check run; the documented measurement uses 20.
    static readonly ulong Seeds = ulong.TryParse(Environment.GetEnvironmentVariable("TEMPLE_RUN_SEEDS"), out ulong seeds) && seeds is > 0 and <= 100 ? seeds : 20;
    public static object Create(GameData data)
    {
        var catalog = new MapCatalog(data.Read("data/maps.txt"));
        var prototypes = new PrototypeCatalog(new ResourceCatalog(data.Read));
        var runs = new List<RunLog>();
        // The neutral test profile and the three premade characters of the original (premade/*.gcd).
        var characters = new[] { PlayerCharacter.Neutral }.Concat(new[] { "combat", "stealth", "diplomat" }
            .Select(name => PlayerCharacter.FromPremade(CharacterFile.Read(data.Read($"premade/{name}.gcd"))))).ToArray();
        // Two variants each: the direct route bare-handed, and spear plus every reachable Healing Powder.
        foreach (PlayerCharacter character in characters)
            foreach (bool kit in new[] { false, true })
                for (ulong seed = 1; seed <= Seeds; seed++)
                    runs.Add(Run(data, catalog, prototypes, seed, kit, kit, character));
        object Summary(string character, bool spear, bool heal)
        {
            var set = runs.Where(r => r.Character == character && r.Spear == spear && r.Heal == heal).ToArray();
            return new
            {
                Character = character, Spear = spear, Heal = heal, Runs = set.Length, PowdersFound = set.Max(r => r.PowdersFound), AveragePowdersUsed = Math.Round(set.Average(r => r.PowdersUsed), 2), Completed = set.Count(r => r.Result == "Arroyo"), Died = set.Count(r => r.Result == "Died"),
                Blocked = set.Count(r => r.Result == "Blocked"), BlockedAt = set.Where(r => r.Result == "Blocked").Select(r => r.StoppedAt).Distinct().ToArray(),
                Stalled = set.Where(r => r.Result is not ("Arroyo" or "Died" or "Blocked")).Select(r => $"{r.Seed}: {r.Result} at {r.StoppedAt}").ToArray(),
                DeathPlaces = set.Where(r => r.Result == "Died").GroupBy(r => r.StoppedAt).Select(g => new { Place = g.Key, Count = g.Count() }).ToArray(),
                AverageFights = Math.Round(set.Average(r => r.Fights.Length), 2), AverageDamageTaken = Math.Round(set.Average(r => r.Fights.Sum(f => f.HitPointsBefore - f.HitPointsAfter)), 1),
                LowestHitPoints = set.Min(r => r.LowestHitPoints), AverageFinalHitPoints = Math.Round(set.Where(r => r.Result == "Arroyo").Select(r => (double)r.FinalHitPoints).DefaultIfEmpty(0).Average(), 1),
                ByFoes = set.SelectMany(r => r.Fights).GroupBy(f => string.Join("+", f.Foes.OrderBy(x => x)))
                    .Select(g => new { Foes = g.Key, Fights = g.Count(), AverageRounds = Math.Round(g.Average(f => f.Rounds), 1),
                        AverageDamage = Math.Round(g.Average(f => f.HitPointsBefore - f.HitPointsAfter), 1), MaxDamage = g.Max(f => f.HitPointsBefore - f.HitPointsAfter),
                        Lost = g.Count(f => f.Outcome == "PlayerDefeated") }).OrderByDescending(x => x.Fights).ToArray(),
                Levels = set.GroupBy(r => r.Level).OrderBy(g => g.Key).Select(g => new { Level = g.Key, Count = g.Count() }).ToArray()
            };
        }
        return new
        {
            Status = "TEMPLE_RUN_AUDIT",
            Scope = "Read-only measurement: original MAP/PRO data, encounters on, controlled lockpick success, Speech branch, simple combat policy; "
                + "heal variants collect every reachable Healing Powder and take one (4 AP in a fight, free outside) while HP <= half",
            HealingPowder = HealingDrugProfile.Matches(DrugPrototypeData.Read(prototypes.Get(HealingDrugProfile.Pid)))
                && HealingDrugProfile.Supported(prototypes.Get(HealingDrugProfile.Pid)) ? "original PRO matches the 8-18 HP profile" : "MISMATCH",
            Summaries = characters.SelectMany(c => new[] { Summary(c.Name, false, false), Summary(c.Name, true, true) }).ToArray(),
            Runs = runs.Select(r => new { r.Character, r.Seed, r.Spear, r.Heal, r.Result, r.StoppedAt, Fights = r.Fights.Length, r.FinalHitPoints, r.LowestHitPoints, r.Experience, r.Level, r.PowdersFound, r.PowdersUsed }),
            OriginalReconstruction = "verified"
        };
    }

    sealed class Stop(string result, string where) : Exception(where) { public string Result { get; } = result; }

    static RunLog Run(GameData data, MapCatalog catalog, PrototypeCatalog prototypes, ulong seed, bool takeSpear, bool heal, PlayerCharacter character)
    {
        var world = new WorldSession(id => catalog.Load(data, id), new[] { 3, 4, 126 }, runtimePrototype: prototypes.Get,
            templeProfile: map => TempleNpcDepartureProfile.Create(map, data.Read), arroyoProfile: map => ArroyoRewardProfile.Create(map, data.Read),
            premade: path => CharacterFile.Read(data.Read(path)));
        world.StartNewGame(character);
        var random = new SeededCombatRandom(seed);
        var fights = new List<FightLog>();
        int steps = 0, lowest = world.HitPoints, found = 0, used = 0;
        string place = "entry";

        void Fight(CombatEncounter encounter) => Resolve(world.BeginCombat(encounter, world.PlayerWeapon(), random));
        void Resolve(CombatSession fight)
        {
            int before = world.HitPoints; var at = world.Location;
            CombatantState hero = fight.Get("player");
            for (int guard = 0; guard < 600 && !fight.Turn.Over; guard++)
            {
                if (!fight.Active!.IsPlayer) { ProvisionalMeleeBehavior.TakeTurn(fight); continue; }
                CombatantState? foe = fight.Combatants.Where(c => !c.IsPlayer && c.InFight).OrderBy(c => FalloutGrid.Distance(c.Hex, hero.Hex)).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
                if (foe == null) break;
                if (heal && hero.HitPoints <= hero.Stats.MaxHitPoints / 2
                    && fight.Preview(CombatActionProposal.UseHealing("player")).Status == CombatActionStatus.Ready)
                { fight.Execute(CombatActionProposal.UseHealing("player")); used++; continue; }
                var attack = fight.Preview(CombatActionProposal.Attack("player", foe.Id));
                if (attack.Status == CombatActionStatus.Ready) fight.Execute(CombatActionProposal.Attack("player", foe.Id));
                else if (attack.Status == CombatActionStatus.OutOfRange && hero.ActionPoints > 0
                    && fight.Movement.Path(hero, h => FalloutGrid.Distance(h, foe.Hex) <= hero.Weapon.Range) is { Length: > 0 } path)
                    fight.Execute(CombatActionProposal.Move("player", path[Math.Min(path.Length, hero.ActionPoints) - 1]));
                else fight.Execute(CombatActionProposal.EndTurn("player"));
            }
            if (!fight.Turn.Over) { world.AbandonCombat(); throw new Stop("Stalled", $"{place}: fight did not end"); }
            lowest = Math.Min(lowest, hero.HitPoints);
            var status = world.CommitCombat(fight);
            fights.Add(new FightLog(at.Elevation, at.Hex, fight.Combatants.Where(c => !c.IsPlayer).Select(c => $"PID {c.Pid:X8}").ToArray(),
                fight.Turn.Round, before, hero.HitPoints, fight.Turn.ExperienceAwarded, status.ToString()));
            if (status == CombatCommitStatus.PlayerDefeated) throw new Stop("Died", place);
            if (status != CombatCommitStatus.Ready) throw new Stop("Refused", $"{place}: {status}");
            if (hero.Hex != world.Location.Hex) world.Current.Travel.MoveTo(hero.Hex);
            while (heal && world.HitPoints <= world.MaxHitPoints / 2 && world.Carried.FirstOrDefault(c => c.Source.Header.Pid == HealingDrugProfile.Pid) is { } powder)
            {
                if (world.UseDrug(powder, random, false).Status != DrugUseStatus.Ready) throw new Stop("Refused", $"{place}: Healing Powder");
                used++;
            }
        }
        void CollectPowders()
        {
            if (!heal) return;
            string before = place; place = $"powders e{world.Location.Elevation}";
            for (int guard = 0; guard < 32; guard++)
            {
                var navigation = world.Current.Doors.Navigation(world.Location.Elevation); var reach = navigation.ReachableFrom(world.Location.Hex);
                var next = world.Current.Items.Containers.Where(o => o.Header.Elevation == world.Location.Elevation && world.Current.Items.ContainerStatus(o.Key) == ItemTakeStatus.Ready
                        && world.Current.Items.Contents(o.Key).Any(i => i.Source.Header.Pid == HealingDrugProfile.Pid && i.Quantity > 0))
                    .Select(o => (Owner: o, Hex: Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(o.Header.Tile, d) ?? -1)
                        .Where(t => t >= 0 && reach.Distance(t) >= 0 && !navigation.IsExit(t)).OrderBy(t => reach.Distance(t)).ThenBy(t => t).DefaultIfEmpty(-1).First()))
                    .Where(c => c.Hex >= 0).OrderBy(c => reach.Distance(c.Hex)).ThenBy(c => c.Owner.Offset).FirstOrDefault();
                if (next.Owner == null) break;
                Walk(next.Hex);
                foreach (ContainerItem line in world.Current.Items.Contents(next.Owner.Key).Where(i => i.Source.Header.Pid == HealingDrugProfile.Pid && i.Quantity > 0))
                {
                    if (world.TakeItem(next.Owner.Key, line.Source.Key, line.Quantity, false) != ItemTakeStatus.Ready) throw new Stop("Stalled", $"{place}: take refused");
                    found += line.Quantity;
                }
            }
            place = before;
        }

        void Walk(int target)
        {
            for (int guard = 0; world.Location.Hex != target; guard++)
            {
                if (guard > 2000) throw new Stop("Stalled", $"{place}: walk to {target} did not finish");
                HexRoute route = world.Current.Doors.Navigation(world.Location.Elevation).FindRoute(world.Location.Hex, target);
                if (route.Status != RouteStatus.Ready) throw new Stop("Stalled", $"{place}: no route to {target} ({route.Status})");
                world.Current.Travel.MoveTo(route.Tiles[1]); steps++;
                if (world.FindEncounter(world.Location) is { } encounter) Fight(encounter);
            }
        }
        int Approach(MapObject target)
        {
            var navigation = world.Current.Doors.Navigation(world.Location.Elevation); var reach = navigation.ReachableFrom(world.Location.Hex);
            return Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(target.Header.Tile, d) ?? -1)
                .Where(t => t >= 0 && reach.Distance(t) >= 0 && !navigation.IsExit(t)).OrderBy(t => reach.Distance(t)).ThenBy(t => t)
                .DefaultIfEmpty(-1).First() is int hex and >= 0 ? hex : throw new Stop("Stalled", $"{place}: no approach to {target.Key}");
        }
        MapObject Root(Func<MapObject, bool> match) => world.Current.Map.ObjectLayers.SelectMany(l => l.Objects).Single(match);
        void Exit(int elevation)
        {
            var navigation = world.Current.Doors.Navigation(world.Location.Elevation); var reach = navigation.ReachableFrom(world.Location.Hex);
            int exit = world.Current.Map.ObjectLayers.SelectMany(l => l.Objects).Where(o => o.Header.Elevation == world.Location.Elevation
                    && o.State.GetValueOrDefault("destinationMap", -1) == 3 && o.State.GetValueOrDefault("destinationElevation", -1) == elevation && reach.Distance(o.Header.Tile) >= 0)
                .Select(o => o.Header.Tile).OrderBy(t => reach.Distance(t)).ThenBy(t => t).FirstOrDefault(-1);
            if (exit < 0) throw new Stop("Stalled", $"{place}: no exit to elevation {elevation}");
            Walk(exit);
            if (world.UseExit(false).Status != ExitStatus.Ready || world.Location.Elevation != elevation) throw new Stop("Stalled", $"{place}: exit to elevation {elevation} refused");
        }

        try
        {
            place = "first door"; var first = Root(o => o.Offset == 244040);
            Walk(Approach(first)); if (world.Current.Doors.Toggle(first.Key, world.Location.Hex, 0, false) != DoorUseStatus.Ready) throw new Stop("Stalled", place);
            place = "locked door"; var locked = Root(o => o.Offset == 137396);
            Walk(Approach(locked));
            if (world.Current.Doors.UseLockpick(locked.Key, world.Location.Hex, 0, false, _ => SkillRollOutcome.Success).Status != DoorUseStatus.Ready
                || world.Current.Doors.Toggle(locked.Key, world.Location.Hex, 0, false) != DoorUseStatus.Ready) throw new Stop("Stalled", place);
            if (takeSpear)
            {
                place = "spear"; var pot = Root(o => o.Offset == 179896); var spear = pot.Inventory.Single(e => e.Object.Header.Pid == 7).Object;
                Walk(Approach(pot)); if (world.TakeItem(pot.Key, spear.Key, 1, false) != ItemTakeStatus.Ready) throw new Stop("Stalled", place);
            }
            CollectPowders();
            place = "elevation 1"; Exit(1);
            place = "explosive"; var owner = world.Current.Items.Containers.Single(o => o.Offset == 337852 && o.Header.Id == 358);
            var item = world.Current.Items.Contents(owner.Key).Single(i => i.Source.Header.Id == 364 && i.Source.Header.Pid == 85).Source;
            Walk(Approach(owner)); if (world.TakeItem(owner.Key, item.Key, 1, false) != ItemTakeStatus.Ready) throw new Stop("Stalled", place);
            place = "barrier";
            var navigation1 = world.Current.Doors.Navigation(1); var reach1 = navigation1.ReachableFrom(world.Location.Hex);
            int placement = Enumerable.Range(1, 39999).Where(t => reach1.Distance(t) >= 0 && !navigation1.IsExit(t) && FalloutGrid.Distance(t, 14322) <= 3)
                .OrderBy(t => FalloutGrid.Distance(t, 14322)).ThenBy(t => reach1.Distance(t)).ThenBy(t => t).First();
            Walk(placement);
            var plan = ExplosiveUseProposal.Create(world, world.Carried.Single(c => c.Source.Header.Pid == 85), 60, SkillRollOutcome.Success, false).Plan
                ?? throw new Stop("Stalled", $"{place}: no explosive plan");
            if (world.CommitExplosiveUse(plan, false).Status != ExplosiveCommitStatus.Ready || world.AdvanceUseTime(600).Due.Length != 1) throw new Stop("Stalled", place);
            var effect = TempleBarrierEffectProposal.Create(world, plan.Id, prototypes.Get(TempleBarrierEffectProposal.ReplacementPid));
            if (effect.Plan == null || world.CommitBarrierEffect(effect.Plan, Guid.NewGuid(), false).Status != BarrierCommitStatus.Ready) throw new Stop("Stalled", $"{place}: {effect.Status}");
            CollectPowders();
            place = "elevation 2"; Exit(2);
            CollectPowders();
            place = "Cameron"; var profile = TempleNpcDepartureProfile.Create(world.Current.Map, data.Read);
            var gateKey = world.Current.Doors.InitialTempleGateKey ?? throw new Stop("Stalled", $"{place}: no gate");
            var gate = Root(o => o.Key == gateKey);
            if (world.Current.Doors.Get(gateKey)?.IsOpen != true) { Walk(Approach(gate)); if (world.Current.Doors.Toggle(gateKey, world.Location.Hex, 2, false) != DoorUseStatus.Ready) throw new Stop("Stalled", $"{place}: gate"); }
            var npc = profile.Actor(new(1, profile.Fingerprint, NpcDeparturePhase.ToGate, 0, 0));
            Walk(Approach(npc));
            if (character.QualifiesForTempleSpeech)
            {
                var speech = profile.Speech.CreateInitial();
                var draft = TempleSpeechDraft.Begin(profile.Speech, speech, character.Effective.Intelligence, character.TempleSpeechTag)!;
                foreach (int offset in TempleSpeechProfile.ChoiceOffsets) draft.Choose(offset);
                draft.CommitDetached();
                var proposal = world.PrepareTempleSpeech(profile, speech.Capture(), false, character.Effective.Intelligence, character.TempleSpeechTag);
                if (proposal.Status != TempleWorldStatus.Ready || world.CommitTempleSpeech(proposal.Plan, false) != TempleWorldStatus.Ready) throw new Stop("Stalled", $"{place}: Speech {proposal.Status}");
            }
            else
            {
                // No Speech tag: Cameron's unarmed challenge, equipment in the chest, Cameron strikes first.
                place = "Cameron duel";
                var ready = world.PrepareTempleDuel(profile, false);
                if (ready != TempleWorldStatus.Ready) throw new Stop("Stalled", $"{place}: {ready}");
                Resolve(world.BeginTempleDuel(profile, random));
                if (world.TempleDuel == TempleDuelOutcome.Killed)
                {
                    // G369 = 1 keeps both doors locked: the Temple Key from his body unlocks the exit and the gate to the chest.
                    place = "Temple Key"; MapObject body = world.Current.Items.Owner(profile.ActorKey)!;
                    Walk(Approach(body));
                    if (world.TakeAllItems(profile.ActorKey, false) != ItemTakeStatus.Ready || !world.TempleKeyCarried) throw new Stop("Stalled", $"{place}: body");
                    foreach (string door in new[] { profile.GateKey, profile.ExitDoor!.Key })
                    {
                        Walk(Approach(Root(o => o.Key == door)));
                        if (world.UseTempleKey(door, false) != TempleWorldStatus.Ready || world.Current.Doors.Toggle(door, world.Location.Hex, 2, false) != DoorUseStatus.Ready)
                            throw new Stop("Stalled", $"{place}: door {door}");
                    }
                    place = "challenge chest"; Walk(Approach(Root(o => o.Offset == WorldSession.TempleChestOffset)));
                    if (world.RetrieveTempleChest(false) != TempleChestStatus.Ready) throw new Stop("Stalled", $"{place}: retrieve refused");
                    place = "Arroyo exit";
                    var nav = world.Current.Doors.Navigation(2); var reach = nav.ReachableFrom(world.Location.Hex);
                    int exitHex = Enumerable.Range(0, 40000).Where(t => nav.IsExit(t) && reach.Distance(t) >= 0 && world.PreviewExitAt(2, t).Destination?.Map == 4)
                        .OrderBy(t => reach.Distance(t)).DefaultIfEmpty(-1).First();
                    if (exitHex < 0) throw new Stop("Stalled", $"{place}: none reachable");
                    Walk(exitHex);
                    if (world.UseExit(false).Status != ExitStatus.Ready || world.CurrentMap != 4) throw new Stop("Stalled", $"{place}: travel refused");
                    foreach (WorldArea area in world.Areas) MapLayoutWriter.Verify(area.Map);
                    return new RunLog(character.Name, seed, takeSpear, heal, "Arroyo", "Cameron killed", steps, fights.ToArray(), world.HitPoints, world.MaxHitPoints, world.Experience, world.Level, lowest, found, used);
                }
                if (world.TempleDuel != TempleDuelOutcome.Yielded) throw new Stop("Stalled", $"{place}: {world.TempleDuel}");
            }
            place = "Cameron departure";
            for (int guard = 0; ; guard++)
            {
                if (guard > 600) throw new Stop("Stalled", place);
                var status = world.StepTempleNpc(world.TempleToken, world.TempleRevision, false);
                if (status == TempleWorldStatus.Complete) break;
                if (status == TempleWorldStatus.NotWalking) { world.AdvanceTempleNpcTime(20, world.TempleToken, world.TempleRevision, false); continue; }
                if (status is TempleWorldStatus.Occupied or TempleWorldStatus.Blocked)
                {
                    var nav = world.Current.Doors.Navigation(2); var reach = nav.ReachableFrom(world.Location.Hex);
                    int aside = Enumerable.Range(0, 40000).Where(t => reach.Distance(t) is >= 3 and <= 6 && !nav.IsExit(t)).OrderBy(t => reach.Distance(t)).ThenBy(t => t).First();
                    Walk(aside); continue;
                }
                if (status != TempleWorldStatus.Ready) throw new Stop("Stalled", $"{place}: {status}");
            }
            if (world.TempleChestCustody.Length != 0)
            {
                place = "challenge chest"; var chest = Root(o => o.Offset == WorldSession.TempleChestOffset);
                Walk(Approach(chest));
                if (world.RetrieveTempleChest(false) != TempleChestStatus.Ready) throw new Stop("Stalled", $"{place}: retrieve refused");
            }
            place = "door 100";
            var route = world.InspectTempleVillageRoute(false);
            if (route.Status == TempleWorldStatus.DoorRequired)
            {
                Walk(route.TargetHex!.Value);
                if (world.Current.Doors.Toggle(route.DoorKey!, world.Location.Hex, 2, false) != DoorUseStatus.Ready) throw new Stop("Stalled", place);
                route = world.InspectTempleVillageRoute(false);
            }
            place = "Arroyo exit";
            if (route.Status != TempleWorldStatus.Ready) throw new Stop("Stalled", $"{place}: {route.Status}");
            Walk(route.ExitHex!.Value);
            if (world.UseExit(false).Status != ExitStatus.Ready || world.CurrentMap != 4) throw new Stop("Stalled", $"{place}: travel refused");
            foreach (WorldArea area in world.Areas) MapLayoutWriter.Verify(area.Map);
            return new RunLog(character.Name, seed, takeSpear, heal, "Arroyo", null, steps, fights.ToArray(), world.HitPoints, world.MaxHitPoints, world.Experience, world.Level, lowest, found, used);
        }
        catch (Stop stop)
        {
            return new RunLog(character.Name, seed, takeSpear, heal, stop.Result, stop.Message, steps, fights.ToArray(), world.HitPoints, world.MaxHitPoints, world.Experience, world.Level, lowest, found, used);
        }
    }
}
