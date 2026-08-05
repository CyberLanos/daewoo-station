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
    /// Our own <see cref="SharedMapSystem.SetTile"/> calls raise <see cref="TileChangedEvent"/> again,
    /// and a variant change never changes what any neighbour smooths to, so one pass is enough.
    /// </summary>
    private bool _updating;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MapGridComponent, TileChangedEvent>(OnTileChanged);
    }

    private void OnTileChanged(Entity<MapGridComponent> grid, ref TileChangedEvent args)
    {
        if (_updating)
            return;

        _updating = true;

        try
        {
            foreach (var change in args.Changes)
            {
                UpdateTile(grid, change.GridIndices);

                foreach (var dir in Cardinals)
                {
                    UpdateTile(grid, change.GridIndices.Offset(dir));
                }
            }
        }
        finally
        {
            _updating = false;
        }
    }

    /// <summary>
    /// Recalculates the variant of a single tile. Does nothing for tiles that don't smooth.
    /// </summary>
    public void UpdateTile(Entity<MapGridComponent> grid, Vector2i indices)
    {
        if (!_maps.TryGetTile(grid.Comp, indices, out var tile))
            return;

        if (_tileDefs[tile.TypeId] is not ContentTileDefinition def
            || def.SmoothGroup == null
            || def.SmoothMode == TileSmoothMode.None)
            return;

        var mask = 0;

        for (var i = 0; i < Cardinals.Length; i++)
        {
            // The neighbour smooths with us through the edge of theirs that faces us.
            if (Connects(grid, indices.Offset(Cardinals[i]), def.SmoothGroup, Cardinals[i].GetOpposite()))
                mask |= 1 << i;
        }

        if (mask >= def.Variants)
        {
            Log.Error($"Tile {def.ID} smooths with mode {def.SmoothMode} but only has {def.Variants} variants, needs 16.");
            return;
        }

        var variant = (byte) mask;

        if (tile.Variant == variant)
            return;

        _maps.SetTile(grid.Owner, grid.Comp, indices, new Tile(tile.TypeId, tile.Flags, variant, tile.RotationMirroring));
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

        // Collect first, SetTile while enumerating the grid's chunks would invalidate the enumerator.
        var indices = new List<Vector2i>();
        var tiles = _maps.GetAllTilesEnumerator(grid.Owner, grid.Comp);

        while (tiles.MoveNext(out var tile))
        {
            indices.Add(tile.Value.GridIndices);
        }

        _updating = true;

        try
        {
            foreach (var index in indices)
            {
                UpdateTile(grid, index);
            }
        }
        finally
        {
            _updating = false;
        }
    }
}
