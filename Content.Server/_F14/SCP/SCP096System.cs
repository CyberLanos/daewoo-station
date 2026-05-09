using System;
using System.Numerics;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Damage;
using Content.Shared.Mobs.Systems;
using Content.Shared.Inventory;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Weapons.Melee;
using Content.Shared._F14.SCP;
using Content.Server.NPC.Systems;
using Content.Server.NPC.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Audio.Systems;

namespace Content.Server._F14.SCP;

public sealed class SCP096System : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSys = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SCP096Component, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<SCP096Component, AttackAttemptEvent>(OnAttackAttempt);
    }

    private void OnAttackAttempt(EntityUid uid, SCP096Component component, AttackAttemptEvent args)
    {
        if (component.State != SCP096State.Enraged || args.Target != component.Target)
            args.Cancel();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var scpQuery = EntityQueryEnumerator<SCP096Component, TransformComponent, MovementSpeedModifierComponent>();
        while (scpQuery.MoveNext(out var scpUid, out var scpComp, out var scpXform, out var move))
        {
            if (_mobState.IsDead(scpUid))
            {
                if (scpComp.State != SCP096State.Dead)
                {
                    scpComp.State = SCP096State.Dead;
                    _appearance.SetData(scpUid, SCP096Visuals.State, scpComp.State);
                }
                continue;
            }

            if (scpComp.State != scpComp.PrevState)
            {
                _appearance.SetData(scpUid, SCP096Visuals.State, scpComp.State);
                
                var sound = scpComp.State switch
                {
                    SCP096State.Charging => scpComp.ChargeSound,
                    SCP096State.Enraged => scpComp.EnrageSound,
                    SCP096State.Idle => scpComp.IdleSound,
                    _ => null
                };

                if (sound != null)
                    _audio.PlayPvs(sound, scpUid);

                scpComp.PrevState = scpComp.State;
            }

            switch (scpComp.State)
            {
                case SCP096State.Idle:
                    _movementSys.ChangeBaseSpeed(scpUid, scpComp.CalmSpeed, scpComp.CalmSpeed, 20f, move);

                    if (_inventory.TryGetSlotEntity(scpUid, "head", out _)) continue;

                    var playerQuery = EntityQueryEnumerator<Robust.Shared.Player.ActorComponent, TransformComponent>();
                    var scpPos = _transform.GetWorldPosition(scpXform);

                    while (playerQuery.MoveNext(out var pUid, out _, out var pXform))
                    {
                        if (scpXform.MapID != pXform.MapID || HasComp<GhostComponent>(pUid)) continue;
                        if (_mobState.IsDead(pUid)) continue;

                        if (TryComp<BlinkingComponent>(pUid, out var blink) && blink.IsBlinking)
                            continue;

                        var pPos = _transform.GetWorldPosition(pXform);
                        var distToPlayer = (scpPos - pPos).Length();

                        if (distToPlayer > 15f) continue;
                        if (!_interaction.InRangeUnobstructed(pUid, scpUid, 15f)) continue;

                        var pRot = _transform.GetWorldRotation(pXform).GetDir().ToVec();
                        var scpRot = _transform.GetWorldRotation(scpXform).GetDir().ToVec();
                        var dirToScp = (scpPos - pPos).Normalized();
                        var dirToPlayer = (pPos - scpPos).Normalized();

                        if (Vector2.Dot(scpRot, dirToPlayer) < -0.1f) continue;

                        if (Vector2.Dot(pRot, dirToScp) > 0.4f)
                        {
                            scpComp.State = SCP096State.Charging;
                            scpComp.CurrentChargeTimer = scpComp.ChargeTime;
                            scpComp.Target = pUid;
                            break;
                        }
                    }
                    break;

                case SCP096State.Charging:
                    _movementSys.ChangeBaseSpeed(scpUid, 0f, 0f, 20f, move);
                    scpComp.CurrentChargeTimer -= frameTime;
                    
                    if (scpComp.CurrentChargeTimer <= 0f)
                    {
                        scpComp.State = SCP096State.Enraged;
                        EnsureComp<NPCSteeringComponent>(scpUid);
                        if (TryComp<PhysicsComponent>(scpUid, out var physEnrage))
                            _physics.SetSleepingAllowed(scpUid, physEnrage, false);
                    }
                    break;

                case SCP096State.Enraged:
                    _movementSys.ChangeBaseSpeed(scpUid, scpComp.EnragedSpeed, scpComp.EnragedSpeed, 20f, move);

                    if (scpComp.Target == null || !Exists(scpComp.Target.Value) || _mobState.IsDead(scpComp.Target.Value) || Deleted(scpComp.Target.Value))
                    {
                        scpComp.State = SCP096State.Idle;
                        scpComp.Target = null;
                        RemComp<NPCSteeringComponent>(scpUid); 
                        if (TryComp<PhysicsComponent>(scpUid, out var physIdle))
                            _physics.SetSleepingAllowed(scpUid, physIdle, true);
                        break;
                    }

                    var targetXform = Transform(scpComp.Target.Value);
                    
                    if (targetXform.MapID == scpXform.MapID)
                    {
                        _steering.Register(scpUid, targetXform.Coordinates);

                        var scpPosEnraged = _transform.GetWorldPosition(scpXform);
                        var targetPos = _transform.GetWorldPosition(targetXform);
                        var dist = (targetPos - scpPosEnraged).Length();

                        if (TryComp<PhysicsComponent>(scpUid, out var phys))
                        {
                            _physics.SetSleepingAllowed(scpUid, phys, false);

                            if (phys.LinearVelocity.Length() < 1f && dist > 1.6f)
                            {
                                var dirNorm = (targetPos - scpPosEnraged).Normalized();
                                _physics.SetLinearVelocity(scpUid, dirNorm * scpComp.EnragedSpeed, body: phys);
                                _transform.SetLocalRotation(scpUid, dirNorm.ToAngle());
                            }
                        }
                        
                        if (dist < 1.6f)
                        {
                            if (TryComp<MeleeWeaponComponent>(scpUid, out var weapon))
                            {
                                _damageable.TryChangeDamage(scpComp.Target.Value, weapon.Damage, true, origin: scpUid);
                                _audio.PlayPvs(weapon.HitSound, scpUid);
                            }
                        }
                    }
                    break;
            }
        }
    }

    private void OnCollide(EntityUid uid, SCP096Component component, ref StartCollideEvent args)
    {
        if (component.State != SCP096State.Enraged) return;
        var target = args.OtherEntity;

        if (HasComp<DoorComponent>(target) || (TryComp<PhysicsComponent>(target, out var phys) && phys.BodyType == BodyType.Static))
        {
            var dmg = new DamageSpecifier();
            dmg.DamageDict.Add("Blunt", 650); 
            _damageable.TryChangeDamage(target, dmg, true, origin: uid);
        }
    }
}