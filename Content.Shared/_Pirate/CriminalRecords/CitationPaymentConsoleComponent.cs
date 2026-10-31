// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.CriminalRecords;

/// <summary>
/// Public citation payment terminal. It has no criminal records editing actions.
/// </summary>
[RegisterComponent]
public sealed partial class CitationPaymentConsoleComponent : Component
{
    /// <summary>
    /// How long the screen shows the result of a payment before it goes back to idle.
    /// </summary>
    [DataField]
    public TimeSpan ResultTime = TimeSpan.FromSeconds(2);

    [ViewVariables]
    public TimeSpan? ResultEnd;
}

[Serializable, NetSerializable]
public enum CitationPaymentConsoleVisuals : byte
{
    Status,
}

[Serializable, NetSerializable]
public enum CitationPaymentConsoleStatus : byte
{
    Idle,
    Accepted,
    Denied,
}

public enum CitationPaymentConsoleVisualLayers : byte
{
    Screen,
}
