using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Time zone of the times on printed record histories (IANA id, e.g. Europe/Kyiv). Paper can't follow each
    /// reader's zone like the console's History tab does. Empty uses the server machine's zone.
    /// </summary>
    public static readonly CVarDef<string> RecordsPrintTimeZone =
        CVarDef.Create("records.print_timezone", "Europe/Kyiv", CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether records-console histories carry over between rounds: saved at round end, restored when the
    /// character arrives. Turning it off stops both and hides the restored entries straight away.
    /// </summary>
    public static readonly CVarDef<bool> RecordsHistoryPersistence =
        CVarDef.Create("records.history_persistence", true, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// How many entries of each console's history (criminal, psionics, crew) are kept per character between
    /// rounds, newest first. The HR background isn't counted, it comes from the character profile.
    /// </summary>
    public static readonly CVarDef<int> RecordsHistoryLimit =
        CVarDef.Create("records.history_limit", 100, CVar.SERVERONLY | CVar.ARCHIVE);
}
