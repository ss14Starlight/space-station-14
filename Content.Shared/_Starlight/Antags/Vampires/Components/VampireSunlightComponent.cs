using Content.Shared.FixedPoint;

namespace Content.Shared._Starlight.Antags.Vampires.Components;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class VampireSunlightComponent : Component
{
    /// <summary>
    ///     How much heat damage is applied when the burn effect triggers
    /// </summary>
    [DataField]
    public FixedPoint2 BurnDamage = FixedPoint2.New(3);

    /// <summary>
    ///     Interval between exposure ticks while in space
    /// </summary>
    [DataField]
    public TimeSpan DamageInterval = TimeSpan.FromSeconds(2f);

    /// <summary>
    ///     Blood cost per exposure tick while the vampire still has reserves
    /// </summary>
    [DataField]
    public int BloodDrainPerInterval = 10;

    /// <summary>
    ///     Chance to apply the burn/ignite effect while the vampire still has blood
    /// </summary>
    [DataField]
    public float BloodEffectChance = 0.1f;

    /// <summary>
    ///     Chance to apply the burn/ignite effect while the vampire has no blood
    /// </summary>
    [DataField]
    public float BloodlessEffectChance = 0.85f;

    /// <summary>
    ///     Fire stacks added when the vampire ignites
    /// </summary>
    [DataField]
    public float FireStacksOnIgnite = 2f;

    /// <summary>
    ///     Genetic damage applied each tick when the vampire has no blood
    /// </summary>
    [DataField]
    public FixedPoint2 GeneticDamagePerInterval = FixedPoint2.New(10);

    /// <summary>
    ///     Threshold of accumulated genetic damage after which the vampire turns to ash
    /// </summary>
    [DataField]
    public FixedPoint2 GeneticDustThreshold = FixedPoint2.New(100);

    /// <summary>
    ///     How long a vampire can linger in space before they start taking damage
    /// </summary>
    [DataField]
    public TimeSpan GracePeriod = TimeSpan.FromSeconds(1.5f);

    /// <summary>
    ///     Minimum seconds between popup warnings to the player
    /// </summary>
    [DataField]
    public TimeSpan WarningPopupCooldown = TimeSpan.FromSeconds(5f);

    /// <summary>
    ///     Localization string displayed when the vampire starts burning
    /// </summary>
    [DataField]
    public LocId WarningPopup = "vampire-space-burn-warning";

    [ViewVariables]
    [AutoPausedField]
    public TimeSpan? TimeEnteredSpace;

    [ViewVariables]
    [AutoPausedField]
    public TimeSpan? NextDamageTime;

    [ViewVariables]
    [AutoPausedField]
    public TimeSpan NextWarningPopup;
}
