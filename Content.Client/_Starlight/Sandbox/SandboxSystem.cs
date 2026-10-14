// ReSharper disable CheckNamespace
using Content.Shared._Starlight.Sandbox;
using Robust.Shared.Prototypes;

namespace Content.Client.Sandbox;

public sealed partial class SandboxSystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    private void SLOverrideCopy(EntityUid uid, ref EntityPrototype? entProto, out bool overriden)
    {
        if (TryComp<SandboxCopyOverrideComponent>(uid, out var over))
        {
            if (_prototype.HasIndex(over.Override))
            {
                entProto = _prototype.Index(over.Override);
                overriden = true;
                return;
            }
        }
        overriden = false;
        return;
    }
}
