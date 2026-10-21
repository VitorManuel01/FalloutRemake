using FalloutRemake.Geometry;
using FalloutRemake.Import;

internal static class NaturePresentationTests
{
    public static void Run(Action<string,Action> check)
    {
        var source = WorldTests.Map(3,4).ObjectLayers[0].Objects[0];
        var rock = source with { Subtype = 5,Inventory = Array.Empty<InventoryEntry>(),Header = source.Header with {
            Pid = 33554510,Fid = 33554605,Sid = -1,ScriptIndex = -1 } };
        check("Nature presentation requires known PID and FID and never substitutes benches village trees or interactive objects",() =>
        {
            Assert(NaturePresentation.Resolve(126,rock)?.Name == "RockS01" && NaturePresentation.Resolve(4,rock)?.Width == 1.05f);
            foreach (var obj in new[] { rock with { Header = rock.Header with { Fid = 1 } },rock with { Header = rock.Header with { Pid = 33556035 } },
                rock with { Header = rock.Header with { Pid = 33555377,Fid = 33555388 } },rock with { Header = rock.Header with { Sid = 100 } },
                rock with { Header = rock.Header with { ScriptIndex = 100 } },rock with { Subtype = 0 },
                rock with { Inventory = new[] { new InventoryEntry(0,1,source) } } }) Assert(NaturePresentation.Resolve(4,obj) == null);
            Assert(NaturePresentation.Resolve(3,rock) == null && NaturePresentation.Resolve(999,rock) == null);
        });
        check("Nature burnt trees replace only the three verified exterior prototypes without changing the native object",() =>
        {
            foreach (var pair in new[] {(33555377,33555388),(33555378,33555389),(33555379,33555390)})
            {
                var tree = rock with {Header = rock.Header with {Pid = pair.Item1,Fid = pair.Item2,Rotation = 4}};
                var model = NaturePresentation.Resolve(126,tree);
                Assert(model is {Name:"BlastedForestBurntTreeStump01",Tree:true} && tree.Header.Rotation == 4
                    && NaturePresentation.Resolve(4,tree) == null && NaturePresentation.Resolve(126,tree with {Header = tree.Header with {Fid = 0}}) == null);
            }
        });
        check("Nature mapping owns no original pose visibility or navigation state",() =>
        {
            var small = rock with { Header = rock.Header with { Pid = 33554506,Fid = 33554603,Flags = HexNavigation.Hidden,Tile = 1234,Rotation = 5 } };
            var header = small.Header; var model = NaturePresentation.Resolve(126,small);
            Assert(model?.Name == "RiverRock1" && small.Header == header && small.Header.Flags == HexNavigation.Hidden);
        });
    }
    private static void Assert(bool value) { if (!value) throw new Exception("Nature category/source-bound mapping assertion failed."); }
}
