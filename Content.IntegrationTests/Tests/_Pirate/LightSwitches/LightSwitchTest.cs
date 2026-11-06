// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Common.DeviceLinking;
using Content.Server.DeviceLinking.Systems;
using Content.Server.Light.EntitySystems;
using Content.Server.Power.Components;
using Content.Shared.Interaction;
using Content.Shared.Light.Components;
using Content.Shared.Power;
using Robust.Client.GameObjects;
using Robust.Client.Utility;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Pirate.LightSwitches;

public sealed class LightSwitchTest
{
    [Test]
    public async Task BothSwitchSpritesSelectDirectionalFramesWithoutTurning()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var entities = client.EntMan;
            var sprites = client.System<SpriteSystem>();
            foreach (var prototype in new[] { "PirateLightSwitch", "PirateLightSwitchStartsOff" })
            {
                var uid = entities.Spawn(prototype);
                var sprite = entities.GetComponent<SpriteComponent>(uid);
                sprites.GetLocalBounds((uid, sprite));

                foreach (var layer in sprite.AllLayers.Cast<SpriteComponent.Layer>())
                {
                    layer.GetLayerDrawMatrix(RsiDirection.South, out var upright);
                    foreach (var direction in new[] { Direction.North, Direction.East, Direction.West })
                    {
                        var frameDirection = layer.EffectiveDirection(direction.ToAngle());
                        Assert.That(frameDirection.Convert(), Is.EqualTo(direction));
                        layer.GetLayerDrawMatrix(frameDirection, out var matrix);
                        Assert.That(matrix, Is.EqualTo(upright));
                    }
                }

                entities.DeleteEntity(uid);
            }
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("PirateLightSwitch", true)]
    [TestCase("PirateLightSwitchStartsOff", false)]
    public async Task MappedSwitchAppliesStartupStateAndRetainsStateThroughPowerLoss(string prototype, bool startsOn)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var maps = server.System<SharedMapSystem>();
        var links = server.System<DeviceLinkSystem>();
        var lights = server.System<PoweredLightSystem>();
        var appearance = server.System<SharedAppearanceSystem>();
        MapId mapId = default;
        EntityUid lightSwitch = default;
        EntityUid fixture = default;
        EntityUid toggleFixture = default;
        EntityUid darkFixture = default;

        await server.WaitAssertion(() =>
        {
            maps.CreateMap(out mapId, runMapInit: false);
            var grid = maps.CreateGridEntity(mapId);
            maps.SetTile(grid, Vector2i.Zero, new Tile(1));
            var coords = new EntityCoordinates(grid, 0.5f, 0.5f);
            lightSwitch = entities.SpawnAttachedTo(prototype, coords);
            fixture = entities.SpawnAttachedTo("Poweredlight", coords);
            toggleFixture = entities.SpawnAttachedTo("Poweredlight", coords);
            darkFixture = entities.SpawnAttachedTo("Poweredlight", coords);
            lights.SetState(darkFixture, false);
            entities.GetComponent<ApcPowerReceiverComponent>(lightSwitch).NeedsPower = false;

            links.SaveLinks(null, lightSwitch, fixture, [("On", "On"), ("Off", "Off")]);
            links.SaveLinks(null, lightSwitch, toggleFixture, [("Pressed", "Toggle")]);
            links.SaveLinks(null, lightSwitch, darkFixture, [("Pressed", "Toggle")]);
            maps.InitializeMap(mapId);
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<PoweredLightComponent>(fixture).On, Is.EqualTo(startsOn));
            Assert.That(entities.GetComponent<PoweredLightComponent>(toggleFixture).On, Is.EqualTo(startsOn));
            Assert.That(entities.GetComponent<PoweredLightComponent>(darkFixture).On, Is.False);
            AssertIndicator(startsOn, true);

            entities.EventBus.RaiseLocalEvent(lightSwitch, new ActivateInWorldEvent(lightSwitch, lightSwitch, true));
            Assert.That(entities.GetComponent<PoweredLightComponent>(fixture).On, Is.EqualTo(!startsOn));
            Assert.That(entities.GetComponent<PoweredLightComponent>(toggleFixture).On, Is.EqualTo(!startsOn));
            Assert.That(entities.GetComponent<PoweredLightComponent>(darkFixture).On, Is.True);
            AssertIndicator(!startsOn, true);
            entities.GetComponent<ApcPowerReceiverComponent>(lightSwitch).PowerDisabled = true;
        });

        // Let the click delay expire so only the power check can block activation.
        await server.WaitRunTicks(60);
        await server.WaitAssertion(() =>
        {
            AssertIndicator(!startsOn, false);
            entities.EventBus.RaiseLocalEvent(lightSwitch, new ActivateInWorldEvent(lightSwitch, lightSwitch, true));
            Assert.That(entities.GetComponent<PoweredLightComponent>(fixture).On, Is.EqualTo(!startsOn));
            entities.GetComponent<ApcPowerReceiverComponent>(lightSwitch).PowerDisabled = false;
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            AssertIndicator(!startsOn, true);
            Assert.That(entities.GetComponent<PoweredLightComponent>(fixture).On, Is.EqualTo(!startsOn));
            maps.DeleteMap(mapId);
        });

        await pair.CleanReturnAsync();

        void AssertIndicator(bool on, bool powered)
        {
            Assert.That(appearance.TryGetData<SignalSwitchState>(lightSwitch, SignalSwitchVisuals.State, out var state), Is.True);
            Assert.That(state, Is.EqualTo(on ? SignalSwitchState.On : SignalSwitchState.Off));
            Assert.That(appearance.TryGetData<bool>(lightSwitch, PowerDeviceVisuals.Powered, out var hasPower), Is.True);
            Assert.That(hasPower, Is.EqualTo(powered));
        }
    }
}
