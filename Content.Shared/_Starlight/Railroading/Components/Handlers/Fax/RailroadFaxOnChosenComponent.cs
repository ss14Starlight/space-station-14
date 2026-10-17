namespace Content.Shared._Starlight.Railroading.Components.Handlers.Fax;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class RailroadFaxOnChosenComponent : Component, IRailroadFaxComponent
{
    [DataField]
    public HashSet<string> Addresses { get; set; } = [];

    [DataField(required: true)]
    public List<RailroadFaxLetter> Letters { get; set; } = [];

    /// <summary>
    /// How long after the card is chosen the fax is sent.
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.Zero;

    [DataField, AutoPausedField]
    public TimeSpan? SendAt;

    [DataField]
    public EntityUid? PendingSubject;
}
