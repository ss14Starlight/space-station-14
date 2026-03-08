namespace Content.Shared._Starlight.Damage.Components;

[RegisterComponent]
public sealed partial class AmputateOnHitComponent : Component
{
    [DataField]
    public float Chance = 0.5f;

    [DataField]
    public List<string> Parts;
}
