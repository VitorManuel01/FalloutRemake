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
            var expected = ItemOriginRoute.Find(_campaign, _campaign.Carried.Single(), false)!.Legs[0].Tiles;
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
            approach = (Button)_bag!.FindChild("BagOriginApproachAction", true, false);
            var route = ItemOriginRoute.Find(_campaign, _campaign.Carried.Single(), false)!;
            if (approach.Disabled || approach.Text != "Ir à passagem" || !_bag.ReturnStatusText.Contains("Enter")
                || !((Button)_bag.FindChild("ReturnOneAction", true, false)).Disabled || route.Transitions != 1)
                throw new InvalidDataException("Bag failed to offer explicit cross-area guidance.");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "bag-return-route.png"));
            approach.EmitSignal(Button.SignalName.Pressed);
            if (_bag != null || !_walker!.Moving || !_committedRoute.SequenceEqual(route.Legs[0].Tiles)
                || _campaign.Location != outside || _campaign.Areas.Count != areaCount || _selected?.Kind != Geometry.PreviewKind.Exit)
                throw new InvalidDataException("Bag travel guidance skipped the walk or changed area automatically.");
            _walker.Advance(1000); UpdateWalkerPosition(); RefreshReachability(); OpenBag();
            approach = (Button)_bag!.FindChild("BagOriginApproachAction", true, false);
            if (approach.Disabled || approach.Text != "Ver passagem" || _campaign.CurrentMap != 126 || _campaign.CarriedCount != 1)
                throw new InvalidDataException("Exit arrival crossed or returned an item automatically.");
            approach.EmitSignal(Button.SignalName.Pressed);
            if (_bag != null || _walker.Moving || _campaign.CurrentMap != 126) throw new InvalidDataException("Exit guidance performed implicit travel.");
            using var enter = new InputEventKey { Pressed = true, PhysicalKeycode = Key.Enter }; _UnhandledInput(enter);
            if (_campaign.CurrentMap != 3 || _campaign.CarriedCount != 1) throw new InvalidDataException("Explicit Enter did not follow the proposed passage.");
            OpenBag(); ((Button)_bag!.FindChild("BagOriginApproachAction", true, false)).EmitSignal(Button.SignalName.Pressed);
            _walker!.Advance(1000); UpdateWalkerPosition(); RefreshReachability(); OpenBag();
            var returnOne = (Button)_bag!.FindChild("ReturnOneAction", true, false);
            if (returnOne.Disabled) throw new InvalidDataException("Cross-area guidance failed to reach the original source.");
            returnOne.EmitSignal(Button.SignalName.Pressed);
            if (_campaign.CarriedCount != 0 || _campaign.Current.Items.Contents(pot.Key).Single().Quantity != 1)
                throw new InvalidDataException("Explicit return after travel lost the item.");
            CloseBag(); if (!_campaign.Areas.Single(a => a.Map.Prefix.Header.MapIndex == 3).Doors.Capture().Doors.SequenceEqual(doors.Doors))
                throw new InvalidDataException("Bag origin route changed source doors.");
            foreach (var area in _campaign.Areas) MapLayoutWriter.Verify(area.Map);
            ResetWalker();
            GD.Print("BAG_ORIGIN_OK: source approach, inspection-to-actor elevation, cross-area guidance through cached exits, first-leg walk, no automatic travel/transfer, explicit Enter and return, no-load guard, unchanged source doors/bytes verified.");
        }
        finally { CloseBag(); _checkingNavigation = false; }
    }
}
