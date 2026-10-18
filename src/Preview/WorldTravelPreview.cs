using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private int ClosestExitTo(int mapId)
    {
        HexReachability reach = _navigation!.ReachableFrom(_walker!.CurrentHex);
        return _layout!.Objects.Where(o => o.Kind == PreviewKind.Exit
                && o.Source.State.GetValueOrDefault("destinationMap", -99) == mapId && reach.Distance(o.Source.Header.Tile) >= 0)
            .Select(o => o.Source.Header.Tile).OrderBy(reach.Distance).First();
    }
    private async Task CheckWorldTravel()
    {
        _checkingNavigation = true;
        try
        {
            ResetWalker(); ToggleWalkMode();
            string first = _layout!.Objects.Single(o => o.Source.Offset == 244040).Source.Key;
            string second = _doors!.TempleDoor!.SourceKey;
            RequestWalk(24508); _walker!.Advance(100); UpdateWalkerPosition(); UseDoor(first);
            HexReachability reached = _navigation!.ReachableFrom(_walker.CurrentHex);
            int approach = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(11108, d))
                .Where(t => t.HasValue && reached.Distance(t.Value) >= 0 && !_navigation.IsExit(t.Value))
                .Select(t => t!.Value).OrderBy(reached.Distance).First();
            RequestWalk(approach); _walker.Advance(100); UpdateWalkerPosition();
            SelectObject(_layout.Objects.Single(o => o.Source.Key == second)); UseSelectedLockpick(); UseDoor(second);
            var templeDoors = _doors;
            int outside = ClosestExitTo(126);
            RequestWalk(outside); _walker.Advance(1000); UpdateWalkerPosition(); UseExit();
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_campaign!.CurrentMap != 126 || _walker?.CurrentHex != 16486 || _campaign.Experience != 25
                || _map!.DeclaredRootObjects != 567 || _lookup.Count != 567)
                throw new InvalidDataException("Exterior transition failed to preserve destination, objects or XP.");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "outside.png"));
            int back = ClosestExitTo(3);
            RequestWalk(back); _walker!.Advance(1000); UpdateWalkerPosition(); UseExit();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_campaign.CurrentMap != 3 || _walker?.CurrentHex != 28707 || _campaign.Experience != 25
                || !ReferenceEquals(_doors, templeDoors) || !_doors.Get(first)!.IsOpen || !_doors.Get(second)!.IsOpen)
                throw new InvalidDataException("Returning to the cached temple lost door state or XP.");
            foreach (var area in _campaign.Areas) MapLayoutWriter.Verify(area.Map);
            var villageData = new GameData(_directory!);
            MapDocument village = _mapCatalog!.Load(villageData, 4);
            if (village.DeclaredRootObjects != 1836) throw new InvalidDataException("Village structural validation mismatch.");
            ResetWalker();
            if (_campaign.CurrentMap != 3 || _campaign.Experience != 0 || _reachable!.Count != 540)
                throw new InvalidDataException("World reset did not restore all area sessions.");
            GD.Print($"WORLD_PREVIEW_OK: exit {outside} -> ARTEMPLE / 16486; exit {back} -> ARCAVES / 28707; cached doors, XP 25, 567 exterior objects, village structure, global reset and source bytes verified.");
        }
        finally { _checkingNavigation = false; }
    }
}
