// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Pirate.StationRecords;

/// <summary>
/// Profile / History switch of the records consoles: two mutually exclusive buttons, one view at a time.
/// The choice sticks while switching records. With <see cref="BindSearch"/>, the History tab turns the console's
/// search bar into a search through the history: the field filter is disabled and a separate box takes the record
/// search's place, so the record search keeps its text for when the Profile tab comes back.
/// </summary>
public sealed class RecordTabs
{
    private readonly Button _profileButton;
    private readonly Button _historyButton;
    private readonly Control _profileView;
    private readonly Control _historyView;

    private OptionButton? _filterType;
    private LineEdit? _recordSearch;
    private LineEdit? _historySearch;
    private RecordHistoryPanel? _history;
    private Control[] _recordOnly = [];
    private bool _hasRecord = true;
    private bool _searchingHistory;

    public bool ShowingHistory { get; private set; }

    public RecordTabs(Button profileButton, Button historyButton, Control profileView, Control historyView)
    {
        _profileButton = profileButton;
        _historyButton = historyButton;
        _profileView = profileView;
        _historyView = historyView;

        var group = new ButtonGroup();
        profileButton.Group = group;
        historyButton.Group = group;
        profileButton.OnPressed += _ => Show(history: false);
        historyButton.OnPressed += _ => Show(history: true);
        Show(history: false);
    }

    /// <summary>
    /// Makes the search bar search the history while the History tab is shown. <paramref name="recordOnly"/> are
    /// hidden meanwhile (the delete / create record buttons).
    /// </summary>
    public void BindSearch(OptionButton filterType, LineEdit recordSearch, RecordHistoryPanel history, params Control[] recordOnly)
    {
        _filterType = filterType;
        _recordSearch = recordSearch;
        _history = history;
        _recordOnly = recordOnly;

        _historySearch = new LineEdit
        {
            PlaceHolder = Loc.GetString("records-log-search-placeholder"),
            HorizontalExpand = recordSearch.HorizontalExpand,
            Margin = recordSearch.Margin,
            Visible = false,
        };
        _historySearch.OnTextChanged += args => _history.SetQuery(args.Text);

        var parent = recordSearch.Parent!;
        parent.AddChild(_historySearch);
        _historySearch.SetPositionInParent(recordSearch.GetPositionInParent() + 1);

        UpdateSearch();
    }

    /// <summary>
    /// Whether a record is shown; without one the search bar always searches records.
    /// </summary>
    public void SetHasRecord(bool hasRecord)
    {
        _hasRecord = hasRecord;
        UpdateSearch();
    }

    /// <summary>
    /// Re-applies the current tab, for consoles that show or hide the views themselves.
    /// </summary>
    public void Refresh()
    {
        Show(ShowingHistory);
    }

    private void Show(bool history)
    {
        ShowingHistory = history;
        _profileButton.Pressed = !history;
        _historyButton.Pressed = history;
        _profileView.Visible = !history;
        _historyView.Visible = history;
        UpdateSearch();
    }

    private void UpdateSearch()
    {
        if (_historySearch == null || _recordSearch == null || _filterType == null)
            return;

        var searching = ShowingHistory && _hasRecord;
        if (searching == _searchingHistory)
            return;

        _searchingHistory = searching;
        _recordSearch.Visible = !searching;
        _historySearch.Visible = searching;
        _filterType.Disabled = searching;
        foreach (var control in _recordOnly)
        {
            control.Visible = !searching;
        }

        if (searching)
        {
            _historySearch.GrabKeyboardFocus();
            return;
        }

        _historySearch.Text = string.Empty;
        _history?.SetQuery(string.Empty);
    }
}
