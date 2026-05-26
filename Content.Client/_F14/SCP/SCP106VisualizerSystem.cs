using Content.Shared._F14.SCP;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using System;

namespace Content.Client._F14.SCP;

[RegisterComponent]
public sealed partial class SCP106VisualsComponent : Component
{
    public bool IsSubmerged = false;
}

public sealed class SCP106VisualizerSystem : VisualizerSystem<SCP106Component>
{
    [Dependency] private readonly AnimationPlayerSystem _anim = default!;

    private const string SinkAnimId   = "scp106_sink";
    private const string EmergeAnimId = "scp106_emerge";

    private static readonly TimeSpan AnimDuration = TimeSpan.FromSeconds(3.0);

    protected override void OnAppearanceChange(
        EntityUid uid,
        SCP106Component comp,
        ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        AppearanceSystem.TryGetData<bool>(
            uid, SCP106Visuals.Submerged, out var submerged, args.Component);

        AppearanceSystem.TryGetData<bool>(
            uid, SCP106Visuals.FlashlightSlowed, out var slowed, args.Component);

        var visComp = EnsureComp<SCP106VisualsComponent>(uid);

        if (!args.Sprite.LayerMapTryGet(SCP106VisualLayers.Base, out var layer))
            return;

        // 1. Якщо стан ДІЙСНО змінився
        if (submerged != visComp.IsSubmerged)
        {
            visComp.IsSubmerged = submerged;

            if (submerged)
            {
                _anim.Stop(uid, EmergeAnimId);
                PlaySinking(uid);
                
                // ВИПРАВЛЕНО: Явно вказуємо повний шлях до ігрового DrawDepth
                args.Sprite.DrawDepth = (int) Content.Shared.DrawDepth.DrawDepth.FloorObjects; 
            }
            else
            {
                _anim.Stop(uid, SinkAnimId);
                PlayEmerging(uid);
                
                // ВИПРАВЛЕНО: Повертаємо на рівень мобів
                args.Sprite.DrawDepth = (int) Content.Shared.DrawDepth.DrawDepth.Mobs;
            }
        }
        // 2. Якщо гравець щойно підключився або анімація вже закінчилася (Підстраховка)
        else
        {
            if (submerged && !_anim.HasRunningAnimation(uid, SinkAnimId))
            {
                args.Sprite.LayerSetState(layer, "submerged");
                args.Sprite.Color = new Color(1f, 1f, 1f, 0.05f);
                args.Sprite.DrawDepth = (int) Content.Shared.DrawDepth.DrawDepth.FloorObjects; 
            }
            else if (!submerged && !_anim.HasRunningAnimation(uid, EmergeAnimId))
            {
                args.Sprite.LayerSetState(layer, "alive");
                args.Sprite.DrawDepth = (int) Content.Shared.DrawDepth.DrawDepth.Mobs; 
            }
        }

        // 3. Світло від ліхтарика — Зберігаємо прозорість
        if (!submerged)
        {
            var currentAlpha = args.Sprite.Color.A;
            args.Sprite.Color = slowed
                ? new Color(1f, 0.30f, 0.30f, currentAlpha)
                : new Color(1f, 1f, 1f, currentAlpha);
        }
    }

    private void PlaySinking(EntityUid uid)
    {
        var anim = new Animation
        {
            Length = AnimDuration,
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick
                {
                    LayerKey  = SCP106VisualLayers.Base,
                    KeyFrames =
                    {
                        new AnimationTrackSpriteFlick.KeyFrame("sinking",   0.0f),
                        new AnimationTrackSpriteFlick.KeyFrame("submerged", (float)AnimDuration.TotalSeconds - 0.05f),
                    }
                },
                new AnimationTrackComponentProperty
                {
                    ComponentType     = typeof(SpriteComponent),
                    Property          = nameof(SpriteComponent.Color),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Color.White,                   0.0f),
                        new AnimationTrackProperty.KeyFrame(new Color(1f, 1f, 1f, 0.05f), (float)AnimDuration.TotalSeconds),
                    }
                }
            }
        };
        _anim.Play(uid, anim, SinkAnimId);
    }

    private void PlayEmerging(EntityUid uid)
    {
        var anim = new Animation
        {
            Length = AnimDuration,
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick
                {
                    LayerKey  = SCP106VisualLayers.Base,
                    KeyFrames =
                    {
                        new AnimationTrackSpriteFlick.KeyFrame("emerging", 0.0f),
                        new AnimationTrackSpriteFlick.KeyFrame("alive",    (float)AnimDuration.TotalSeconds - 0.05f),
                    }
                },
                new AnimationTrackComponentProperty
                {
                    ComponentType     = typeof(SpriteComponent),
                    Property          = nameof(SpriteComponent.Color),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(new Color(1f, 1f, 1f, 0.05f), 0.0f),
                        new AnimationTrackProperty.KeyFrame(Color.White,                   (float)AnimDuration.TotalSeconds),
                    }
                }
            }
        };
        _anim.Play(uid, anim, EmergeAnimId);
    }
}