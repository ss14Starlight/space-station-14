namespace Content.Shared._Starlight.Railroading.Components.Handlers.Fax;

[RegisterComponent]
public sealed partial class RailroadFaxOnFailedComponent : Component, IRailroadFaxComponent
{
    [DataField]
    public HashSet<string> Addresses { get; set; } = [];

    [DataField(required: true)]
    public List<RailroadFaxLetter> Letters { get; set; } = [];
}
