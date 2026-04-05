using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Starlight.Shoelaces.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShoelaceTiedComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan NextTripAttempt;

    [NonSerialized]
    public Entity<ShoelaceTieableComponent>? TiedShoes;

    [DataField]
    public float TripKnockdownTime = 1.5f;

    [DataField]
    public float TripAttemptCooldown = 0.75f;
}
