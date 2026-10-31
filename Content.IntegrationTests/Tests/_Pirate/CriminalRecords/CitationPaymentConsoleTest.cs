// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._Pirate.CriminalRecords;
using Content.IntegrationTests.Pair;
using Content.Server._Pirate.Banking;
using Content.Server._Pirate.CriminalRecords;
using Content.Server.Access.Systems;
using Content.Server.CriminalRecords.Systems;
using Content.Server.Power.Components;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared._Pirate.Banking;
using Content.Shared._Pirate.CriminalRecords;
using Content.Shared.Cargo.Components;
using Content.Shared.CriminalRecords;
using Content.Shared.CriminalRecords.Components;
using Content.Shared.StationRecords;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.Input;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Placement;
using Robust.Shared.GameObjects;
using Robust.Shared.Enums;
using Robust.Shared.Input;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Pirate.CriminalRecords;

[TestFixture]
public sealed class CitationPaymentConsoleTest
{
    [Test]
    public async Task PlacementPreviewKeepsCustomArtworkAndUsesPlacementDirection()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
            Dirty = true,
        });
        var client = pair.Client;
        await client.WaitAssertion(() =>
        {
            var entities = client.EntMan;
            var placement = client.ResolveDependency<IPlacementManager>();
            var eye = client.ResolveDependency<IEyeManager>().CurrentEye;
            var transform = client.System<SharedTransformSystem>();
            var visuals = client.System<CitationPaymentConsoleSpriteSystem>();
            var parent = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var placed = entities.SpawnEntity("CitationPaymentConsole", MapCoordinates.Nullspace);
            var oldEyeRotation = eye.Rotation;
            var oldDirection = placement.Direction;
            try
            {
                eye.Rotation = Angle.Zero;
                placement.BeginPlacing(new PlacementInformation
                {
                    EntityType = "CitationPaymentConsole",
                    PlacementOption = "SnapgridCenter",
                    UseEditorContext = false,
                });
                var mode = placement.CurrentMode!;
                mode.MouseCoords = new EntityCoordinates(parent, Vector2.Zero);
                var ghost = mode.pManager.CurrentPlacementOverlayEntity!.Value;
                var previewSprite = entities.GetComponent<SpriteComponent>(ghost);
                var placedSprite = entities.GetComponent<SpriteComponent>(placed);

                Assert.That(previewSprite.BaseRSI!.Path.ToString(), Does.EndWith("/_Pirate/Structures/Machines/citation_payment.rsi"));
                Assert.That(previewSprite.NoRotation, Is.True);
                foreach (var state in new[] { "unshaded", "accepted", "denied" })
                {
                    Assert.That(previewSprite.BaseRSI.TryGetState(state, out var animation), Is.True);
                    Assert.That(animation!.IsAnimated, Is.True, $"The user's {state} animation must remain available.");
                }
                Assert.That(entities.HasComponent<GenericVisualizerComponent>(ghost), Is.True);

                foreach (var (direction, expected) in new[]
                         {
                             (Direction.South, new Vector2(0f, -0.125f)),
                             (Direction.East, new Vector2(0.125f, 0f)),
                             (Direction.North, new Vector2(0f, 0.125f)),
                             (Direction.West, new Vector2(-0.125f, 0f)),
                         })
                {
                    placement.Direction = direction;
                    transform.SetLocalRotation(placed, direction.ToAngle());
                    visuals.FrameUpdate(0f);
                    Assert.That(Vector2.Distance(previewSprite.Offset, expected), Is.LessThan(0.00001f), $"Preview: {direction}");
                    Assert.That(Vector2.Distance(placedSprite.Offset, expected), Is.LessThan(0.00001f), $"Placed: {direction}");
                    Assert.That(entities.GetComponent<TransformComponent>(ghost).LocalRotation, Is.EqualTo(Angle.Zero),
                        "The preview direction must not depend on rotating the nullspace ghost.");
                }

                // Line placement renders relative to its start grid, which can differ from the cursor's grid.
                mode.pManager.PlacementType = PlacementManager.PlacementTypes.Line;
                mode.pManager.StartPoint = new EntityCoordinates(parent, Vector2.Zero);
                mode.MouseCoords = new EntityCoordinates(placed, Vector2.Zero);
                transform.SetLocalRotation(parent, Angle.FromDegrees(90));
                placement.Direction = Direction.South;
                eye.Rotation = Angle.FromDegrees(90);
                visuals.FrameUpdate(0f);
                Assert.That(Vector2.Distance(previewSprite.Offset, new Vector2(0f, 0.125f)), Is.LessThan(0.00001f),
                    "Preview offsets must include the placement grid and camera rotations.");
            }
            finally
            {
                placement.Clear();
                placement.Direction = oldDirection;
                eye.Rotation = oldEyeRotation;
                entities.DeleteEntity(placed);
                entities.DeleteEntity(parent);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PublicTerminalPaysCitationsWithoutRecordsEditingAccess()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
            Dirty = true,
        });
        var server = pair.Server;
        var entities = server.EntMan;
        var records = server.System<StationRecordsSystem>();
        var banking = server.System<BankCardSystem>();
        var ui = server.System<SharedUserInterfaceSystem>();
        var actor = server.PlayerMan.Sessions.Single().AttachedEntity!.Value;
        EntityUid terminal = default;
        CitationPaymentConsoleState publicState = default!;
        StationRecordKey key = default;
        BankAccount payer = default!;
        BankAccount debtor = default!;
        CriminalRecord record = default!;
        StationBankAccountComponent budget = default!;
        var initialBudget = 0;

        await server.WaitAssertion(() =>
        {
            var station = server.System<StationSystem>().GetOwningStation(actor)!.Value;
            terminal = entities.SpawnEntity("CitationPaymentConsole", entities.GetComponent<TransformComponent>(actor).Coordinates);
            var power = entities.GetComponent<ApcPowerReceiverComponent>(terminal);
            power.NeedsPower = false;
            power.Powered = true;
            Assert.That(entities.GetComponent<TransformComponent>(terminal).Anchored, Is.True);
            Assert.That(entities.HasComponent<CriminalRecordsConsoleComponent>(terminal), Is.False);
            Assert.That(entities.HasComponent<ATMComponent>(terminal), Is.False);

            key = records.AddRecordEntry(station, new GeneralStationRecord { Name = "Citation Test Debtor", JobTitle = "Botanist" });
            record = new CriminalRecord();
            record.Citations.Add(new CriminalRecordCitation { Id = 1, Fine = 100, Reason = "Littering", IssuedAt = DateTime.UtcNow });
            record.Citations.Add(new CriminalRecordCitation { Id = 2, Fine = 80, Reason = "Paid already", Status = CitationStatus.Paid });
            record.Citations.Add(new CriminalRecordCitation { Id = 3, Fine = 50, Reason = "Voided already", Status = CitationStatus.Voided });
            record.Citations.Add(new CriminalRecordCitation { Id = 4, Fine = 2000, Reason = "Second fine", IssuedAt = DateTime.UtcNow });
            record.NextCitationId = 5;
            records.AddRecordEntry(key, record);
            debtor = banking.CreateAccount(startingBalance: 500);
            debtor.Name = "Citation Test Debtor";
            server.System<CitationAccountArrestSystem>().Refresh(key);
            Assert.That(debtor.Arrested, Is.True);
            records.Synchronize(key);

            Assert.That(ui.TryGetUiState<CitationPaymentConsoleState>(terminal, CitationPaymentConsoleUiKey.Key, out publicState), Is.True);
            var person = publicState.People.Single(p => p.RecordId == key.Id);
            Assert.That(person.Name, Is.EqualTo("Citation Test Debtor"));
            Assert.That(person.Citations.Select(c => c.Id), Is.EquivalentTo(new uint[] { 1, 4 }));

            Assert.That(server.System<IdCardSystem>().TryFindIdCard(actor, out var idCard), Is.True);
            var card = entities.GetComponent<BankCardComponent>(idCard);
            Assert.That(banking.TryGetAccount(card.AccountId!.Value, out payer), Is.True);
            Assert.That(banking.TryChangeBalance(payer.AccountId, 500 - payer.Balance, BankTransactionType.Adjustment), Is.True);
            budget = entities.GetComponent<StationBankAccountComponent>(station);
            initialBudget = budget.Accounts["Security"];
            Assert.That(ui.TryOpenUi(terminal, CitationPaymentConsoleUiKey.Key, actor), Is.True);

            // Old security-console commands have no handler on the public terminal.
            entities.EventBus.RaiseLocalEvent(terminal, new CriminalRecordVoidCitation(1) { Actor = actor, UiKey = CitationPaymentConsoleUiKey.Key });
            entities.EventBus.RaiseLocalEvent(terminal, new CriminalRecordAddCitation("Forged", "", 1) { Actor = actor, UiKey = CitationPaymentConsoleUiKey.Key });
            Assert.That(record.Citations[0].Status, Is.EqualTo(CitationStatus.Active));
            Assert.That(record.Citations, Has.Count.EqualTo(4));

            Pay(4);
            Assert.That(payer.Balance, Is.EqualTo(500), "An unaffordable payment must not deduct money.");
            Assert.That(record.Citations[3].Status, Is.EqualTo(CitationStatus.Active));

            power.Powered = false;
            Pay(1);
            Assert.That(payer.Balance, Is.EqualTo(500), "An unpowered terminal must reject payment.");
            power.Powered = true;

            Pay(1);
            Assert.That(payer.Balance, Is.EqualTo(400));
            Assert.That(budget.Accounts["Security"], Is.EqualTo(initialBudget + 100));
            Assert.That(record.Citations[0].Status, Is.EqualTo(CitationStatus.Paid));
            Assert.That(record.Citations[0].ClosedBy, Is.Not.Null.And.Not.Empty);
            Assert.That(debtor.Arrested, Is.True, "The second active citation must keep the account arrested.");

            Pay(1);
            Pay(999);
            Assert.That(payer.Balance, Is.EqualTo(400), "Repeated and invalid payment requests must not charge again.");
            var strangerStation = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            Assert.That(server.System<CriminalRecordsConsoleSystem>().TryPayCitation(terminal, actor,
                new StationRecordKey(key.Id, strangerStation), 4), Is.False);

            Assert.That(banking.TryChangeBalance(payer.AccountId, 2000, BankTransactionType.Adjustment), Is.True);
            Pay(4);
            Assert.That(payer.Balance, Is.EqualTo(400));
            Assert.That(budget.Accounts["Security"], Is.EqualTo(initialBudget + 2100));
            Assert.That(debtor.Arrested, Is.False);
            Assert.That(ui.TryGetUiState<CitationPaymentConsoleState>(terminal, CitationPaymentConsoleUiKey.Key, out var finalState), Is.True);
            Assert.That(finalState.People.Any(p => p.RecordId == key.Id), Is.False);
        });

        await pair.RunTicksSync(10);
        CitationPaymentConsoleWindow window = default!;
        CitationPaymentConsoleWindow secondWindow = default!;
        ItemList.Item selectPerson = default!;
        Button payButton = default!;
        var requests = new List<(uint Record, uint Citation)>();
        await pair.Client.WaitAssertion(() =>
        {
            window = new CitationPaymentConsoleWindow();
            window.UpdateState(publicState);
            window.OnPay += (person, citation) => requests.Add((person, citation));
            window.OpenCentered();
            secondWindow = new CitationPaymentConsoleWindow();
            secondWindow.UpdateState(publicState);
            var people = Descendants(window).OfType<ItemList>().Single();
            selectPerson = people.Single(item => item.Metadata is uint id && id == key.Id);
        });
        await pair.Client.WaitPost(() => selectPerson.Selected = true);
        await pair.Client.WaitAssertion(() =>
        {
            var buttons = Descendants(window).OfType<Button>().ToArray();
            var loc = pair.Client.ResolveDependency<ILocalizationManager>();
            Assert.That(buttons.Any(b => b.Text == loc.GetString("criminal-records-citation-void")), Is.False);
            Assert.That(buttons.Any(b => b.Text == loc.GetString("criminal-records-citation-issue") && b.Visible), Is.False);
            payButton = buttons.First(b => b.Text == loc.GetString("criminal-records-citation-pay"));
            Assert.That(Descendants(secondWindow).OfType<Button>().Any(b => b.Text == loc.GetString("criminal-records-citation-pay")), Is.False,
                "Each viewer must keep their own selection.");
        });
        await Click(pair, payButton);
        await pair.Client.WaitAssertion(() =>
        {
            Assert.That(requests, Is.Empty, "The first click must only ask for confirmation.");
            var loc = pair.Client.ResolveDependency<ILocalizationManager>();
            var confirmations = publicState.People.Single(p => p.RecordId == key.Id).Citations
                .Select(c => loc.GetString("criminal-records-citation-pay-confirm", ("fine", c.Fine))).ToHashSet();
            payButton = Descendants(window).OfType<Button>().Single(b => confirmations.Contains(b.Text));
        });
        await Click(pair, payButton);
        await pair.Client.WaitAssertion(() =>
        {
            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].Record, Is.EqualTo(key.Id));
            window.Close();
            window.Dispose();
            secondWindow.Dispose();
        });

        await pair.CleanReturnAsync();

        void Pay(uint id) => entities.EventBus.RaiseLocalEvent(terminal, new CitationPaymentMessage(key.Id, id)
        {
            Actor = actor,
            UiKey = CitationPaymentConsoleUiKey.Key,
        });
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (var child in parent.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static async Task Click(TestPair pair, BaseButton button)
    {
        await pair.Client.WaitPost(() =>
        {
            button.Mode = BaseButton.ActionMode.Press;
            button.MuteSounds = true;
        });
        var screen = new ScreenCoordinates(Vector2.Zero, default);
        await pair.Client.DoGuiEvent(button, new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick,
            BoundKeyState.Down, screen, false, Vector2.Zero, Vector2.Zero));
        await pair.Client.DoGuiEvent(button, new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick,
            BoundKeyState.Up, screen, false, Vector2.Zero, Vector2.Zero));
    }
}
