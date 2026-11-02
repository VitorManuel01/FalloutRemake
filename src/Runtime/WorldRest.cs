using System;
using System.Collections.Generic;
using System.Linq;
using FalloutRemake.Geometry;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public enum RestStatus { Ready, NoPipBoy, InCombat, Busy, EnemiesNearby, Healthy, InvalidDuration }

/// <summary>What a rest did: minutes rested, HP healed, and whether poison stopped it (or killed).</summary>
public sealed record RestResult(RestStatus Status, int Minutes = 0, int Healed = 0, int PoisonLost = 0, bool Died = false);

/// <summary>
/// The player's natural healing, which FO2 grants only while resting (CE pipboyRest) or travelling the world map. The
/// Pip-Boy rests in animation steps: each step adds its truncated share of minutes to a counter, and every 180 counted
/// minutes heal the healing rate (_Check4Health, _AddHealth via _partyMemberRestingHeal(3)); the counter starts at 0 each
/// time the Pip-Boy opens. "Until healed" rests hpToHeal / rate x 3 hours in 24-hour pieces, then 3 hours at a time.
/// Game time passes as it rests (poison acts; nothing wanders). _critter_can_obj_dude_rest refuses an elevation the map
/// does not let rest on (maps.txt can_rest_here) while a living critter of another team stands on it. The Pip-Boy itself
/// only works once the vault suit is worn (wmMapPipboyActive), which this game does not reach yet; CE's option to have
/// it from the start (pipboy_available_at_game_start) is <see cref="PipBoyFromStart"/>.
/// </summary>
public sealed partial class WorldSession
{
    private int _restMinutes;

    /// <summary>CE/sfall option: the Pip-Boy (and rest) from the start, without the vault suit. Off as in the original; not saved.</summary>
    public bool PipBoyFromStart { get; set; }
    /// <summary>maps.txt can_rest_here of a map's elevation (CE default: allowed); set by whoever reads the catalog.</summary>
    public Func<int, int, bool>? CanRestOn { get; set; }

    /// <summary>The Pip-Boy opens: its rest counter starts over (pipboyOpen sets _rest_time = 0).</summary>
    public void OpenPipBoy() => _restMinutes = 0;

    public RestStatus CanRest(bool moving)
    {
        if (!PipBoyFromStart) return RestStatus.NoPipBoy;
        if (InCombat) return RestStatus.InCombat;
        if (moving) return RestStatus.Busy;
        return HostileNearby() ? RestStatus.EnemiesNearby : RestStatus.Ready;
    }

    // _critter_can_obj_dude_rest outside combat (whoHitMe is cleared when a fight ends): only where the map forbids rest.
    private bool HostileNearby()
    {
        if (CanRestOn?.Invoke(CurrentMap, Location.Elevation) ?? true) return false;
        foreach (MapObject source in Current.Map.ObjectLayers.SelectMany(l => l.Objects).Where(o => o.Header.Pid >> 24 == 1))
        {
            if (IsDefeated(CurrentMap, source.Key)) continue;
            MapObject critter = DefeatedObject(CurrentMap, source);
            if (critter.Header.Elevation != Location.Elevation || (critter.Header.Flags & HexNavigation.Hidden) != 0) continue;
            Prototype? prototype = Current.Map.Prototypes.FirstOrDefault(p => p.Pid == source.Header.Pid);
            int team = critter.State.TryGetValue("team", out int instanceTeam) ? instanceTeam : prototype == null ? -1 : CritterPrototypeData.Read(prototype).Team;
            if (team != 0) return true;
        }
        return false;
    }

    /// <summary>CE pipboyRest(hours, minutes): 10 minutes up to whole hours.</summary>
    public RestResult Rest(int hours, int minutes, bool moving)
    {
        if (hours is < 0 or > 24 || minutes is < 0 or > 59 || hours == 0 && minutes == 0) return new(RestStatus.InvalidDuration);
        RestStatus status = CanRest(moving);
        if (status != RestStatus.Ready) return new(status);
        var total = new RestTotal();
        RestSteps(hours, minutes, total);
        return total.Result();
    }

    /// <summary>The Pip-Boy's "until healed": hpToHeal / rate x 3 hours in 24-hour pieces, then 3 hours at a time until whole.</summary>
    public RestResult RestUntilHealed(bool moving)
    {
        RestStatus status = CanRest(moving);
        if (status != RestStatus.Ready) return new(status);
        if (HitPoints >= MaxHitPoints) return new(RestStatus.Healthy);
        var total = new RestTotal();
        int hours = (int)((double)(MaxHitPoints - HitPoints) / Sheet.HealingRate * 3.0);
        while (!total.Stopped && hours != 0)
        {
            int piece = Math.Min(hours, 24);
            RestSteps(piece, 0, total); hours -= piece;
        }
        // The second pass: whatever is still missing, 3 hours at a time. CE tests the HP it measured before the last rest, so
        // the rest that heals the last point is followed by one more.
        int missing = MaxHitPoints - HitPoints;
        for (int guard = 0; guard < 10000 && !total.Stopped && missing != 0; guard++) { missing = MaxHitPoints - HitPoints; RestSteps(3, 0, total); }
        return total.Result();
    }

    private sealed class RestTotal
    {
        public int Minutes, Healed, PoisonLost; public bool Died;
        public bool Stopped => Died;
        public RestResult Result() => new(RestStatus.Ready, Minutes, Healed, PoisonLost, Died);
    }

    // One pipboyRest(hours, minutes, 0) with CE's animation steps; time moves step by step so poison acts on the way.
    private void RestSteps(int hours, int minutes, RestTotal total)
    {
        double v1 = hours * 60 + minutes, v2 = v1 * (1.0 / 1440.0) * 3.5 + 0.25, v3 = minutes / v1 * v2;
        long start = _gameTime;
        bool Advance(long target)
        {
            int ticks = (int)Math.Max(0, target - _gameTime);
            if (ticks == 0) return true;
            WorldTimeAdvance step = AdvanceTime(ticks);
            total.PoisonLost += step.HitPointsLost;
            if (step.Died) { total.Died = true; return false; }
            return true;
        }
        if (minutes != 0)
        {
            double v4 = v3 * 20.0;
            for (int i = 0; i < (int)v4; i++)
                if (!Advance((long)(i / v4 * (minutes * 600.0) + start))) return;
            if (!Advance(start + 600L * minutes)) return;
            total.Minutes += minutes;
            CountRest(minutes, total);
        }
        if (hours != 0)
        {
            long from = _gameTime;
            double v7 = (v2 - v3) * 20.0;
            for (int i = 0; i < (int)v7; i++)
            {
                if (!Advance((long)(i / v7 * (hours * 36000.0) + from))) return;
                CountRest((int)(hours * 60 / v7), total);
            }
            if (!Advance(from + 36000L * hours)) return;
            total.Minutes += hours * 60;
        }
    }

    // _Check4Health: 180 counted minutes heal the healing rate (_partyMemberRestingHeal(3)), then the count starts over.
    private void CountRest(int minutes, RestTotal total)
    {
        _restMinutes += minutes;
        if (_restMinutes < 180) return;
        _restMinutes = 0;
        int healed = Math.Min(Sheet.HealingRate, _playerDamage);
        _playerDamage -= healed; total.Healed += healed;
    }
}
