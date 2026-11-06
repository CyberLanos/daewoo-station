// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared._Pirate.CCVars;

public sealed partial class PirateVars
{
    /// <summary>
    /// Space lights what is in it, like SS13 starlight: things floating in space, lattice and glass floors over open
    /// space are lit, and the light blur lets it reach a little way into the station. Drawn on the client, but set
    /// by the server only (replicated), so players cannot turn it up for an advantage.
    /// </summary>
    public static readonly CVarDef<bool> Starlight =
        CVarDef.Create("pirate.starlight", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Default brightness of the starlight, 0 to 1. 1 is SS13's full-bright space. Server only, replicated.
    /// A parallax prototype or a map's StarlightComponent can override it.
    /// </summary>
    public static readonly CVarDef<float> StarlightStrength =
        CVarDef.Create("pirate.starlight_strength", 0.5f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Default colour of the starlight, as hex. SS13's COLOR_STARLIGHT. Server only, replicated.
    /// A parallax prototype or a map's StarlightComponent can override it.
    /// </summary>
    public static readonly CVarDef<string> StarlightColor =
        CVarDef.Create("pirate.starlight_color", "#8589FA", CVar.SERVER | CVar.REPLICATED);
}
