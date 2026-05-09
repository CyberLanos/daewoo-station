using System.Numerics;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Ghost;
using Content.Shared.Interaction.Events;
using Robust.Shared.Maths; 
using Robust.Shared.Physics; 
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._F14.SCP;

public sealed class SCPSightSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSys = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SCPFreezeOnSightComponent, AttackAttemptEvent>(OnAttackAttempt);
    }

    private void OnAttackAttempt(EntityUid uid, SCPFreezeOnSightComponent component, AttackAttemptEvent args)
    {
        if (component.IsWatched)
            args.Cancel();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var scpQuery = EntityQueryEnumerator<SCPFreezeOnSightComponent, TransformComponent, PhysicsComponent, MovementSpeedModifierComponent>();
        while (scpQuery.MoveNext(out var scpUid, out var scpComp, out var scpXform, out var phys, out var move))
        {
            bool isWatched = false;
            var scpPos = _transform.GetWorldPosition(scpXform);

            var playerQuery = EntityQueryEnumerator<Robust.Shared.Player.ActorComponent, TransformComponent>();
            while (playerQuery.MoveNext(out var pUid, out var actor, out var pXform))
            {
                if (HasComp<GhostComponent>(pUid)) continue;

                if (TryComp<BlinkingComponent>(pUid, out var blink) && blink.IsBlinking)
                    continue;

                if (scpXform.MapID != pXform.MapID) continue;

                var pPos = _transform.GetWorldPosition(pXform);
                var vecToScp = scpPos - pPos;

                if (vecToScp.Length() > 15f) continue;

                var dirToScp = vecToScp.Normalized();
                var pRot = _transform.GetWorldRotation(pXform).GetDir().ToVec();

                if (Vector2.Dot(pRot, dirToScp) > 0.3f)
                {
                    isWatched = true;
                    break; 
                }
            }

            scpComp.IsWatched = isWatched;

            if (isWatched)
            {
                _physics.SetLinearVelocity(scpUid, Vector2.Zero, body: phys);
                _physics.SetBodyType(scpUid, BodyType.Static, body: phys);
                _movementSys.ChangeBaseSpeed(scpUid, 0f, 0f, 0f, move);
            }
            else
            {
                _physics.SetBodyType(scpUid, BodyType.KinematicController, body: phys);
                _movementSys.ChangeBaseSpeed(scpUid, scpComp.WalkSpeed, scpComp.SprintSpeed, 20f, move);
            }
        }
    }
}