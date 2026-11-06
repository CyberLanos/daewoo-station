// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Pirate.Light;
using Content.Shared._Pirate.CCVars;
using Content.Shared._Pirate.Light;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Pirate.Light;

[TestFixture]
[TestOf(typeof(StarlightMapSystem))]
public sealed class StarlightTest
{
    [Test]
    public async Task MapOverrideReachesClientAndFallsBack()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var testMap = await pair.CreateTestMap();
        var red = Color.FromHex("#FF2020");

        await server.WaitPost(() =>
        {
            server.ResolveDependency<IConfigurationManager>().SetCVar(PirateVars.StarlightStrength, 0.5f);
            server.System<StarlightMapSystem>().SetStarlight(testMap.MapUid, red, 0.3f);
        });
        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var starlight = client.System<StarlightSystem>();
            var cmap = testMap.CMapUid;

            Assert.That(starlight.IsStarlit(cmap), Is.True, "the test map is a plain space map");

            var (color, strength) = starlight.GetStarlight(cmap);
            Assert.That(color, Is.EqualTo(red));
            Assert.That(strength, Is.EqualTo(0.3f).Within(0.001f));

            // Players cannot raise it: the CVar is server-only.
            Assert.That(client.ResolveDependency<IConfigurationManager>().GetCVar(PirateVars.StarlightStrength),
                Is.EqualTo(0.5f).Within(0.001f));
        });

        await server.WaitPost(() => server.System<StarlightMapSystem>().ClearStarlight(testMap.MapUid));
        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var (_, strength) = client.System<StarlightSystem>().GetStarlight(testMap.CMapUid);
            Assert.That(strength, Is.EqualTo(0.5f).Within(0.001f), "without the override the server default applies");
        });

        await pair.CleanReturnAsync();
    }
}
