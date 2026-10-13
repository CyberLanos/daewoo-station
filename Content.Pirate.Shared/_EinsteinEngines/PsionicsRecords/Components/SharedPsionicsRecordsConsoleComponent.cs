using Content.Shared.Psionics;
using Content.Shared.PsionicsRecords.Systems;
using Content.Shared.Radio;
using Content.Shared.StationRecords;
using Robust.Shared.Prototypes;

namespace Content.Shared.PsionicsRecords.Components;

[RegisterComponent]
[Access(typeof(SharedPsionicsRecordsConsoleSystem))]
public sealed partial class PsionicsRecordsConsoleComponent : Component
{
    // Keep selection server-owned so clients cannot race over the active record.
    [DataField]
    public uint? ActiveKey;

    // Echoed with each state to correlate selection replies.
    public uint SelectionRequest;

    [DataField]
    public StationRecordsFilter? Filter;

    /// <summary>
    /// Only list people with this psionics status; None lists everyone.
    /// </summary>
    [DataField]
    public PsionicsStatus FilterStatus = PsionicsStatus.None;

    /// <summary>
    /// Channel to send messages to when someone's status gets changed.
    /// </summary>
    [DataField]
    public ProtoId<RadioChannelPrototype> RadioChannel = "Science";

    [DataField]
    public uint MaxStringLength = 256;
}
