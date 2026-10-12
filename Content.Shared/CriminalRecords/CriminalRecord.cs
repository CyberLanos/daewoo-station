// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Security;
using Content.Shared.StationRecords; // Pirate: records photos
using Robust.Shared.Serialization;

namespace Content.Shared.CriminalRecords;

/// <summary>
/// Criminal record for a crewmember.
/// Can be viewed and edited in a criminal records console by security.
/// </summary>
[Serializable, NetSerializable, DataRecord]
public sealed partial record CriminalRecord
{
    /// <summary>
    /// Status of the person (None, Wanted, Detained).
    /// </summary>
    [DataField]
    public SecurityStatus Status = SecurityStatus.None;

    /// <summary>
    /// When Status is Wanted, the reason for it.
    /// Should never be set otherwise.
    /// </summary>
    [DataField]
    public string? Reason;

    /// <summary>
    /// The name of the person who changed the status.
    /// </summary>
    [DataField]
    public string? InitiatorName;

    #region Pirate: records photos
    /// <summary>
    /// Snapshot of the linked general record data.
    /// Used when a criminal record temporarily exists without a general record entry.
    /// </summary>
    [DataField]
    public GeneralStationRecord? GeneralRecordSnapshot;
    #endregion

    #region Pirate: records select
    /// <summary>
    /// <see cref="PortraitImageData"/> is a bare render of the character (transparent background), not a photo:
    /// each records console builds its own mugshot from it.
    /// </summary>
    [DataField]
    public bool PortraitIsRender;

    /// <summary>
    /// Number on the mugshot placard, unique per station. 0 until the server assigns it.
    /// </summary>
    [DataField]
    public int PortraitNumber;

    /// <summary>
    /// Name on the mugshot placard, frozen when the photo is taken (on arrival) so renames don't change it.
    /// </summary>
    [DataField]
    public string? PortraitName;

    /// <summary>
    /// Birth year on the mugshot placard, frozen with <see cref="PortraitName"/>. 0 if unknown.
    /// </summary>
    [DataField]
    public int PortraitBirthYear;
    #endregion

    /// <summary>
    /// Criminal history of the person.
    /// This should have charges and time served added after someone is detained.
    /// </summary>
    [DataField]
    public List<CrimeHistory> History = new();
}

/// <summary>
/// A line of criminal activity and the time it was added at.
/// </summary>
[Serializable, NetSerializable]
public record struct CrimeHistory(TimeSpan AddTime, string Crime, string? InitiatorName);
