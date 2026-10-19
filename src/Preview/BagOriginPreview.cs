using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Import;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private async Task CheckBagOrigin()
    {
        _checkingNavigation = true;
        try
        {
            ResetWalker(); ToggleWalkMode(); RequestWalk(24508); _walker!.Advance(1000); UpdateWalkerPosition();
            UseDoor(_layout!.Objects.Single(o => o.Source.Offset == 244040).Source.Key);
            var pot = _campaign!.Current.Items.Containers.Single(o => o.Offset == 206348); var powder = _campaign.Current.Items.Contents(pot.Key).Single();
            RequestWalk(ContainerApproach(pot)!.Value); _walker!.Advance(1000); UpdateWalkerPosition(); _campaign.TakeItem(pot.Key, powder.Source.Key, 1, false);
            RequestWalk(28906); _walker.Advance(1000); UpdateWalkerPosition(); RefreshReachability();
            var before = _campaign.Location; var doors = _doors!.Capture();
            var expected = _navigation!.FindRoute(_walker.CurrentHex, BagOriginApproach(_campaign.Carried.Single())!.Value).Tiles;
            ChangeElevation(1); OpenBag();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var approach = (Button)_bag!.FindChild("BagOriginApproachAction", true, false);
            if (approach.Disabled || _elevation != 1 || _campaign.Location != before) throw new InvalidDataException("Bag origin approach confused inspection with the actor's elevation.");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "bag-origin.png"));
            approach.EmitSignal(Button.SignalName.Pressed);
            if (_bag != null || _elevation != 0 || !_walkMode || !_walker!.Moving || !_committedRoute.SequenceEqual(expected)
                || _selected?.Source.Key != pot.Key || _campaign.CarriedCount != 1 || _campaign.Location != before)
                throw new InvalidDataException("Bag approach did not follow the shared route or transferred an item automatically.");
            _walker.Advance(1000); UpdateWalkerPosition(); RefreshReachability(); OpenBag();
            if (!((Button)_bag!.FindChild("BagOriginApproachAction", true, false)).Disabled
                || ((Button)_bag.FindChild("ReturnOneAction", true, false)).Disabled) throw new InvalidDataException("Arrival failed to enable explicit return and disable needless approach.");
            CloseBag(); RequestWalk(ClosestExitTo(126)); _walker.Advance(1000); UpdateWalkerPosition(); UseExit();
            int areaCount = _campaign.Areas.Count; var outside = _campaign.Location; OpenBag();
            approach = (Button)_bag!.FindChild("BagOriginApproachAction", true, false); approach.EmitSignal(Button.SignalName.Pressed);
            if (!approach.Disabled || _bag == null || _campaign.Location != outside || _campaign.Areas.Count != areaCount)
                throw new InvalidDataException("Bag initiated remote world travel or loaded an inactive origin.");
            CloseBag(); if (!_campaign.Areas.Single(a => a.Map.Prefix.Header.MapIndex == 3).Doors.Capture().Doors.SequenceEqual(doors.Doors))
                throw new InvalidDataException("Bag origin route changed source doors.");
            foreach (var area in _campaign.Areas) MapLayoutWriter.Verify(area.Map);
            ResetWalker();
            GD.Print("BAG_ORIGIN_OK: source approach from bag, inspection-to-actor elevation, shared route/selection without auto-return, arrival gating, remote/no-load guard, unchanged source doors/items/bytes verified.");
        }
        finally { CloseBag(); _checkingNavigation = false; }
    }
}
