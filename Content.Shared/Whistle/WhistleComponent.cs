using Robust.Shared.GameStates;
using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;

namespace Content.Shared.Whistle;

/// <summary>
/// Spawn attached entity for entities in range with <see cref="HumanoidAppearanceComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WhistleComponent : Component
{
    /// <summary>
    /// Entity prototype to spawn
    /// </summary>
    [DataField]
    public EntProtoId Effect = "WhistleExclamation";

    /// <summary>
    /// Range value.
    /// </summary>
    [DataField]
    public float Distance = 0;

    /// <summary>
    // Action granted while the whistle is held or worn so it can be blown via the action bar/ a number key
    /// </summary>
    [DataField]
    public EntProtoId Action = "ActionBlowWhistle";

    [DataField]
    public EntityUid? ActionEntity;
}
