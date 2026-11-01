using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FalloutRemake.Import;

/// <summary>
/// One section of data/ai.txt as CE aiInit reads it. Enumerated keys hold their CE index, -1 when absent; RunAwayMode
/// and Disposition keep CE's off-by-one (the first listed word becomes -1). HurtTooMuch holds CE damage flags.
/// Message ranges, font and colors are left out: the project shows no combat taunts.
/// </summary>
public sealed record AiPacket(string Name, int Number, int MaxDistance, int MinToHit, int MinHitPoints, int Aggression, int HurtTooMuch,
    int SecondaryFrequency, int CalledFrequency, int Chance, int AreaAttackMode, int RunAwayMode, int BestWeapon, int Distance, int AttackWho,
    int ChemUse, int Disposition)
{
    // CE gDistanceModeKeys order.
    public const int StayClose = 0, Charge = 1, Snipe = 2, OnYourOwn = 3, Stay = 4;
}

public static class AiPacketCatalog
{
    static readonly string[] AreaAttackModes = { "always", "sometimes", "be_sure", "be_careful", "be_absolutely_sure" };
    static readonly string[] AttackWho = { "whomever_attacking_me", "strongest", "weakest", "whomever", "closest" };
    static readonly string[] BestWeapons = { "no_pref", "melee", "melee_over_ranged", "ranged_over_melee", "ranged", "unarmed", "unarmed_over_thrown", "random" };
    static readonly string[] ChemUses = { "clean", "stims_when_hurt_little", "stims_when_hurt_lots", "sometimes", "anytime", "always" };
    static readonly string[] Distances = { "stay_close", "charge", "snipe", "on_your_own", "stay" };
    static readonly string[] RunAwayModes = { "none", "coward", "finger_hurts", "bleeding", "not_feeling_good", "tourniquet", "never" };
    static readonly string[] Dispositions = { "none", "custom", "coward", "defensive", "aggressive", "berserk" };
    // _parse_hurt_str: blind, crippled (all limbs), crippled_legs, crippled_arms.
    static readonly (string Key, int Flags)[] HurtFlags = { ("blind", 0x40), ("crippled", 0x3C), ("crippled_legs", 0x0C), ("crippled_arms", 0x30) };

    /// <summary>Packets by packet_num; a section missing one of CE's required integers makes the file invalid, as in aiInit.</summary>
    public static IReadOnlyDictionary<int, AiPacket> Read(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var sections = new List<(string Name, Dictionary<string, string> Keys)>();
        foreach (string raw in Encoding.Latin1.GetString(bytes).Split('\n'))
        {
            // CE config: ';' starts a comment; [section]; key=value with both sides trimmed.
            string line = raw.Split(';')[0].Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith('[') && line.EndsWith(']')) sections.Add((line[1..^1].Trim(), new Dictionary<string, string>(StringComparer.Ordinal)));
            else if (sections.Count > 0 && line.IndexOf('=') is int eq and > 0) sections[^1].Keys[line[..eq].Trim()] = line[(eq + 1)..].Trim();
        }
        var packets = new Dictionary<int, AiPacket>();
        foreach (var (name, keys) in sections)
        {
            int Int(string key) => keys.TryGetValue(key, out string? value) && int.TryParse(value, out int number) ? number
                : throw new InvalidDataException($"ai.txt [{name}] lacks the integer {key}.");
            // _cai_match_str_to_list keeps the last case-insensitive match, -1 without one.
            int Match(string key, string[] list) => keys.TryGetValue(key, out string? value)
                ? Enumerable.Range(0, list.Length).LastOrDefault(i => string.Equals(list[i], value, StringComparison.OrdinalIgnoreCase), -1) : -1;
            foreach (string required in new[] { "font", "color", "outline_color", "run_start", "run_end", "move_start", "move_end", "attack_start", "attack_end",
                "miss_start", "miss_end" }) Int(required);
            int hurt = 0;
            if (keys.TryGetValue("hurt_too_much", out string? hurtText))
                foreach (string word in hurtText.ToLowerInvariant().Split(',').Select(w => w.TrimStart(' ')))
                    hurt |= HurtFlags.FirstOrDefault(h => h.Key == word).Flags;
            int runAway = Match("run_away_mode", RunAwayModes), disposition = Match("disposition", Dispositions);
            var packet = new AiPacket(name, Int("packet_num"), Int("max_dist"), Int("min_to_hit"), Int("min_hp"), Int("aggression"), hurt,
                Int("secondary_freq"), Int("called_freq"), Int("chance"), Match("area_attack_mode", AreaAttackModes),
                runAway >= 0 ? runAway - 1 : runAway, Match("best_weapon", BestWeapons), Match("distance", Distances), Match("attack_who", AttackWho),
                Match("chem_use", ChemUses), keys.ContainsKey("disposition") ? disposition - 1 : -1);
            if (packet.CalledFrequency < 1 || packet.SecondaryFrequency < 1) throw new InvalidDataException($"ai.txt [{name}] has a frequency below 1.");
            // CE looks packets up by number in file order: the first section with a number wins.
            packets.TryAdd(packet.Number, packet);
        }
        return packets;
    }
}
