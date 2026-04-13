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
    /// min(default_instrs * ((maximum_instrs/(default_instrs * count)), default_instrs)
    /// or simply put it will run default_instrs at once max. but if there is more uxns then it divides the avaliable instructions from maximum_instrs among the processors.
    /// </summary>
    public static readonly CVarDef<int> UxnDefaultInstrLimit =
        CVarDef.Create("uxn.default_instrs", 1000, CVar.SERVERONLY);
}
