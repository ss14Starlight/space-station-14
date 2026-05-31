using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;

namespace Content.Shared._Starlight.Antags.Vampires.Components.Classes;

/// <summary>
/// Marker component indicating Blood Swell is currently active
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ActiveBloodSwellComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan EndTime;

    [DataField, AutoNetworkedField]
    public float EnhancedThreshold = 400f;

    [DataField, AutoNetworkedField]
    public FixedPoint2 MeleeBonusDamage = FixedPoint2.New(14f);

    [DataField, AutoNetworkedField]
    public ProtoId<DamageTypePrototype> MeleeBonusDamageType = "Blunt";

    [DataField]
    public HashSet<string> ReducedDamageTypes = new()
    {
        "Blunt",
        "Slash",
        "Piercing",
        "Heat",
        "Cold",
        "Shock",
        "Caustic",
    };

    [DataField, AutoNetworkedField]
    public float IncomingDamageMultiplier = 0.5f;

    [DataField, AutoNetworkedField]
    public float StaminaDamageMultiplier = 0.5f;

    [DataField, AutoNetworkedField]
    public float StatusEffectDurationMultiplier = 0.5f;
}
