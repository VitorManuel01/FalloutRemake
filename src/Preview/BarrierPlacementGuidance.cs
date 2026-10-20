using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private Guid _placementProfileSession;
    private Prototype? _placementReplacement;
    private MapObject? RehearsalBarrier()
    {
        if (_campaign == null || _directory == null || _campaign.CurrentMap != 3) return null;
        try
        {
            if (_placementProfileSession != _campaign.SessionToken)
            {
                _placementReplacement = new PrototypeCatalog(new ResourceCatalog(new GameData(_directory).Read)).Get(TempleBarrierEffectProposal.ReplacementPid);
                _placementProfileSession = _campaign.SessionToken;
            }
            var barrier = TempleBarrierEffectProposal.Match(_campaign.Current.Map, _placementReplacement);
            return barrier != null && !_campaign.Current.Scenery.Replaces(barrier.Key) ? barrier : null;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException) { return null; }
    }
    private bool RehearsalPlacementInRange() => RehearsalBarrier() is MapObject barrier && _campaign!.Location.Elevation == barrier.Header.Elevation
        && FalloutGrid.Distance(_campaign.Location.Hex, barrier.Header.Tile) is >= 1 and <= ExplosiveUseProposal.SceneryRadius;
    private string PreparationHint(ExplosiveProposalStatus status) => status == ExplosiveProposalStatus.Unavailable && _campaign != null && _elevation == _campaign.Location.Elevation
        ? RehearsalBarrier() is MapObject barrier && _elevation == barrier.Header.Elevation
            ? "Aproxime-se da barreira do Templo antes de posicionar. Use Ir à barreira; o alcance é de até 3 hexes."
            : "Este ensaio só resolve a barreira ainda fechada na elevação 1 do Templo."
        : ExplosivePreparationWindow.Hint(status);

    private int? BarrierPlacementApproach(CarriedItem item)
    {
        if (_campaign == null || _navigation == null || _reachable == null || _walker?.Moving != false || _elevation != _campaign.Location.Elevation
            || RehearsalBarrier() is not MapObject barrier || _elevation != barrier.Header.Elevation
            || ExplosiveUseProposal.Create(_campaign, item, 60, SkillRollOutcome.Success, false).Status != ExplosiveProposalStatus.Ready) return null;
        var radius = new HashSet<int> { barrier.Header.Tile };
        var edge = new[] { barrier.Header.Tile };
        for (int distance = 0; distance < ExplosiveUseProposal.SceneryRadius; distance++)
        {
            edge = edge.SelectMany(t => Enumerable.Range(0, 6).Select(d => FalloutGrid.Neighbor(t, d)))
                .Where(t => t.HasValue && radius.Add(t.Value)).Select(t => t!.Value).ToArray();
        }
        int? target = radius.Where(t => _reachable.Distance(t) >= 0 && !_navigation.IsExit(t))
            .OrderBy(t => _reachable.Distance(t)).ThenBy(t => FalloutGrid.Distance(t, barrier.Header.Tile)).ThenBy(t => t).Cast<int?>().FirstOrDefault();
        return target == _walker.CurrentHex ? null : target;
    }
    private void ApproachBarrierForPlacement(CarriedItem item)
    {
        int? target = BarrierPlacementApproach(item);
        if (!target.HasValue) { RefreshBag(); return; }
        MapObject barrier = RehearsalBarrier()!;
        CloseBag(); if (!_walkMode) ToggleWalkMode();
        SelectObject(_layout!.Objects.Single(o => o.Source.Key == barrier.Key));
        RequestWalk(target.Value); FrameRoute();
        NavigationStatus("Caminhando até o alcance da barreira · abra B ao chegar para posicionar o explosivo");
    }
}
