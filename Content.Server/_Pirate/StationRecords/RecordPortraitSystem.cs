// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Server._Pirate.Photo;
using Content.Server.Hands.Systems;
using Content.Server.Popups;
using Content.Server.StationRecords.Systems;
using System.Text;
using Content.Shared._Pirate;
using Content.Shared._Pirate.StationRecords;
using Content.Shared.GameTicking;
using Content.Shared.Access.Systems;
using Robust.Shared.Network;
using Content.Shared.CriminalRecords;
using Content.Shared.Hands.Components;
using Content.Shared.Preferences;
using Content.Shared.StationRecords;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.StationRecords;

/// <summary>
/// Record photos for the records consoles (criminal, crew, psionics). A person's photo is stored on their criminal
/// record, so every console reads and writes the same one. Without an uploaded photo it is a bare render of the
/// character, and each console composes its own mugshot (background + placard) from it client-side.
/// </summary>
public sealed class RecordPortraitSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly PhotoSystem _photo = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly StationRecordsSystem _records = default!;

    private static readonly SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");
    private static readonly SoundSpecifier UploadSound = new SoundPathSpecifier("/Audio/_Pirate/Machines/terminal_prompt_confirm.ogg");
    private static readonly TimeSpan PrintDelay = TimeSpan.FromSeconds(2.3f);
    // 6 digits: with ~200 people a hash clash is rare (~2%), so nearly everyone keeps the same number every round.
    private const int MaxNumber = 999999;

    /// <summary>
    /// Consoles with a photo in the printer.
    /// </summary>
    private readonly HashSet<EntityUid> _activePrintJobs = new();

    /// <summary>
    /// Arrival photos asked of a player's client: record -> who may answer.
    /// </summary>
    private readonly Dictionary<(EntityUid Station, uint Id), NetUserId> _pendingRenders = new();

    public override void Initialize()
    {
        base.Initialize();

        // After the records exist: StationRecordsSystem creates them (and the criminal record) on this event.
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn, after: [typeof(StationRecordsSystem)]);
        SubscribeNetworkEvent<RecordPortraitRenderResultEvent>(OnRenderResult);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _pendingRenders.Clear());
    }

    /// <summary>
    /// The photo is taken when the character arrives: number and placard frozen now, and the player's own client
    /// renders their character (the server can't draw). Consoles still render one on first view if it never comes.
    /// </summary>
    private void OnPlayerSpawn(PlayerSpawnCompleteEvent args)
    {
        // The key is on the ID card itself, which usually sits inside the PDA in the ID slot.
        if (!_idCard.TryFindIdCard(args.Mob, out var idCard)
            || !TryComp<StationRecordKeyStorageComponent>(idCard, out var keyStorage)
            || keyStorage.Key is not { } key
            || !_records.TryGetRecord<CriminalRecord>(key, out var record))
        {
            return;
        }

        EnsureNumber(key, record);
        FreezePlacard(key, record);

        if (record.PortraitImageData is { Length: > 0 } || record.PortraitProfileSnapshot == null)
            return;

        _records.TryGetRecord<GeneralStationRecord>(key, out var general);
        _pendingRenders[(key.OriginStation, key.Id)] = args.Player.UserId;
        RaiseNetworkEvent(new RecordPortraitRenderRequestEvent
        {
            Station = GetNetEntity(key.OriginStation),
            RecordId = key.Id,
            Profile = record.PortraitProfileSnapshot,
            JobPrototype = general?.JobPrototype,
        }, args.Player);
    }

    private void OnRenderResult(RecordPortraitRenderResultEvent ev, EntitySessionEventArgs args)
    {
        var station = GetEntity(ev.Station);

        // Only the player it was asked of, once.
        if (!_pendingRenders.TryGetValue((station, ev.RecordId), out var user) || user != args.SenderSession.UserId)
            return;

        _pendingRenders.Remove((station, ev.RecordId));
        StoreGenerated(new StationRecordKey(ev.RecordId, station), ev.ImageData);
    }

    /// <summary>
    /// Fixes the placard's name and birth year as they are now, if not done yet. Later renames or age edits on
    /// the record don't touch the photo.
    /// </summary>
    public void FreezePlacard(StationRecordKey key, CriminalRecord record)
    {
        if (record.PortraitName != null)
            return;

        var general = _records.TryGetRecord<GeneralStationRecord>(key, out var stationRecord)
            ? stationRecord
            : record.GeneralRecordSnapshot;

        if (general == null)
            return;

        record.PortraitName = general.Name;
        record.PortraitBirthYear = general.Age > 0 ? PirateStationCalendar.CurrentYear - general.Age : 0;
    }

    /// <summary>
    /// The record's photo for a console state, with its placard number assigned.
    /// </summary>
    public RecordPortraitData? GetPortrait(StationRecordKey key)
    {
        if (!_records.TryGetRecord<CriminalRecord>(key, out var record))
            return null;

        EnsureNumber(key, record);
        return RecordPortraitData.FromRecord(record);
    }

    /// <summary>
    /// Gives the record its mugshot number if it has none: a hash of DNA + fingerprint, moved to the next free
    /// number if someone on the station already has it. Assigned once, so it stays put if forensics are edited.
    /// </summary>
    public void EnsureNumber(StationRecordKey key, CriminalRecord? record = null)
    {
        if (record == null && !_records.TryGetRecord(key, out record))
            return;

        if (record.PortraitNumber != 0)
            return;

        var general = _records.TryGetRecord<GeneralStationRecord>(key, out var stationRecord)
            ? stationRecord
            : record.GeneralRecordSnapshot;

        var taken = new HashSet<int>();
        foreach (var (_, other) in _records.GetRecordsOfType<CriminalRecord>(key.OriginStation))
        {
            if (other.PortraitNumber != 0)
                taken.Add(other.PortraitNumber);
        }

        var number = (int) (Fnv1a($"{general?.DNA}|{general?.Fingerprint}") % MaxNumber) + 1;
        for (var i = 0; i < MaxNumber && taken.Contains(number); i++)
        {
            number = number % MaxNumber + 1;
        }

        record.PortraitNumber = number;
    }

    /// <summary>
    /// Prints the record's photo at <paramref name="console"/>: an uploaded photo as it is, otherwise
    /// <paramref name="renderedImage"/>, the console's mugshot of the character.
    /// </summary>
    public void Print(EntityUid console, EntityUid user, StationRecordKey key, byte[]? renderedImage)
    {
        if (_activePrintJobs.Contains(console))
        {
            _popup.PopupEntity(Loc.GetString("records-console-photo-print-busy"), console, user);
            return;
        }

        if (!_records.TryGetRecord<CriminalRecord>(key, out var record))
            return;

        byte[]? imageData = null;
        byte[]? previewData = null;

        if (record.PortraitImageData is { Length: > 0 } existingImage && !record.PortraitIsRender)
        {
            // An uploaded photo prints as it is.
            if (_photo.TryPreparePhotoCardData(existingImage, record.PortraitPreviewData, out var preparedImage, out var preparedPreview))
            {
                imageData = preparedImage;
                previewData = preparedPreview;

                if (!ByteArraysEqual(record.PortraitImageData, preparedImage) ||
                    !ByteArraysEqual(record.PortraitPreviewData, preparedPreview))
                {
                    record.PortraitImageData = [.. preparedImage];
                    record.PortraitPreviewData = preparedPreview == null ? null : [.. preparedPreview];
                    _records.Synchronize(key);
                }
            }
        }
        else if (renderedImage is { Length: > 0 }
                 && _photo.TryPreparePhotoCardData(renderedImage, null, out var mugshot, out var mugshotPreview))
        {
            // The console's mugshot of the stored render. Not stored: each console has its own look.
            imageData = mugshot;
            previewData = mugshotPreview;
        }
        else if (record.PortraitImageData is { Length: > 0 } render
                 && _photo.TryPreparePhotoCardData(render, record.PortraitPreviewData, out var bareRender, out var bareRenderPreview))
        {
            // No mugshot from the console; the bare render beats printing nothing.
            imageData = bareRender;
            previewData = bareRenderPreview;
        }

        if (imageData is not { Length: > 0 })
            return;

        var printedName = _records.TryGetRecord<GeneralStationRecord>(key, out var stationRecord)
            ? stationRecord.Name
            : record.GeneralRecordSnapshot?.Name;

        _activePrintJobs.Add(console);
        _audio.PlayPvs(PrintSound, console);
        _popup.PopupEntity(Loc.GetString("records-console-photo-print-start"), console, user);

        Timer.Spawn(PrintDelay, () =>
        {
            _activePrintJobs.Remove(console);

            if (TerminatingOrDeleted(console))
                return;

            var printed = Spawn("PhotoCard", Transform(console).Coordinates);
            if (!TryComp<PhotoCardComponent>(printed, out var photo))
            {
                Del(printed);
                return;
            }

            _photo.TrySetPhotoCardData(printed, photo, imageData, previewData, customName: printedName);

            if (!TerminatingOrDeleted(user))
                _popup.PopupEntity(Loc.GetString("records-console-photo-print-complete"), printed, user);
        });
    }

    /// <summary>
    /// Stores the photo card <paramref name="user"/> is holding as the record's photo.
    /// </summary>
    public bool Upload(EntityUid console, EntityUid user, StationRecordKey key)
    {
        if (!TryGetHeldPhotoCard(user, out var heldPhoto)
            || heldPhoto.ImageData is not { Length: > 0 } imageData
            || !_records.TryGetRecord<CriminalRecord>(key, out var record)
            || !_photo.TryPreparePhotoCardData(imageData, heldPhoto.PreviewData, out var preparedImage, out var preparedPreview))
        {
            return false;
        }

        record.PortraitImageData = [.. preparedImage];
        record.PortraitPreviewData = preparedPreview == null ? null : [.. preparedPreview];
        record.PortraitIsRender = false;

        _audio.PlayPvs(UploadSound, console);
        _records.Synchronize(key);
        return true;
    }

    /// <summary>
    /// Stores a client's bare render of the record's character, if it has no photo yet. Consoles compose their
    /// mugshots from it.
    /// </summary>
    public bool StoreGenerated(StationRecordKey key, byte[] imageData)
    {
        if (!_records.TryGetRecord<CriminalRecord>(key, out var record)
            || record.PortraitProfileSnapshot == null
            || record.PortraitImageData is { Length: > 0 }
            || !_photo.TryPreparePhotoCardData(imageData, null, out var preparedImage, out var preparedPreview))
        {
            return false;
        }

        record.PortraitImageData = [.. preparedImage];
        record.PortraitPreviewData = preparedPreview == null ? null : [.. preparedPreview];
        record.PortraitIsRender = true;
        EnsureNumber(key, record);
        FreezePlacard(key, record);
        _records.Synchronize(key);
        return true;
    }

    public void CancelPrintJob(EntityUid console)
    {
        _activePrintJobs.Remove(console);
    }

    private bool TryGetHeldPhotoCard(EntityUid user, [NotNullWhen(true)] out PhotoCardComponent? photo)
    {
        photo = null;

        if (!TryComp<HandsComponent>(user, out var hands))
            return false;

        foreach (var held in _hands.EnumerateHeld((user, hands)))
        {
            if (TryComp(held, out photo))
                return true;
        }

        return false;
    }

    private static uint Fnv1a(string text)
    {
        var hash = 2166136261u;
        foreach (var b in Encoding.UTF8.GetBytes(text))
        {
            hash = unchecked((hash ^ b) * 16777619u);
        }

        return hash;
    }

    private static bool ByteArraysEqual(byte[]? left, byte[]? right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left == null || right == null || left.Length != right.Length)
            return false;

        return left.AsSpan().SequenceEqual(right);
    }
}
