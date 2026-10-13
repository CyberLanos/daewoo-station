using Content.Shared.Psionics;
using Content.Shared.StationRecords;
using Robust.Shared.Serialization;

namespace Content.Shared.PsionicsRecords;

[Serializable, NetSerializable]
public enum PsionicsRecordsConsoleKey : byte
{
    Key
}
[Serializable, NetSerializable]
public sealed class PsionicsRecordsConsoleState : BoundUserInterfaceState
{
    public uint? SelectedKey = null;

    public PsionicsRecord? PsionicsRecord = null;
    public GeneralStationRecord? StationRecord = null;

    /// <summary>
    /// The selected person's photo, stored on their criminal record.
    /// </summary>
    public Content.Shared._Pirate.StationRecords.RecordPortraitData? Portrait;

    /// <summary>
    /// The History tab: this console's log of the selected person.
    /// </summary>
    public List<Content.Shared._Pirate.StationRecords.RecordLogEntry>? Log;
    public readonly Dictionary<uint, string>? RecordListing;
    public readonly StationRecordsFilter? Filter;

    /// <summary>
    /// The status the listing is filtered by; None lists everyone.
    /// </summary>
    public PsionicsStatus FilterStatus = PsionicsStatus.None;

    public PsionicsRecordsConsoleState(Dictionary<uint, string>? recordListing, StationRecordsFilter? newFilter)
    {
        RecordListing = recordListing;
        Filter = newFilter;
    }
    public PsionicsRecordsConsoleState() : this(null, null)
    {
    }

    public bool IsEmpty() => SelectedKey == null && StationRecord == null && PsionicsRecord == null && RecordListing == null;
}
[Serializable, NetSerializable]
public sealed class PsionicsRecordChangeStatus : BoundUserInterfaceMessage
{
    public readonly PsionicsStatus Status;
    public readonly string? Reason;

    public PsionicsRecordChangeStatus(PsionicsStatus status, string? reason)
    {
        Status = status;
        Reason = reason;
    }
}

/// <summary>
/// Only list people with this psionics status on the console; None lists everyone.
/// </summary>
[Serializable, NetSerializable]
public sealed class PsionicsRecordSetStatusFilter(PsionicsStatus filterStatus) : BoundUserInterfaceMessage
{
    public readonly PsionicsStatus FilterStatus = filterStatus;
}

/// <summary>
/// Request from the client to populate the psionics ID-examine menu state.
/// </summary>
[Serializable, NetSerializable]
public sealed class PsionicsMenuRequestState : BoundUserInterfaceMessage
{
}
