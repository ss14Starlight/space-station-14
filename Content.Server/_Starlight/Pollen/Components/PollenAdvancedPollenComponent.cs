namespace Content.Server._Starlight.Pollen.Components;

[RegisterComponent]
public sealed partial class PollenAdvancedPollenComponent : Component
{
    [DataField]
    public float CheckRange = 2f;

    [DataField]
    public float DionaChance = 0.8f;

    [DataField]
    public float AllergicChance = 1f;

    [DataField]
    public float MindChance = 1f;

    public TimeSpan NextCheck;
}
