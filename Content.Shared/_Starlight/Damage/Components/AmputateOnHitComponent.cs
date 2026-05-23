namespace Content.Shared._Starlight.Damage.Components;

[RegisterComponent]
public sealed partial class AmputateOnHitComponent : Component
{
    [DataField]
    public bool Hidden = false;

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public float Chance = 0.5f;

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public List<string> Parts = [];
}
