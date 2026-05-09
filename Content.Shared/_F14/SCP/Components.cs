using System;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Audio;

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

    public bool IsWatched = false;
}

//096 component
[Serializable, NetSerializable]
public enum SCP096State : byte
{
    Idle,      
    Charging,   
    Enraged,
    Dead
}

[Serializable, NetSerializable]
public enum SCP096Visuals : byte
{
    State
}

[Serializable, NetSerializable]
public enum SCP096VisualLayers : byte
{
    Base
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SCP096Component : Component
{
    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public SCP096State State = SCP096State.Idle;

    [AutoNetworkedField]
    public SCP096State PrevState = SCP096State.Idle; 

    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? Target = null;

    [DataField("chargeTime")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float ChargeTime = 20f; 
    
    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float CurrentChargeTimer = 0f;

    [DataField("calmSpeed")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float CalmSpeed = 0f;

    [DataField("enragedSpeed")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float EnragedSpeed = 15f;

    [DataField("idleSound")]
    public SoundSpecifier? IdleSound;

    [DataField("chargeSound")]
    public SoundSpecifier? ChargeSound;

    [DataField("enrageSound")]
    public SoundSpecifier? EnrageSound;
}