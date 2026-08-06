// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Tiles;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Pirate.Tiles;

/// <summary>
/// Autotiling for grid tiles: picks <see cref="Tile.Variant"/> from the tiles around it so tiles of the
/// same <see cref="ContentTileDefinition.SmoothGroup"/> draw connected edges, the way
/// <c>IconSmoothSystem</c> does it for walls.
/// </summary>
/// <remarks>
/// The variant lives in the grid's tile data, so this only runs on the server and the result is saved
/// into map files. Sprite strips are indexed by the cardinal neighbour mask, see <see cref="TileSmoothMode"/>.
/// </remarks>
public sealed class TileSmoothingSystem : EntitySystem
{
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;

    /// <summary>
    /// Order matters, the index into this is the bit in the variant mask. Kept the same as
    /// <c>IconSmoothSystem</c>'s CardinalConnectDirs so tile strips are numbered like smoothed wall states.
    /// </summary>
    private static readonly Direction[] Cardinals =
    {
        Direction.North,
        Direction.South,
        Direction.East,
        Direction.West,
    };

    /// <summary>
    /// Order matters, the index into this is the inner corner variant after the cardinal states.
    /// </summary>
    private static readonly Direction[] Diagonals =
    {
        Direction.NorthEast,
        Direction.NorthWest,
        Direction.SouthEast,
        Direction.SouthWest,
    };

    /// <summary>
    /// How many variants the cardinal neighbour mask uses on its own.
    /// </summary>
    private const int CardinalStates = 16;

    /// <summary>
    /// Our own <see cref="SharedMapSystem.SetTiles"/> call raises <see cref="TileChangedEvent"/> again, and a
    /// variant change never changes what any neighbour smooths to, so one pass is enough.
    /// </summary>
    private bool _updating;

    public override void Initialize()
    {
        base.Initialize();

        // Broadcast, not directed: the engine only allows one directed MapGridComponent/TileChangedEvent
        // subscription across all systems, and SharedCrawlUnderFloorSystem already holds it.
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
    }

    private void OnTileChanged(ref TileChangedEvent args)
    {
        if (_updating)
            return;

        var grid = args.Entity;

        // A bulk SetTiles arrives as one event, so gather the whole neighbourhood and write it back once.
        var dirty = new HashSet<Vector2i>();

        foreach (var change in args.Changes)
        {
            dirty.Add(change.GridIndices);

            foreach (var dir in Cardinals)
            {
                dirty.Add(change.GridIndices.Offset(dir));
            }
        }

        Apply(grid, dirty);
    }

    /// <summary>
    /// Recalculates the variants of the given tiles and writes the ones that changed in a single batch.
    /// </summary>
    private void Apply(Entity<MapGridComponent> grid, IEnumerable<Vector2i> indices)
    {
        var tiles = new List<(Vector2i, Tile)>();

        foreach (var index in indices)
        {
            if (TryGetSmoothedTile(grid, index, out var tile))
                tiles.Add((index, tile));
        }

        if (tiles.Count == 0)
            return;

        _updating = true;

        try
        {
            _maps.SetTiles(grid.Owner, grid.Comp, tiles);
        }
        finally
        {
            _updating = false;
        }
    }

    /// <summary>
    /// Works out the smoothed tile for a position. False when the tile doesn't smooth or already has the
    /// right variant.
    /// </summary>
    private bool TryGetSmoothedTile(Entity<MapGridComponent> grid, Vector2i indices, out Tile smoothed)
    {
        smoothed = Tile.Empty;

        if (!_maps.TryGetTile(grid.Comp, indices, out var tile))
            return false;

        if (_tileDefs[tile.TypeId] is not ContentTileDefinition def
            || def.SmoothGroup == null
            || def.SmoothMode == TileSmoothMode.None)
            return false;

        var mask = 0;

        for (var i = 0; i < Cardinals.Length; i++)
        {
            // The neighbour smooths with us through the edge of theirs that faces us.
            if (Connects(grid, indices.Offset(Cardinals[i]), def.SmoothGroup, Cardinals[i].GetOpposite()))
                mask |= 1 << i;
        }

        var needed = def.SmoothMode == TileSmoothMode.CardinalCorners ? CardinalStates + Diagonals.Length : CardinalStates;

        if (def.Variants < needed)
        {
            Log.Error($"Tile {def.ID} smooths with mode {def.SmoothMode} but only has {def.Variants} variants, needs {needed}.");
            return false;
        }

        var variant = (byte) mask;

        // Fully surrounded but missing a diagonal neighbour: draw the lip around that inner corner. Only the
        // single-corner case has art, anything busier keeps the flat fully-surrounded tile.
        if (def.SmoothMode == TileSmoothMode.CardinalCorners && mask == (1 << Cardinals.Length) - 1)
        {
            var corner = -1;

            for (var i = 0; i < Diagonals.Length; i++)
            {
                if (ConnectsDiagonally(grid, indices.Offset(Diagonals[i]), def.SmoothGroup, Diagonals[i]))
                    continue;

                if (corner >= 0)
                {
                    corner = -1;
                    break;
                }

                corner = i;
            }

            if (corner >= 0)
                variant = (byte) (CardinalStates + corner);
        }

        if (tile.Variant == variant)
            return false;

        smoothed = new Tile(tile.TypeId, tile.Flags, variant, tile.RotationMirroring);
        return true;
    }

    /// <summary>
    /// Whether the tile diagonally at <paramref name="indices"/> belongs to <paramref name="group"/> and covers
    /// the corner it shares with the tile that is asking, which sits in the <paramref name="diagonal"/>
    /// direction from it.
    /// </summary>
    /// <remarks>
    /// A half tile leaves exactly one corner uncovered, the one opposite the half it fills, so it fails to cover
    /// the shared corner only when the half it fills faces the same way we looked.
    /// </remarks>
    private bool ConnectsDiagonally(Entity<MapGridComponent> grid, Vector2i indices, string group, Direction diagonal)
    {
        if (!_maps.TryGetTile(grid.Comp, indices, out var tile))
            return false;

        if (_tileDefs[tile.TypeId] is not ContentTileDefinition def || def.SmoothGroup != group)
            return false;

        if (def.SmoothSides.Count == 0)
            return true;

        var (a, b) = CornerCardinals(diagonal);
        return !(def.SmoothSides.Contains(a) && def.SmoothSides.Contains(b));
    }

    /// <summary>
    /// The two cardinals a diagonal direction sits between.
    /// </summary>
    private static (Direction, Direction) CornerCardinals(Direction diagonal)
    {
        return diagonal switch
        {
            Direction.NorthEast => (Direction.North, Direction.East),
            Direction.NorthWest => (Direction.North, Direction.West),
            Direction.SouthEast => (Direction.South, Direction.East),
            _ => (Direction.South, Direction.West),
        };
    }

    /// <summary>
    /// Whether the tile at <paramref name="indices"/> belongs to <paramref name="group"/> and covers its
    /// <paramref name="side"/> edge, i.e. the edge pointing back at the tile that is asking.
    /// </summary>
    private bool Connects(Entity<MapGridComponent> grid, Vector2i indices, string group, Direction side)
    {
        if (!_maps.TryGetTile(grid.Comp, indices, out var tile))
            return false;

        if (_tileDefs[tile.TypeId] is not ContentTileDefinition def || def.SmoothGroup != group)
            return false;

        return def.SmoothSides.Count == 0 || def.SmoothSides.Contains(side);
    }

    /// <summary>
    /// Recalculates every smoothing tile on a grid. For maps saved before their tiles could smooth.
    /// </summary>
    public void UpdateGrid(Entity<MapGridComponent> grid)
    {
        if (_updating)
            return;

        // Collect first, writing tiles while enumerating the grid's chunks would invalidate the enumerator.
        var indices = new List<Vector2i>();
        var tiles = _maps.GetAllTilesEnumerator(grid.Owner, grid.Comp);

        while (tiles.MoveNext(out var tile))
        {
            indices.Add(tile.Value.GridIndices);
        }

        Apply(grid, indices);
    }
}
