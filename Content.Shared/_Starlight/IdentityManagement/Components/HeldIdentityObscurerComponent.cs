using Content.Shared.IdentityManagement.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.IdentityManagement.Components;

/// <summary>
/// When this item is held in hands, it blocks the holder's identity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HeldIdentityObscurerComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField]
    public IdentityBlockerCoverage Coverage = IdentityBlockerCoverage.FULL;
}
