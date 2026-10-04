using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Wieldable;
using Robust.Shared.Player;
using Robust.Shared.Configuration;

namespace Content.Client._Starlight.Wieldable;

public sealed partial class WieldOrderClientSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, StarlightCCVars.WieldBeforeRack, OnChanged);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnChanged(bool wieldBeforeRack)
        => RaiseNetworkEvent(new SetWieldOrderEvent(wieldBeforeRack));

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
        => OnChanged(_cfg.GetCVar(StarlightCCVars.WieldBeforeRack));
}
