using Content.Shared._Starlight.CCVar;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Client._Starlight.Combat;

public sealed partial class HeldItemShowModeSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();

        if (_cfg.GetCVar(CCVars.HudHeldItemShow))
            return;

        if (_cfg.GetCVar(StarlightCCVars.HeldItemShowMode) == (int) HeldItemShowMode.Always)
            _cfg.SetCVar(StarlightCCVars.HeldItemShowMode, (int) HeldItemShowMode.Never);

        _cfg.SetCVar(CCVars.HudHeldItemShow, true);
        _cfg.SaveToFile();
    }
}
