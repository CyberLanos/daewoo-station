using Content.Shared._F14.SCP;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using System;

namespace Content.Client._F14.SCP;

public sealed class SCP096VisualizerSystem : VisualizerSystem<SCP096Component>
{
    [Dependency] private readonly AnimationPlayerSystem _anim = default!;

    private const string ScreamAnimId = "scp096_scream";
    
    private static readonly TimeSpan ScreamDuration = TimeSpan.FromSeconds(3.6);

    public override void Initialize()
    {
        base.Initialize();
    }

    protected override void OnAppearanceChange(EntityUid uid, SCP096Component component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<SCP096State>(uid, SCP096Visuals.State, out var state, args.Component))
            return;

        if (state != SCP096State.Charging && _anim.HasRunningAnimation(uid, ScreamAnimId))
        {
            _anim.Stop(uid, ScreamAnimId);
        }

        switch (state)
        {
            case SCP096State.Idle:
                args.Sprite.LayerSetState("base", "alive");
                args.Sprite.Rotation = Angle.Zero; 
                break;
            case SCP096State.Charging:
                args.Sprite.Rotation = Angle.Zero;
                PlayScreaming(uid);
                break;
            case SCP096State.Enraged:
                args.Sprite.LayerSetState("base", "running");
                args.Sprite.Rotation = Angle.Zero;
                break;
            case SCP096State.Dead:
                args.Sprite.LayerSetState("base", "dead");
                args.Sprite.Rotation = Angle.FromDegrees(90);
                break;
        }
    }

    private void PlayScreaming(EntityUid uid)
    {
        if (_anim.HasRunningAnimation(uid, ScreamAnimId))
            return;

        var anim = new Animation
        {
            Length = ScreamDuration,
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick
                {
                    LayerKey = "base", // Змінено з 0 на "base"
                    KeyFrames =
                    {
                        new AnimationTrackSpriteFlick.KeyFrame("screaming", 0.0f)
                    }
                }
            }
        };
        
        _anim.Play(uid, anim, ScreamAnimId);
    }
}