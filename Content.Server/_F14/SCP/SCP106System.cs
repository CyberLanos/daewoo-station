using Content.Shared._F14.SCP;
using Content.Shared.Flash;
using Content.Shared.Flash.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Maps;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Server.Flash;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._F14.SCP;

public sealed class SCP106System : EntitySystem
{
    [Dependency] private readonly IRobustRandom               _random      = default!;
    [Dependency] private readonly IMapManager                 _mapManager  = default!;
    [Dependency] private readonly ITileDefinitionManager      _tileDefMan  = default!;
    [Dependency] private readonly IGameTiming                 _timing      = default!;
    [Dependency] private readonly TransformSystem             _transform   = default!;
    [Dependency] private readonly SharedPhysicsSystem         _physics     = default!;
    [Dependency] private readonly MapSystem                   _mapSystem   = default!;
    [Dependency] private readonly SharedPopupSystem           _popup       = default!;
    [Dependency] private readonly AppearanceSystem            _appearance  = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed       = default!;
    [Dependency] private readonly EntityLookupSystem          _lookup      = default!;

    private const string MainFixture = "fix1";

    private const float LightCheckInterval = 0.25f;
    private float _lightCheckTimer = 0f;

    private const float DecayInterval = 0.5f;
    private float _decayTimer = 0f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SCP106Component, FlashAttemptEvent>(OnFlashed);
        SubscribeLocalEvent<SCP106Component, MeleeHitEvent>(OnMeleeHit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _decayTimer      += frameTime;
        _lightCheckTimer += frameTime;

        bool doDecay = _decayTimer      >= DecayInterval;
        bool doLight = _lightCheckTimer >= LightCheckInterval;

        if (!doDecay && !doLight)
            return;

        if (doDecay) _decayTimer      = 0f;
        if (doLight) _lightCheckTimer = 0f;

        var query = EntityQueryEnumerator<SCP106Component, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (doDecay) TryDecayTile(uid, comp, xform);
            if (doLight) UpdateFlashlightFear(uid, comp, xform);
        }
    }

    private void TryDecayTile(EntityUid uid, SCP106Component comp, TransformComponent xform)
    {
        if (!_random.Prob(comp.TileDecayChance)) return;

        var gridUid = xform.GridUid;
        if (gridUid == null) return;
        if (!TryComp<MapGridComponent>(gridUid, out var grid)) return;

        var tileRef = _mapSystem.GetTileRef(gridUid.Value, grid, xform.Coordinates);

        if (!_tileDefMan.TryGetDefinition("FloorDecayed", out var decayDef))
            _tileDefMan.TryGetDefinition("FloorDirt", out decayDef);

        if (decayDef == null) return;

        _mapSystem.SetTile(gridUid.Value, grid, tileRef.GridIndices, new Tile(decayDef.TileId));
    }

    private void OnFlashed(EntityUid uid, SCP106Component comp, FlashAttemptEvent args)
    {
        Submerge(uid, comp);
    }

    private void UpdateFlashlightFear(EntityUid uid, SCP106Component comp, TransformComponent xform)
    {
        bool illuminated = IsIlluminatedByFlashlight(uid);

        if (illuminated)
        {
            comp.FlashlightSlowTimer = comp.FlashlightLingerTime;

            if (!comp.IsFlashlightSlowed)
            {
                comp.IsFlashlightSlowed = true;
                ApplyFlashlightSlow(uid, comp);
                _appearance.SetData(uid, SCP106Visuals.FlashlightSlowed, true);
            }
        }
        else if (comp.IsFlashlightSlowed)
        {
            comp.FlashlightSlowTimer -= LightCheckInterval;
            if (comp.FlashlightSlowTimer <= 0f)
            {
                comp.IsFlashlightSlowed = false;
                RemoveFlashlightSlow(uid);
                _appearance.SetData(uid, SCP106Visuals.FlashlightSlowed, false);
                Dirty(uid, comp);
            }
        }
    }

    private bool IsIlluminatedByFlashlight(EntityUid uid)
    {
        const float range = 8f;

        foreach (var nearEnt in _lookup.GetEntitiesInRange(uid, range))
        {
            if (nearEnt == uid) continue;
            if (!TryComp<PointLightComponent>(nearEnt, out var light)) continue;
            if (!light.Enabled) continue;

            return true;
        }
        return false;
    }

    private void ApplyFlashlightSlow(EntityUid uid, SCP106Component comp)
    {
        var slowComp = EnsureComp<SCP106FlashlightSlowedComponent>(uid);
        slowComp.SpeedMultiplier = comp.FlashlightSpeedMult;
        _speed.RefreshMovementSpeedModifiers(uid);
    }

    private void RemoveFlashlightSlow(EntityUid uid)
    {
        RemCompDeferred<SCP106FlashlightSlowedComponent>(uid);
        _speed.RefreshMovementSpeedModifiers(uid);
    }

    private void OnMeleeHit(EntityUid uid, SCP106Component comp, MeleeHitEvent args)
    {
        foreach (var hit in args.HitEntities)
        {
            if (HasComp<SCP106Component>(hit)) continue;

            TeleportToPocket(hit, comp);

            _popup.PopupEntity(
                Loc.GetString("scp106-pocket-teleport"),
                hit, hit, PopupType.LargeCaution);
        }
    }

    public void Submerge(EntityUid uid, SCP106Component comp)
    {
        if (comp.IsSubmerged) return;

        comp.IsSubmerged = true;
        EnsureComp<SCP106SubmergedComponent>(uid);
        Dirty(uid, comp);

        if (TryComp<FixturesComponent>(uid, out var fixturesSub)
            && fixturesSub.Fixtures.TryGetValue(MainFixture, out var fixtureSub))
        {
            _physics.SetCollisionMask(uid, MainFixture, fixtureSub, 0, fixturesSub);
        }

        _appearance.SetData(uid, SCP106Visuals.Submerged, true);
    }

    public void Emerge(EntityUid uid, SCP106Component comp)
    {
        if (!comp.IsSubmerged) return;

        comp.IsSubmerged = false;
        RemCompDeferred<SCP106SubmergedComponent>(uid);
        Dirty(uid, comp);

        if (TryComp<FixturesComponent>(uid, out var fixturesEm)
            && fixturesEm.Fixtures.TryGetValue(MainFixture, out var fixtureEm))
        {
            _physics.SetCollisionMask(uid, MainFixture, fixtureEm, (int)CollisionGroup.MobMask, fixturesEm);
        }

        _appearance.SetData(uid, SCP106Visuals.Submerged, false);
    }
    public void TeleportToPocket(EntityUid victim, SCP106Component comp)
    {
        MapId? pocketMap = null;

        foreach (var mapId in _mapManager.GetAllMapIds())
        {
            var mapEnt = _mapManager.GetMapEntityId(mapId);
            if (HasComp<SCPPocketDimensionComponent>(mapEnt))
            {
                pocketMap = mapId;
                break;
            }
        }

        if (pocketMap == null) return;

        _transform.SetMapCoordinates(victim, new MapCoordinates(0f, 0f, pocketMap.Value));
    }
}
