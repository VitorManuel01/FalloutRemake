using System;
using FalloutRemake.Runtime;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private readonly NpcPlaybackClock _templeClock = new();
    private bool _templeAutomatic, _skipTempleFrame;
    private Guid _templeClockToken;
    private long _templeClockRevision;
    private TempleWorldStatus? _lastPlaybackRefusal;
    private void SynchronizeTemplePlayback()
    {
        if (_campaign == null || _templeClockToken != _campaign.TempleToken
            || _campaign.TempleProgress?.Departure.Phase == NpcDeparturePhase.Hidden || _campaign.TempleProgress == null)
        {
            _templeAutomatic = false; _templeClock.Reset(); _lastPlaybackRefusal = null;
            _templeClockToken = _campaign?.TempleToken ?? Guid.Empty;
            _templeClockRevision = _campaign?.TempleRevision ?? 0;
        }
    }
    private void SetTemplePlayback(bool enabled)
    {
        SynchronizeTemplePlayback();
        if (enabled && (_campaign?.TempleProgress == null || _campaign.CurrentMap != 3 || _campaign.Location.Elevation != 2 || _elevation != 2
            || _walker?.Moving != false || InspectorOpen || _campaign.TempleProgress.Departure.Phase == NpcDeparturePhase.Hidden))
        { RefreshActiveTempleHud(); return; }
        _templeAutomatic = enabled; _skipTempleFrame = enabled; _templeClock.Reset(); _lastPlaybackRefusal = null;
        _templeClockToken = _campaign?.TempleToken ?? Guid.Empty; _templeClockRevision = _campaign?.TempleRevision ?? 0;
        RefreshActiveTempleHud();
    }
    private void AdvanceTemplePlayback(double seconds)
    {
        SynchronizeTemplePlayback();
        if (!_templeAutomatic || _checkingNavigation) return;
        if (_skipTempleFrame) { _skipTempleFrame = false; _templeClock.Reset(); return; }
        if (_campaign!.CurrentMap != 3 || _campaign.Location.Elevation != 2 || _elevation != 2 || _walker?.Moving != false || InspectorOpen)
        { _templeClock.Reset(); return; }
        if (_campaign.TempleRevision != _templeClockRevision) { SetTemplePlayback(false); return; }
        _templeClock.Add(seconds); long before = _campaign.TempleRevision;
        TempleWorldStatus result = TempleWorldStatus.Ready;
        // Bound work per frame after a stall. Remaining active time stays in the clock.
        for (int work = 0; work < 16; work++)
        {
            var state = _campaign.TempleProgress!.Departure;
            if (state.Phase == NpcDeparturePhase.Hidden) break;
            if (state.Phase == NpcDeparturePhase.GateDelay)
            {
                int ticks = _templeClock.TakeTicks(state.RemainingTicks); if (ticks == 0) break;
                result = _campaign.AdvanceTempleNpcTime(ticks,_campaign.TempleToken,_campaign.TempleRevision,false).Status;
            }
            else
            {
                if (!_templeClock.TakeStep()) break;
                result = _campaign.StepTempleNpc(_campaign.TempleToken,_campaign.TempleRevision,false);
            }
            if (result != TempleWorldStatus.Ready) { _templeClock.Reset(); break; }
        }
        _templeClockRevision = _campaign.TempleRevision;
        if (_campaign.TempleRevision != before)
        { RefreshActiveTempleProjection(); _lastPlaybackRefusal = null; }
        if (result != TempleWorldStatus.Ready && result != _lastPlaybackRefusal)
        { _lastPlaybackRefusal = result; NavigationStatus(TempleActionText(result)); }
        SynchronizeTemplePlayback(); RefreshActiveTempleHud();
    }
}
