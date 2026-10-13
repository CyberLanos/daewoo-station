// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Client.Lobby.UI;

/// <summary>
/// Station records section of the Background tab: the OOC HR file entries shown in the records consoles' history.
/// </summary>
public sealed partial class HumanoidProfileEditor
{
    private void SetupRecordBackgroundEditor()
    {
        RecordBackgroundEditor.OnChanged += () =>
        {
            if (Profile is null || _suppressCharacterInfoChanges)
                return;

            Profile = Profile.WithRecordBackgrounds(RecordBackgroundEditor.GetEntries());
            SetDirty();
        };
    }

    private void UpdateRecordBackgroundText()
    {
        RecordBackgroundEditor.SetEntries(Profile?.RecordBackgrounds ?? []);
    }
}
