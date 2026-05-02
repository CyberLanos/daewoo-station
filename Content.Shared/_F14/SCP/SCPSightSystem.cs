using System.Numerics;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
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

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var scpQuery = EntityQueryEnumerator<SCPFreezeOnSightComponent, TransformComponent>();
        while (scpQuery.MoveNext(out var scpUid, out var scpComp, out var scpXform))
        {
            bool isWatched = false;
            var scpPos = _transform.GetWorldPosition(scpXform);

            var playerQuery = EntityQueryEnumerator<Robust.Shared.Player.ActorComponent, TransformComponent>();
            while (playerQuery.MoveNext(out var pUid, out var actor, out var pXform))
            {
                if (TryComp<BlinkingComponent>(pUid, out var blink) && blink.IsBlinking)
                    continue;

                if (scpXform.MapID != pXform.MapID) continue;

                var pPos = _transform.GetWorldPosition(pXform);
                var dirToScp = scpPos - pPos;

                if (dirToScp.Length() > 15f) continue;

                var dirNormalized = dirToScp.Normalized();

                var pRot = _transform.GetWorldRotation(pXform).GetDir().ToVec();

                if (Vector2.Dot(dirNormalized, pRot) > 0.3f)
                {
                    isWatched = true;
                    break; 
                }
            }

            if (TryComp<PhysicsComponent>(scpUid, out var phys))
            {
                if (isWatched)
                {
                    _physics.SetLinearVelocity(scpUid, Vector2.Zero, body: phys);
                    _physics.SetBodyType(scpUid, BodyType.Static, body: phys);
                }
                else
                {
                    _physics.SetBodyType(scpUid, BodyType.KinematicController, body: phys);
                }
            }

            if (TryComp<MovementSpeedModifierComponent>(scpUid, out var move))
            {
                if (isWatched)
                    _movementSys.ChangeBaseSpeed(scpUid, 0f, 0f, 0f, move);
                else
                    _movementSys.ChangeBaseSpeed(scpUid, scpComp.WalkSpeed, scpComp.SprintSpeed, 20f, move);
            }
        }
    }
}
