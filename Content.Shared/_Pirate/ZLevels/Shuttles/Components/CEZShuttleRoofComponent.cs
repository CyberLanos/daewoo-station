using Content.Shared._Pirate.ZLevels.Roof;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.ZLevels.Shuttles.Components;

/// <summary>
/// Marks a grid as a runtime roof owned by <see cref="Shuttles.CEZShuttleRoofSystem"/>. Never authored or saved.
/// </summary>
[RegisterComponent, NetworkedComponent, UnsavedComponent]
public sealed partial class CEZShuttleRoofComponent : Component
{
    /// <summary>Shuttle root grid this roof belongs to.</summary>
    [DataField]
    public EntityUid Shuttle;

    /// <summary>Topmost shuttle grid whose tile silhouette was copied.</summary>
    [DataField]
    public EntityUid SourceGrid;

    /// <summary>
    /// Roof tile group resolved from the walls on <see cref="SourceGrid"/>, null when the deck had no
    /// eligible walls and the roof falls back to copying its subfloor. Resolved when the roof grid is built
    /// or its source deck changes, not on every tile edit.
    /// </summary>
    [DataField]
    public ProtoId<CERoofTileGroupPrototype>? TileGroup;
}
