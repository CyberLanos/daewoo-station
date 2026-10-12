// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.CriminalRecords;
using Content.Shared.Preferences;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.StationRecords;

/// <summary>
/// A person's record photo as the records consoles need it. It lives on their <see cref="CriminalRecord"/>;
/// the crew and psionics consoles get it through this, the criminal records console reads the record directly.
/// </summary>
[Serializable, NetSerializable]
public sealed class RecordPortraitData
{
    /// <summary>
    /// Stored image: an uploaded photo, or a bare character render when <see cref="IsRender"/>.
    /// </summary>
    public byte[]? ImageData;

    public bool IsRender;

    /// <summary>
    /// Character the render is made from when there is no stored image yet.
    /// </summary>
    public HumanoidCharacterProfile? Snapshot;

    /// <summary>
    /// Mugshot placard number, unique per station.
    /// </summary>
    public int Number;

    /// <summary>
    /// Placard name and birth year as of when the photo was taken; null / 0 until then.
    /// </summary>
    public string? Name;

    public int BirthYear;

    public static RecordPortraitData FromRecord(CriminalRecord record)
    {
        return new RecordPortraitData
        {
            ImageData = record.PortraitImageData,
            IsRender = record.PortraitIsRender,
            Snapshot = record.PortraitProfileSnapshot,
            Number = record.PortraitNumber,
            Name = record.PortraitName,
            BirthYear = record.PortraitBirthYear,
        };
    }
}
