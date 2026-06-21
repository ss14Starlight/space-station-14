using Content.Server._Starlight.Antags.Vampires.Systems;

namespace Content.Server._Starlight.Objectives.Components;

[RegisterComponent, Access(typeof(VampireSystem))]
public sealed partial class BloodDrainConditionComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float BloodDranked = 0f;
}
