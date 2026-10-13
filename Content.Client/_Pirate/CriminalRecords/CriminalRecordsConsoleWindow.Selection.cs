// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Pirate.StationRecords;
using Content.Shared.CriminalRecords;
using Content.Shared.StationRecords;
using Robust.Shared.Enums;

namespace Content.Client.CriminalRecords;

/// <summary>
/// Record switching on the criminal records console is a full server round trip (the client only ever has the
/// selected record), so list selection goes through <see cref="RecordSelectionController{TState}"/>; the photo is the
/// shared <see cref="RecordPortraitPanel"/>.
/// </summary>
public sealed partial class CriminalRecordsConsoleWindow
{
    private RecordSelectionController<CriminalRecordsConsoleState> _selection = default!;
    private RecordFieldWidths _fieldWidths = default!;

    private uint? _identityEditorKey;
    private (string Species, string Nationality, string Employer, Gender Gender, int Age, string? RawSpecies, string? RawNationality, string? RawEmployer, int RawAge) _identityEditorSource;

    private void InitRecordSelection()
    {
        _selection = new RecordSelectionController<CriminalRecordsConsoleState>(RecordListing);
        _selection.SelectionRequested += key => OnKeySelected?.Invoke(key);
        _selection.ShowCached += (cached, last) =>
            // No filter: it would overwrite whatever is being typed into the search box.
            UpdateState(new CriminalRecordsConsoleState(last.RecordListing, null)
            {
                SelectedKey = cached.SelectedKey,
                StationRecord = cached.StationRecord,
                CriminalRecord = cached.CriminalRecord,
                FilterStatus = last.FilterStatus,
            });
    }

    /// <summary>
    /// False for a state that predates a record the user already clicked; only the list is refreshed from it.
    /// </summary>
    private bool AcceptServerState(CriminalRecordsConsoleState state)
    {
        if (_selection.AcceptServerState(state, state.SelectedKey, state is { StationRecord: not null, CriminalRecord: not null }))
            return true;

        NoRecords.Visible = state.RecordListing == null || state.RecordListing.Count == 0;
        PopulateRecordListing(state.RecordListing);
        return false;
    }

    /// <summary>
    /// The identity selectors re-run every nationality / employer requirement check; skip that when the state
    /// carries the same identity for the same record (every unrelated record change sends a new state).
    /// </summary>
    private void RefreshRecordIdentityEditor(uint key, GeneralStationRecord stationRecord, CriminalRecord criminalRecord)
    {
        var profile = BuildRecordIdentityProfile(stationRecord, criminalRecord);
        var source = (profile.Species, profile.Nationality, profile.Employer, profile.Gender, profile.Age,
            stationRecord.Species, stationRecord.Nationality, stationRecord.Employer, stationRecord.Age);

        if (_identityEditorKey == key && _identityEditorSource == source)
            return;

        UpdateRecordIdentityProfile(profile, false, stationRecord);
        _identityEditorKey = key;
        _identityEditorSource = source;
    }

    public event Action<string>? OnAddLogNote;
    public event Action<uint>? OnDeleteLogEntry;
    public event Action? OnPrintLog;
    private RecordTabs _tabs = default!;

    /// <summary>
    /// Profile / History switch over the record; the history is this console's log of the person.
    /// </summary>
    private void InitTabs()
    {
        _tabs = new RecordTabs(ProfileTabButton, HistoryTabButton, ProfileView, HistoryView);
        _tabs.BindSearch(FilterType, FilterText, HistoryView);
        HistoryView.OnAddNote += text => OnAddLogNote?.Invoke(text);
        HistoryView.OnDeleteEntry += id => OnDeleteLogEntry?.Invoke(id);
        HistoryView.OnPrint += () => OnPrintLog?.Invoke();
    }

    /// <summary>
    /// Aligned label column; values fill the rest, at least the widest dropdown option plus 20px.
    /// </summary>
    private void InitFieldWidths()
    {
        _fieldWidths = new RecordFieldWidths(
            [SpeciesButton, NationalityButton, EmployerButton, GenderButton, StatusOptionButton],
            [AgeEdit],
            [PersonJob.Parent!]);
        _fieldWidths.Update();
    }
}
