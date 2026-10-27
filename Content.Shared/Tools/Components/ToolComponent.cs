using Content.Shared.Tools.Systems;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Tools.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState] // Starlight - AutoState
[Access(typeof(SharedToolSystem))]
public sealed partial class ToolComponent : Component
{
    [DataField] [AutoNetworkedField] // Starlight - Auto for component toggler, to update qualities on client
    public HashSet<ProtoId<ToolQualityPrototype>> Qualities  = [];

    /// <summary>
    ///     For tool interactions that have a delay before action this will modify the rate, time to wait is divided by this value
    /// </summary>
    [DataField] [AutoNetworkedField] // Starlight - Auto for component toggler, to update on client
    public float SpeedModifier = 1f;

    [DataField] [AutoNetworkedField] // Starlight - Auto for component toggler, to update on client
    public SoundSpecifier? UseSound;
}

/// <summary>
/// Attempt event called *before* any do afters to see if the tool usage should succeed or not.
/// Raised on both the tool and then target.
/// </summary>
public sealed class ToolUseAttemptEvent(EntityUid user, float fuel) : CancellableEntityEventArgs
{
    public EntityUid User { get; } = user;
    public float Fuel = fuel;
}

/// <summary>
/// Event raised on the user of a tool to see if they can actually use it.
/// </summary>
[ByRefEvent]
public struct ToolUserAttemptUseEvent(EntityUid? target)
{
    public EntityUid? Target = target;
    public bool Cancelled = false;
}
