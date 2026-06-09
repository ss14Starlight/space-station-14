using Robust.Shared.Configuration;

namespace Content.Shared.Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    /// <summary>
    /// Persisted sawmill log level overrides (format: name=level;name=level;).
    /// Applied on client startup.
    /// </summary>
    public static readonly CVarDef<string> LogSawmillLevels =
        CVarDef.Create("starlight.log.sawmill_levels", "", CVar.CLIENTONLY | CVar.ARCHIVE);
}
