// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Pirate.StationRecords;
using Content.Shared.StationRecords;

namespace Content.Client.StationRecords;

/// <summary>
/// List selection for the crew records console (see <see cref="RecordSelectionController{TState}"/>) and its record photo.
/// </summary>
public sealed partial class GeneralStationRecordConsoleWindow
{
    private RecordSelectionController<GeneralStationRecordConsoleState> _selection = default!;

    private void InitRecordSelection()
    {
        _selection = new RecordSelectionController<GeneralStationRecordConsoleState>(RecordListing);
        _selection.SelectionRequested += key => OnKeySelected?.Invoke(key);
        _selection.ShowCached += (cached, last) =>
            // No filter: it would overwrite whatever is being typed into the search box.
            UpdateState(new GeneralStationRecordConsoleState(
                cached.SelectedKey,
                cached.Record,
                last.RecordListing,
                null,
                last.CanDeleteEntries,
                last.MaxStringLength)
            {
                Portrait = cached.Portrait,
            });
    }

    public event Action<string>? OnAddLogNote;
    public event Action<uint>? OnDeleteLogEntry;
    public event Action? OnPrintLog;
    private RecordTabs _tabs = default!;

    /// <summary>
    /// Profile / History switch; the history is this console's log of the person.
    /// </summary>
    private void InitTabs()
    {
        _tabs = new RecordTabs(ProfileTabButton, HistoryTabButton, RecordContainer, HistoryView);
        _tabs.BindSearch(StationRecordsFilterType, StationRecordsFiltersValue, HistoryView);
        HistoryView.OnAddNote += text => OnAddLogNote?.Invoke(text);
        HistoryView.OnDeleteEntry += id => OnDeleteLogEntry?.Invoke(id);
        HistoryView.OnPrint += () => OnPrintLog?.Invoke();
    }

    public event Action<uint, byte[]?>? OnPrintPortrait;
    public event Action? OnUploadPortrait;
    public event Action<uint, byte[]>? OnStoreGeneratedPortrait;

    private void ShowRecordPortrait(GeneralStationRecordConsoleState state)
    {
        if (state is not { SelectedKey: { } key, Record: { } record }
            || RecordContainer.ChildCount == 0
            || RecordContainer.GetChild(0) is not GeneralRecord panel)
        {
            return;
        }

        panel.SetPortrait(key, state.Portrait, record, _access && _canManageRecords);
    }

    /// <summary>
    /// False for a state that predates a record the user already clicked; only the list is refreshed from it.
    /// </summary>
    private bool AcceptServerState(GeneralStationRecordConsoleState state)
    {
        if (_selection.AcceptServerState(state, state.SelectedKey, state.Record != null))
            return true;

        if (state.RecordListing != null)
            _selection.SetListing(state.RecordListing);

        return false;
    }
}
