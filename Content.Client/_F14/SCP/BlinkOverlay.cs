using Content.Shared._F14.SCP;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using Robust.Shared.IoC;
using Robust.Shared.GameObjects;

namespace Content.Client._F14.SCP;

public sealed class BlinkOverlay : Overlay
{
    private readonly IEntityManager _entMan;
    private readonly IPlayerManager _playerMan;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public BlinkOverlay()
    {
        _entMan = IoCManager.Resolve<IEntityManager>();
        _playerMan = IoCManager.Resolve<IPlayerManager>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var player = _playerMan.LocalSession?.AttachedEntity;
        if (player == null) return;

        if (_entMan.TryGetComponent<BlinkingComponent>(player.Value, out var blink) && blink.IsBlinking)
        {
            args.WorldHandle.DrawRect(args.WorldBounds, Color.Black);
        }
    }
}
