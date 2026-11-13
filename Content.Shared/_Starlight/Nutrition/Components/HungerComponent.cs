using Content.Shared.Damage;
using Content.Shared.FixedPoint;

// ReSharper disable CheckNamespace
// Partial of the upstream HungerComponent, so it has to share its namespace.
namespace Content.Shared.Nutrition.Components;

public sealed partial class HungerComponent : Component
{
    /// <summary>
    /// A list of values to drain hunger by every update tick. Gets multiplied by decay rate.
    /// </summary>
    [ViewVariables] public readonly List<(EntityUid, float, TimeSpan?)> HungerDrains = [];

    /// <summary>
    /// A vignette to show to the player when they are starving
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public bool ApplyStarvationVignette = false;

    /// Maximum allowed damage in order to receive passive damage/healing.
    [DataField("passiveCap"), AutoNetworkedField] public FixedPoint2 PassiveDamageCap = 0;

    /// The minimum required hunger threshold to receive passive damage/healing (e.g. with it at Peckish, you will not get healed while Starving).
    [DataField("passiveThreshold"), AutoNetworkedField] public HungerThreshold PassiveDamageThreshold = HungerThreshold.Peckish;

    /// How much damage/healing to apply per second when passive damage/healing is active.
    [DataField("passiveDamage"), AutoNetworkedField] public DamageSpecifier? PassiveDamageSpecifier;
}
