namespace Content.Shared._Starlight.Clumsy;

/// <summary>
/// This component restricts the usage of items to entities with a ClumsyComponent
/// </summary>
[RegisterComponent]
public sealed partial class ClumsyOnlyComponent : Component
{
    [DataField("slipStrength"), ViewVariables(VVAccess.ReadWrite)]
    public int SlipStrength = 10;
}
