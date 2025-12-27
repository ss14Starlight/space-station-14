using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Content.Shared.Chemistry.Components;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.GameStates;

namespace Content.Shared._FarHorizons.ReagantDraw.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ReagantDrawComponent : Component
{
    /// <summary>
    /// ReagentID for what solution to whitelist.
    /// </summary>
    [DataField("whitelistedReagants")]
    public List<ProtoId<ReagentPrototype>> WhitelistedReagants = new();

    /// <summary>
    /// Solution container name
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string SolutionContainer = "default";

    /// <summary>
    /// The solution on the <see cref="SolutionContainerManagerComponent"/> to use.
    /// </summary>
    [ViewVariables]
    public Entity<SolutionComponent>? Solution = null;
    
    /// <summary>
    /// Whether the reagant drain is enabled
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    /// <summary>
    /// How much reagant is drained
    /// </summary>
    [DataField]
    public float DrainRate = 1f;

    /// <summary>
    /// When the next reagant drain will go off
    /// </summary>
    [DataField("nextUpdate", customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextUpdateTime;

    /// <summary>
    /// How long between drains
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1);
}

/// <summary>
///     Raised when a reagant container's volume is changed
/// </summary>
[ByRefEvent]
public readonly record struct ReagantChangedEvent(float Volume, float MaxVolume);

/// <summary>
/// Raised directed on an entity when it no longer has any solution to draw from
/// </summary>
[ByRefEvent]
public readonly record struct ReagantContainerSlotEmptyEvent;
