// SPDX-License-Identifier: AGPL-3.0-or-later
// TEMPORARY diagnostic fixture. Mirrors EntityTest but reports timing/entity-count
// progress so a super-linear slowdown can be localised. Delete before merging.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Robust.Shared;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.IntegrationTests.Tests._Pirate.ZDiag;

[TestFixture]
public sealed class ZDiagEntityTest
{
    private static readonly HashSet<ProtoId<EntityCategoryPrototype>> IgnoredCategories = ["Spawner", "Debug"];

    /// <summary>
    /// Same spawn/delete loop as EntityTest.SpawnAndDeleteEntityCountTest, but it never asserts and
    /// instead prints a progress line every <see cref="ReportEvery"/> prototypes with the wall time
    /// spent on that block plus the live entity counts. A flat ms/proto curve means the test is
    /// linear; a rising one means something is accumulating.
    /// </summary>
    [Test]
    public async Task ZDiagSpawnDeleteEntityCountProfile()
    {
        const int reportEvery = 250;

        var settings = new PoolSettings { Connected = true, Dirty = true };
        await using var pair = await PoolManager.GetServerClient(settings);
        var mapSys = pair.Server.System<SharedMapSystem>();
        var server = pair.Server;
        var client = pair.Client;

        var excluded = new[]
        {
            "MapGrid", "StationEvent", "TimedDespawn", "AnnounceOnSpawn", "EntityTableContainerFill",
            "ContainerFill", "GameRule", "SpawnOnDespawn", "Mutation", "PendingSlimeSpawn", "Slime",
            "Anomaly", "LabyrinthPortal", "Area", "StatusEffect", "AshJaunt", "SpawnEntityTableOnTrigger",
        };

        var protoIds = new List<EntProtoId>();
        foreach (var p in server.ProtoMan.EnumeratePrototypes<EntityPrototype>())
        {
            if (pair.IsTestPrototype(p) || excluded.Any(p.Components.ContainsKey) || p.Categories.Any(id => IgnoredCategories.Contains(id)))
                continue;

            protoIds.Add(p.ID);
        }

        protoIds.Sort();
        var mapId = MapId.Nullspace;
        await server.WaitPost(() => mapSys.CreateMap(out mapId));
        var coords = new MapCoordinates(Vector2.Zero, mapId);
        await pair.RunTicksSync(3);

        int Count(IEntityManager ent) => ent.GetEntities().Count(e => !ent.HasComponent<AudioComponent>(e));
        int CountPersistent(IEntityManager ent) => ent.GetEntities()
            .Count(e => !ent.HasComponent<AudioComponent>(e) && !ent.HasComponent<TimedDespawnComponent>(e));

        await TestContext.Progress.WriteLineAsync(
            $"ZDIAG total_protos={protoIds.Count} start_server_ents={Count(server.EntMan)} start_client_ents={Count(client.EntMan)}");

        var overall = Stopwatch.StartNew();
        var block = Stopwatch.StartNew();
        var leakedAfterDelete = 0;

        for (var i = 0; i < protoIds.Count; i++)
        {
            var protoId = protoIds[i];
            var persistentBefore = CountPersistent(server.EntMan);

            EntityUid uid = default;
            await server.WaitPost(() => uid = server.EntMan.SpawnEntity(protoId, coords));
            await pair.RunTicksSync(3);

            if (server.EntMan.EntityExists(uid))
            {
                await server.WaitPost(() => server.EntMan.DeleteEntity(uid));
                await pair.RunTicksSync(3);
            }

            var persistentAfter = CountPersistent(server.EntMan);
            if (persistentAfter != persistentBefore)
            {
                leakedAfterDelete++;
                if (leakedAfterDelete <= 40)
                {
                    await TestContext.Progress.WriteLineAsync(
                        $"ZDIAG LEAK #{i} {protoId}: persistent {persistentBefore} -> {persistentAfter}");
                }
            }

            if ((i + 1) % reportEvery != 0)
                continue;

            var blockMs = block.ElapsedMilliseconds;
            block.Restart();
            await TestContext.Progress.WriteLineAsync(
                $"ZDIAG i={i + 1}/{protoIds.Count} block_ms={blockMs} ms_per_proto={blockMs / (double) reportEvery:F1} " +
                $"total_s={overall.Elapsed.TotalSeconds:F0} sEnts={Count(server.EntMan)} cEnts={Count(client.EntMan)} " +
                $"leaks={leakedAfterDelete} mem_gb={GC.GetTotalMemory(false) / (1024 * 1024 * 1024.0):F2}");
        }

        await TestContext.Progress.WriteLineAsync(
            $"ZDIAG DONE total_s={overall.Elapsed.TotalSeconds:F0} leaks={leakedAfterDelete} " +
            $"sEnts={Count(server.EntMan)} cEnts={Count(client.EntMan)}");

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Same-spot spawn stress, instrumented: reports how long the spawn loop and each tick take.
    /// </summary>
    [Test]
    public async Task ZDiagSameSpotProfile()
    {
        var settings = new PoolSettings { Dirty = true };
        await using var pair = await PoolManager.GetServerClient(settings);
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entityMan = server.ResolveDependency<IEntityManager>();
        var prototypeMan = server.ResolveDependency<IPrototypeManager>();

        var protoIds = prototypeMan
            .EnumeratePrototypes<EntityPrototype>()
            .Where(p => !p.Abstract)
            .Where(p => !pair.IsTestPrototype(p))
            .Where(p => !p.Components.ContainsKey("MapGrid"))
            .Where(p => !p.Components.ContainsKey("Supermatter"))
            .Where(p => !p.Components.ContainsKey("RoomFill"))
            .Where(p => !p.Components.ContainsKey("SoundCollection"))
            .Where(p => !p.Components.ContainsKey("RandomSpawner"))
            .Where(p => !p.Components.ContainsKey("Marker"))
            .Where(p => !p.Components.ContainsKey("GameRule"))
            .Where(p => !p.Components.ContainsKey("DarkLord"))
            .Where(p => !p.Components.ContainsKey("GrapplingProjectile"))
            .Where(p => !p.Components.ContainsKey("SpawnOnDespawn"))
            .Where(p => !p.Components.ContainsKey("Chasm"))
            .Select(p => p.ID)
            .ToList();

        await TestContext.Progress.WriteLineAsync($"ZDIAG samespot protos={protoIds.Count}");

        var sw = Stopwatch.StartNew();
        const int chunk = 1000;
        for (var start = 0; start < protoIds.Count; start += chunk)
        {
            var slice = protoIds.Skip(start).Take(chunk).ToList();
            sw.Restart();
            await server.WaitPost(() =>
            {
                foreach (var protoId in slice)
                    entityMan.SpawnEntity(protoId, map.GridCoords);
            });
            await TestContext.Progress.WriteLineAsync(
                $"ZDIAG samespot spawned={start + slice.Count} chunk_ms={sw.ElapsedMilliseconds} " +
                $"ents={entityMan.EntityCount} mem_gb={GC.GetTotalMemory(false) / (1024 * 1024 * 1024.0):F2}");
        }

        for (var tick = 0; tick < 15; tick++)
        {
            sw.Restart();
            await server.WaitRunTicks(1);
            await TestContext.Progress.WriteLineAsync(
                $"ZDIAG samespot tick={tick + 1} tick_ms={sw.ElapsedMilliseconds} ents={entityMan.EntityCount} " +
                $"mem_gb={GC.GetTotalMemory(false) / (1024 * 1024 * 1024.0):F2}");
        }

        sw.Restart();
        await server.WaitPost(() =>
        {
            var query = entityMan.AllEntityQueryEnumerator<MetaDataComponent>();
            var toDelete = new List<EntityUid>();
            while (query.MoveNext(out var uid, out var meta))
            {
                if (!meta.EntityDeleted)
                    toDelete.Add(uid);
            }

            foreach (var uid in toDelete)
            {
                if (entityMan.EntityExists(uid))
                    entityMan.DeleteEntity(uid);
            }
        });
        await TestContext.Progress.WriteLineAsync(
            $"ZDIAG samespot delete_ms={sw.ElapsedMilliseconds} remaining={entityMan.EntityCount}");

        await pair.CleanReturnAsync();
    }
}
