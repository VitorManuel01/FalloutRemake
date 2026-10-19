using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private string MapDisplayName(int id)
    {
        try
        {
            string name = _mapCatalog!.Get(id).Name;
            return name.Length > 40 ? name[..39] + "…" : name;
        }
        catch (InvalidDataException) { return $"Mapa {id}"; }
    }
    private string DestinationName(ActorLocation destination) => $"{MapDisplayName(destination.Map)} · elevação {destination.Elevation}";
    private string ExitRouteHint(int target, RouteStatus route, int steps)
    {
        ExitOption option = _campaign!.PreviewExitAt(_elevation, target);
        string destination = option.Destination == null ? "destino inválido" : DestinationName(option.Destination);
        string access = route == RouteStatus.Unreachable ? "sem caminho até a saída" : route == RouteStatus.Blocked ? "acesso bloqueado"
            : route == RouteStatus.AlreadyThere ? "neste hex" : $"{steps} passos";
        string availability = option.Status switch
        {
            ExitStatus.Ready => "destino validado",
            ExitStatus.ExternalMap => "validar ao chegar",
            ExitStatus.UnavailableMap => "área indisponível",
            ExitStatus.ReloadRequired => "recarga da área ainda indisponível",
            ExitStatus.BlockedDestination => "destino bloqueado",
            ExitStatus.Ambiguous => "destinos conflitantes",
            _ => "dados de saída indisponíveis"
        };
        return $"Saída → {destination} · {access} · {availability}";
    }
    private async Task CheckExitRouteHover()
    {
        _checkingNavigation = true;
        WorldSession original = _campaign!;
        try
        {
            int loads = 0; var data = new GameData(_directory!);
            _campaign = new WorldSession(id => { loads++; return _mapCatalog!.Load(data, id); }, new[] { 3, 4, 126 });
            BindCurrentArea(); ChangeElevation(0); ToggleWalkMode(); SetNavigationGrid(false);
            byte[] before = CheckpointFile.Encode(_campaign.Capture(_mapCatalog!.Source.Sha256));
            PreviewGroundRoute(31306);
            if (loads != 1 || _campaign.Areas.Count != 1 || !_hud.RouteHintText!.Contains(MapDisplayName(126))
                || !_hud.RouteHintText.Contains("validar ao chegar") || _hoverPath.Length != 13)
                throw new InvalidDataException("Exit hover loaded a map or failed to identify an unvalidated external destination.");
            _focus = (Point(FalloutGrid.HexCenter(31306) - _layout!.Origin, 0) + _walkerNode!.Position) / 2;
            _camera.Size = 32; UpdateCamera();
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "exit-preview.png"));
            PreviewGroundRoute(10708);
            if (!_hud.RouteHintText!.Contains(MapDisplayName(3)) || !_hud.RouteHintText.Contains("sem caminho")
                || !_hud.RouteHintText.Contains("destino validado"))
                throw new InvalidDataException("Internal exit hover lost destination or mixed path reachability with destination validity.");
            if (!before.SequenceEqual(CheckpointFile.Encode(_campaign.Capture(_mapCatalog.Source.Sha256))) || loads != 1)
                throw new InvalidDataException("Remote exit preview changed the checkpoint world/actor/doors/area set.");
            // Read an actual unsupported exit in the village without entering its gameplay or loading its target.
            int villageLoads = 0;
            var village = new WorldSession(id => { villageLoads++; return _mapCatalog.Load(data, id); }, new[] { 3, 4, 126 }, 4);
            MapObject unsupported = village.Current.Map.AllObjects.First(o => o.Header.Pid is >= 0x05000010 and <= 0x05000017
                && village.PreviewExitAt(o.Header.Elevation, o.Header.Tile).Status == ExitStatus.UnavailableMap);
            if (village.PreviewExitAt(unsupported.Header.Elevation, unsupported.Header.Tile).Destination == null || villageLoads != 1 || village.Areas.Count != 1)
                throw new InvalidDataException("Unsupported actual village exit loaded a destination or lost its identity.");
            MapLayoutWriter.Verify(_map!); MapLayoutWriter.Verify(village.Current.Map);
            GD.Print("EXIT_ROUTE_PREVIEW_OK: destination names/elevations, remote internal/external/unsupported exits, no target loads, unchanged checkpoint area set/actor/doors, route vs destination status and source bytes verified.");
        }
        finally
        {
            _campaign = original; BindCurrentArea(); SetNavigationGrid(true);
            ChangeElevation(_campaign.Location.Elevation); FocusEntry(); _checkingNavigation = false;
        }
    }
}
