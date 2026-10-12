// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Preferences;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.StationRecords;

// Record photo messages for the crew and psionics records consoles. The criminal records console predates these
// and keeps its own (CriminalRecordPrintPhoto etc.); all of them end up in the server's RecordPortraitSystem.

/// <summary>
/// Print the selected record's photo. <see cref="ImageData"/> is the client's render, used only when the record has
/// no stored photo yet.
/// </summary>
[Serializable, NetSerializable]
public sealed class RecordPortraitPrintMessage : BoundUserInterfaceMessage
{
    public readonly uint RecordKey;
    public readonly byte[]? ImageData;

    public RecordPortraitPrintMessage(uint recordKey, byte[]? imageData)
    {
        RecordKey = recordKey;
        ImageData = imageData == null ? null : (byte[]) imageData.Clone();
    }
}

/// <summary>
/// Store the photo card the user holds as the selected record's photo.
/// </summary>
[Serializable, NetSerializable]
public sealed class RecordPortraitUploadMessage : BoundUserInterfaceMessage;

/// <summary>
/// Server to the arriving player's client: render your character for your new record's photo.
/// The server can't draw; the reply is <see cref="RecordPortraitRenderResultEvent"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class RecordPortraitRenderRequestEvent : EntityEventArgs
{
    public NetEntity Station;
    public uint RecordId;
    public HumanoidCharacterProfile Profile = default!;
    public string? JobPrototype;
}

/// <summary>
/// The bare render asked for by <see cref="RecordPortraitRenderRequestEvent"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class RecordPortraitRenderResultEvent : EntityEventArgs
{
    public NetEntity Station;
    public uint RecordId;
    public byte[] ImageData = [];
}

/// <summary>
/// Store the client's render of a record's character as its photo, if it has none yet.
/// </summary>
[Serializable, NetSerializable]
public sealed class RecordPortraitStoreGeneratedMessage : BoundUserInterfaceMessage
{
    public readonly uint RecordKey;
    public readonly byte[] ImageData;

    public RecordPortraitStoreGeneratedMessage(uint recordKey, byte[] imageData)
    {
        RecordKey = recordKey;
        ImageData = (byte[]) imageData.Clone();
    }
}
