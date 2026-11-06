// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Server._Pirate.Tiles;

/// <summary>
/// SS13-style bitmask smoothing for grid tiles. Tiles with the same <see cref="ContentTileDefinition.SmoothGroup"/>
/// get the <see cref="Tile.Variant"/> that matches their 8 neighbours, so a glass floor of any size draws as one pane.
/// </summary>
/// <remarks>
/// Carpets can smooth on the client because they are entities. Tiles are drawn by the engine grid renderer, which only
/// reads <see cref="Tile.Variant"/>, and the variant is grid data. So this runs on the server, and the result is
/// networked and saved with the map. Grids are recomputed when they start (maps saved before smoothing, or with
/// random variants) and the neighbourhood of every tile change after that.
/// </remarks>
public sealed class TileSmoothingSystem : EntitySystem
{
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;

    // tg junction bits (code/__DEFINES/icon_smoothing.dm). The sprite strips are packed in this numbering.
    private const int North = 1;
    private const int South = 2;
    private const int East = 4;
    private const int West = 8;
    private const int NorthEast = 16;
    private const int SouthEast = 32;
    private const int SouthWest = 64;
    private const int NorthWest = 128;

    /// <summary>
    /// The number of distinct states: a diagonal only counts when both cardinals next to it connect.
    /// </summary>
    public const int VariantCount = 47;

    /// <summary>
    /// Junction mask to variant index. A strip holds the 47 valid masks in ascending order.
    /// </summary>
    private static readonly byte[] MaskToVariant = BuildVariantTable();

    /// <summary>
    /// Our own <see cref="SharedMapSystem.SetTiles"/> raises <see cref="TileChangedEvent"/> again. A variant change
    /// never changes what a neighbour smooths to, so one pass is enough.
    /// </summary>
    private bool _updating;

    public override void Initialize()
    {
        base.Initialize();

        // Broadcast: only one system may hold the directed MapGridComponent/TileChangedEvent subscription.
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<GridStartupEvent>(OnGridStartup);
    }

    private static byte[] BuildVariantTable()
    {
        var table = new byte[256];
        byte index = 0;

        for (var mask = 0; mask < 256; mask++)
        {
            table[mask] = IsValid(mask) ? index++ : byte.MaxValue;
        }

        DebugTools.Assert(index == VariantCount);
        return table;
    }

    private static bool IsValid(int mask)
    {
        return Diagonal(mask, NorthEast, North, East)
            && Diagonal(mask, SouthEast, South, East)
            && Diagonal(mask, SouthWest, South, West)
            && Diagonal(mask, NorthWest, North, West);

        static bool Diagonal(int mask, int diagonal, int a, int b)
        {
            return (mask & diagonal) == 0 || (mask & a) != 0 && (mask & b) != 0;
        }
    }

    private void OnGridStartup(GridStartupEvent args)
    {
        if (TryComp<MapGridComponent>(args.EntityUid, out var grid))
            UpdateGrid((args.EntityUid, grid));
    }

    private void OnTileChanged(ref TileChangedEvent args)
    {
        if (_updating)
            return;

        // A bulk SetTiles is one event, so gather the whole neighbourhood and write it back once.
        var dirty = new HashSet<Vector2i>();

        foreach (var change in args.Changes)
        {
            for (var x = -1; x <= 1; x++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    dirty.Add(change.GridIndices + new Vector2i(x, y));
                }
            }
        }

        Apply(args.Entity, dirty);
    }

    /// <summary>
    /// Recalculates every smoothing tile on a grid.
    /// </summary>
    public void UpdateGrid(Entity<MapGridComponent> grid)
    {
        if (_updating)
            return;

        // Collect first: writing tiles while we enumerate the chunks would break the enumerator.
        var indices = new List<Vector2i>();
        var tiles = _maps.GetAllTilesEnumerator(grid.Owner, grid.Comp);

        while (tiles.MoveNext(out var tile))
        {
            if (GetGroup(tile.Value.Tile) != null)
                indices.Add(tile.Value.GridIndices);
        }

        Apply(grid, indices);
    }

    /// <summary>
    /// Recalculates the variants of the given tiles and writes the ones that changed in one batch.
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
    /// Gets the smoothed tile for a position. False when the tile does not smooth or already has the correct variant.
    /// </summary>
    private bool TryGetSmoothedTile(Entity<MapGridComponent> grid, Vector2i indices, out Tile smoothed)
    {
        smoothed = Tile.Empty;

        if (!_maps.TryGetTile(grid.Comp, indices, out var tile)
            || _tileDefs[tile.TypeId] is not ContentTileDefinition { SmoothGroup: { } group } def)
            return false;

        if (def.Variants != VariantCount)
        {
            Log.Error($"Tile {def.ID} has smoothGroup {group} but {def.Variants} variants, it needs {VariantCount}.");
            return false;
        }

        var mask = 0;

        if (Connects(grid, indices + new Vector2i(0, 1), group))
            mask |= North;
        if (Connects(grid, indices + new Vector2i(0, -1), group))
            mask |= South;
        if (Connects(grid, indices + new Vector2i(1, 0), group))
            mask |= East;
        if (Connects(grid, indices + new Vector2i(-1, 0), group))
            mask |= West;

        if ((mask & (North | East)) == (North | East) && Connects(grid, indices + new Vector2i(1, 1), group))
            mask |= NorthEast;
        if ((mask & (South | East)) == (South | East) && Connects(grid, indices + new Vector2i(1, -1), group))
            mask |= SouthEast;
        if ((mask & (South | West)) == (South | West) && Connects(grid, indices + new Vector2i(-1, -1), group))
            mask |= SouthWest;
        if ((mask & (North | West)) == (North | West) && Connects(grid, indices + new Vector2i(-1, 1), group))
            mask |= NorthWest;

        var variant = MaskToVariant[mask];

        // Rotated or mirrored strips would put the frame on the wrong edges.
        if (tile.Variant == variant && tile.RotationMirroring == 0)
            return false;

        smoothed = new Tile(tile.TypeId, tile.Flags, variant);
        return true;
    }

    private bool Connects(Entity<MapGridComponent> grid, Vector2i indices, string group)
    {
        return _maps.TryGetTile(grid.Comp, indices, out var tile) && GetGroup(tile) == group;
    }

    private string? GetGroup(Tile tile)
    {
        return tile.IsEmpty ? null : (_tileDefs[tile.TypeId] as ContentTileDefinition)?.SmoothGroup;
    }
}
