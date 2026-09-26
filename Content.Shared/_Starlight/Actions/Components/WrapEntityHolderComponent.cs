using Content.Shared.Alert;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Actions.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class WrapEntityHolderComponent : Component
{
    /// <summary>
    /// The entity that is currently being held by the wrapper. This is used to keep track of the entity that is being wrapped and to ensure that it is properly unwrapped when the wrapper is removed.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public EntityUid? Hold = null;

    /// <summary>
    /// How much time it takes for player to unwrap someone from web using sharp item.
    /// </summary>
    [DataField]
    public TimeSpan UnWrapItemTime = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How much time it takes for player to unwrap someone from web without using any item.
    /// </summary>
    [DataField]
    public TimeSpan UnWrapHandTime = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Container that the wrapped entity will be put into.
    /// </summary>
    [DataField]
    public string ContainerId = "entity";

    public BaseContainer? Container = null;

    [DataField]
    public ProtoId<AlertPrototype> WrappedAlert = "Wrapped";
}
