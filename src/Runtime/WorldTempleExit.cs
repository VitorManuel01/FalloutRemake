using System.IO;

namespace FalloutRemake.Runtime;

public sealed partial class WorldSession
{
    // This bounded world has no operation which writes G369. It stays at the verified GAM initial value.
    private bool CanUseTempleExit(string key) => _templeProgress?.Profile.ExitDoor is { } exit && exit.Key == key && exit.InitialGlobal.Value == 0;
    private void ValidateTempleExit()
    {
        if (!_areas.TryGetValue(3, out var area)) return;
        var source = TempleExitDoorProfile.Match(area.Map); if (source == null) return;
        var door = area.Doors.Get(source.Key)!;
        if ((door.Flags != source.Header.Flags || door.OpenFlags != source.State["openFlags"]) && !CanUseTempleExit(source.Key))
            throw new InvalidDataException("Temple exit door changes require verified accepted Speech and source-fixed G369=0.");
    }
}
