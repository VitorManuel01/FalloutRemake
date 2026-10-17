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
            var data = new GameData(args[1]);
            MapDocument map = MapDocument.Read(data.Read("maps/arcaves.map"), data.Read);
            MapLayoutWriter.Verify(map);
            GD.Print($"IMPORT_SMOKE_OK: {map.Prefix.Header.Name}; map ID {map.Prefix.Header.MapIndex}; {map.DeclaredRootObjects} root objects; {map.ConsumedBytes} bytes parsed; exact reconstruction verified.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"IMPORT_SMOKE_FAILED: {error.Message}");
            GetTree().Quit(1);
        }
    }
}
