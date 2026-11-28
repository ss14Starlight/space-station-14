using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Starlight.ReagentDraw.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ReagentDrawComponent : Component
{
    [DataField]
    public HashSet<ProtoId<ReagentPrototype>> WhitelistedReagents = new();

    [DataField]
    public string SolutionContainer = "default";

    public Entity<SolutionComponent>? Solution;

    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField, AutoNetworkedField]
    public FixedPoint2 DrainRate = 1;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUpdate;
}

[ByRefEvent]
public readonly record struct ReagentContainerSlotEmptyEvent;
