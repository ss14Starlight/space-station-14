using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Cybernetics;

/// <summary>
/// This contains all the events raised by the CyberneticDisruptionSystem
/// </summary>

/// <summary>
///     Raised directed on an entity when it is disrupted.
/// </summary>
[ByRefEvent]
public record struct CyberneticDisruptionEvent(EntityUid Target);

/// <summary>
///     Raised on a disrupted entity when something wants to remove the cybernetic disruption component.
/// </summary>
[ByRefEvent]
public record struct CyberneticDisruptionEndAttemptEvent(bool Cancelled);

/// <summary>
///     Raised on a disruptor when the doafter finishes
/// </summary>
[Serializable, NetSerializable]
public sealed partial class CyberneticDisruptorDoafterEvent : SimpleDoAfterEvent
{
}
