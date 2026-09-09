using Content.Shared.Turrets;

namespace Content.Shared._Pirate.Turrets;

/// <summary>
/// Puts a <see cref="DeployableTurretComponent"/> into a chosen armament state as soon as it is
/// map initialized, so mapped turrets can begin the round already deployed (and on their lethal
/// fire mode) without anyone having to visit a turret control panel first.
/// </summary>
[RegisterComponent]
public sealed partial class TurretStartupStateComponent : Component
{
    /// <summary>
    /// The armament state to apply on map init. Uses the same indices as
    /// <see cref="Content.Shared.TurretController.DeployableTurretControllerComponent.ArmamentState"/>:
    /// -1 leaves the turret retracted (safe), 0 is the first fire mode (stun), 1 the second (lethal), etc.
    /// </summary>
    [DataField]
    public int ArmamentState = 1;
}
