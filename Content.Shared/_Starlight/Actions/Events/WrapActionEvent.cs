using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Actions.Events;

public sealed partial class WrapActionEvent : EntityTargetActionEvent
{
    [DataField]
    public TimeSpan WrapTime = TimeSpan.FromSeconds(2);

    [DataField]
    public EntProtoId WrapContainerId = "EffectTerrorCocoon";
}

[Serializable, NetSerializable]
public sealed partial class WrapDoAfterEvent : DoAfterEvent
{
    public EntProtoId WrapContainerId;

    public WrapDoAfterEvent(EntProtoId wrapContainerId)
    {
        WrapContainerId = wrapContainerId;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class UnwrapDoAfterEvent : SimpleDoAfterEvent;
