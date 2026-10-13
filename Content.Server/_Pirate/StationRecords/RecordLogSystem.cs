// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Text;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared._Pirate;
using Content.Shared._Pirate.StationRecords;
using Content.Shared.Access.Systems;
using Content.Shared.CCVar;
using Content.Shared.IdentityManagement;
using Content.Shared.Paper;
using Content.Shared.StationRecords;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Pirate.StationRecords;

/// <summary>
/// The History tab of the records consoles: per-person logs, one book per console (sec, psi, crew).
/// Status changes are logged by the consoles, notes are typed at them, and the HR background note
/// (from character creation) is read-only.
/// </summary>
public sealed class RecordLogSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly StationRecordsSystem _records = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private static readonly SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");
    private static readonly TimeSpan PrintDelay = TimeSpan.FromSeconds(2.3f);
    private const string PrintPaper = "PaperOffice";

    /// <summary>Leaves room under the paper's 10000 characters for the "more entries" line.</summary>
    private const int PrintBudget = 9000;

    private readonly HashSet<EntityUid> _activePrintJobs = new();

    /// <summary>Zone of the times on printed histories, from <see cref="CCVars.RecordsPrintTimeZone"/>.</summary>
    private TimeZoneInfo _printZone = TimeZoneInfo.Local;

    /// <summary><see cref="CCVars.RecordsHistoryPersistence"/>: off hides entries restored from earlier rounds.</summary>
    private bool _showArchived = true;

    private void SetPrintZone(string id)
    {
        _printZone = TimeZoneInfo.Local;
        if (string.IsNullOrWhiteSpace(id))
            return;

        // Older tz databases only know Kyiv by its old spelling.
        foreach (var candidate in new[] { id.Trim(), id.Trim().Replace("Kyiv", "Kiev") })
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(candidate, out var zone))
            {
                _printZone = zone;
                return;
            }
        }

        Log.Warning($"Unknown time zone '{id}' in {CCVars.RecordsPrintTimeZone.Name}, printing record histories in the server's zone ({TimeZoneInfo.Local.Id}).");
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AfterGeneralRecordCreatedEvent>(OnGeneralRecordCreated);
        Subs.CVar(_cfg, CCVars.RecordsPrintTimeZone, SetPrintZone, true);
        Subs.CVar(_cfg, CCVars.RecordsHistoryPersistence, enabled => _showArchived = enabled, true);
    }

    /// <summary>
    /// The character's OOC records background from the profile editor: each entry becomes a read-only HR file
    /// entry in its console's history, in the profile's order.
    /// </summary>
    private void OnGeneralRecordCreated(AfterGeneralRecordCreatedEvent args)
    {
        foreach (var background in args.Profile.RecordBackgrounds)
        {
            AddBackground(args.Key, background);
        }
    }

    private void AddBackground(StationRecordKey key, RecordBackgroundEntry background)
    {
        var text = background.Body.Trim();
        if (text.Length == 0)
            return;

        var title = background.Title.Trim();
        if (Add(key, background.Book, RecordLogKind.Background, title.Length > 0 ? title : null, text, null, null)
            is not { } entry)
        {
            return;
        }

        // An empty author shows NanoTrasen HR.
        var author = background.Author.Trim();
        entry.AuthorName = author.Length > 0 ? author : null;

        // Dated to the shift start, also for late joiners: it opens this round's part of the history.
        entry.Time = DateTime.UtcNow - _ticker.RoundDuration();
        entry.RoundTime = TimeSpan.Zero;
    }

    /// <summary>
    /// A copy of one book for a console state, in <see cref="RecordLogOrder"/>. Entries from earlier rounds are
    /// left out while history persistence is off.
    /// </summary>
    public List<RecordLogEntry> GetLog(StationRecordKey key, RecordLogBook book)
    {
        if (!_records.TryGetRecord<RecordLogs>(key, out var logs))
            return new List<RecordLogEntry>();

        return RecordLogOrder.ForDisplay(_showArchived
            ? logs.Book(book)
            : logs.Book(book).Where(e => !e.Archived));
    }

    /// <summary>
    /// Adds entries restored from earlier rounds, with fresh ids.
    /// </summary>
    public void AddArchived(StationRecordKey key, IEnumerable<(RecordLogBook Book, RecordLogEntry Entry)> entries)
    {
        if (!key.IsValid())
            return;

        var logs = EnsureLogs(key);
        foreach (var (book, entry) in entries)
        {
            logs.Book(book).Add(entry with { Id = logs.NextId++, Archived = true });
        }
    }

    /// <summary>
    /// Adds an entry written by <paramref name="author"/> (their ID card's name and job, else who they look like)
    /// at <paramref name="origin"/> (the console, or the author for examine menus) - its station is noted.
    /// </summary>
    public RecordLogEntry? Add(StationRecordKey key, RecordLogBook book, RecordLogKind kind, string? topic,
        string message, EntityUid? author, EntityUid? origin, string? status = null)
    {
        if (!key.IsValid())
            return null;

        var logs = EnsureLogs(key);

        string? authorName = null;
        string? authorTitle = null;
        if (author is { } user)
        {
            if (_idCard.TryFindIdCard(user, out var idCard))
            {
                authorName = idCard.Comp.FullName;
                authorTitle = idCard.Comp.LocalizedJobTitle;
            }

            if (string.IsNullOrWhiteSpace(authorName))
                authorName = Identity.Name(user, EntityManager);
        }

        var entry = new RecordLogEntry
        {
            Id = logs.NextId++,
            Kind = kind,
            Topic = topic,
            Status = status,
            Message = message,
            AuthorName = authorName,
            AuthorTitle = string.IsNullOrWhiteSpace(authorTitle) ? null : authorTitle,
            StationName = origin is { } at && _station.GetOwningStation(at) is { } station ? Name(station) : null,
            Time = DateTime.UtcNow,
            RoundTime = _ticker.RoundDuration(),
        };

        logs.Book(book).Add(entry);
        return entry;
    }

    /// <summary>
    /// Deletes an entry; background entries are read-only.
    /// </summary>
    public bool TryDelete(StationRecordKey key, RecordLogBook book, uint entryId, out RecordLogEntry? removed)
    {
        removed = null;
        if (!_records.TryGetRecord<RecordLogs>(key, out var logs))
            return false;

        var entries = logs.Book(book);
        var index = entries.FindIndex(e => e.Id == entryId);
        if (index < 0 || entries[index].Kind == RecordLogKind.Background)
            return false;

        removed = entries[index];
        entries.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Prints one book of the person's history at <paramref name="console"/>, newest first. Paper can't follow
    /// each reader's time zone like the History tab does, so times are printed in <see cref="CCVars.RecordsPrintTimeZone"/>.
    /// </summary>
    public void Print(EntityUid console, EntityUid user, StationRecordKey key, RecordLogBook book)
    {
        if (_activePrintJobs.Contains(console))
        {
            _popup.PopupEntity(Loc.GetString("records-log-print-busy"), console, user);
            return;
        }

        if (!_records.TryGetRecord<GeneralStationRecord>(key, out var person))
            return;

        var name = person.Name;
        var text = BuildPrintout(name, GetLog(key, book), book);

        _activePrintJobs.Add(console);
        _audio.PlayPvs(PrintSound, console);
        _popup.PopupEntity(Loc.GetString("records-log-print-start"), console, user);

        Timer.Spawn(PrintDelay, () =>
        {
            _activePrintJobs.Remove(console);
            if (TerminatingOrDeleted(console))
                return;

            var printed = Spawn(PrintPaper, Transform(console).Coordinates);
            if (!TryComp<PaperComponent>(printed, out var paper))
            {
                Del(printed);
                return;
            }

            _metaData.SetEntityName(printed, Loc.GetString("records-log-print-paper-name", ("name", name)));
            _paper.SetContent((printed, paper), text);
        });
    }

    private string BuildPrintout(string name, List<RecordLogEntry> log, RecordLogBook book)
    {
        var title = Loc.GetString($"records-log-print-title-{book.ToString().ToLowerInvariant()}");
        var text = new StringBuilder();
        text.AppendLine($"[head=2]{FormattedMessage.EscapeText($"{title} - {name}")}[/head]");
        text.AppendLine("────────────────────");

        if (log.Count == 0)
            text.AppendLine(Loc.GetString("records-log-empty"));

        // Already in display order (GetLog): HR background, then newest first.
        for (var i = 0; i < log.Count; i++)
        {
            var entry = PrintEntry(log[i]);
            if (text.Length + entry.Length > PrintBudget)
            {
                text.AppendLine(Loc.GetString("records-log-print-more", ("count", log.Count - i)));
                break;
            }

            text.Append(entry);
        }

        return text.ToString();
    }

    private string PrintEntry(RecordLogEntry entry)
    {
        var title = entry.Kind switch
        {
            RecordLogKind.Background => entry.Topic ?? Loc.GetString("records-log-kind-background"),
            RecordLogKind.StatusChange => entry.Topic ?? Loc.GetString("records-log-kind-status-change"),
            _ => Loc.GetString("records-log-kind-note"),
        };

        var author = entry.AuthorName
            ?? Loc.GetString(entry.Kind == RecordLogKind.Background ? "records-log-hr-author" : "records-log-unknown-author");
        if (!string.IsNullOrWhiteSpace(entry.AuthorTitle))
            author = $"{author} ({entry.AuthorTitle})";

        var text = new StringBuilder();
        // The HR file's time (the shift start) only places it in the history, so it isn't printed.
        text.AppendLine(entry.Kind == RecordLogKind.Background
            ? $"[bold]{FormattedMessage.EscapeText(title)}[/bold]"
            : $"[bold]{FormattedMessage.EscapeText(title)}[/bold] ({FormatTime(entry.Time)})");
        text.AppendLine($"[italic]{FormattedMessage.EscapeText(author)}[/italic]");
        if (!string.IsNullOrWhiteSpace(entry.StationName))
            text.AppendLine(Loc.GetString("records-log-station", ("station", FormattedMessage.EscapeText(entry.StationName))));
        if (!string.IsNullOrWhiteSpace(entry.Message))
            text.AppendLine(FormattedMessage.EscapeText(entry.Message));
        text.AppendLine();
        return text.ToString();
    }

    /// <summary>
    /// The History tab's format in the print time zone: 12/10/2468 14:05, real date and time with the in-game year.
    /// </summary>
    private string FormatTime(DateTime utc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _printZone);
        return $"{local.Day:00}/{local.Month:00}/{PirateStationCalendar.CurrentYear:0000} {local.Hour:00}:{local.Minute:00}";
    }

    private RecordLogs EnsureLogs(StationRecordKey key)
    {
        if (_records.TryGetRecord<RecordLogs>(key, out var logs))
            return logs;

        logs = new RecordLogs();
        _records.AddRecordEntry(key, logs);
        return logs;
    }
}
