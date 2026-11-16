namespace Content.Server._Daiquiri.Planets;

[RegisterComponent]
public sealed partial class PlanetJumpCooldownComponent : Component
{
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan NextJump;
}