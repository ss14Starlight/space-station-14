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
    /// Body that created this organ during initial anatomy setup or regeneration.
    /// Unlike <see cref="Body"/>, this is preserved when the organ is detached or transplanted.
    /// </summary>
    /// <remarks>
    /// Null means no native origin was recorded, as with a separately spawned implant. Regeneration uses this
    /// to distinguish native organs from grafts; it does not determine who owns a mind inside the organ.
    /// </remarks>
    [DataField]
    public EntityUid? OriginalBody;
}
