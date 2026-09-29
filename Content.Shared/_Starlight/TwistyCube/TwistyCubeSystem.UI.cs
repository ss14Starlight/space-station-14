using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.TwistyCube;

[Serializable, NetSerializable]
public sealed class TwistyCubeActionMessage(TwistyCubeAction action) : BoundUserInterfaceMessage
{
    public readonly TwistyCubeAction Action = action;
}
