using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Import;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private PrototypeTextCatalog? _objectText;
    private readonly HashSet<int> _unavailableTextTypes = new();
    private void LoadObjectText(GameData data)
    {
        _objectText = null; _unavailableTextTypes.Clear();
        try { _objectText = new PrototypeTextCatalog(data.Read, data.FindPaths("pro_")); }
        catch (Exception error) when (error is IOException or InvalidDataException)
        { Godot.GD.Print("OBJECT_TEXT_UNAVAILABLE: " + error.Message); }
    }
    private PrototypeText? OriginalText(MapObject obj)
    {
        int type = obj.Header.Pid >> 24;
        if (_objectText == null || _unavailableTextTypes.Contains(type)) return null;
        try { return _objectText.Resolve(_map!.Prototypes.Single(p => p.Pid == obj.Header.Pid)); }
        catch (Exception error) when (error is IOException or InvalidDataException)
        { _unavailableTextTypes.Add(type); Godot.GD.Print("OBJECT_TEXT_UNAVAILABLE: " + error.Message); return null; }
    }
    private static string ShortName(string name) => name.Length <= 80 ? name : name[..79] + "…";
    private async Task CheckObjectText()
    {
        if (_objectText == null) throw new InvalidDataException("No unambiguous original text language found.");
        int entries = 0;
        for (int type = 0; type < 6; type++) entries += _objectText.FileForType(type).Entries.Count;
        ResetWalker(); var location = _campaign!.Location;
        var target = _layout!.Objects.Where(VisibleObject).OrderBy(o => o.Kind == Geometry.PreviewKind.Critter ? 0 : o.Kind == Geometry.PreviewKind.Door ? 1 : 2)
            .First(o => OriginalText(o.Source)?.Description is { Length: > 0 }
            && OriginalText(o.Source)?.Name is { Length: > 0 });
        PrototypeText expected = OriginalText(target.Source)!;
        SelectObject(target);
        if (!_hud.SelectionHeading.Contains(ShortName(expected.Name!)) || !_hud.SelectionDetails.Contains(expected.Description!)
            || !_hud.SelectionDetails.Contains($"PID  {target.Source.Header.Pid}"))
            throw new InvalidDataException("Original object text failed to reach the inspector with IDs.");
        int resolved = 0;
        foreach (int mapId in new[] { 3, 126, 4 })
        {
            MapDocument map = _mapCatalog!.Load(new GameData(_directory!), mapId);
            foreach (Prototype proto in map.Prototypes)
                if (_objectText.Resolve(proto)?.Name != null) resolved++;
            MapLayoutWriter.Verify(map);
        }
        var original = _objectText;
        try
        {
            _objectText = null; SelectObject(target);
            if (!_hud.SelectionHeading.Contains($"objeto {target.Source.Header.Id}") || !_hud.SelectionDetails.Contains("PID"))
                throw new InvalidDataException("Missing object text removed the identity fallback.");
        }
        finally { _objectText = original; }
        SelectObject(target);
        if (_campaign.Location != location) throw new InvalidDataException("Object text changed actor state.");
        _focus = Point(target.Position, 0); _camera.Size = 24; UpdateCamera();
        if (_capture != null) await Capture(Path.ChangeExtension(_capture, "names.png"));
        foreach (ResourceIdentity source in _objectText.Dependencies)
            Godot.GD.Print($"OBJECT_TEXT_RESOURCE: {source.Path}; {source.Size}; {source.Sha256}");
        ClearSelection();
        Godot.GD.Print($"OBJECT_TEXT_OK: discovered {_objectText.Language}; 6 MSG lists, {entries} entries, {resolved} named prototypes across 3 maps; name/description, IDs, fallback, actor and source bytes verified.");
    }
}
