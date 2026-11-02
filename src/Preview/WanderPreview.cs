using System;
using System.Linq;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    // The creatures' wandering draws from its own stream; the checks seed it for repeatable runs.
    private ICombatRandom _wanderRandom = new SeededCombatRandom((ulong)DateTime.UtcNow.Ticks);
    // A wanderer moved on this elevation: the walker's graph is rebuilt before the next route (never mid-route).
    private bool _navigationStale;

    // Hexes the walking actor is about to enter; wanderers avoid them, so a route in progress never runs into a creature.
    private int[] ReservedForWalker() => _walker is { Moving: true } walker && _travel != null && _elevation == _travel.Location.Elevation
        ? walker.RemainingRoute.Prepend(walker.CurrentHex).ToArray() : Array.Empty<int>();

    private void ShowWanderers(string[] keys)
    {
        if (_campaign == null || _layout == null || keys.Length == 0) return;
        var roots = _campaign.Current.Map.ObjectLayers.SelectMany(l => l.Objects).ToDictionary(o => o.Key, StringComparer.Ordinal);
        bool reselect = false;
        _layout = _layout with { Objects = _layout.Objects.Select(obj =>
        {
            if (!keys.Contains(obj.Source.Key)) return obj;
            MapObject effective = _campaign.EffectiveObject(_campaign.CurrentMap, roots[obj.Source.Key]);
            if (effective.Header.Elevation == _elevation) _navigationStale = true;
            if (_bodies.TryGetValue(obj.Source.Key, out StaticBody3D? body))
                body.Position = Point(FalloutGrid.HexCenter(effective.Header.Tile) - _layout.Origin, Dimensions(PreviewKind.Critter).Y / 2);
            reselect |= _selected?.Source.Key == obj.Source.Key;
            return obj with { Source = effective, Position = FalloutGrid.HexCenter(effective.Header.Tile) - _layout.Origin };
        }).ToArray() };
        if (reselect) SelectObject(_layout.Objects.Single(o => o.Source.Key == _selected!.Source.Key));
        // A creature that walked into view may now see a standing actor: look again on the next frame.
        _encounterCheckedHex = -1;
    }

    private void RebuildWalkerNavigation()
    {
        if (!_navigationStale || _doors == null || _walker == null || _walker.Moving) return;
        _navigationStale = false;
        int current = _walker.CurrentHex;
        _navigation = _doors.Navigation(_elevation); _walker = new HexWalker(_navigation, current);
        RefreshReachability(); UpdateWalkerPosition();
    }
}
