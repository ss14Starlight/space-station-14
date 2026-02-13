using Robust.Shared.Configuration;

namespace Content.Shared.Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    /// <summary>
    /// The maximum number of instructions a UXN can run in a single tick. above this UXNs start getting throttled
    /// </summary>
    
    public static readonly CVarDef<int> UxnMaxInstrLimit =
        CVarDef.Create("uxn.maximum_instrs", 100000, CVar.SERVERONLY);

    /// <summary>
    /// The default maximum number of instrs a UXN can execute at once.
    /// this value can be overriden/underclocked by <see cref="UxnMaxInstrLimit"/> via
    /// Math.Min(UxnDefaultInstrLimit * ((UxnMaxInstrLimit/(UxnDefaultInstrLimit*UxnCount)), UxnDefaultInstrLimit)
    /// </summary>
    public static readonly CVarDef<int> UxnDefaultInstrLimit =
        CVarDef.Create("uxn.default_instrs", 1000, CVar.SERVERONLY);
}
