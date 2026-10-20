using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private async Task CheckContainerLocator()
    {
        byte[] before = CheckpointFile.Encode(_campaign!.Capture(_mapCatalog!.Source.Sha256));
        var locator = (OptionButton)_hud.FindChild("ContainerLocator", true, false); int inspected = 0;
        for (int elevation = 0; elevation < 3; elevation++)
        {
            ChangeElevation(elevation);
            var containers = _layout!.Objects.Where(o => IsContainer(o) && VisibleObject(o)).OrderBy(o => o.Source.Offset).ToArray();
            if (locator.ItemCount != containers.Length + 1) throw new InvalidDataException("Locator did not list current visible containers.");
            for (int i = 0; i < containers.Length; i++)
            {
                var obj = containers[i]; locator.EmitSignal(OptionButton.SignalName.ItemSelected, i + 1);
                if (_selected?.Source.Key != obj.Source.Key || _camera.Size != 18 || _focus.DistanceTo(Point(obj.Position, 0)) > .001f)
                    throw new InvalidDataException("Locator did not select/frame the original source container.");
                if (obj.Source.Offset == 337852 && !locator.GetItemText(i + 1).Contains("Plastic Explosives")) throw new InvalidDataException("Explosive container is not identifiable by contents.");
                inspected++;
            }
            if (elevation == 1 && _capture != null)
            {
                LocateContainer(containers.Single(o => o.Source.Offset == 337852).Source.Key);
                await Capture(Path.ChangeExtension(_capture, "container-locator.png"));
            }
        }
        if (inspected != 7 || !before.SequenceEqual(CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256))))
            throw new InvalidDataException("Locator changed the world or omitted source containers.");
        string oldKey = _selected!.Source.Key;
        _hud.Filters["scenery"] = false; ApplyFilters();
        if (!locator.Disabled || locator.ItemCount != 1) throw new InvalidDataException("Hidden containers remained in locator.");
        LocateContainer(oldKey);
        if (_selected != null) throw new InvalidDataException("Locator bypassed scenery visibility.");
        _hud.Filters["scenery"] = true; ApplyFilters();
        ChangeElevation(0); ClearSelection(); OpenBag();
        locator.EmitSignal(OptionButton.SignalName.ItemSelected, 1);
        if (_selected != null) throw new InvalidDataException("Locator bypassed a modal.");
        CloseBag();
        GD.Print("CONTAINER_LOCATOR_OK: all 7 original containers across 3 elevations, native content names including explosive, source-key selection and camera framing, filter/modal guards, unchanged actor/items/doors/checkpoint/source bytes.");
    }
}
