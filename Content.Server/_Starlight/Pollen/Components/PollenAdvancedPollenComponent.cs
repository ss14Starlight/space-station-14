namespace Content.Server._Starlight.Pollen.Components;

[RegisterComponent]
public sealed partial class PollenAdvancedPollenComponent : Component
{
    [DataField]
    public float CheckRange = 2f;

    [DataField]
    public float DionaChance = 0.2f;

    [DataField]
    public float AllergicChance = 0.5f;

    [DataField]
    public float MindChance = 0.1f;

    public TimeSpan NextCheck;

    [DataField]
    public float HealBrute = 0.5f;

    [DataField]
    public float HealBurn = 0.2f;

    [DataField]
    public float SpeedWalkModifier = 1.1f;

    [DataField]
    public float SpeedSprintModifier = 1.1f;

    [DataField]
    public TimeSpan SpeedBuffDuration = TimeSpan.FromSeconds(5);
}
