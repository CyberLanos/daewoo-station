using Content.Shared._F14.SCP;
using Robust.Client.GameObjects;

namespace Content.Client._F14.SCP;

public sealed class SCP096VisualizerSystem : VisualizerSystem<SCP096Component>
{
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

        switch (state)
        {
            case SCP096State.Idle:
                args.Sprite.LayerSetState(0, "alive");
                args.Sprite.Rotation = Angle.Zero; 
                break;
            case SCP096State.Charging:
                args.Sprite.LayerSetState(0, "screaming");
                args.Sprite.Rotation = Angle.Zero;
                break;
            case SCP096State.Enraged:
                args.Sprite.LayerSetState(0, "running");
                args.Sprite.Rotation = Angle.Zero;
                break;
            case SCP096State.Dead:
                args.Sprite.LayerSetState(0, "dead");
                args.Sprite.Rotation = Angle.FromDegrees(90);
                break;
        }
    }
}