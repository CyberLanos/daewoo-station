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
}
