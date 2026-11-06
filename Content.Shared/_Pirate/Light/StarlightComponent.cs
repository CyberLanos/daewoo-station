// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Light;

/// <summary>
/// Starlight of one space map. Put it on the map entity to give a station its own starlight, or change it at
/// runtime with <see cref="StarlightMapSystem"/>. Each value left null falls back to the parallax that is drawn
/// (ParallaxPrototype starlightColor / starlightStrength), then to the server CVars.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StarlightComponent : Component
{
    /// <summary>
    /// Colour of the starlight. Null: the parallax's, else pirate.starlight_color.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color? Color;

    /// <summary>
    /// Strength of the starlight, 0 (off) to 1 (full-bright). Null: the parallax's, else pirate.starlight_strength.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float? Strength;
}
