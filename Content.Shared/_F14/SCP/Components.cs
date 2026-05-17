using System;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Audio;
using Robust.Shared.Map; 
using Content.Shared.Actions;
using Robust.Shared.Serialization; 
using Content.Shared.DoAfter;

namespace Content.Shared._F14.SCP;

// Blinking component for SCP-173
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlinkingComponent : Component
{
    [AutoNetworkedField] public bool IsBlinking = false;
    [AutoNetworkedField] public float CurrentTimer = 7f;
    [DataField("blinkInterval")] public float BlinkInterval = 7f;
    [DataField("blinkDuration")] public float BlinkDuration = 0.50f;
    [AutoNetworkedField] public bool IsAutoBlinking = false;
}

[Serializable, NetSerializable]
public sealed class BlinkChangedEvent : EntityEventArgs
{
    public bool IsBlinking { get; }
    public BlinkChangedEvent(bool isBlinking) => IsBlinking = isBlinking;
}

// SCP-173
[RegisterComponent]
public sealed partial class SCPFreezeOnSightComponent : Component
{
    [DataField("walkSpeed")] public float WalkSpeed = 8f;
    [DataField("sprintSpeed")] public float SprintSpeed = 12f;
    public bool IsWatched = false;
}

// SCP-096
[Serializable, NetSerializable]
public enum SCP096State : byte { Idle, Charging, Enraged, Dead }

[Serializable, NetSerializable]
public enum SCP096Visuals : byte { State }

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SCP096Component : Component
{
    [AutoNetworkedField] public SCP096State State = SCP096State.Idle;
    [AutoNetworkedField] public SCP096State PrevState = SCP096State.Idle; 
    [AutoNetworkedField] public EntityUid? Target = null;
    [DataField("chargeTime")] public float ChargeTime = 20f; 
    [AutoNetworkedField] public float CurrentChargeTimer = 0f;
    [DataField("calmSpeed")] public float CalmSpeed = 0f;
    [DataField("enragedSpeed")] public float EnragedSpeed = 15f;
    [DataField("idleSound")] public SoundSpecifier? IdleSound;
    [DataField("chargeSound")] public SoundSpecifier? ChargeSound;
    [DataField("enrageSound")] public SoundSpecifier? EnrageSound;
}

// SCP-106
[Serializable, NetSerializable]
public enum SCP106State : byte { Idle, Sinking, Phased, Emerging }

[Serializable, NetSerializable]
public enum SCP106Visuals : byte { State }

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SCP106Component : Component
{
    [AutoNetworkedField] public SCP106State State = SCP106State.Idle;
    [AutoNetworkedField] public float CurrentTimer = 0f;
    [DataField("normalSpeed")] public float NormalSpeed = 2.5f;
    [DataField("slowedSpeed")] public float SlowedSpeed = 0.8f;
    [DataField("pocketDimensionMap")] public MapId? PocketDimensionMap;
    [DataField("pocketDimensionX")] public float PocketDimensionX = 0f;
    [DataField("pocketDimensionY")] public float PocketDimensionY = 0f;
    [DataField("phaseAction")] public string? PhaseAction = "ActionSCP106Phase";
    [DataField] public EntityUid? PhaseActionEntity;
}

[RegisterComponent]
public sealed partial class AntiPhaseWallComponent : Component { }

[DataDefinition]
public sealed partial class SCP106PhaseActionEvent : InstantActionEvent { }


//scp scp scp 049
[RegisterComponent]
public sealed partial class SCP049Component : Component { }

//SCP-049-2
[RegisterComponent]
public sealed partial class SCP0492Component : Component { }
[Serializable, NetSerializable]
public sealed partial class SCP049CureDoAfterEvent : SimpleDoAfterEvent { }