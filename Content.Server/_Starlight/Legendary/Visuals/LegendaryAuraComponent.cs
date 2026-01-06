namespace Content.Server._Starlight.Legendary.Visuals;

[RegisterComponent]
public sealed partial class LegendaryAuraComponent : Component
{
    [DataField]
    public bool PickedUpOnce;

    [DataField]
    public EntityUid? AuraEntity;
}
