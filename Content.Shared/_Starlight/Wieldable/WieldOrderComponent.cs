using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Wieldable;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WieldOrderComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool WieldBeforeRack;
}

[Serializable, NetSerializable]
public sealed class SetWieldOrderEvent(bool wieldBeforeRack) : EntityEventArgs
{
    public bool WieldBeforeRack = wieldBeforeRack;
}
