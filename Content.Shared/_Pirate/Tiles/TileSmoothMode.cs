// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.Tiles;

/// <summary>
/// How a tile picks its sprite variant from the tiles around it.
/// See <c>Content.Server._Pirate.Tiles.TileSmoothingSystem</c>.
/// </summary>
public enum TileSmoothMode : byte
{
    /// <summary>
    /// The variant is left alone. The tile still counts as a neighbour for other tiles in its group,
    /// which is what the fixed diagonal tiles use.
    /// </summary>
    None,

    /// <summary>
    /// The variant is the cardinal neighbour mask: North = 1, South = 2, East = 4, West = 8, the same
    /// numbering smoothed wall states use (<c>IconSmoothingMode.CardinalFlags</c>).
    /// Needs a 16 variant sprite strip (512x32) in mask order.
    /// </summary>
    Cardinal,

    /// <summary>
    /// <see cref="Cardinal"/> plus an inner corner: a tile with all four neighbours but a missing diagonal one
    /// takes a variant that draws the lip around that corner. Needs 20 variants (640x32), the 16 cardinal states
    /// followed by the inner corner for NE, NW, SE and SW.
    /// </summary>
    /// <remarks>
    /// Only the case of a single missing diagonal is covered, which is the bulk of them. Tiles wanting an inner
    /// corner alongside an exposed edge, or several corners at once, fall back to their cardinal state and stay
    /// flat there. Covering those needs the full 47 tile set.
    /// </remarks>
    CardinalCorners,
}
