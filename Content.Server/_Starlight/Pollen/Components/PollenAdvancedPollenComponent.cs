namespace Content.Server._Starlight.Pollen.Components;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class PollenAdvancedPollenComponent : Component
{
    /// <summary>
    /// Range around the cloud in which entities can interact with it, in world units.
    /// </summary>
    [DataField]
    public float CheckRange = 2f;

    /// <summary>
    /// Probability of triggering a random beneficial effect on a nearby Diona.
    /// Checked first; a successful roll consumes the cloud before other interactions are checked.
    /// </summary>
    [DataField]
    public float DionaChance = 0.6f;

    /// <summary>
    /// Probability of triggering an allergic sneeze on a nearby pollen-sensitive entity.
    /// Checked after the Diona interaction and before the mind-related interaction.
    /// </summary>
    [DataField]
    public float AllergicChance = 0.5f;

    /// <summary>
    /// Probability of displaying the mind-related message to a nearby entity with a mind.
    /// Checked last, if no earlier interaction consumes the cloud.
    /// </summary>
    [DataField]
    public float MindChance = 0.1f;

    /// <summary>
    /// Absolute time at which the next cloud interaction check is scheduled.
    /// </summary>
    [AutoPausedField]
    public TimeSpan NextCheck;

    /// <summary>
    /// Amount of brute damage healed by the Diona healing effect.
    /// </summary>
    [DataField]
    public float HealBrute = 0.5f;

    /// <summary>
    /// Amount of burn damage healed by the Diona healing effect.
    /// </summary>
    [DataField]
    public float HealBurn = 0.2f;

    /// <summary>
    /// Walking speed multiplier applied by the Diona speed buff.
    /// </summary>
    [DataField]
    public float SpeedWalkModifier = 1.1f;

    /// <summary>
    /// Sprinting speed multiplier applied by the Diona speed buff.
    /// </summary>
    [DataField]
    public float SpeedSprintModifier = 1.1f;

    /// <summary>
    /// Duration of the Diona speed buff, in seconds.
    /// </summary>
    [DataField]
    public TimeSpan SpeedBuffDuration = TimeSpan.FromSeconds(3);
}
