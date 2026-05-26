using Content.Shared._F14.SCP;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using System; // Потрібен для TimeSpan

namespace Content.Client._F14.SCP;

public sealed class SCP457VisualizerSystem : VisualizerSystem<AppearanceComponent>
{
    [Dependency] private readonly AnimationPlayerSystem _anim = default!;

    private const string IdleAnimId    = "scp457_idle";
    private const string AttackAnimId  = "scp457_attack";

    protected override void OnAppearanceChange(EntityUid uid, AppearanceComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (AppearanceSystem.TryGetData<bool>(uid, SCP457Visuals.Attacking, out var attacking, comp))
        {
            if (attacking)
                PlayAttack(uid, args.Sprite);
            else
                EnsureIdle(uid, args.Sprite);
        }
        else
        {
            EnsureIdle(uid, args.Sprite);
        }
    }

    private void EnsureIdle(EntityUid uid, SpriteComponent sprite)
    {
        if (_anim.HasRunningAnimation(uid, IdleAnimId))
            return;

        _anim.Stop(uid, AttackAnimId);

        // ЗАХИСТ ВІД КРАШУ: Якщо шар не знайдено (текстура не завантажилася), просто виходимо, гра не вилетить!
        if (!sprite.LayerMapTryGet(SCP457VisualLayers.Base, out var _))
            return;

        var anim = new Animation
        {
            Length = TimeSpan.FromSeconds(0.64),
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick
                {
                    LayerKey = SCP457VisualLayers.Base,
                    KeyFrames = { new AnimationTrackSpriteFlick.KeyFrame("idle", 0.0f) }
                },
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(new Color(1.0f, 0.85f, 0.4f, 1f), 0.00f),
                        new AnimationTrackProperty.KeyFrame(new Color(1.0f, 0.55f, 0.1f, 1f), 0.32f),
                        new AnimationTrackProperty.KeyFrame(new Color(1.0f, 0.85f, 0.4f, 1f), 0.64f),
                    }
                }
            }
        };

        _anim.Play(uid, anim, IdleAnimId);
    }

    private void PlayAttack(EntityUid uid, SpriteComponent sprite)
    {
        if (_anim.HasRunningAnimation(uid, AttackAnimId))
            return;

        _anim.Stop(uid, IdleAnimId);

        // ЗАХИСТ ВІД КРАШУ
        if (!sprite.LayerMapTryGet(SCP457VisualLayers.Base, out var _))
            return;

        var anim = new Animation
        {
            Length = TimeSpan.FromSeconds(0.32),
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick
                {
                    LayerKey = SCP457VisualLayers.Base,
                    KeyFrames = { new AnimationTrackSpriteFlick.KeyFrame("attacking", 0.0f) }
                },
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(new Color(1.0f, 1.0f, 0.6f, 1f), 0.00f),
                        new AnimationTrackProperty.KeyFrame(new Color(1.0f, 0.5f, 0.1f, 1f), 0.32f),
                    }
                }
            }
        };

        _anim.Play(uid, anim, AttackAnimId);
    }
}