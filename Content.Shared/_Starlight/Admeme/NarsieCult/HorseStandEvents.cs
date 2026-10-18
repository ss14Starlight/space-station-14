using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Admeme.NarsieCult;

/// <summary>
/// Raised when the creature uses its stage-switch action.
/// </summary>
public sealed partial class HorseStandActionEvent : InstantActionEvent;

/// <summary>
/// Standing-only: removes the target's eyes and drops them on the ground.
/// </summary>
public sealed partial class HorseEyesActionEvent : EntityTargetActionEvent;
/// <summary>
/// Instantly puts the target to sleep.
/// </summary>
public sealed partial class HorseSleepActionEvent : EntityTargetActionEvent;
/// <summary>
/// Standing-only: stuns and knocks down the target.
/// </summary>
public sealed partial class HorseStunActionEvent : EntityTargetActionEvent;

/// <summary>
/// Standing-only: instantly cuffs the target.
/// </summary>
public sealed partial class HorseRestrainActionEvent : EntityTargetActionEvent;

/// <summary>
/// Appearance data key used to drive the sprite. The value is a bool: true while standing.
/// </summary>
[Serializable, NetSerializable]
public enum HorseStandVisuals : byte
{
    Standing,
}

/// <summary>
/// Sprite layer map keys for the creature.
/// </summary>
[Serializable, NetSerializable]
public enum HorseStandVisualLayers : byte
{
    Base,
}
