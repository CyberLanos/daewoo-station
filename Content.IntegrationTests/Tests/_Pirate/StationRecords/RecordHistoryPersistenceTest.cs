// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Pair;
using Content.Server._Pirate.StationRecords;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.StationRecords.Systems;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Access.Systems;
using Content.Shared.StationRecords;
using Content.Shared._Pirate.StationRecords;
using Content.Shared.Preferences;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._Pirate.StationRecords;

/// <summary>
/// Records-console histories kept between rounds: saved per character and console, trimmed to the newest N,
/// replaced on each save, gone with the character; and shown as this round, the HR background, then earlier rounds, each newest first.
/// </summary>
[TestFixture]
public sealed class RecordHistoryPersistenceTest
{
    private static readonly DateTime Start = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

    private const string Map = "RecordHistoryTestMap";

    [TestPrototypes]
    private const string Prototypes = $@"
- type: gameMap
  id: {Map}
  mapName: {Map}
  mapPath: /Maps/Test/empty.yml
  minPlayers: 0
  stations:
    Empty:
      stationProto: StandardNanotrasenStation
      components:
        - type: StationNameSetup
          mapNameTemplate: ""Empty""
        - type: StationJobs
          availableJobs:
            Passenger: [ -1, -1 ]
";

    /// <summary>
    /// The real round flow: join as a saved character, get a note, restart without a round end (the fallback), join
    /// again and find the note restored; then the same through a normal round end.
    /// </summary>
    [Test]
    public async Task RoundTripTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            InLobby = true,
        });
        var server = pair.Server;
        server.CfgMan.SetCVar(CCVars.GameMap, Map);
        var ticker = server.System<GameTicker>();
        var db = server.ResolveDependency<IServerDbManager>();
        var user = pair.Client.User!.Value;

        await pair.SetJobPriorities(("Passenger", Content.Shared.Preferences.JobPriority.High));
        var profileId = await db.GetCharacterProfileIdAsync(user, 0);
        Assert.That(profileId, Is.Not.Null);

        // round 1: arrive, get a note, restart without a round end
        var key = await StartAndGetKey(pair, ticker);
        await server.WaitAssertion(() =>
        {
            var records = server.System<StationRecordsSystem>();
            Assert.That(records.TryGetRecord<RecordHistoryOwner>(key, out var owner), "the arrival linked the record to the character");
            Assert.That(owner!.ProfileId, Is.EqualTo(profileId));
            server.System<RecordLogSystem>().Add(key, RecordLogBook.Security, RecordLogKind.Note, null, "first note", null, null);
        });
        await server.WaitPost(() => ticker.RestartRound());
        await pair.RunTicksSync(5);

        var saved = await db.GetRecordHistoryAsync(profileId!.Value, 100);
        Assert.That(saved.Select(e => e.Entry.Message), Is.EqualTo(new[] { "first note" }), "restart without a round end saved");

        // round 2: restored, then a normal round end
        key = await StartAndGetKey(pair, ticker);
        await server.WaitAssertion(() =>
        {
            var log = server.System<RecordLogSystem>().GetLog(key, RecordLogBook.Security);
            Assert.That(log.Where(e => e.Kind != RecordLogKind.Background).Select(e => (e.Message, e.Archived)),
                Is.EqualTo(new[] { ("first note", true) }), "restored on arrival");
            server.System<RecordLogSystem>().Add(key, RecordLogBook.Security, RecordLogKind.Note, null, "second note", null, null);
            ticker.EndRound();
        });
        await pair.RunTicksSync(5);
        await server.WaitPost(() => ticker.RestartRound());
        await pair.RunTicksSync(5);

        saved = await db.GetRecordHistoryAsync(profileId.Value, 100);
        Assert.That(saved.Select(e => e.Entry.Message), Is.EquivalentTo(new[] { "first note", "second note" }), "round end saved");

        await pair.CleanReturnAsync();
    }

    private static async Task<StationRecordKey> StartAndGetKey(TestPair pair, GameTicker ticker)
    {
        var server = pair.Server;
        ticker.ToggleReadyAll(true);
        await server.WaitPost(() => ticker.StartRound());
        await pair.RunTicksSync(30);

        StationRecordKey key = default;
        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            var mob = server.PlayerMan.SessionsDict[pair.Client.User!.Value].AttachedEntity;
            Assert.That(mob, Is.Not.Null);
            Assert.That(server.System<SharedIdCardSystem>().TryFindIdCard(mob!.Value, out var id), "has an ID");
            Assert.That(server.EntMan.TryGetComponent(id.Owner, out StationRecordKeyStorageComponent? storage) && storage.Key != null,
                "the ID holds a record key");
            key = storage!.Key!.Value;
        });
        return key;
    }

    private static RecordLogEntry Entry(int minutes, string message, RecordLogKind kind = RecordLogKind.Note)
    {
        return new RecordLogEntry
        {
            Kind = kind,
            Message = message,
            AuthorName = "Officer",
            StationName = "Dev",
            Time = Start.AddMinutes(minutes),
        };
    }

    [Test]
    public async Task SaveTrimReplaceAndCascadeTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var db = GetDb(pair.Server);
        var user = new NetUserId(Guid.NewGuid());
        HumanoidCharacterProfile profile = default!;
        await pair.Server.WaitPost(() => profile = HumanoidCharacterProfile.DefaultWithSpecies());
        await db.InitPrefsAsync(user, profile);
        var profileId = await db.GetCharacterProfileIdAsync(user, 0);
        Assert.That(profileId, Is.Not.Null);

        var security = Enumerable.Range(0, 5).Select(i => Entry(i, $"sec {i}")).ToList();
        var crew = new List<RecordLogEntry> { Entry(1, "crew 1"), Entry(2, "crew 2") };
        await db.SaveRecordHistoryAsync(new[]
        {
            new RecordHistorySnapshot(profileId!.Value, RecordLogBook.Security, security),
            new RecordHistorySnapshot(profileId.Value, RecordLogBook.Crew, crew),
            // a character deleted during the round is skipped, not an error
            new RecordHistorySnapshot(int.MaxValue, RecordLogBook.Crew, crew),
        }, limitPerBook: 3);

        var loaded = await db.GetRecordHistoryAsync(profileId.Value, limitPerBook: 100);
        var loadedSecurity = loaded.Where(e => e.Book == RecordLogBook.Security).Select(e => e.Entry).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(loadedSecurity.Select(e => e.Message), Is.EquivalentTo(new[] { "sec 4", "sec 3", "sec 2" }),
                "only the newest 3 are kept");
            Assert.That(loaded.Count(e => e.Book == RecordLogBook.Crew), Is.EqualTo(2));
            Assert.That(loaded.All(e => e.Entry.Archived), "restored entries are marked as from earlier rounds");
            Assert.That(loadedSecurity.All(e => e.Time.Kind == DateTimeKind.Utc));
            Assert.That(loadedSecurity.Single(e => e.Message == "sec 4").Time, Is.EqualTo(Start.AddMinutes(4)));
        });

        // a lower limit at load time applies straight away
        Assert.That((await db.GetRecordHistoryAsync(profileId.Value, limitPerBook: 1))
            .Count(e => e.Book == RecordLogBook.Security), Is.EqualTo(1));

        // the next save replaces that console's history and leaves the others alone
        await db.SaveRecordHistoryAsync(new[]
        {
            new RecordHistorySnapshot(profileId.Value, RecordLogBook.Security, new List<RecordLogEntry> { Entry(10, "sec new") }),
        }, limitPerBook: 3);
        loaded = await db.GetRecordHistoryAsync(profileId.Value, limitPerBook: 100);
        Assert.Multiple(() =>
        {
            Assert.That(loaded.Where(e => e.Book == RecordLogBook.Security).Select(e => e.Entry.Message),
                Is.EqualTo(new[] { "sec new" }));
            Assert.That(loaded.Count(e => e.Book == RecordLogBook.Crew), Is.EqualTo(2));
        });

        // deleting the character deletes their history
        await db.SaveCharacterSlotAsync(user, null, 0);
        Assert.That(await db.GetRecordHistoryAsync(profileId.Value, limitPerBook: 100), Is.Empty);

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The OOC records background: several entries per console with title and author, kept through the database;
    /// entries without text and those past the per-console limit are dropped.
    /// </summary>
    [Test]
    public async Task RecordBackgroundProfileTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var db = GetDb(pair.Server);
        var user = new NetUserId(Guid.NewGuid());

        var entries = new List<RecordBackgroundEntry>
        {
            new() { Book = RecordLogBook.Security, Title = "Особова справа", Author = "Відділ кадрів NanoTrasen", Body = "first" },
            new() { Book = RecordLogBook.Security, Title = "Догана", Author = "Капітан", Body = "second" },
            new() { Book = RecordLogBook.Crew, Title = "", Author = "", Body = "defaults" },
            new() { Book = RecordLogBook.Psionics, Title = "empty", Author = "", Body = "   " },
        };
        for (var i = 0; i < RecordBackgroundEntry.MaxPerBook; i++)
        {
            entries.Add(new RecordBackgroundEntry { Book = RecordLogBook.Crew, Body = $"crew {i}" });
        }

        HumanoidCharacterProfile profile = default!;
        await pair.Server.WaitPost(() => profile = (HumanoidCharacterProfile) HumanoidCharacterProfile.DefaultWithSpecies()
            .WithRecordBackgrounds(entries)
            .Validated(pair.Player!, IoCManager.Instance!));
        await db.InitPrefsAsync(user, profile);
        var loaded = (HumanoidCharacterProfile) (await db.GetPlayerPreferencesAsync(user))!.Characters[0];

        Assert.Multiple(() =>
        {
            Assert.That(loaded.RecordBackgrounds.Where(e => e.Book == RecordLogBook.Security).Select(e => (e.Title, e.Author, e.Body)),
                Is.EqualTo(new[] { ("Особова справа", "Відділ кадрів NanoTrasen", "first"), ("Догана", "Капітан", "second") }));
            Assert.That(loaded.RecordBackgrounds.Count(e => e.Book == RecordLogBook.Crew), Is.EqualTo(RecordBackgroundEntry.MaxPerBook),
                "past the limit is dropped");
            Assert.That(loaded.RecordBackgrounds.First(e => e.Book == RecordLogBook.Crew).Body, Is.EqualTo("defaults"));
            Assert.That(loaded.RecordBackgrounds.Any(e => e.Book == RecordLogBook.Psionics), Is.False, "no text, no entry");
            Assert.That(loaded.MemberwiseEquals(profile));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public void DisplayOrderTest()
    {
        var log = new List<RecordLogEntry>
        {
            Entry(1, "two rounds ago") with { Archived = true, Id = 2 },
            Entry(5, "last round") with { Archived = true, Id = 1 },
            Entry(10, "hr", RecordLogKind.Background) with { Id = 3 },
            Entry(30, "this round, later") with { Id = 5 },
            Entry(20, "this round") with { Id = 4 },
        };

        Assert.That(RecordLogOrder.ForDisplay(log).Select(e => e.Message),
            Is.EqualTo(new[] { "this round, later", "this round", "hr", "last round", "two rounds ago" }));
    }

    private static ServerDbSqlite GetDb(RobustIntegrationTest.ServerIntegrationInstance server)
    {
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var opsLog = server.ResolveDependency<ILogManager>().GetSawmill("db.ops");
        var builder = new DbContextOptionsBuilder<SqliteServerDbContext>();
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        builder.UseSqlite(conn);
        return new ServerDbSqlite(() => builder.Options, true, cfg, true, opsLog);
    }
}
