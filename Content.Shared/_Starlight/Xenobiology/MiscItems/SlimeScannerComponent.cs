using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Xenobiology.MiscItems;

[RegisterComponent, NetworkedComponent]
public sealed partial class SlimeScannerComponent : Component
{

}

public sealed partial class ConsoleMsgToScannerEvent : InstantActionEvent
{
    public EntityUid User;
    public EntityUid Target;

    public ConsoleMsgToScannerEvent(EntityUid user, EntityUid target)
    {
        User = user;
        Target = target;
    }
}

[Serializable, NetSerializable]
public sealed class SlimeScannerSoundMessage : EntityEventArgs
{
    public NetEntity Owner;
    public NetEntity User;
}
