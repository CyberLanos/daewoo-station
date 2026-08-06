using System.Linq;
using Content.Server._Pirate.ZLevels.Core;
using Content.Server.Atmos.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared.Atmos;
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

    private static readonly Direction[] Cardinals =
    {
        Direction.North,
        Direction.South,
        Direction.East,
        Direction.West,
    };

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
            EnsureRoof(ent.Comp.Shuttle, recensus: false);
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

    /// <param name="recensus">
    /// Whether to re-resolve the deck's roof tile group. Off for rebuilds driven by a single tile edit, since
    /// the census walks every entity on the deck. On for lifecycle rebuilds, which is also what makes the
    /// group correct after a map load: the grid is reparented onto its map before its entities are started,
    /// so the census during that parent change sees a deck with no walls on it yet. The map-init that follows
    /// runs once everything is attached.
    /// </param>
    public void EnsureRoof(EntityUid shuttleUid, bool recensus = true)
    {
        // Roof grids and mid-build reparenting must not recurse.
        if (_rebuilding || HasComp<CEZShuttleRoofComponent>(shuttleUid))
            return;

        _rebuilding = true;
        try
        {
            EnsureRoofCore(shuttleUid, recensus);
        }
        finally
        {
            _rebuilding = false;
        }
    }

    private void EnsureRoofCore(EntityUid shuttleUid, bool recensus)
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

        var roofComp = Comp<CEZShuttleRoofComponent>(roofGrid);
        if (recensus || roofComp.SourceGrid != topGrid)
        {
            roofComp.SourceGrid = topGrid;

            // Careful with the null case: string implicitly converts to ProtoId, so `group?.ID` would store a
            // ProtoId wrapping a null id rather than a null ProtoId.
            var resolved = ResolveTileGroup(topGrid);
            if (resolved == null)
                roofComp.TileGroup = null;
            else
                roofComp.TileGroup = new ProtoId<CERoofTileGroupPrototype>(resolved.ID);
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
        // SourceGrid and TileGroup are filled in by EnsureRoofCore, which owns the census.
        roofComp.SourceGrid = EntityUid.Invalid;

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
        var footprint = new HashSet<Vector2i>();

        foreach (var tileRef in _mapSystem.GetAllTiles(topGrid, topMapGrid))
        {
            footprint.Add(tileRef.GridIndices);
        }

        // Only the walled-in part of the deck gets a roof, so thruster bays and outside catwalks stay open.
        var roofArea = ResolveRoofArea(topGrid, topMapGrid, footprint);

        // Where the deck has a diagonal wall the roof keeps the wall's chamfer instead of a full tile.
        var diagonals = group == null
            ? new Dictionary<Vector2i, Direction>()
            : CollectDiagonalWalls(topGrid, topMapGrid, group, roofArea);

        foreach (var tileRef in _mapSystem.GetAllTiles(topGrid, topMapGrid))
        {
            if (!roofArea.Contains(tileRef.GridIndices))
                continue;

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

        // Clear roof tiles that are no longer roofed.
        foreach (var existingTile in _mapSystem.GetAllTiles(roofGrid, roofMapGrid))
        {
            if (!roofArea.Contains(existingTile.GridIndices))
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
    /// Works out which of the deck's tiles carry a roof: the space its airtight walls enclose, plus the wall
    /// tiles bordering that space so the roof reaches the hull line rather than stopping short of it.
    /// </summary>
    /// <remarks>
    /// This is what keeps thruster bays, outside catwalks and other unpressurised trimmings off the roof, so its
    /// outline follows the hull instead of the whole footprint. Walls count by the directions they are built to
    /// block, ignoring whether they happen to be open right now, otherwise an open airlock would leak the fill
    /// into the ship and shrink the roof to nothing. If nothing comes out enclosed - an unfinished hull, or a
    /// deck whose entities are not attached yet - the whole footprint is roofed, as it was before.
    /// </remarks>
    private HashSet<Vector2i> ResolveRoofArea(EntityUid gridUid, MapGridComponent grid, HashSet<Vector2i> footprint)
    {
        if (footprint.Count == 0)
            return footprint;

        var blocked = new Dictionary<Vector2i, AtmosDirection>();
        var min = new Vector2i(int.MaxValue, int.MaxValue);
        var max = new Vector2i(int.MinValue, int.MinValue);

        foreach (var indices in footprint)
        {
            min = Vector2i.ComponentMin(min, indices);
            max = Vector2i.ComponentMax(max, indices);

            var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, indices);
            var dirs = AtmosDirection.Invalid;

            while (anchored.MoveNext(out var anchoredUid))
            {
                if (TryComp<AirtightComponent>(anchoredUid, out var airtight))
                    dirs |= airtight.AirBlockedDirection;
            }

            if (dirs != AtmosDirection.Invalid)
                blocked[indices] = dirs;
        }

        // Flood the space around the deck inwards, stopping at walls. One tile of margin is enough to get all
        // the way around the footprint.
        min -= Vector2i.One;
        max += Vector2i.One;

        var outside = new HashSet<Vector2i>();
        var frontier = new Queue<Vector2i>();

        for (var x = min.X; x <= max.X; x++)
        {
            Seed(new Vector2i(x, min.Y));
            Seed(new Vector2i(x, max.Y));
        }

        for (var y = min.Y; y <= max.Y; y++)
        {
            Seed(new Vector2i(min.X, y));
            Seed(new Vector2i(max.X, y));
        }

        while (frontier.TryDequeue(out var indices))
        {
            foreach (var dir in Cardinals)
            {
                var neighbour = indices.Offset(dir);

                if (neighbour.X < min.X || neighbour.X > max.X || neighbour.Y < min.Y || neighbour.Y > max.Y)
                    continue;

                if (outside.Contains(neighbour))
                    continue;

                var atmosDir = dir.ToAtmosDirection();

                if (blocked.TryGetValue(indices, out var here) && here.IsFlagSet(atmosDir))
                    continue;

                if (blocked.TryGetValue(neighbour, out var there) && there.IsFlagSet(atmosDir.GetOpposite()))
                    continue;

                Seed(neighbour);
            }
        }

        var enclosed = new HashSet<Vector2i>();
        foreach (var indices in footprint)
        {
            if (!outside.Contains(indices))
                enclosed.Add(indices);
        }

        if (enclosed.Count == 0)
            return footprint;

        var area = new HashSet<Vector2i>(enclosed);

        foreach (var (indices, _) in blocked)
        {
            if (area.Contains(indices))
                continue;

            foreach (var dir in Cardinals)
            {
                if (enclosed.Contains(indices.Offset(dir)))
                {
                    area.Add(indices);
                    break;
                }
            }
        }

        return area;

        void Seed(Vector2i indices)
        {
            if (outside.Add(indices))
                frontier.Enqueue(indices);
        }
    }

    /// <summary>
    /// Maps the deck's diagonal walls to the corner their filled half covers, dropping the ones whose chamfer
    /// would only carve a sealed pocket into the roof.
    /// </summary>
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
            result[indices] = RotateCorner(group.DiagonalWallCorner, xform.LocalRotation);
        }

        PruneSealedDiagonals(result, footprint);
        return result;
    }

    /// <summary>
    /// Drops diagonals whose open half does not reach the outside of the roof.
    /// </summary>
    /// <remarks>
    /// A half tile leaves the other half of its tile uncovered. That only reads as a chamfer if the gap is part
    /// of the space around the roof: either it opens straight off the footprint, or it runs into the gap of
    /// another diagonal facing it, the two hypotenuses forming a channel that eventually gets out. A diagonal
    /// whose gap is sealed in by roof on every side is carving a pocket in the middle of the roof instead, and
    /// stays a full tile. Diagonals are also used inside a hull, so this is the common case.
    ///
    /// Each edge of a half tile is either wholly covered or wholly open - the filled half covers the two edges
    /// beside its corner, the gap covers the other two - so the gaps meet through tile edges only and this is
    /// plain connectivity between tiles. Gaps that touch only at a grid corner are not a way through.
    /// </remarks>
    private static void PruneSealedDiagonals(Dictionary<Vector2i, Direction> diagonals, HashSet<Vector2i> footprint)
    {
        var reaching = new HashSet<Vector2i>();
        var frontier = new Queue<Vector2i>();

        // Seed with the gaps that open straight off the footprint.
        foreach (var (indices, corner) in diagonals)
        {
            foreach (var side in OpenSides(corner))
            {
                if (footprint.Contains(indices.Offset(side)))
                    continue;

                if (reaching.Add(indices))
                    frontier.Enqueue(indices);
                break;
            }
        }

        // Then walk gap to gap through the edges both of them leave open.
        while (frontier.TryDequeue(out var indices))
        {
            foreach (var side in OpenSides(diagonals[indices]))
            {
                var neighbour = indices.Offset(side);

                if (reaching.Contains(neighbour) || !diagonals.TryGetValue(neighbour, out var neighbourCorner))
                    continue;

                if (!IsOpenSide(neighbourCorner, side.GetOpposite()))
                    continue;

                reaching.Add(neighbour);
                frontier.Enqueue(neighbour);
            }
        }

        if (reaching.Count == diagonals.Count)
            return;

        foreach (var indices in diagonals.Keys.ToArray())
        {
            if (!reaching.Contains(indices))
                diagonals.Remove(indices);
        }
    }

    private static readonly Direction[] OpenSidesOfSouthEast = { Direction.North, Direction.West };
    private static readonly Direction[] OpenSidesOfSouthWest = { Direction.North, Direction.East };
    private static readonly Direction[] OpenSidesOfNorthEast = { Direction.South, Direction.West };
    private static readonly Direction[] OpenSidesOfNorthWest = { Direction.South, Direction.East };

    /// <summary>
    /// The two cardinal edges left open by a half tile filling <paramref name="corner"/>, i.e. the edges beside
    /// the opposite corner.
    /// </summary>
    private static Direction[] OpenSides(Direction corner)
    {
        return corner switch
        {
            Direction.SouthEast => OpenSidesOfSouthEast,
            Direction.SouthWest => OpenSidesOfSouthWest,
            Direction.NorthEast => OpenSidesOfNorthEast,
            _ => OpenSidesOfNorthWest,
        };
    }

    private static bool IsOpenSide(Direction corner, Direction side)
    {
        var sides = OpenSides(corner);
        return sides[0] == side || sides[1] == side;
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
