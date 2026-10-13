// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.Client._Pirate.Lobby.UI;
using Content.Client._Pirate.Lobby.UI.Background;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Client.Players.PlayTimeTracking;
using Content.Shared._Pirate.StationRecords;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Pirate.Lobby;

/// <summary>
/// The character editor's Background tab builds and fills in: the rules warning, field cards with the profile's
/// text and each section's filled-in count, and the station records entries.
/// </summary>
[TestFixture]
[TestOf(typeof(BackgroundSection))]
public sealed class BackgroundTabTest
{
    [Test]
    public async Task FillsInTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var creator = new HumanoidProfileEditor(
                client.ResolveDependency<IClientPreferencesManager>(),
                client.ResolveDependency<IConfigurationManager>(),
                client.EntMan,
                client.ResolveDependency<IFileDialogManager>(),
                client.ResolveDependency<ILogManager>(),
                client.ResolveDependency<IPlayerManager>(),
                client.ResolveDependency<IPrototypeManager>(),
                client.ResolveDependency<IResourceManager>(),
                client.ResolveDependency<JobRequirementsManager>(),
                client.ResolveDependency<MarkingManager>());
            try
            {
                var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human")
                    .WithOOCNotes("ooc")
                    .WithRecordBackgrounds(new[]
                    {
                        new RecordBackgroundEntry { Book = RecordLogBook.Security, Title = "A", Author = "B", Body = "one" },
                        new RecordBackgroundEntry { Book = RecordLogBook.Security, Body = "two" },
                        new RecordBackgroundEntry { Book = RecordLogBook.Psionics, Body = "three" },
                    });
                creator.SetProfile(profile, 0);
                Descendants(creator).Single(c => c.Name == "BackgroundTab").Visible = true;

                // Sections count their fields every frame.
                var update = typeof(Control).GetMethod("DoFrameUpdateRecursive", BindingFlags.Instance | BindingFlags.NonPublic)!;
                update.Invoke(creator, new object[] { new FrameEventArgs(0.016f) });

                var ooc = Descendants(creator).OfType<OOCInfoEditor>().Single();
                var oocFields = Descendants(ooc).OfType<BackgroundTextField>().Where(f => f.VisibleInTree).ToList();
                var oocSection = Descendants(ooc).OfType<BackgroundSection>().Single();
                var records = Descendants(creator).OfType<RecordBackgroundEditor>().Single();

                Assert.Multiple(() =>
                {
                    Assert.That(Descendants(creator).OfType<BackgroundWarning>().Count(), Is.EqualTo(1));
                    Assert.That((oocSection.Filled, oocSection.Total),
                        Is.EqualTo((oocFields.Count(f => f.IsFilled), oocFields.Count)), "the section counts its shown fields");
                    Assert.That(records.GetEntries().Select(e => e.Body), Is.EqualTo(new[] { "one", "two", "three" }));
                    Assert.That(Descendants(records).OfType<FlatButton>().Count(), Is.EqualTo(3 + 3),
                        "an add button per console and a remove button per entry");
                    Assert.That(Descendants(creator).OfType<BackgroundSection>().Count(), Is.EqualTo(3));
                });
            }
            finally
            {
                client.EntMan.DeleteEntity(creator.PreviewDummy);
                creator.Dispose();
            }
        });

        await pair.CleanReturnAsync();
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (var child in control.Children)
        {
            yield return child;
            foreach (var deeper in Descendants(child))
            {
                yield return deeper;
            }
        }
    }
}
