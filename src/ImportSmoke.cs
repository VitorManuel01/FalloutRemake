using System;
using FalloutRemake.Import;
using Godot;

namespace FalloutRemake;

public partial class ImportSmoke : Node
{
    public override void _Ready()
    {
        try
        {
            string[] args = OS.GetCmdlineUserArgs();
            if (args.Length != 2 || args[0] != "--data-dir")
                throw new ArgumentException("Use -- --data-dir <Fallout 2 language folder>.");
            GameResource resource = new GameData(args[1]).Read("maps/artemple.map");
            MapPrefix map = MapPrefix.Read(resource.Bytes);
            GD.Print($"IMPORT_SMOKE_OK: {map.Header.Name}; map ID {map.Header.MapIndex}; {map.Layers.Count} layers; {map.UnparsedScriptsAndObjects.Length} bytes unparsed (scripts/objects).");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"IMPORT_SMOKE_FAILED: {error.Message}");
            GetTree().Quit(1);
        }
    }
}
