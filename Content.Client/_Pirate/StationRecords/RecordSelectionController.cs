// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Pirate.StationRecords;

/// <summary>
/// Record list handling shared by the records consoles (criminal, crew, psionics).
/// Picking a record is a server round trip and the client only ever holds the selected record, so this:
/// <list type="bullet">
/// <item>works around ItemList's selection events - it raises a deselect for the old item before the select for the
/// new one on every switch, and treats a click on the highlighted item as a toggle-off. The consoles sent
/// "select nothing" for both, which is how a click could land on "select a record" (or, on crew records, jump to the
/// first record, since that server auto-selects one);</item>
/// <item>moves the highlight on click and keeps it there until the server catches up;</item>
/// <item>remembers each record's last server state, so a record seen before is shown without waiting.</item>
/// </list>
/// </summary>
public sealed class RecordSelectionController<TState> where TState : class
{
    private static readonly TimeSpan PendingSelectionTimeout = TimeSpan.FromSeconds(2);

    private readonly ItemList _list;
    private readonly IGameTiming _timing;

    /// <summary>
    /// Keys the user clicked that no server state has confirmed yet, oldest first.
    /// </summary>
    private readonly List<uint> _pendingSelections = new();
    private readonly Dictionary<uint, TState> _stateCache = new();
    private TimeSpan _lastSelectionRequest;
    private uint? _serverSelectedKey;
    private bool _syncingHighlight;

    /// <summary>
    /// Send the selection to the server.
    /// </summary>
    public event Action<uint>? SelectionRequested;

    /// <summary>
    /// A record with a cached state was clicked: render (cached state for that record, latest server state) locally.
    /// Runs with <see cref="ApplyingLocalState"/> set; the server's state replaces it a round trip later.
    /// </summary>
    public event Action<TState, TState>? ShowCached;

    public TState? LastServerState { get; private set; }

    public bool ApplyingLocalState { get; private set; }

    /// <summary>
    /// The record the user is heading to: their latest unconfirmed click, else whatever the server has selected.
    /// </summary>
    public uint? RequestedKey => _pendingSelections.Count > 0 ? _pendingSelections[^1] : _serverSelectedKey;

    public RecordSelectionController(ItemList list)
    {
        _list = list;
        _timing = IoCManager.Resolve<IGameTiming>();
        _list.OnItemSelected += OnItemSelected;
        _list.OnItemDeselected += OnItemDeselected;
    }

    /// <summary>
    /// Bookkeeping for a state from the server. Returns false when the state predates a record the user has
    /// already clicked: the caller should only refresh its list, and keep the panel on the clicked record instead
    /// of flicking back to the old one.
    /// </summary>
    public bool AcceptServerState(TState state, uint? selectedKey, bool hasRecord)
    {
        if (ApplyingLocalState)
            return true;

        LastServerState = state;
        if (selectedKey is { } selected && hasRecord)
            _stateCache[selected] = state;

        var serverChanged = selectedKey != _serverSelectedKey;
        _serverSelectedKey = selectedKey;

        if (_pendingSelections.Count == 0)
            return true;

        // Selection messages are handled in order, so a confirmed key also confirms every click before it.
        var confirmed = selectedKey is { } key ? _pendingSelections.IndexOf(key) : -1;
        if (confirmed >= 0)
            _pendingSelections.RemoveRange(0, confirmed + 1);
        else if (serverChanged || _timing.RealTime - _lastSelectionRequest > PendingSelectionTimeout)
            _pendingSelections.Clear(); // rejected (record gone), or someone else at this console picked one

        return _pendingSelections.Count == 0;
    }

    /// <summary>
    /// Diffs the list against <paramref name="listing"/> (instead of rebuilding it) and puts the highlight on
    /// <see cref="RequestedKey"/>.
    /// </summary>
    public void SetListing(IReadOnlyDictionary<uint, string>? listing)
    {
        if (listing == null)
        {
            _list.Clear();
            return;
        }

        var entries = listing.Select(i => new ItemList.Item(_list)
        {
            Text = i.Value,
            Metadata = i.Key,
        }).ToList();
        entries.Sort(CompareItems);
        _list.SetItems(entries, CompareItems);
        SyncHighlight();
    }

    /// <summary>
    /// Makes the list highlight match <see cref="RequestedKey"/> without raising select / deselect events.
    /// </summary>
    public void SyncHighlight()
    {
        var key = RequestedKey;
        _syncingHighlight = true;
        try
        {
            foreach (var item in _list)
            {
                item.Selected = key != null && item.Metadata is uint itemKey && itemKey == key;
            }
        }
        finally
        {
            _syncingHighlight = false;
        }
    }

    public void ClearCache()
    {
        _stateCache.Clear();
    }

    private void OnItemSelected(ItemList.ItemListSelectedEventArgs args)
    {
        if (_syncingHighlight || _list[args.ItemIndex].Metadata is not uint key)
            return;

        RequestSelection(key);
    }

    private void OnItemDeselected(ItemList.ItemListDeselectedEventArgs args)
    {
        if (_syncingHighlight)
            return;

        // The new item is already selected when ItemList deselects the old one: a switch, not a deselect.
        if (_list.GetSelected().Any())
            return;

        // A click on the highlighted item. Keep that record open (and if the highlight was stale, take the click
        // as a select). ItemList clears it twice on such a click; this re-highlights both times.
        if (_list[args.ItemIndex].Metadata is uint key)
            RequestSelection(key);

        SyncHighlight();
    }

    private void RequestSelection(uint key)
    {
        if (key == RequestedKey)
            return;

        _pendingSelections.Add(key);
        _lastSelectionRequest = _timing.RealTime;
        SelectionRequested?.Invoke(key);

        if (LastServerState is not { } last || !_stateCache.TryGetValue(key, out var cached))
            return;

        ApplyingLocalState = true;
        try
        {
            ShowCached?.Invoke(cached, last);
        }
        finally
        {
            ApplyingLocalState = false;
        }
    }

    /// <summary>
    /// Text first so the list reads alphabetically, then key so two records with the same name stay distinct
    /// (SetItems keeps an existing item, with its old key, when this returns 0). Sort and diff must use the same
    /// order - the consoles sorted ordinally but diffed with a culture compare.
    /// </summary>
    private static int CompareItems(ItemList.Item a, ItemList.Item b)
    {
        var text = string.Compare(a.Text, b.Text, StringComparison.Ordinal);
        if (text != 0)
            return text;

        return ((uint?) a.Metadata ?? 0).CompareTo((uint?) b.Metadata ?? 0);
    }
}
