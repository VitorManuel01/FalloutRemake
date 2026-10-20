using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Geometry;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private async Task CheckBarrierPlacementGuidance()
    {
        byte[] State() => CheckpointFile.Encode(_campaign!.Capture(_mapCatalog!.Source.Sha256));
        var initial = _campaign!.Capture(_mapCatalog!.Source.Sha256); byte[] before = State();
        var item = _campaign.Carried.Single(); var actor = _campaign.Location;
        int target = BarrierPlacementApproach(item) ?? throw new InvalidDataException("Collected explosive has no valid barrier approach.");
        int[] expectedRoute = _navigation!.FindRoute(_walker!.CurrentHex, target).Tiles;
        if (RehearsalPlacementInRange()) throw new InvalidDataException("Guidance fixture is already in range.");
        OpenBag();
        var prepare = (Button)_bag!.FindChild("PrepareExplosiveAction", true, false);
        var approach = (Button)_bag.FindChild("BarrierPlacementApproach", true, false);
        if (!prepare.Disabled || !prepare.TooltipText.Contains("Ir à barreira") || approach.Disabled) throw new InvalidDataException("Far placement lacks an actionable range hint.");
        OpenExplosivePreparation(item);
        if (_explosivePreparation != null || !State().SequenceEqual(before)) throw new InvalidDataException("Out-of-range preparation changed ownership.");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _bag.GetNode<ScrollContainer>("DialogScroll").EnsureControlVisible(approach);
        if (_capture != null) await Capture(Path.ChangeExtension(_capture, "barrier-placement-bag.png"));
        approach.EmitSignal(Button.SignalName.Pressed);
        if (_bag != null || !_walker.Moving || _campaign.Location != actor || !_committedRoute.SequenceEqual(expectedRoute)
            || _selected?.Source.Offset != 303324 || !State().SequenceEqual(before)) throw new InvalidDataException("Barrier approach teleported, transferred or changed the expected route.");
        if (_capture != null) await Capture(Path.ChangeExtension(_capture, "barrier-placement-route.png"));
        CompleteReadabilityWalk();
        if (_walker.CurrentHex != target || !RehearsalPlacementInRange() || FalloutGrid.Distance(target, 14322) > 3)
            throw new InvalidDataException("Approach did not arrive inside the verified scenery radius.");
        var expected = initial with { Areas = initial.Areas.Select(a => a.Map == actor.Map ? a with { Location = _campaign.Location } : a).ToArray() };
        if (!State().SequenceEqual(CheckpointFile.Encode(expected))) throw new InvalidDataException("Barrier approach changed more than actor location.");
        OpenBag(); prepare = (Button)_bag!.FindChild("PrepareExplosiveAction", true, false);
        if (prepare.Disabled || !((Button)_bag.FindChild("BarrierPlacementApproach", true, false)).Disabled)
            throw new InvalidDataException("Arrival did not enable preparation or clear the approach action.");
        CloseBag();
        GD.Print("BARRIER_PLACEMENT_GUIDANCE_OK: actual collected explosive, out-of-range placement refusal, source-bound barrier hint, exact reachable route from bag, no teleport/use/XP/door change, arrival within radius 3 enables preparation, original checkpoint differs only by actor location.");
    }
}
