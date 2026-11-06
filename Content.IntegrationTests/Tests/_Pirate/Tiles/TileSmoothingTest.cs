// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server._Pirate.Tiles;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Pirate.Tiles;

[TestFixture]
[TestOf(typeof(TileSmoothingSystem))]
public sealed class TileSmoothingTest
{
    // tg junction bits, the order the glass strips are packed in.
    private const int N = 1, S = 2, E = 4, W = 8, NE = 16, SE = 32, SW = 64, NW = 128;

    private static readonly int[] ValidMasks = Enumerable.Range(0, 256)
        .Where(m => Ok(m, NE, N, E) && Ok(m, SE, S, E) && Ok(m, SW, S, W) && Ok(m, NW, N, W))
        .ToArray();

    private static bool Ok(int mask, int diagonal, int a, int b)
    {
        return (mask & diagonal) == 0 || (mask & a) != 0 && (mask & b) != 0;
    }

    private static byte Variant(int mask)
    {
        return (byte) Array.IndexOf(ValidMasks, mask);
    }

    [Test]
    public async Task SmoothGroupTilesHaveAllVariants()
    {
        await using var pair = await PoolManager.GetServerClient();
        var tileDefs = pair.Server.ResolveDependency<ITileDefinitionManager>();

        Assert.That(ValidMasks, Has.Length.EqualTo(TileSmoothingSystem.VariantCount));

        Assert.Multiple(() =>
        {
            foreach (var def in tileDefs.OfType<ContentTileDefinition>().Where(d => d.SmoothGroup != null))
            {
                Assert.That(def.Variants, Is.EqualTo(TileSmoothingSystem.VariantCount), def.ID);
                Assert.That(def.AllowRotationMirror, Is.False, def.ID);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GlassFloorsSmoothAsOnePane()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var maps = server.System<SharedMapSystem>();
        var tileDefs = server.ResolveDependency<ITileDefinitionManager>();
        var testMap = await pair.CreateTestMap();
        var grid = testMap.Grid;

        await server.WaitAssertion(() =>
        {
            var glass = tileDefs["FloorGlassSolid"].TileId;
            var plasma = tileDefs["FloorPlasmaGlassReinforced"].TileId;
            var oldGlass = tileDefs["FloorGlass"].TileId;

            // 3x3 block, the middle one reinforced plasma glass: all new glass floors share one group.
            for (var x = 0; x < 3; x++)
            {
                for (var y = 0; y < 3; y++)
                {
                    maps.SetTile(grid, new Vector2i(x, y), new Tile(x == 1 && y == 1 ? plasma : glass, variant: 3));
                }
            }

            // A lone tile two steps away, and an old (non-smoothing) glass floor next to the block that must not connect.
            maps.SetTile(grid, new Vector2i(6, 6), new Tile(glass, variant: 2));
            maps.SetTile(grid, new Vector2i(3, 1), new Tile(oldGlass));

            byte At(int x, int y) => maps.GetTileRef(grid, new Vector2i(x, y)).Tile.Variant;

            Assert.Multiple(() =>
            {
                Assert.That(At(1, 1), Is.EqualTo(Variant(255)), "middle");
                Assert.That(At(0, 0), Is.EqualTo(Variant(N | E | NE)), "south-west corner");
                Assert.That(At(2, 2), Is.EqualTo(Variant(S | W | SW)), "north-east corner");
                Assert.That(At(1, 0), Is.EqualTo(Variant(N | E | W | NE | NW)), "south edge");
                Assert.That(At(2, 1), Is.EqualTo(Variant(N | S | W | NW | SW)), "east edge, old glass on the east");
                Assert.That(At(6, 6), Is.EqualTo(Variant(0)), "lone tile");
            });

            // Removing the middle tile opens a hole: every neighbour loses its connection to it.
            maps.SetTile(grid, new Vector2i(1, 1), Tile.Empty);

            Assert.Multiple(() =>
            {
                Assert.That(At(1, 0), Is.EqualTo(Variant(E | W)), "south edge after hole");
                Assert.That(At(0, 0), Is.EqualTo(Variant(N | E)), "corner after hole");
            });
        });

        await pair.CleanReturnAsync();
    }
}
