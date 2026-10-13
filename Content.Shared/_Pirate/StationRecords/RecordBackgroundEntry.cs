// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.StationRecords;

/// <summary>
/// One entry of a character's OOC records background, written in the character editor: NanoTrasen's file on them
/// for one records console. Shown read-only in that console's history. Empty title or author show the defaults
/// ("Особова справа", "Відділ кадрів NanoTrasen").
/// </summary>
[Serializable, NetSerializable, DataRecord]
public sealed partial record RecordBackgroundEntry
{
    public const int MaxTitleLength = 64;
    public const int MaxAuthorLength = 64;
    public const int MaxBodyLength = 1024;

    /// <summary>How many entries each console can have.</summary>
    public const int MaxPerBook = 10;

    [DataField]
    public RecordLogBook Book;

    [DataField]
    public string Title = string.Empty;

    [DataField]
    public string Author = string.Empty;

    [DataField]
    public string Body = string.Empty;
}
