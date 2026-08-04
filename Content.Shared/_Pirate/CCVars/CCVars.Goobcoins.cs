// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared._Pirate.CCVars;

/// <summary>
/// Pirate specific cvars for the mid-round goobcoin payouts, on top of the Goobstation
/// round-end payout configured under <c>servercurrency.*</c>.
/// </summary>
[CVarDefs]
public sealed class PirateGoobcoinCVars
{
    /// <summary>
    /// Goobcoins granted to a player who readied up and spawned in at round start.
    /// Set to 0 to disable the bonus.
    /// </summary>
    public static readonly CVarDef<int> RoundStartBonus =
        CVarDef.Create("pirate.goobcoin.round_start_bonus", 50, CVar.SERVERONLY);

    /// <summary>
    /// Goobcoins taken from a player who is round-removed by cryostorage after having entered
    /// the pod within <see cref="EarlyCryoWindowMinutes"/> of the round starting.
    /// Never takes more than the player's balance. Set to 0 to disable the penalty.
    /// </summary>
    public static readonly CVarDef<int> EarlyCryoPenalty =
        CVarDef.Create("pirate.goobcoin.early_cryo_penalty", 100, CVar.SERVERONLY);

    /// <summary>
    /// How many minutes into the round climbing into a cryopod still counts as leaving early.
    /// Measured from when the player entered the pod, not from when the pod's grace period expired.
    /// </summary>
    public static readonly CVarDef<float> EarlyCryoWindowMinutes =
        CVarDef.Create("pirate.goobcoin.early_cryo_window_minutes", 10f, CVar.SERVERONLY);
}
