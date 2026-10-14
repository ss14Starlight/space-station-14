using Content.Server._Starlight.Changeling;
using Content.Server.Objectives.Systems;

namespace Content.Server._Starlight.Objectives.Components;

[RegisterComponent, Access(typeof(ChangelingObjectiveSystem), typeof(ChangelingSystem))]
public sealed partial class AbsorbConditionComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Absorbed = 0f;
}
