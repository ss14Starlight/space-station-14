using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Sol.Medical.Allergy;

/// <summary>
/// Ongoing allergic reaction. Severe+ blocks asphyxiation healing and ticks airloss damage.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ActiveAllergyReactionComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<AllergyPrototype> AllergyId;

    [DataField, AutoNetworkedField]
    public AllergySeverity Severity = AllergySeverity.Severe;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan EndsAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextTick;
}
