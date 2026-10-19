using FalloutRemake.Geometry;

internal static class EntityPresentationTests
{
    public static void Run(Action<string, Action> check)
    {
        check("presentation recognizes containers even when empty without changing their import kind", () =>
        {
            var owner = ItemTransferTests.Map().ObjectLayers[0].Objects[0];
            Assert(EntityPresentation.Role(owner) == EntityVisualRole.Container && PreviewLayout.Kind(owner) == PreviewKind.Item);
            Assert(EntityPresentation.Role(owner with { Inventory = Array.Empty<FalloutRemake.Import.InventoryEntry>(), InventoryCount = 0 }) == EntityVisualRole.Container);
        });
        check("only actual emitting lights get lamp presentation and creature door silhouettes retain priority", () =>
        {
            var obj = BarrierEffectTests.Fixture().ObjectLayers[1].Objects[0];
            var lit = obj with { Header = obj.Header with { LightDistance = 3, LightIntensity = 65536 } };
            Assert(EntityPresentation.Role(lit) == EntityVisualRole.LightSource);
            Assert(EntityPresentation.Role(lit with { Header = lit.Header with { LightIntensity = 0 } }) == EntityVisualRole.Barrier);
            Assert(EntityPresentation.Role(lit with { Subtype = 0 }) == EntityVisualRole.Original);
            Assert(EntityPresentation.Role(lit with { Header = lit.Header with { Pid = 0x01000001, Fid = 0x01000001 } }) == EntityVisualRole.Original);
        });
        check("obstacle presentation follows original navigation blocking flags rather than the scenery name", () =>
        {
            var obj = BarrierEffectTests.Fixture().ObjectLayers[1].Objects[0];
            Assert(EntityPresentation.Role(obj) == EntityVisualRole.Barrier);
            foreach (int flag in new[] { HexNavigation.NoBlock, HexNavigation.Hidden }) Assert(EntityPresentation.Role(obj with { Header = obj.Header with { Flags = flag } }) == EntityVisualRole.Original);
            Assert(EntityPresentation.Role(obj with { Header = obj.Header with { Fid = 0x05000001 } }) == EntityVisualRole.Original);
        });
    }
    private static void Assert(bool condition) { if (!condition) throw new Exception("Entity presentation assertion failed."); }
}
