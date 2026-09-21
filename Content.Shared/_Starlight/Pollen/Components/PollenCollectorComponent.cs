[RegisterComponent]
public sealed partial class PollenCollectorComponent : Component
{
    [DataField]
    public float AbsorptionChance = 0.2f;

    [DataField]
    public List<string> CollectedPollen = new();
}
