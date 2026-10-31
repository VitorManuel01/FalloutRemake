using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

public sealed record SavedArroyoReward([property:JsonRequired] string Profile,[property:JsonRequired] int Experience);
public sealed record SavedArroyoGlobals([property:JsonRequired] string Profile,[property:JsonRequired] int Karma,
    [property:JsonRequired] int ArroyoReputation,[property:JsonRequired] int KarmaTierGlobal);
public sealed record SavedWorldEntries([property:JsonRequired] int[] EnteredMaps,[property:JsonRequired] SavedArroyoReward? ArroyoReward,
    SavedArroyoGlobals? ArroyoGlobals = null);

public sealed partial class WorldSession
{
    private readonly HashSet<int> _enteredMaps = new();
    private readonly Func<MapDocument,ArroyoRewardProfile>? _arroyoProfile;
    private SavedArroyoReward? _arroyoReward;
    private SavedArroyoGlobals? _arroyoGlobals;
    public int[] EnteredMaps => _enteredMaps.OrderBy(id => id).ToArray();
    public SavedArroyoReward? ArroyoReward => _arroyoReward;
    public SavedArroyoGlobals? ArroyoGlobals => _arroyoGlobals;
    private SavedWorldEntries CaptureEntries() => new(EnteredMaps,_arroyoReward,_arroyoGlobals);
    private (SavedArroyoReward Reward,SavedArroyoGlobals Globals)? PrepareEntryReward(ActorLocation destination,WorldArea target)
    {
        if (_enteredMaps.Contains(destination.Map) || destination != new ActorLocation(4,0,20517,0) || CurrentMap != 3
            || InitialMap != 3 || _templeProgress == null || _arroyoProfile == null) return null;
        if (_templeProgress.Profile.Speech.MapKey != TempleBarrierEffectProposal.MapHash
            || Current.Map.Source.Sha256 != TempleBarrierEffectProposal.MapHash || Experience < 0)
            throw new InvalidDataException("Arroyo reward requires verified Speech and a valid neutral character.");
        var profile = _arroyoProfile(target.Map);
        if (!profile.Matches(target.Map)) throw new InvalidDataException("Arroyo entry reward MAP mismatch.");
        return (new(profile.Fingerprint,ArroyoRewardProfile.BaseExperience),profile.EntryGlobals());
    }
    private void RestoreEntries(WorldCheckpoint saved)
    {
        _enteredMaps.Clear(); _arroyoReward = null; _arroyoGlobals = null;
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
        if (entries.ArroyoReward is not { } reward)
        {
            if (entries.ArroyoGlobals != null) throw new InvalidDataException("Arroyo entry globals require their verified first-entry receipt.");
            return;
        }
        if (InitialMap != 3 || !maps.Contains(4) || _templeProgress == null || _arroyoProfile == null
            || !_areas.TryGetValue(4,out var village) || _templeProgress.Profile.Speech.MapKey != TempleBarrierEffectProposal.MapHash
            || reward.Experience != ArroyoRewardProfile.BaseExperience)
            throw new InvalidDataException("Checkpoint grants an unsupported Arroyo reward.");
        var profile = _arroyoProfile(village.Map);
        // Any level: the 600 XP is fixed in arvillag.int and level-ups are modeled by CharacterProgression.
        if (!profile.Matches(village.Map) || reward.Profile != profile.Fingerprint || Experience < 0)
            throw new InvalidDataException("Checkpoint Arroyo reward profile or character scope differs.");
        var expected = profile.EntryGlobals();
        if (saved.Version >= 9 && entries.ArroyoGlobals != expected || saved.Version < 9 && entries.ArroyoGlobals != null)
            throw new InvalidDataException("Checkpoint Arroyo entry globals differ from the neutral source profile.");
        _arroyoReward = reward;
        // v8 receipts prove this exact neutral first-entry event. Derive its globals once; never award XP again.
        _arroyoGlobals = expected;
    }
}
