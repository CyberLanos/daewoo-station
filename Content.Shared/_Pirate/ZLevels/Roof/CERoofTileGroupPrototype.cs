// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.ZLevels.Roof;

/// <summary>
/// Ties a set of base walls to the roof tiles that match them. The multiz roof generator counts the walls
/// on the deck it is roofing over, and if a group reaches <see cref="MinWalls"/> the roof is built from that
/// group's tiles instead of the default subfloor copy.
/// </summary>
[Prototype("roofTileGroup")]
public sealed partial class CERoofTileGroupPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Full-tile walls that count towards this group.
    /// </summary>
    [DataField]
    public HashSet<EntProtoId> Walls = new();

    /// <summary>
    /// Half-tile walls that count towards this group. The roof tile above one of these is taken from
    /// <see cref="DiagonalTiles"/> instead of <see cref="Tile"/>, so the roof keeps the hull's chamfer.
    /// </summary>
    [DataField]
    public HashSet<EntProtoId> DiagonalWalls = new();

    /// <summary>
    /// How many walls the deck needs before this group may be used. Guards against picking a group off a
    /// couple of stray walls.
    /// </summary>
    [DataField]
    public int MinWalls = 10;

    /// <summary>
    /// Tile used for the whole roof footprint.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<ContentTileDefinition> Tile { get; private set; }

    /// <summary>
    /// Tiles used above <see cref="DiagonalWalls"/>, keyed by the corner their filled half covers.
    /// </summary>
    [DataField]
    public Dictionary<Direction, ProtoId<ContentTileDefinition>> DiagonalTiles = new();

    /// <summary>
    /// Which corner a diagonal wall's filled half covers at zero rotation; the wall's own rotation is
    /// applied on top. Matches Structures/Walls/shuttle_diagonal.rsi, whose fixture and airtight
    /// directions cover South and East unrotated.
    /// </summary>
    [DataField]
    public Direction DiagonalWallCorner = Direction.SouthEast;

    /// <summary>
    /// Tie breaker when a deck reaches the threshold for more than one group. Highest wins, then wall count.
    /// </summary>
    [DataField]
    public int Priority;
}
