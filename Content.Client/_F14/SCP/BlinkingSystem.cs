using Content.Shared._F14.SCP;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Shared.Input.Binding;

namespace Content.Client._F14.SCP;

public sealed class BlinkingSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IInputManager _inputMan = default!;

    public override void Initialize()
    {
        base.Initialize();

        if (!_overlayMan.HasOverlay<BlinkOverlay>())
            _overlayMan.AddOverlay(new BlinkOverlay());

        if (_inputMan.Contexts.TryGetContext("common", out var context))
        {
            context.AddFunction(F14KeyFunctions.Blink);
        }

        _inputMan.SetInputCommand(F14KeyFunctions.Blink,
            InputCmdHandler.FromDelegate(
                session => RaiseNetworkEvent(new BlinkChangedEvent(true)),
                session => RaiseNetworkEvent(new BlinkChangedEvent(false))
            ));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay<BlinkOverlay>();
    }
}
