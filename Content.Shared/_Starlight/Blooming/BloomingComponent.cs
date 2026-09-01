[RegisterComponent]
public sealed partial class BloomingComponent : Component
{
    [DataField]
    public List<string> Pollen = new();

    [DataField]
    public float BloomInterval = 10f;

    public float BloomAccumulator;
}
