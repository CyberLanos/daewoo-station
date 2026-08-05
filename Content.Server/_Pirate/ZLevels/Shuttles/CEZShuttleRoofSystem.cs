using System.Linq;
using Content.Server._Pirate.ZLevels.Core;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared._Pirate.ZLevels.Roof;
using Content.Shared._Pirate.ZLevels.Shuttles.Components;
using Content.Shared.Station.Components;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.ZLevels.Shuttles;

/// <summary>
/// Spawns a subfloor-silhouette roof grid above a shuttle's topmost layer when a z-level above exists,
/// linked as a peer so it follows the shuttle. Cleaned up on FTL departure or when no level above exists.
/// </summary>
public sealed class CEZShuttleRoofSystem : EntitySystem
{
    [Dependency] private readonly CEZLevelsSystem _zLevels = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefMan = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;

    private const string FallbackPlatingTileId = "Plating";

    // Roof grid creation reparents mid-build.
    private bool _rebuilding;

    /// <summary>
    /// When true, the parent-change roof rebuild is skipped. Set by z-traversal while it relocates the
    /// decks: each deck move fires <see cref="OnShuttleParentChanged"/>, and rebuilding the roof
    /// per-deck mid-move churns grids at intermediate positions. The mover rebuilds it once afterwards.
    /// </summary>
    internal bool SuppressAutoUpdates;

    public override void Initialize()
    {
        base.Initialize();

        // Broadcast subs; ShuttleSystem already owns the per-ShuttleComponent FTL subscription.
        SubscribeLocalEvent<FTLCompletedEvent>(OnFTLCompleted);
        SubscribeLocalEvent<FTLStartedEvent>(OnFTLStarted);
        SubscribeLocalEvent<ShuttleComponent, MapInitEvent>(OnShuttleMapInit);
        SubscribeLocalEvent<ShuttleComponent, EntityTerminatingEvent>(OnShuttleTerminating);
        // Dock/proximity moves may skip FTLCompletedEvent.
        SubscribeLocalEvent<ShuttleComponent, EntParentChangedMessage>(OnShuttleParentChanged);
        SubscribeLocalEvent<CEZShuttleRoofSourceComponent, TileChangedEvent>(OnSourceTileChanged);
    }

    /// <summary>
    /// Rebuilds roofs for station shuttles after the z-network exists.
    /// </summary>
    public void RebuildStationRoofs(EntityUid station)
    {
        if (!TryComp<StationDataComponent>(station, out var data))
            return;

        foreach (var grid in data.Grids)
        {
            if (HasComp<ShuttleComponent>(grid) && !HasComp<CEZShuttleRoofComponent>(grid))
                EnsureRoof(grid);
        }
    }

    private void OnShuttleParentChanged(Entity<ShuttleComponent> ent, ref EntParentChangedMessage args)
    {
        if (SuppressAutoUpdates)
            return;

        // Roof grids do not get roofs.
        if (HasComp<CEZShuttleRoofComponent>(ent))
            return;

        // Wait until the shuttle is back on a map.
        if (!HasComp<MapComponent>(args.Transform.ParentUid))
            return;

        EnsureRoof(ent);
    }

    private void OnSourceTileChanged(Entity<CEZShuttleRoofSourceComponent> ent, ref TileChangedEvent args)
    {
        if (Exists(ent.Comp.Shuttle))
            EnsureRoof(ent.Comp.Shuttle);
    }

    private void OnFTLCompleted(ref FTLCompletedEvent args)
    {
        if (!HasComp<ShuttleComponent>(args.Entity))
            return;
        EnsureRoof(args.Entity);
    }

    private void OnFTLStarted(ref FTLStartedEvent args)
    {
        if (!HasComp<ShuttleComponent>(args.Entity))
            return;
        // Hyperspace puts each peer on its own FTL map; rebuild at destination.
        RemoveRoof(args.Entity);
    }

    private void OnShuttleMapInit(Entity<ShuttleComponent> ent, ref MapInitEvent args)
    {
        EnsureRoof(ent);
    }

    private void OnShuttleTerminating(Entity<ShuttleComponent> ent, ref EntityTerminatingEvent args)
    {
        RemoveRoof(ent);
    }

    public void EnsureRoof(EntityUid shuttleUid)
    {
        // Roof grids and mid-build reparenting must not recurse.
        if (_rebuilding || HasComp<CEZShuttleRoofComponent>(shuttleUid))
            return;

        _rebuilding = true;
        try
        {
            EnsureRoofCore(shuttleUid);
        }
        finally
        {
            _rebuilding = false;
        }
    }

    private void EnsureRoofCore(EntityUid shuttleUid)
    {
        if (!TryFindTopShuttleGrid(shuttleUid, out var topGrid, out var topDepth))
            return;

        var topXform = Transform(topGrid);
        if (topXform.MapUid is not { } topMapUid)
        {
            RemoveRoof(shuttleUid);
            return;
        }

        if (!_zLevels.TryMapOffset(topMapUid, 1, out var aboveMap))
        {
            RemoveRoof(shuttleUid);
            return;
        }

        var aboveMapUid = aboveMap.Value.Owner;
        var roofDepth = topDepth + 1;

        EntityUid roofGrid;
        if (TryFindExistingRoof(shuttleUid, out var existingRoof))
        {
            var existingDepth = TryComp<CEZLinkedGridComponent>(existingRoof, out var existingLinked)
                ? existingLinked.Depth
                : (int?)null;

            if (Transform(existingRoof).MapUid != aboveMapUid || existingDepth != roofDepth)
            {
                // Rebuild if the roof moved networks or depths.
                RemoveRoofGrid(existingRoof);
                roofGrid = CreateRoofGrid(aboveMapUid, topGrid, shuttleUid);
                LinkRoofToShuttle(shuttleUid, roofGrid, roofDepth);
            }
            else
            {
                roofGrid = existingRoof;
            }
        }
        else
        {
            roofGrid = CreateRoofGrid(aboveMapUid, topGrid, shuttleUid);
            LinkRoofToShuttle(shuttleUid, roofGrid, roofDepth);
        }

        SyncRoofTransform(topGrid, roofGrid);

        // Re-census only when the deck we roof over changes, the census walks every entity on it.
        var roofComp = Comp<CEZShuttleRoofComponent>(roofGrid);
        if (roofComp.SourceGrid != topGrid)
        {
            roofComp.SourceGrid = topGrid;
            roofComp.TileGroup = ResolveTileGroup(topGrid)?.ID;
        }

        CopyTiles(topGrid, roofGrid, GetTileGroup((roofGrid, roofComp)));

        // Track tile changes on the current top deck.
        ClearSourceMarkers(shuttleUid, topGrid);
        EnsureComp<CEZShuttleRoofSourceComponent>(topGrid).Shuttle = shuttleUid;
    }

    public void RemoveRoof(EntityUid shuttleUid)
    {
        if (TryFindExistingRoof(shuttleUid, out var roof))
            RemoveRoofGrid(roof);

        ClearSourceMarkers(shuttleUid, EntityUid.Invalid);
    }

    // Keep only the active tile-change marker.
    private void ClearSourceMarkers(EntityUid shuttleUid, EntityUid keepGrid)
    {
        if (shuttleUid != keepGrid)
            RemComp<CEZShuttleRoofSourceComponent>(shuttleUid);

        if (!TryComp<CEZLinkedGridComponent>(shuttleUid, out var linked))
            return;

        foreach (var (_, peer) in linked.PeerGrids)
        {
            if (peer != keepGrid)
                RemComp<CEZShuttleRoofSourceComponent>(peer);
        }
    }

    private bool TryFindTopShuttleGrid(EntityUid shuttleUid, out EntityUid topGrid, out int topDepth)
    {
        topGrid = shuttleUid;
        topDepth = 0;

        if (!TryComp<CEZLinkedGridComponent>(shuttleUid, out var linked))
            return true;

        topDepth = linked.Depth;
        foreach (var (depth, peer) in linked.PeerGrids)
        {
            if (HasComp<CEZShuttleRoofComponent>(peer))
                continue;

            if (depth > topDepth)
            {
                topDepth = depth;
                topGrid = peer;
            }
        }

        return true;
    }

    private bool TryFindExistingRoof(EntityUid shuttleUid, out EntityUid roof)
    {
        roof = default;
        if (!TryComp<CEZLinkedGridComponent>(shuttleUid, out var linked))
            return false;

        foreach (var (_, peer) in linked.PeerGrids)
        {
            if (HasComp<CEZShuttleRoofComponent>(peer))
            {
                roof = peer;
                return true;
            }
        }

        return false;
    }

    private EntityUid CreateRoofGrid(EntityUid mapUid, EntityUid topShuttleGrid, EntityUid shuttleUid)
    {
        var grid = _mapManager.CreateGridEntity(mapUid);
        var gridUid = grid.Owner;

        var roofComp = AddComp<CEZShuttleRoofComponent>(gridUid);
        roofComp.Shuttle = shuttleUid;
        roofComp.SourceGrid = topShuttleGrid;
        roofComp.TileGroup = ResolveTileGroup(topShuttleGrid)?.ID;

        _meta.SetEntityName(gridUid, $"Shuttle Roof ({ToPrettyString(shuttleUid)})");

        return gridUid;
    }

    private void SyncRoofTransform(EntityUid topShuttleGrid, EntityUid roofGrid)
    {
        var topXform = Transform(topShuttleGrid);
        var roofXform = Transform(roofGrid);

        _transform.SetLocalPositionRotation(roofGrid, topXform.LocalPosition, topXform.LocalRotation, roofXform);
    }

    private void CopyTiles(EntityUid topGrid, EntityUid roofGrid, CERoofTileGroupPrototype? group)
    {
        if (!TryComp<MapGridComponent>(topGrid, out var topMapGrid) ||
            !TryComp<MapGridComponent>(roofGrid, out var roofMapGrid))
        {
            return;
        }

        var fallback = _tileDefMan[FallbackPlatingTileId];

        var tilesToSet = new List<(Vector2i, Tile)>();
        var sourcePositions = new HashSet<Vector2i>();

        foreach (var tileRef in _mapSystem.GetAllTiles(topGrid, topMapGrid))
        {
            sourcePositions.Add(tileRef.GridIndices);
        }

        // Where the deck has a diagonal wall the roof keeps the hull's chamfer instead of a full tile.
        var diagonals = group == null
            ? new Dictionary<Vector2i, Direction>()
            : CollectDiagonalWalls(topGrid, topMapGrid, group, sourcePositions);

        foreach (var tileRef in _mapSystem.GetAllTiles(topGrid, topMapGrid))
        {
            ITileDefinition targetDef;

            if (group != null)
            {
                targetDef = diagonals.TryGetValue(tileRef.GridIndices, out var corner) &&
                            group.DiagonalTiles.TryGetValue(corner, out var diagonalTile)
                    ? _tileDefMan[diagonalTile]
                    : _tileDefMan[group.Tile];

                tilesToSet.Add((tileRef.GridIndices, new Tile(targetDef.TileId)));
                continue;
            }

            var sourceDef = (ContentTileDefinition)_tileDefMan[tileRef.Tile.TypeId];

            if (sourceDef.IsSubFloor)
            {
                targetDef = sourceDef;
            }
            else if (!string.IsNullOrEmpty(sourceDef.BaseTurf) &&
                     _tileDefMan.TryGetDefinition(sourceDef.BaseTurf, out var baseDef))
            {
                // Use subfloor under normal floors.
                targetDef = baseDef;
            }
            else
            {
                targetDef = fallback;
            }

            tilesToSet.Add((tileRef.GridIndices, new Tile(targetDef.TileId)));
        }

        // Clear roof tiles with no source tile.
        foreach (var existingTile in _mapSystem.GetAllTiles(roofGrid, roofMapGrid))
        {
            if (!sourcePositions.Contains(existingTile.GridIndices))
                tilesToSet.Add((existingTile.GridIndices, Tile.Empty));
        }

        if (tilesToSet.Count > 0)
            _mapSystem.SetTiles(roofGrid, roofMapGrid, tilesToSet);
    }

    /// <summary>
    /// Picks the roof tile group for a deck by counting the walls standing on it. Returns null when no group
    /// reaches its wall threshold, which leaves the roof on the default subfloor copy.
    /// </summary>
    private CERoofTileGroupPrototype? ResolveTileGroup(EntityUid sourceGrid)
    {
        var groups = _protoMan.EnumeratePrototypes<CERoofTileGroupPrototype>().ToList();
        if (groups.Count == 0)
            return null;

        var counts = new Dictionary<CERoofTileGroupPrototype, int>();
        var children = Transform(sourceGrid).ChildEnumerator;

        while (children.MoveNext(out var child))
        {
            if (MetaData(child).EntityPrototype?.ID is not { } protoId)
                continue;

            foreach (var group in groups)
            {
                if (group.Walls.Contains(protoId) || group.DiagonalWalls.Contains(protoId))
                    counts[group] = counts.GetValueOrDefault(group) + 1;
            }
        }

        CERoofTileGroupPrototype? best = null;
        var bestCount = 0;

        foreach (var (group, count) in counts)
        {
            if (count < group.MinWalls)
                continue;

            if (best == null || group.Priority > best.Priority || (group.Priority == best.Priority && count > bestCount))
            {
                best = group;
                bestCount = count;
            }
        }

        return best;
    }

    private CERoofTileGroupPrototype? GetTileGroup(Entity<CEZShuttleRoofComponent> roof)
    {
        if (roof.Comp.TileGroup is not { } groupId)
            return null;

        return _protoMan.TryIndex(groupId, out var group) ? group : null;
    }

    /// <summary>
    /// Maps the deck's diagonal walls to the corner their filled half covers, keeping only the ones that sit
    /// on the edge of the footprint.
    /// </summary>
    /// <remarks>
    /// Diagonal walls are also used inside a hull, where a half tile would punch a notch into the middle of
    /// the roof. A wall only chamfers the roof when both of the edges its filled half leaves open face off
    /// the footprint, otherwise the roof stays square there.
    /// </remarks>
    private Dictionary<Vector2i, Direction> CollectDiagonalWalls(EntityUid sourceGrid, MapGridComponent grid,
        CERoofTileGroupPrototype group, HashSet<Vector2i> footprint)
    {
        var result = new Dictionary<Vector2i, Direction>();
        if (group.DiagonalWalls.Count == 0)
            return result;

        var children = Transform(sourceGrid).ChildEnumerator;

        while (children.MoveNext(out var child))
        {
            if (MetaData(child).EntityPrototype?.ID is not { } protoId || !group.DiagonalWalls.Contains(protoId))
                continue;

            var xform = Transform(child);
            var indices = _mapSystem.TileIndicesFor(sourceGrid, grid, xform.Coordinates);
            var corner = RotateCorner(group.DiagonalWallCorner, xform.LocalRotation);
            var (coveredA, coveredB) = CornerSides(corner);

            if (footprint.Contains(indices.Offset(coveredA.GetOpposite())) ||
                footprint.Contains(indices.Offset(coveredB.GetOpposite())))
            {
                continue;
            }

            result[indices] = corner;
        }

        return result;
    }

    /// <summary>
    /// The two cardinal edges a corner's filled half covers.
    /// </summary>
    private static (Direction, Direction) CornerSides(Direction corner)
    {
        return corner switch
        {
            Direction.NorthEast => (Direction.North, Direction.East),
            Direction.NorthWest => (Direction.North, Direction.West),
            Direction.SouthEast => (Direction.South, Direction.East),
            _ => (Direction.South, Direction.West),
        };
    }

    /// <summary>
    /// Rotates a corner direction by an entity rotation. Direction steps of two are a quarter turn, and
    /// rotation runs counter clockwise from South, the same way airtight directions are rotated.
    /// </summary>
    private static Direction RotateCorner(Direction corner, Angle rotation)
    {
        var quarterTurns = (int) MathF.Round((float) (rotation.Theta / MathHelper.PiOver2)) & 3;
        return (Direction) (((int) corner + quarterTurns * 2) % 8);
    }

    private void LinkRoofToShuttle(EntityUid shuttleUid, EntityUid roofUid, int roofDepth)
    {
        var shuttleLinked = EnsureComp<CEZLinkedGridComponent>(shuttleUid);

        var fullGraph = new Dictionary<int, EntityUid>(shuttleLinked.PeerGrids)
        {
            [shuttleLinked.Depth] = shuttleUid,
        };

        if (fullGraph.ContainsKey(roofDepth))
        {
            Log.Error($"Cannot link shuttle roof {ToPrettyString(roofUid)} for {ToPrettyString(shuttleUid)} at depth {roofDepth}: a peer already occupies that depth.");
            QueueDel(roofUid);
            return;
        }

        fullGraph[roofDepth] = roofUid;

        foreach (var (depth, gridUid) in fullGraph)
        {
            var comp = EnsureComp<CEZLinkedGridComponent>(gridUid);
            comp.Depth = depth;
            comp.ZNetwork = shuttleLinked.ZNetwork;
            comp.PeerGrids = new Dictionary<int, EntityUid>(fullGraph);
            comp.PeerGrids.Remove(depth);
            Dirty(gridUid, comp);
        }
    }

    private void RemoveRoofGrid(EntityUid roofUid)
    {
        // Removal cleanup does not dirty peers for us.
        if (TryComp<CEZLinkedGridComponent>(roofUid, out var roofLinked))
        {
            var roofDepth = roofLinked.Depth;
            foreach (var (_, peer) in roofLinked.PeerGrids.ToArray())
            {
                if (TryComp<CEZLinkedGridComponent>(peer, out var peerLinked) &&
                    peerLinked.PeerGrids.Remove(roofDepth))
                {
                    Dirty(peer, peerLinked);
                }
            }

            roofLinked.PeerGrids.Clear();
            Dirty(roofUid, roofLinked);
        }

        QueueDel(roofUid);
    }
}
