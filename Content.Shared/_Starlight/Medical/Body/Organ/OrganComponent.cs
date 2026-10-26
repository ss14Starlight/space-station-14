using Content.Shared.Body.Systems;
using Robust.Shared.GameStates;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Body.Organ;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedBodySystem))]
public sealed partial class OrganComponent : Component
{
    /// <summary>
    /// Relevant body this organ is attached to.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Body;

    /// <summary>
    /// Body that grew this organ. Implants and transplanted organs keep their own origin.
    /// </summary>
    [DataField]
    public EntityUid? OriginalBody;
}
