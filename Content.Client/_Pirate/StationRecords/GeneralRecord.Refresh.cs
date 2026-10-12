// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Pirate.StationRecords;
using Content.Shared._Pirate.StationRecords;
using Content.Shared.StationRecords;

namespace Content.Client.StationRecords;

public sealed partial class GeneralRecord
{
    /// <summary>
    /// The record the panel last showed from the server; null after a local edit so the next state repopulates.
    /// </summary>
    private GeneralStationRecord? _shownRecord;

    private RecordFieldWidths? _fieldWidths;

    public event Action<uint, byte[]?>? OnPrintPortrait;
    public event Action? OnUploadPortrait;
    public event Action<uint, byte[]>? OnStoreGeneratedPortrait;

    private RecordFieldWidths FieldWidths => _fieldWidths ??= new RecordFieldWidths(
        [SpeciesButton, NationalityButton, EmployerButton, GenderButton],
        [AgeEdit],
        [PersonJob.Parent!]);

    /// <summary>
    /// Refreshes the panel in place. The console used to build a new panel (XAML load + every nationality /
    /// employer requirement check) on each state, and every record change on the station sends one.
    /// </summary>
    public void SetRecord(GeneralStationRecord record)
    {
        if (record.Equals(_shownRecord))
            return;

        PopulateRecord(record);
    }

    private void InitPortrait()
    {
        Portrait.OnPrint += (key, image) => OnPrintPortrait?.Invoke(key, image);
        Portrait.OnUpload += () => OnUploadPortrait?.Invoke();
        Portrait.OnStoreGenerated += (key, image) => OnStoreGeneratedPortrait?.Invoke(key, image);
    }

    public void SetPortrait(uint key, RecordPortraitData? portrait, GeneralStationRecord record, bool editable)
    {
        Portrait.SetPortrait(key, portrait, record.JobPrototype, record.Name, record.Age);
        Portrait.SetEditable(editable);
    }

    /// <summary>
    /// The panel is populated before the console adds it to the window, when nothing can be measured yet.
    /// Deferred: EnteredTree runs before the children are in the tree, so measuring here was skipped and the first
    /// record showed unaligned, full-width dropdowns until the next repopulate.
    /// </summary>
    protected override void EnteredTree()
    {
        base.EnteredTree();
        UserInterfaceManager.DeferAction(() =>
        {
            if (!Disposed && IsInsideTree)
                FieldWidths.Update();
        });
    }
}
