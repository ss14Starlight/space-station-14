using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Toggle for non-gameplay-affecting or otherwise status indicative post-process effects, such additive lighting.
    /// In the future, this could probably be turned into an enum that allows only disabling more expensive post-process shaders.
    /// However, for now (mid-July of 2024), this only applies specifically to a particularly cheap shader: additive lighting.
    /// </summary>
    public static readonly CVarDef<bool>
        PostProcess = CVarDef.Create("cp14_shaders.post_process", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
