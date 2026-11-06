// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.Light;

/// <summary>
/// Sets the starlight of a map, for example from a sector change. Server-side changes reach every client
/// through the networked <see cref="StarlightComponent"/>.
/// </summary>
public sealed class StarlightMapSystem : EntitySystem
{
    /// <summary>
    /// Overrides the map's starlight. A null value goes back to the parallax / server default for that value.
    /// </summary>
    public void SetStarlight(EntityUid mapUid, Color? color, float? strength)
    {
        var comp = EnsureComp<StarlightComponent>(mapUid);
        comp.Color = color;
        comp.Strength = strength is { } s ? Math.Clamp(s, 0f, 1f) : null;
        Dirty(mapUid, comp);
    }

    /// <summary>
    /// Removes the map's override, so the parallax / server default applies again.
    /// </summary>
    public void ClearStarlight(EntityUid mapUid)
    {
        RemComp<StarlightComponent>(mapUid);
    }
}
