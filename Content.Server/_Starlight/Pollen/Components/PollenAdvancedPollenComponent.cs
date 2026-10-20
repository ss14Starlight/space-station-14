namespace Content.Server._Starlight.Pollen.Components;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class PollenAdvancedPollenComponent : Component
{
    [DataField]
    public float CheckRange = 2f; // Range in which entities can interact with the pollen cloud.

    [DataField]
    public float DionaChance = 0.6f; // Chance to trigger a beneficial effect on a Diona.

    [DataField]
    public float AllergicChance = 0.5f; // Chance to trigger an allergic sneeze on a pollen-sensitive entity.

    [DataField]
    public float MindChance = 0.1f; // Chance to trigger the mind-related effect on an entity with a mind.

    [AutoPausedField]
    public TimeSpan NextCheck; // Time of the next interaction check.

    [DataField]
    public float HealBrute = 0.5f; // Amount of brute damage healed by the healing effect.

    [DataField]
    public float HealBurn = 0.2f; // Amount of burn damage healed by the healing effect.

    [DataField]
    public float SpeedWalkModifier = 1.1f; // Walking speed multiplier applied by the speed buff.

    [DataField]
    public float SpeedSprintModifier = 1.1f; // Sprinting speed multiplier applied by the speed buff.

    [DataField]
    public TimeSpan SpeedBuffDuration = TimeSpan.FromSeconds(3); // Duration of the Diona speed buff.
}
