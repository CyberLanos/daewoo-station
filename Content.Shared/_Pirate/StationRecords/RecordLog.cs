// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.StationRecords;

/// <summary>
/// What a record log entry is.
/// </summary>
[Serializable, NetSerializable]
public enum RecordLogKind : byte
{
    /// <summary>Written automatically when a console changes the person's status (sec, psi).</summary>
    StatusChange,

    /// <summary>Typed by someone at a console.</summary>
    Note,

    /// <summary>NanoTrasen HR background on the character, from character creation. Read-only, never deleted.</summary>
    Background,
}

/// <summary>
/// Which console's history an entry belongs to; each console only shows its own.
/// </summary>
[Serializable, NetSerializable]
public enum RecordLogBook : byte
{
    Security,
    Psionics,
    Crew,
}

/// <summary>
/// One history entry on a person's record.
/// </summary>
[Serializable, NetSerializable, DataRecord]
public sealed partial record RecordLogEntry
{
    /// <summary>Unique within the person's log, used to delete it.</summary>
    [DataField]
    public uint Id;

    [DataField]
    public RecordLogKind Kind;

    /// <summary>What it is about beyond the kind, e.g. the new status. Optional.</summary>
    [DataField]
    public string? Topic;

    /// <summary>For status changes: the new status (enum name), for its HUD icon and colour.</summary>
    [DataField]
    public string? Status;

    [DataField]
    public string Message = string.Empty;

    [DataField]
    public string? AuthorName;

    [DataField]
    public string? AuthorTitle;

    /// <summary>Name of the station it was written on. None for the HR background.</summary>
    [DataField]
    public string? StationName;

    /// <summary>Real time (UTC) it was written; shown with the in-game year.</summary>
    [DataField]
    public DateTime Time;

    /// <summary>Round time it was written.</summary>
    [DataField]
    public TimeSpan RoundTime;
}

/// <summary>
/// A person's record histories, one book per console. Stored as its own entry in the station records.
/// </summary>
[Serializable, NetSerializable, DataRecord]
public sealed partial record RecordLogs
{
    [DataField]
    public uint NextId = 1;

    [DataField]
    public List<RecordLogEntry> Security = new();

    [DataField]
    public List<RecordLogEntry> Psionics = new();

    [DataField]
    public List<RecordLogEntry> Crew = new();

    public List<RecordLogEntry> Book(RecordLogBook book)
    {
        return book switch
        {
            RecordLogBook.Security => Security,
            RecordLogBook.Psionics => Psionics,
            _ => Crew,
        };
    }
}

/// <summary>
/// Notes typed into a record's history: one paragraph of limited length, so entries stay readable.
/// </summary>
public static class RecordLogNotes
{
    public const int MaxLength = 300;

    /// <summary>
    /// Trims and collapses every run of whitespace, line breaks included, into one space.
    /// </summary>
    public static string Sanitize(string text)
    {
        var result = new System.Text.StringBuilder(text.Length);
        var space = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                space = result.Length > 0;
                continue;
            }

            if (space)
                result.Append(' ');
            space = false;
            result.Append(c);
        }

        return result.ToString();
    }
}

/// <summary>
/// Add a note to the selected record's history on this console.
/// </summary>
[Serializable, NetSerializable]
public sealed class RecordLogAddNoteMessage(string text) : BoundUserInterfaceMessage
{
    public readonly string Text = text;
}

/// <summary>
/// Print the selected record's history on this console.
/// </summary>
[Serializable, NetSerializable]
public sealed class RecordLogPrintMessage : BoundUserInterfaceMessage;

/// <summary>
/// Delete an entry from the selected record's history on this console. Background entries can't be deleted.
/// </summary>
[Serializable, NetSerializable]
public sealed class RecordLogDeleteMessage(uint entryId) : BoundUserInterfaceMessage
{
    public readonly uint EntryId = entryId;
}
