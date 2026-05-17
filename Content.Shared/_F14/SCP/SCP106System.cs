using System;
using System.Numerics;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared._F14.SCP;
using Content.Shared.Damage;
using Content.Shared.StatusEffect; 
using Content.Shared.Light.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Actions;
using Content.Shared.Popups; 
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Network;

namespace Content.Shared._F14.SCP;

public sealed class SharedSCP106System : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSys = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _net = default!; 

    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<SCP106Component, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SCP106Component, SCP106PhaseActionEvent>(OnPhaseAction);
        SubscribeLocalEvent<SCP106Component, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<SCP106Component, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<SCP106Component, StatusEffectAddedEvent>(OnStatusEffect);
    }

    private void OnMapInit(EntityUid uid, SCP106Component component, MapInitEvent args)
    {
        if (_net.IsServer && component.PhaseAction != null)
            _actions.AddAction(uid, ref component.PhaseActionEntity, component.PhaseAction);
    }

    private void OnPhaseAction(EntityUid uid, SCP106Component component, SCP106PhaseActionEvent args)
    {
        args.Handled = true; 

        if (component.State == SCP106State.Idle)
        {
            
            _popup.PopupEntity("You started sinking uderground", uid, uid);
            SetState(uid, component, SCP106State.Sinking, 3.0f);
        }
        else
        {
            _popup.PopupEntity($"I can't, currnet state: {component.State}", uid, uid);
        }
    }

    private void OnDamageModify(EntityUid uid, SCP106Component component, DamageModifyEvent args)
    {
        args.Damage = new DamageSpecifier();
    }

    private void OnStatusEffect(EntityUid uid, SCP106Component component, StatusEffectAddedEvent args)
    {
        if ((args.Key == "Flashed" || args.Key == "Stun") && component.State == SCP106State.Idle)
            SetState(uid, component, SCP106State.Sinking, 3.0f);
    }

    private void OnMeleeHit(EntityUid uid, SCP106Component component, MeleeHitEvent args)
    {
        if (component.PocketDimensionMap == null || component.State != SCP106State.Idle) return;
        foreach (var target in args.HitEntities)
        {
            if (HasComp<ActorComponent>(target))
            {
                var coords = new MapCoordinates(new Vector2(component.PocketDimensionX, component.PocketDimensionY), component.PocketDimensionMap.Value);
                _transform.SetMapCoordinates(target, coords);
            }
        }
        args.ModifiersList.Clear(); 
    }

    private void SetState(EntityUid uid, SCP106Component comp, SCP106State state, float timer)
    {
        comp.State = state;
        comp.CurrentTimer = timer;
        Dirty(uid, comp);
        _appearance.SetData(uid, SCP106Visuals.State, state);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<SCP106Component, TransformComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform, out var physics))
        {
            comp.CurrentTimer -= frameTime;
            switch (comp.State)
            {
                case SCP106State.Idle:
                    if (!physics.CanCollide)
                        _physics.SetCanCollide(uid, true, body: physics);

                    bool isSlowed = false;
                    var scpPos = _transform.GetWorldPosition(xform);
                    var lightQuery = EntityQueryEnumerator<HandheldLightComponent, TransformComponent>();
                    while (lightQuery.MoveNext(out var lUid, out var lComp, out var lXform))
                    {
                        if (lComp.Activated && lXform.MapID == xform.MapID && (_transform.GetWorldPosition(lXform) - scpPos).Length() < 6f)
                        {
                            isSlowed = true; break;
                        }
                    }
                    var speed = isSlowed ? comp.SlowedSpeed : comp.NormalSpeed;
                    _movementSys.ChangeBaseSpeed(uid, speed, speed, 20f);
                    break;

                case SCP106State.Sinking:
                    _movementSys.ChangeBaseSpeed(uid, 0f, 0f, 20f);
                    _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
                    
                    if (comp.CurrentTimer <= 0f)
                    {
                        _popup.PopupEntity("phasing enabled, you can now walk through walls!", uid, uid);
                        SetState(uid, comp, SCP106State.Phased, 8.0f);
                        _physics.SetCanCollide(uid, false, body: physics);
                    }
                    break;

                case SCP106State.Phased:
                    _movementSys.ChangeBaseSpeed(uid, comp.NormalSpeed, comp.NormalSpeed, 20f);
                    if (physics.CanCollide)
                        _physics.SetCanCollide(uid, false, body: physics);

                    var nearEntities = _lookup.GetEntitiesInRange(xform.MapPosition, 0.5f);
                    foreach (var ent in nearEntities)
                    {
                        if (HasComp<AntiPhaseWallComponent>(ent))
                        {
                            SetState(uid, comp, SCP106State.Emerging, 3.0f); break;
                        }
                    }
                    if (comp.CurrentTimer <= 0f) SetState(uid, comp, SCP106State.Emerging, 3.0f);
                    break;

                case SCP106State.Emerging:
                    _movementSys.ChangeBaseSpeed(uid, 0f, 0f, 20f);
                    _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
                    if (comp.CurrentTimer <= 0f)
                    {
                        SetState(uid, comp, SCP106State.Idle, 0f);
                        _physics.SetCanCollide(uid, true, body: physics);
                    }
                    break;
            }
        }
    }
}