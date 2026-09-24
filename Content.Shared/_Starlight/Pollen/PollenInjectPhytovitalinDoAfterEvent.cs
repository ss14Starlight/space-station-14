using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Pollen;

[Serializable, NetSerializable]
public sealed partial class PollenInjectPhytovitalinDoAfterEvent : DoAfterEvent
{
    public NetEntity Action;

    public PollenInjectPhytovitalinDoAfterEvent(NetEntity action) => Action = action;

    public override DoAfterEvent Clone() => this;
}
