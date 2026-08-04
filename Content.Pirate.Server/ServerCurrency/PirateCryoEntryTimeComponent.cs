// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Pirate.Server.ServerCurrency;

/// <summary>
/// Records how far into the round an entity climbed into a cryostorage pod, so the early-cryo
/// goobcoin penalty is judged by when they decided to leave rather than by when the pod's grace
/// period happened to expire.
/// </summary>
/// <remarks>
/// Added by <see cref="PirateGoobcoinRewardSystem"/> on every cryopod insertion and overwritten
/// each time, so climbing back out and returning later re-times the entry.
/// </remarks>
[RegisterComponent]
public sealed partial class PirateCryoEntryTimeComponent : Component
{
    /// <summary>
    /// Round duration at the moment of the most recent insertion into a cryopod.
    /// </summary>
    [ViewVariables]
    public TimeSpan RoundTimeOnEntry;

    /// <summary>
    /// Whether the penalty has already been taken for this stay, so a second mind removal
    /// cannot charge the player twice.
    /// </summary>
    [ViewVariables]
    public bool Charged;
}
