namespace Content.Server._Moffstation.Objectives.Components;

[RegisterComponent]
public sealed partial class LocationObjectiveComponent : Component
{
    [DataField(required: true)]
    public LocId Title = string.Empty; // Starlight

    [DataField]
    public string? Target;
}
