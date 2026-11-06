// SPDX-License-Identifier: MIT

using Content.Goobstation.Shared.Parallax; // Goob - background meteors
using Robust.Shared.Prototypes;

namespace Content.Client.Parallax.Data;

/// <summary>
/// Prototype data for a parallax.
/// </summary>
[Prototype]
public sealed partial class ParallaxPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Parallax layers.
    /// </summary>
    [DataField("layers")]
    public List<ParallaxLayerConfig> Layers { get; private set; } = new();

    /// <summary>
    /// Parallax layers, low-quality.
    /// </summary>
    [DataField("layersLQ")]
    public List<ParallaxLayerConfig> LayersLQ { get; private set; } = new();

    /// <summary>
    /// If low-quality layers don't exist for this parallax and high-quality should be used instead.
    /// </summary>
    [DataField("layersLQUseHQ")]
    public bool LayersLQUseHQ { get; private set; } = true;

    // Goob - background meteors
    /// <summary>
    /// Optional cosmetic meteor/asteroid field drawn over this parallax.
    /// </summary>
    [DataField]
    public MeteorParallaxConfig? Meteors { get; private set; }

    // Pirate: arrival card
    /// <summary>
    ///     Locale key of the system this parallax depicts, shown on the warp arrival card. Empty for
    ///     backgrounds that are not star systems.
    /// </summary>
    [DataField]
    public string SectorName { get; private set; } = "";

    /// <summary>
    ///     Locale key of the one-line description the arrival card runs as a marquee.
    /// </summary>
    [DataField]
    public string SectorDescription { get; private set; } = "";

    #region Pirate: starlight
    /// <summary>
    ///     Colour of the starlight on maps that draw this parallax. Null uses the server default
    ///     (pirate.starlight_color). A map's StarlightComponent overrides it.
    /// </summary>
    [DataField]
    public Color? StarlightColor { get; private set; }

    /// <summary>
    ///     Strength of the starlight, 0 to 1, on maps that draw this parallax. Null uses the server
    ///     default (pirate.starlight_strength). A map's StarlightComponent overrides it.
    /// </summary>
    [DataField]
    public float? StarlightStrength { get; private set; }
    #endregion
}