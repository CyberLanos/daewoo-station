using System;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._F14.SCP;

// blinking component
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlinkingComponent : Component
{
    [AutoNetworkedField]
    public bool IsBlinking = false;

    [AutoNetworkedField]
    public float CurrentTimer = 7f;

    [DataField("blinkInterval")]
    public float BlinkInterval = 7f;

    [DataField("blinkDuration")]
    public float BlinkDuration = 0.50f;

    [AutoNetworkedField]
    public bool IsAutoBlinking = false;
}

[Serializable, NetSerializable]
public sealed class BlinkChangedEvent : EntityEventArgs
{
    public bool IsBlinking { get; }

    public BlinkChangedEvent(bool isBlinking)
    {
        IsBlinking = isBlinking;
    }
}

//scp-173 component, please, do not use it for other entities
[RegisterComponent]
public sealed partial class SCPFreezeOnSightComponent : Component
{
    [DataField("walkSpeed")]
    public float WalkSpeed = 8f;

    [DataField("sprintSpeed")]
    public float SprintSpeed = 12f;
}
