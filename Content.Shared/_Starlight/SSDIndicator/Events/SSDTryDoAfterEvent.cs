using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.SSDIndicator.Events;

[Serializable, NetSerializable]
public sealed partial class SSDTryDoAfterEvent : SimpleDoAfterEvent
{
}
