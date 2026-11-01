using System;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    // Exploration game time for poison: CE adds one tick per 100 ms outside combat (_script_chk_timed_events).
    // It pauses with the inspector windows (_Process returns early) and during the automated checks.
    private double _gameTimeCarry;

    private void AdvanceGameTime(double seconds)
    {
        if (_campaign == null || InCombat || _checkingNavigation || !_campaign.Poison.Active) { _gameTimeCarry = 0; return; }
        _gameTimeCarry += seconds;
        int ticks = (int)(_gameTimeCarry * PoisonRules.TicksPerSecond);
        if (ticks == 0) return;
        _gameTimeCarry -= (double)ticks / PoisonRules.TicksPerSecond;
        ApplyGameTime(ticks);
    }

    private void ApplyGameTime(int ticks)
    {
        PoisonTimeAdvance step = _campaign!.AdvanceTime(ticks);
        if (step.Died)
        {
            ResetWalker();
            NavigationStatus("Você morreu envenenado · a prévia voltou ao início do Templo.");
            GD.Print("POISON_DEATH");
            return;
        }
        if (step.HitPointsLost == 0) return;
        RefreshDoorAction();
        NavigationStatus($"O veneno tira {step.HitPointsLost} HP · {_campaign.HitPoints}/{_campaign.MaxHitPoints} HP"
            + (step.Poison.Active ? $" · envenenado ({step.Poison.Level})" : " · você se sente melhor"));
    }
}
