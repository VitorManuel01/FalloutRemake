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
    private async Task CheckReturnAllChest(MapObject chest)
    {
        byte[] State() => CheckpointFile.Encode(_campaign!.Capture(_mapCatalog!.Source.Sha256));
        byte[] before = State(); var actor = _campaign!.Location; int xp = _campaign.Experience;
        OpenBag(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var all = (Button)_bag!.FindChild("ReturnOriginAction", true, false);
        var item = _bag.SelectedItem!; Action<CarriedItem> oldCallback = _bag.ReturnAllRequested!;
        long revision = _campaign.Current.Items.Revision;
        if (all.Disabled || _bag.RowCount != 2 || !all.TooltipText.Contains("2 unidade(s)"))
            throw new InvalidDataException("Two-source bag did not explain a common-origin return.");
        if (_capture != null) await Capture(Path.ChangeExtension(_capture, "return-all-before.png"));
        all.EmitSignal(Button.SignalName.Pressed);
        if (!all.Disabled || _bag.RowCount != 0 || _bag.TotalQuantity != 0 || _campaign.CarriedCount != 0
            || _campaign.Current.Items.Contents(chest.Key).Sum(i => i.Quantity) != 2 || _campaign.Current.Items.Revision != revision + 1
            || _campaign.Location != actor || _campaign.Experience != xp)
            throw new InvalidDataException("Common-origin return lost a stack or changed unrelated state.");
        if (_capture != null) await Capture(Path.ChangeExtension(_capture, "return-all-after.png"));
        byte[] empty = State(); all.EmitSignal(Button.SignalName.Pressed); oldCallback(item);
        if (!empty.SequenceEqual(State())) throw new InvalidDataException("Empty-bag callback changed returned contents.");
        CloseBag();
        if (_campaign.TakeItem(chest.Key, item.Source.Key, 1, false) != ItemTakeStatus.Ready)
            throw new InvalidDataException("Return-all stale-callback fixture could not recollect a unit.");
        byte[] single = State(); oldCallback(item); OpenBag(); oldCallback(item);
        if (!single.SequenceEqual(State())) throw new InvalidDataException("Old bag callback returned a currently carried unit.");
        Action<CarriedItem> beforeLoad = _bag!.ReturnAllRequested!; var loadItem = _bag.SelectedItem!;
        CloseBag(); CheckpointFile.Write(CheckpointPath, CheckpointFile.Decode(single)); LoadCheckpoint(); OpenBag();
        beforeLoad(loadItem);
        if (!single.SequenceEqual(State())) throw new InvalidDataException("Previous-session bag callback affected restored ownership.");
        ((Button)_bag!.FindChild("ReturnOriginAction", true, false)).EmitSignal(Button.SignalName.Pressed); CloseBag();
        if (_campaign.CarriedCount != 0 || _campaign.TakeAllItems(chest.Key, false) != ItemTakeStatus.Ready || !before.SequenceEqual(State()))
            throw new InvalidDataException("Return/recollect rehearsal did not restore its complete initial world state.");
        RefreshContainerVisuals(); MapLayoutWriter.Verify(_map!);
        GD.Print("RETURN_ALL_UI_OK: actual two-stack common-origin return, one revision, empty/repeat/closed/reopened/loaded callback guards with carried units, exact checkpoint state after recollection, actor/XP/source/used allocation preserved.");
    }
}
