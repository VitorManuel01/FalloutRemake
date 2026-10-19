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
    private async Task CheckQuantityTransfer()
    {
        _checkingNavigation = true;
        try
        {
            ResetWalker(); SelectObject(_layout!.Objects.Single(o => o.Source.Offset == 206348)); OpenLoot();
            var real = _campaign!.Current.Items.Contents(_lootOwner!).Single();
            var fixture = new ContainerItem(real.Source with { Key = "synthetic-quantity" }, 5);
            PrototypeText Text(MapObject _) => new(49, 0, "Item sintético de teste", "Fixture de interface: nenhum item real é transferido.", new("synthetic.msg", "synthetic", 0, "synthetic"));
            int requested = 0; bool blocked = false; _loot!.TakeRequested = (_, qty) => requested += qty;
            _loot.Refresh("Teste sintético de quantidade", new[] { fixture }, Text,
                (_, qty) => blocked || qty > 3 ? ItemTakeStatus.OverCapacity : ItemTakeStatus.Ready, 0, false, 120, 150, _ => 10);
            var selector = (QuantityTransfer)_loot.FindChild("TakeQuantity", true, false);
            var input = (SpinBox)selector.FindChild("QuantityValue", true, false); var button = (Button)selector.FindChild("QuantityTransferAction", true, false);
            input.Value = 4;
            if (!button.Disabled) throw new InvalidDataException("Explicit quantity ignored the carry limit.");
            input.GetLineEdit().Text = "2"; input.Apply();
            if (button.Disabled || input.Value != 2) throw new InvalidDataException("Typed integer quantity was not applied.");
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "quantity.png"));
            button.EmitSignal(Button.SignalName.Pressed);
            if (requested != 2) throw new InvalidDataException("Quantity action did not request the selected amount.");
            blocked = true; button.EmitSignal(Button.SignalName.Pressed);
            if (requested != 2 || !button.Disabled) throw new InvalidDataException("Quantity action failed to revalidate immediately before transfer.");
            selector.Configure(int.MaxValue, "Retirar quantidade", _ => ItemTakeStatus.Ready); input.Value = int.MaxValue;
            if (input.Value != int.MaxValue) throw new InvalidDataException("Large source quantity lost precision.");
            CloseLoot(); OpenBag();
            var carried = new CarriedItem(3, "synthetic-owner", fixture.Source, 5);
            _bag!.Refresh(new[] { carried }, _ => Text(fixture.Source), _ => "Fixture", 50, 150, _ => 10, (_, _) => ItemTakeStatus.Ready);
            int returned = 0; _bag.ReturnRequested = (_, qty) => returned += qty;
            selector = (QuantityTransfer)_bag.FindChild("ReturnQuantity", true, false); input = (SpinBox)selector.FindChild("QuantityValue", true, false);
            button = (Button)selector.FindChild("QuantityTransferAction", true, false); input.GetLineEdit().Text = "3"; button.EmitSignal(Button.SignalName.Pressed);
            if (returned != 3) throw new InvalidDataException($"Bag explicit quantity did not use the typed amount: requested={returned}, input={input.Value}, disabled={button.Disabled}.");
            _bag.Refresh(Array.Empty<CarriedItem>(), _ => null, _ => "Fixture", 0, 150, _ => 10);
            button.EmitSignal(Button.SignalName.Pressed);
            if (selector.Visible || returned != 3 || !button.Disabled || _campaign.CarriedCount != 0)
                throw new InvalidDataException("Empty inventory retained an active quantity transfer or fixtures changed real state.");
            CloseBag(); MapLayoutWriter.Verify(_map!); ResetWalker();
            GD.Print("QUANTITY_TRANSFER_OK: typed integer amount, load gating, selected pickup/return amount, click-time revalidation, int32 precision and empty-state cleanup; synthetic component fixtures only, real inventory/source unchanged.");
        }
        finally { CloseLoot(); CloseBag(); _checkingNavigation = false; }
    }
}
