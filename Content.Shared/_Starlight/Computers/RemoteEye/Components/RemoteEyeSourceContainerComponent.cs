using Content.Shared._Starlight.Antags.Abductor;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Computers.RemoteEye.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedRemoteEyeSystem))]
public sealed partial class RemoteEyeSourceContainerComponent : Component
{
    [DataField]
    public EntityUid? Actor;
}
