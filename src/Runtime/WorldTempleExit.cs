using System.IO;

namespace FalloutRemake.Runtime;

public sealed partial class WorldSession
{
    // map_update unlocks the exit at M20 = 2 while G369 keeps its GAM value 0 (Speech or a yielded challenge). Killing Cameron
    // writes G369 = 1: then only the Temple Key from his body unlocks it (L7 = 1).
    private bool CanUseTempleExit(string key) => _templeProgress?.Profile.ExitDoor is { } exit && exit.Key == key && exit.InitialGlobal.Value == 0
        || _duelOutcome == TempleDuelOutcome.Killed && _duelProfile?.ExitDoor?.Key == key && _keyUnlocked.Contains(key);
    private void ValidateTempleExit()
    {
        if (!_areas.TryGetValue(3, out var area)) return;
        var source = TempleExitDoorProfile.Match(area.Map); if (source == null) return;
        var door = area.Doors.Get(source.Key)!;
        if ((door.Flags != source.Header.Flags || door.OpenFlags != source.State["openFlags"]) && !CanUseTempleExit(source.Key))
            throw new InvalidDataException("Temple exit door changes require the trial passed with G369=0, or the Temple Key after Cameron's death.");
    }
}
