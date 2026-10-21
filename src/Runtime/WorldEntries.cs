using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public sealed record SavedArroyoReward([property:JsonRequired] string Profile,[property:JsonRequired] int Experience);
public sealed record SavedWorldEntries([property:JsonRequired] int[] EnteredMaps,[property:JsonRequired] SavedArroyoReward? ArroyoReward);

public sealed partial class WorldSession
{
    private readonly HashSet<int> _enteredMaps = new();
    private readonly Func<MapDocument,ArroyoRewardProfile>? _arroyoProfile;
    private SavedArroyoReward? _arroyoReward;
    public int[] EnteredMaps => _enteredMaps.OrderBy(id => id).ToArray();
    public SavedArroyoReward? ArroyoReward => _arroyoReward;
    private SavedWorldEntries CaptureEntries() => new(EnteredMaps,_arroyoReward);
    private SavedArroyoReward? PrepareEntryReward(ActorLocation destination,WorldArea target)
    {
        if (_enteredMaps.Contains(destination.Map) || destination != new ActorLocation(4,0,20517,0) || CurrentMap != 3
            || InitialMap != 3 || _templeProgress == null || _arroyoProfile == null) return null;
        if (_templeProgress.Profile.Speech.MapKey != TempleBarrierEffectProposal.MapHash
            || Current.Map.Source.Sha256 != TempleBarrierEffectProposal.MapHash || Experience is < 0 or >= 400)
            throw new InvalidDataException("Arroyo reward requires verified Speech and a neutral character below level two.");
        var profile = _arroyoProfile(target.Map);
        if (!profile.Matches(target.Map)) throw new InvalidDataException("Arroyo entry reward MAP mismatch.");
        return new(profile.Fingerprint,ArroyoRewardProfile.BaseExperience);
    }
    private void RestoreEntries(WorldCheckpoint saved)
    {
        _enteredMaps.Clear(); _arroyoReward = null;
        if (saved.Version < 8)
        {
            // A cached legacy area may have been entered. Never award retroactive XP from uncertain history.
            foreach (var area in saved.Areas) _enteredMaps.Add(area.Map);
            return;
        }
        var entries = saved.Entries;
        if (entries?.EnteredMaps is not {Length:>0 and <=16} maps || maps.Distinct().Count() != maps.Length
            || maps.Any(id => !_areas.ContainsKey(id)) || !maps.Contains(InitialMap) || !maps.Contains(saved.CurrentMap))
            throw new InvalidDataException("Checkpoint entry history is incomplete, duplicated or outside its loaded areas.");
        foreach (int id in maps) _enteredMaps.Add(id);
        if (entries.ArroyoReward is not { } reward) return;
        if (InitialMap != 3 || !maps.Contains(4) || _templeProgress == null || _arroyoProfile == null
            || !_areas.TryGetValue(4,out var village) || _templeProgress.Profile.Speech.MapKey != TempleBarrierEffectProposal.MapHash
            || reward.Experience != ArroyoRewardProfile.BaseExperience)
            throw new InvalidDataException("Checkpoint grants an unsupported Arroyo reward.");
        var profile = _arroyoProfile(village.Map);
        if (!profile.Matches(village.Map) || reward.Profile != profile.Fingerprint || Experience is < 0 or >= 400)
            throw new InvalidDataException("Checkpoint Arroyo reward profile or character scope differs.");
        _arroyoReward = reward;
    }
}
