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
    private async Task CheckTakeAllChest(MapObject chest)
    {
        SelectObject(_layout!.Objects.Single(o => o.Source.Key == chest.Key)); OpenLoot();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var all = (Button)_loot!.FindChild("TakeAllAction", true, false);
        Action callback = _loot.TakeAllRequested!;
        var actor = _campaign!.Location; int xp = _campaign.Experience; long revision = _campaign.Current.Items.Revision;
        if (all.Disabled || _loot.Remaining != 2 || _campaign.CarriedCount != 0)
            throw new InvalidDataException("Two-source chest did not offer take all.");
        if (_capture != null) await Capture(Path.ChangeExtension(_capture, "take-all-before.png"));
        all.EmitSignal(Button.SignalName.Pressed);
        if (!all.Disabled || _loot.Remaining != 0 || _campaign.CarriedCount != 2 || _campaign.CarriedWeight != 2
            || _campaign.Current.Items.Revision != revision + 1 || _campaign.Location != actor || _campaign.Experience != xp
            || !_campaign.Carried.Select(i => (i.Source.Header.Id, i.Source.Header.Pid)).ToHashSet().SetEquals(new[] { (126, 273), (100, 49) }))
            throw new InvalidDataException("Take-all UI lost source identities or made a partial transfer.");
        if (_capture != null) await Capture(Path.ChangeExtension(_capture, "take-all-after.png"));
        revision = _campaign.Current.Items.Revision;
        all.EmitSignal(Button.SignalName.Pressed); callback();
        if (_campaign.Current.Items.Revision != revision) throw new InvalidDataException("Repeated take all duplicated inventory.");
        CloseLoot();
        var returned = _campaign.Carried.First();
        if (_campaign.ReturnItem(returned.Map, returned.OwnerKey, returned.Source.Key, 1, false) != ItemTakeStatus.Ready)
            throw new InvalidDataException("Late-callback fixture could not restore a collectable unit.");
        revision = _campaign.Current.Items.Revision;
        callback(); OpenLoot(); callback();
        if (_campaign.Current.Items.Revision != revision || _campaign.CarriedCount != 1 || _loot!.Remaining != 1)
            throw new InvalidDataException("Closed-dialog callback affected collectable contents in a later dialog.");
        var currentAll = (Button)_loot.FindChild("TakeAllAction", true, false);
        if (currentAll.Disabled) throw new InvalidDataException("Reopened dialog did not offer the returned unit.");
        currentAll.EmitSignal(Button.SignalName.Pressed);
        if (_campaign.CarriedCount != 2 || _loot.Remaining != 0) throw new InvalidDataException("Current dialog failed after rejecting a stale callback.");
        CloseLoot(); MapLayoutWriter.Verify(_map!);
        GD.Print("TAKE_ALL_UI_OK: actual two-source upper chest, one revision, exact source identities/weight, empty/repeat/closed/reopened callback guards, unchanged actor/XP/MAP; checkpoint and return checked by applied continuation.");
    }
}
