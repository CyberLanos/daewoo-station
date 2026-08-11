using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._F14.Doors;

/// <summary>
/// Adds blockers to doors spanning multiple tiles.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MultiTileDoorComponent : Component
{
    /// <summary>
    /// Extra covered tiles, relative to an unrotated door.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public List<Vector2i> Offsets = new();

    [DataField]
    public EntProtoId Blocker = "MultiTileDoorBlocker";

    [DataField]
    public List<EntityUid> Blockers = new();

    /// <summary>
    /// Rotates an offset to match the door orientation.
    /// </summary>
    public static Vector2i Rotate(Vector2i offset, Angle rotation)
    {
        var steps = (int) Math.Round(rotation.Theta / MathHelper.PiOver2) & 3;
        for (var i = 0; i < steps; i++)
        {
            offset = new Vector2i(-offset.Y, offset.X);
        }

        return offset;
    }
}
