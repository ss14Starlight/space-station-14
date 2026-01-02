// _STARLIGHT: Welder Healing DoAfter Event
// Event fired when welder healing completes

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Silicons;

[Serializable, NetSerializable]
public sealed partial class WelderHealingDoAfterEvent : SimpleDoAfterEvent
{
}
