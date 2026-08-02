// SPDX-License-Identifier: AGPL-3.0-or-later
// TEMPORARY local diagnostic. Not part of the deliverable - delete when done.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.ZDiag;

[TestFixture]
public sealed class ZDiagSameSpot
{
    [Test]
    public async Task ZDiagSameSpotTicks()
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

        void Report(string tag, long ms)
        {
            var total = entityMan.EntityCount;
            var awake = 0;
            var contacts = 0;
            var dynamic = 0;
            var canCollide = 0;
            var awakeTop = new Dictionary<string, int>();
            var metaQuery = entityMan.GetEntityQuery<MetaDataComponent>();
            var q = entityMan.AllEntityQueryEnumerator<PhysicsComponent>();
            while (q.MoveNext(out var bodyUid, out var body))
            {
                if (body.CanCollide)
                    canCollide++;
                if (body.BodyType == Robust.Shared.Physics.BodyType.Dynamic)
                    dynamic++;
                if (body.Awake)
                {
                    awake++;
                    var pid = metaQuery.CompOrNull(bodyUid)?.EntityPrototype?.ID ?? "<null>";
                    awakeTop.TryGetValue(pid, out var awc);
                    awakeTop[pid] = awc + 1;
                }
                contacts += body.ContactCount;
            }

            var top = new Dictionary<string, int>();
            var mq = entityMan.AllEntityQueryEnumerator<MetaDataComponent>();
            while (mq.MoveNext(out _, out var meta))
            {
                var id = meta.EntityPrototype?.ID ?? "<null>";
                top.TryGetValue(id, out var c);
                top[id] = c + 1;
            }

            var topStr = string.Join(", ",
                top.OrderByDescending(kv => kv.Value).Take(8).Select(kv => $"{kv.Key}={kv.Value}"));

            var audioFiles = new Dictionary<string, int>();
            var aq = entityMan.AllEntityQueryEnumerator<Robust.Shared.Audio.Components.AudioComponent>();
            while (aq.MoveNext(out _, out var audio))
            {
                var f = audio.FileName ?? "<null>";
                audioFiles.TryGetValue(f, out var ac);
                audioFiles[f] = ac + 1;
            }

            TestContext.Progress.WriteLine(
                $"ZDIAG {tag} audio: " +
                string.Join(", ", audioFiles.OrderByDescending(kv => kv.Value).Take(8).Select(kv => $"{kv.Key}={kv.Value}")));
            TestContext.Progress.WriteLine(
                $"ZDIAG {tag} dynamic={dynamic} canCollide={canCollide} awakeTop: " +
                string.Join(", ", awakeTop.OrderByDescending(kv => kv.Value).Take(12).Select(kv => $"{kv.Key}={kv.Value}")));

            TestContext.Progress.WriteLine(
                $"ZDIAG {tag} ms={ms} ents={total} awakeBodies={awake} contactRefs={contacts} " +
                $"mem_gb={GC.GetTotalMemory(false) / (1024 * 1024 * 1024.0):F2} | top: {topStr}");
        }

        TestContext.Progress.WriteLine($"ZDIAG protos={protoIds.Count}");

        var sw = Stopwatch.StartNew();
        await server.WaitPost(() =>
        {
            foreach (var protoId in protoIds)
                entityMan.SpawnEntity(protoId, map.GridCoords);
        });
        await server.WaitPost(() => Report("after-spawn", sw.ElapsedMilliseconds));

        for (var tick = 0; tick < 1; tick++)
        {
            sw.Restart();
            await server.WaitRunTicks(1);
            var ms = sw.ElapsedMilliseconds;
            await server.WaitPost(() => Report($"tick{tick + 1}", ms));
        }

        await pair.CleanReturnAsync();
    }
}
