using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private async Task CheckItemCheckpoint()
    {
        _checkingNavigation = true;
        try
        {
            ResetWalker(); ToggleWalkMode();
            RequestWalk(24508); _walker!.Advance(1000); UpdateWalkerPosition();
            if (UseDoor(_layout!.Objects.Single(o => o.Source.Offset == 244040).Source.Key) != DoorUseStatus.Ready)
                throw new InvalidDataException("First door did not open for item checkpoint test.");
            PreviewObject pot = _layout.Objects.Single(o => o.Source.Offset == 206348);
            ContainerItem powder = _campaign!.Current.Items.Contents(pot.Source.Key).Single();
            int target = Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(pot.Source.Header.Tile, d))
                .Where(t => t.HasValue && _reachable!.Distance(t.Value) >= 0).OrderBy(t => _reachable!.Distance(t!.Value)).ThenBy(t => t).First()!.Value;
            RequestWalk(target); _walker!.Advance(1000); UpdateWalkerPosition();
            if (_campaign.TakeItem(pot.Source.Key, powder.Source.Key, 1, false) != ItemTakeStatus.Ready || _campaign.CarriedCount != 1)
                throw new InvalidDataException("Real world did not collect source powder.");
            SaveCheckpoint(); ResetWalker(); LoadCheckpoint();
            if (_campaign.Carried.Single().Source.Header.Id != 106 || _campaign.Carried.Single().Source.Header.Pid != 273
                || _campaign.Current.Items.Contents(pot.Source.Key).Single().Quantity != 0)
                throw new InvalidDataException("Checkpoint lost the collected item identity or remaining source count.");
            RequestWalk(ClosestExitTo(126)); _walker!.Advance(1000); UpdateWalkerPosition(); UseExit();
            SaveCheckpoint(); ResetWalker();
            if (_campaign.CarriedCount != 0) throw new InvalidDataException("Reset retained collected items.");
            LoadCheckpoint();
            if (_campaign.CurrentMap != 126 || _campaign.CarriedCount != 1 || _campaign.Carried[0].Map != 3)
                throw new InvalidDataException("Inactive-area item state failed restoration in the exterior.");
            WorldCheckpoint saved = _campaign.Capture(_mapCatalog!.Source.Sha256);
            var legacyV2 = saved with { Entries = null, Combat = null, Character = null, Duel = null, Skills = null, Version = 2, Profile = "neutral-v2", Areas = saved.Areas.Select(a => a with { Consumed = null, Uses = null, Replacements = null }).ToArray() };
            CheckpointFile.Write(CheckpointPath, legacyV2); LoadCheckpoint();
            if (_campaign.CurrentMap != 126 || _campaign.CarriedCount != 1 || _campaign.ItemUses.Length != 0 || _campaign.Capture(_mapCatalog.Source.Sha256).Version != 13)
                throw new InvalidDataException("Legacy v2 checkpoint failed to upgrade with the original carried item.");
            var legacyV1 = legacyV2 with { Entries = null, Combat = null, Character = null, Duel = null, Skills = null, Version = 1, Profile = "neutral-v1", Areas = legacyV2.Areas.Select(a => a with { Consumed = null, Taken = null }).ToArray() };
            CheckpointFile.Write(CheckpointPath, legacyV1); LoadCheckpoint();
            if (_campaign.CarriedCount != 0 || _campaign.ItemUses.Length != 0) throw new InvalidDataException("Legacy v1 checkpoint invented carried or used items.");
            CheckpointFile.Write(CheckpointPath, saved); LoadCheckpoint();
            WorldSession before = _campaign;
            SavedArea source = saved.Areas.Single(a => a.Map == 3);
            CheckpointFile.Write(CheckpointPath, saved with { Areas = saved.Areas.Select(a => a.Map == 3
                ? a with { Taken = new[] { source.Taken!.Single() with { Quantity = 2 } } } : a).ToArray() });
            LoadCheckpoint();
            if (!ReferenceEquals(before, _campaign) || _campaign.CarriedCount != 1) throw new InvalidDataException("Forged item quantities replaced the live campaign.");
            SaveCheckpoint();
            RequestWalk(ClosestExitTo(3)); _walker!.Advance(1000); UpdateWalkerPosition(); UseExit();
            if (_campaign.TakeItem(pot.Source.Key, powder.Source.Key, 1, false) != ItemTakeStatus.Empty)
                throw new InvalidDataException("Returning to the temple duplicated the original item.");
            foreach (WorldArea area in _campaign.Areas) MapLayoutWriter.Verify(area.Map);
            ResetWalker(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print("ITEM_CHECKPOINT_OK: real pot 330/powder 106 PID 273, take/save/reset/load, v6 exterior checkpoint, legacy v1/v2 upgrade, return, source depletion, repeated take and forged quantities refused atomically, source bytes verified; test slot only.");
        }
        finally { _checkingNavigation = false; }
    }
}
