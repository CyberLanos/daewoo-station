using Content.Shared._F14.SCP;
using Robust.Shared.GameObjects;
using System.Numerics;

namespace Content.Server._F14.SCP;

public sealed class BlinkingSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<BlinkChangedEvent>(OnBlinkRequest);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BlinkingComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var blink, out var pXform))
        {
            if (blink.IsBlinking && !blink.IsAutoBlinking)
                continue;


            bool isScpNear = false;
            var pPos = _transform.GetWorldPosition(pXform);

            var scpQuery = EntityQueryEnumerator<SCPFreezeOnSightComponent, TransformComponent>();
            while (scpQuery.MoveNext(out var scpUid, out var scpComp, out var scpXform))
            {
                if (pXform.MapID != scpXform.MapID) continue;

                if ((_transform.GetWorldPosition(scpXform) - pPos).Length() < 20f)
                {
                    isScpNear = true;
                    break;
                }
            }

            if (!isScpNear)
            {
                if (blink.IsAutoBlinking)
                {
                    blink.IsBlinking = false;
                    blink.IsAutoBlinking = false;
                    Dirty(uid, blink);
                }
                blink.CurrentTimer = blink.BlinkInterval;
                continue;
            }

            
            blink.CurrentTimer -= frameTime;

            if (blink.CurrentTimer <= 0f)
            {
                if (!blink.IsAutoBlinking)
                {
                    // shuts your eyes, the world is a scary place, especially with a BIG DIG RANDY nearby
                    blink.IsBlinking = true;
                    blink.IsAutoBlinking = true;
                    blink.CurrentTimer = blink.BlinkDuration; 
                    Dirty(uid, blink);
                }
                else
                {
                    // open your eyes! MY LITTLE DARK AGE! ehm... sorry
                    blink.IsBlinking = false;
                    blink.IsAutoBlinking = false;
                    blink.CurrentTimer = blink.BlinkInterval; // start timer
                    Dirty(uid, blink);
                }
            }
        }
    }

    private void OnBlinkRequest(BlinkChangedEvent ev, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        if (player == null) return;

        if (TryComp<BlinkingComponent>(player.Value, out var blink))
        {
            blink.IsBlinking = ev.IsBlinking;
            blink.IsAutoBlinking = false; 
            Dirty(player.Value, blink);
        }
    }
}
