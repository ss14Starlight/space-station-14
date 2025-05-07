namespace Content.Server._Moffstation.Objectives.Components;

[RegisterComponent]
public sealed partial class SimpleObjectiveComponent : Component
{
    [DataField(required: true), ViewVariables(VVAccess.ReadWrite)]
    public string Title = string.Empty;
}
