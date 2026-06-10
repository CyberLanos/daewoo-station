using Content.Shared._F14.SCP;
using Robust.Shared.GameObjects;
using System.Numerics;
using Content.Shared._Pirate.ZLevels.Core.Components; 

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
                bool sameZNetwork = (scpXform.MapID == pXform.MapID);

                if (!sameZNetwork && scpXform.GridUid != null && pXform.GridUid != null)
                {
                    if (TryComp<CEZLinkedGridComponent>(pXform.GridUid.Value, out var pLinked))
                    {
                        foreach (var peerGrid in pLinked.PeerGrids.Values)
                        {
                            if (peerGrid == scpXform.GridUid.Value)
                            {
                                sameZNetwork = true;
                                break;
                            }
                        }
                    }
                }

                if (!sameZNetwork) continue;

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
                    blink.IsBlinking = true;
                    blink.IsAutoBlinking = true;
                    blink.CurrentTimer = blink.BlinkDuration; 
                    Dirty(uid, blink);
                }
                else
                {
                    blink.IsBlinking = false;
                    blink.IsAutoBlinking = false;
                    blink.CurrentTimer = blink.BlinkInterval;
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