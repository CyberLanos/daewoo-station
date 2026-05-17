using Content.Shared._F14.SCP;
using Robust.Client.GameObjects;

namespace Content.Client._F14.SCP;

public sealed class SCP106VisualizerSystem : VisualizerSystem<SCP106Component>
{
#pragma warning disable CS0618
    protected override void OnAppearanceChange(EntityUid uid, SCP106Component component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<SCP106State>(uid, SCP106Visuals.State, out var state, args.Component))
            return;

        switch (state)
        {
            case SCP106State.Idle:
                args.Sprite.LayerSetState(0, "alive");
                break;
                
            case SCP106State.Sinking:
                args.Sprite.LayerSetState(0, "sinking");
                break;
                
            case SCP106State.Phased:
                args.Sprite.LayerSetState(0, "phased");
                break;
                
            case SCP106State.Emerging:
                args.Sprite.LayerSetState(0, "emerging");
                break;
        }
    }
#pragma warning restore CS0618
}