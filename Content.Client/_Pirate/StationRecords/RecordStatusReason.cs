// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Utility;

namespace Content.Client._Pirate.StationRecords;

/// <summary>
/// The "why" under a status on the criminal and psionics records consoles: a grey title for the status
/// ("Підстава для розшуку:") followed by the reason given, the same way on both consoles.
/// </summary>
public static class RecordStatusReason
{
    /// <param name="console">"security" or "psionics": picks the locale keys records-status-reason-{console}-{status}.</param>
    public static FormattedMessage Format(string console, string status, string reason)
    {
        var title = Loc.TryGetString($"records-status-reason-{console}-{status.ToLowerInvariant()}", out var specific)
            ? specific
            : Loc.GetString("records-status-reason");

        var message = new FormattedMessage();
        message.PushColor(Color.DarkGray);
        message.AddText($"{title}:");
        message.Pop();
        message.AddText($" {reason}");
        return message;
    }
}
