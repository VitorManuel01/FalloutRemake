using FalloutRemake.Import;

namespace FalloutRemake.Geometry;

public enum EntityVisualRole { Original, Barrier, LightSource, Container }

/// <summary>Presentation only: does not change import kinds, traversal flags or interaction eligibility.</summary>
public static class EntityPresentation
{
    public static EntityVisualRole Role(MapObject obj)
    {
        PreviewKind kind = PreviewLayout.Kind(obj);
        if (kind == PreviewKind.Item && obj.Subtype == 1) return EntityVisualRole.Container;
        // A glowing creature or door retains its own silhouette and behavior.
        if (kind is not (PreviewKind.Scenery or PreviewKind.Marker or PreviewKind.Item)) return EntityVisualRole.Original;
        if (obj.Header.LightDistance > 0 && obj.Header.LightIntensity > 0) return EntityVisualRole.LightSource;
        int fidType = (obj.Header.Fid >> 24) & 15;
        return kind == PreviewKind.Scenery && fidType is >= 1 and <= 3 && (obj.Header.Flags & (HexNavigation.Hidden | HexNavigation.NoBlock)) == 0
            ? EntityVisualRole.Barrier : EntityVisualRole.Original;
    }
}
