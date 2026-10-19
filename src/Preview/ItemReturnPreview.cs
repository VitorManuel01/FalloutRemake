using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private async Task CheckItemReturn()
    {
        _checkingNavigation = true;
        try
        {
            ResetWalker(); ToggleWalkMode(); RequestWalk(24508); _walker!.Advance(1000); UpdateWalkerPosition();
            UseDoor(_layout!.Objects.Single(o => o.Source.Offset == 244040).Source.Key);
            var pot = _campaign!.Current.Items.Containers.Single(o => o.Offset == 206348); var powder = _campaign.Current.Items.Contents(pot.Key).Single();
            RequestWalk(ContainerApproach(pot)!.Value); _walker!.Advance(1000); UpdateWalkerPosition();
            _campaign.TakeItem(pot.Key, powder.Source.Key, 1, false);
            var location = _campaign.Location; var doors = _doors!.Capture();
            OpenBag(); var one = (Button)_bag!.FindChild("ReturnOneAction", true, false);
            if (one.Disabled) throw new InvalidDataException("Nearby source return was unavailable.");
            one.EmitSignal(Button.SignalName.Pressed); one.EmitSignal(Button.SignalName.Pressed);
            if (_campaign.CarriedCount != 0 || _bag.RowCount != 0 || _campaign.Current.Items.Contents(pot.Key).Single().Quantity != 1
                || !one.Disabled || _campaign.Location != location || !_doors.Capture().Doors.SequenceEqual(doors.Doors))
                throw new InvalidDataException("Return duplicated quantities or changed unrelated world state.");
            CloseBag(); SelectObject(_layout.Objects.Single(o => o.Source.Key == pot.Key)); OpenLoot();
            if (_loot!.Remaining != 1) throw new InvalidDataException("Returned item failed to reappear in source container.");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "return.png"));
            ((Button)_loot.FindChild("TakeStackAction", true, false)).EmitSignal(Button.SignalName.Pressed); CloseLoot();
            if (_campaign.ReturnItem(3, pot.Key, powder.Source.Key, 2, false) != ItemTakeStatus.InvalidQuantity || _campaign.CarriedCount != 1)
                throw new InvalidDataException("Excessive return was not rejected atomically.");
            RequestWalk(28906); _walker!.Advance(.05); UpdateWalkerPosition(); OpenBag();
            if (!((Button)_bag!.FindChild("ReturnOneAction", true, false)).Disabled || !_bag.ReturnStatusText.Contains("aguarde"))
                throw new InvalidDataException("Moving actor was allowed to return an item.");
            CloseBag(); _walker.Advance(1000); UpdateWalkerPosition(); OpenBag();
            if (!((Button)_bag!.FindChild("ReturnOneAction", true, false)).Disabled || !_bag.ReturnStatusText.Contains("Aproxime-se"))
                throw new InvalidDataException("Far actor was allowed to return an item.");
            CloseBag(); RequestWalk(ClosestExitTo(126)); _walker.Advance(1000); UpdateWalkerPosition(); UseExit(); OpenBag();
            if (!((Button)_bag!.FindChild("ReturnStackAction", true, false)).Disabled || !_bag.ReturnStatusText.Contains("Volte à área")
                || _campaign.ReturnItem(3, pot.Key, powder.Source.Key, 1, false) != ItemTakeStatus.Unavailable)
                throw new InvalidDataException("Inactive source area accepted a remote return.");
            CloseBag(); RequestWalk(ClosestExitTo(3)); _walker!.Advance(1000); UpdateWalkerPosition(); UseExit();
            RequestWalk(ContainerApproach(pot)!.Value); _walker!.Advance(1000); UpdateWalkerPosition(); OpenBag();
            ((Button)_bag!.FindChild("ReturnStackAction", true, false)).EmitSignal(Button.SignalName.Pressed); CloseBag(); SaveCheckpoint(); ResetWalker(); LoadCheckpoint();
            if (_campaign.CarriedCount != 0 || _campaign.Current.Items.Contents(pot.Key).Single().Quantity != 1)
                throw new InvalidDataException("Checkpoint failed to restore net inventory after full return.");
            foreach (WorldArea area in _campaign.Areas) MapLayoutWriter.Verify(area.Map);
            ResetWalker();
            GD.Print("ITEM_RETURN_OK: real powder return/retake, one/stack buttons, empty bag and source recovery, duplicate/excessive/moving/far/remote guards, checkpoint net state, unchanged actor/doors/source bytes verified.");
        }
        finally { CloseBag(); CloseLoot(); _checkingNavigation = false; }
    }
}
