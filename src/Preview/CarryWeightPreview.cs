using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private async Task CheckCarryWeightViews()
    {
        _checkingNavigation = true;
        try
        {
            ResetWalker(); SelectObject(_layout!.Objects.Single(o => o.Source.Offset == 206348)); OpenLoot();
            var line = _campaign!.Current.Items.Contents(_lootOwner!).Single();
            int requests = 0; _loot!.TakeRequested = (_, _) => requests++;
            var one = (Button)_loot.FindChild("TakeOneAction", true, false); var stack = (Button)_loot.FindChild("TakeStackAction", true, false);
            // Component fixtures exercise capacity states not present in the temple's small real stacks.
            _loot.Refresh("Teste de capacidade", new[] { line with { Quantity = 5 } }, OriginalText, (_, _) => ItemTakeStatus.OverCapacity, 15, false, 150, 150, _ => 10);
            one.EmitSignal(Button.SignalName.Pressed); stack.EmitSignal(Button.SignalName.Pressed);
            if (!one.Disabled || !stack.Disabled || !_loot.StatusText.Contains("capacidade") || _loot.SelectedWeightText != "50 lb" || requests != 0)
                throw new InvalidDataException("Capacity UI allowed transfer or misrepresented stack weight.");
            _loot.Refresh("Teste parcial", new[] { line with { Quantity = 5 } }, OriginalText, (_, qty) => qty == 1 ? ItemTakeStatus.Ready : ItemTakeStatus.OverCapacity, 14, false, 140, 150, _ => 10);
            if (one.Disabled || !stack.Disabled || !stack.TooltipText.Contains("capacidade")) throw new InvalidDataException("Partial-capacity UI failed to distinguish one unit from the entire stack.");
            _loot.Refresh("Teste de peso desconhecido", new[] { line }, OriginalText, (_, _) => ItemTakeStatus.WeightUnavailable, 1, false, null, 150, _ => null);
            if (!one.Disabled || !stack.Disabled || !_loot.SelectedWeightText.Contains("indisponível") || !_loot.LoadText.Contains("indisponível / 150 lb"))
                throw new InvalidDataException("Unknown weight was displayed as a measured zero.");
            var all = (Button)_loot.FindChild("TakeAllAction", true, false); int allRequests = 0;
            _loot.TakeAllRequested = () => allRequests++;
            _loot.Refresh("Teste de todas as pilhas", new[] { line }, OriginalText, (_, _) => ItemTakeStatus.Ready,
                0, false, 0, 150, _ => 1, canTakeAll: () => ItemTakeStatus.OverCapacity);
            all.EmitSignal(Button.SignalName.Pressed);
            if (one.Disabled || !all.Disabled || !all.TooltipText.Contains("capacidade") || allRequests != 0)
                throw new InvalidDataException("Take-all UI did not distinguish aggregate capacity from a selected item.");
            _loot.Refresh("Teste de todas as pilhas", new[] { line }, OriginalText, (_, _) => ItemTakeStatus.Ready,
                0, false, 0, 150, _ => 1, canTakeAll: () => ItemTakeStatus.Ready);
            all.EmitSignal(Button.SignalName.Pressed);
            if (all.Disabled || allRequests != 1) throw new InvalidDataException("Ready take-all UI did not issue one request.");
            _loot.Refresh("Teste de peso desconhecido", new[] { line }, OriginalText, (_, _) => ItemTakeStatus.Ready,
                0, false, 0, 150, _ => null, canTakeAll: () => ItemTakeStatus.WeightUnavailable);
            all.EmitSignal(Button.SignalName.Pressed);
            if (!all.Disabled || !all.TooltipText.Contains("indisponível") || allRequests != 1)
                throw new InvalidDataException("Unknown aggregate weight allowed a take-all request.");
            if (_campaign.CarriedCount != 0 || _campaign.Current.Items.Contents(_lootOwner!).Single().Quantity != 1)
                throw new InvalidDataException("UI fixtures changed real inventory state.");
            CloseLoot(); ResetWalker(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print("CARRY_WEIGHT_UI_OK: real item/stack/load labels verified by loot and bag checks; over-capacity, one-vs-stack, tooltip and unknown-weight component fixtures gated without transferring real items.");
        }
        finally { CloseLoot(); _checkingNavigation = false; }
    }
}
