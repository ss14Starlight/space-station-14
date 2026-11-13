using Content.Shared.Actions;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Reptilian.Components;

/// <summary>
/// Lets the mob breathe a small puff of fire. Burning fuel from the stomach makes the flame bigger.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FireBreathComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionReptilianFireBreath";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    /// <summary>
    /// The flame spat without any fuel in the stomach.
    /// </summary>
    [DataField]
    public EntProtoId Projectile = "ProjectileReptilianBreath";

    /// <summary>
    /// Stronger flames fed by reagents in the stomach. The first tier the stomach can pay for is used,
    /// so list them from strongest to weakest.
    /// </summary>
    [DataField]
    public List<FireBreathFuelTier> FuelTiers = new();

    [DataField]
    public float ProjectileSpeed = 10f;

    [DataField]
    public SoundSpecifier BreathSound = new SoundPathSpecifier("/Audio/Magic/fireball.ogg", AudioParams.Default.WithVolume(-4f).WithVariation(0.15f));

    /// <summary>
    /// How far away a clicked item can be to get lit directly instead of spitting a flame.
    /// </summary>
    [DataField]
    public float LightRange = 1.5f;

    [DataField]
    public float LightStaminaCost = 5f;

    [DataField]
    public float LightHungerCost = 1f;

    /// <summary>
    /// Cooldown after only lighting an item, instead of the action's full use delay.
    /// </summary>
    [DataField]
    public TimeSpan LightCooldown = TimeSpan.FromSeconds(4);

    [DataField]
    public SoundSpecifier LightSound = new SoundPathSpecifier("/Audio/Magic/fireball.ogg", AudioParams.Default.WithVolume(-10f).WithVariation(0.15f).WithPitchScale(1.3f));

    [DataField]
    public float StaminaCost = 20f;

    [DataField]
    public float HungerCost = 5f;

    [DataField]
    public EntProtoId SlowdownEffect = "FireBreathSlowdownStatusEffect";

    [DataField]
    public TimeSpan SlowdownDuration = TimeSpan.FromSeconds(1.5);

    [DataField]
    public float SlowdownMultiplier = 0.6f;
}

public sealed partial class FireBreathActionEvent : WorldTargetActionEvent;

/// <summary>
/// A stronger flame that burns <see cref="Amount"/> of <see cref="Reagent"/> from the stomach.
/// </summary>
[DataDefinition]
public sealed partial class FireBreathFuelTier
{
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    [DataField(required: true)]
    public FixedPoint2 Amount;

    [DataField(required: true)]
    public EntProtoId Projectile;

    [DataField]
    public SoundSpecifier? Sound;

    /// <summary>
    /// Locale prefix for popups: "{Popup}-self" and "{Popup}-others".
    /// </summary>
    [DataField]
    public string Popup = "fire-breath-fueled";
}
