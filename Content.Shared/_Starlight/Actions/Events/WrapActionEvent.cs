using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Actions.Events;

[Serializable, NetSerializable]
public sealed partial class WrapActionEvent : EntityTargetActionEvent
{
    [DataField]
    public TimeSpan WrapTime = TimeSpan.FromSeconds(2);
}

[Serializable, NetSerializable]
public sealed partial class WrapDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class UnwrapDoAfterEvent : SimpleDoAfterEvent;
