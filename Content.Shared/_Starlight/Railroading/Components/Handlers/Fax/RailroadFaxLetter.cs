using Content.Shared.Paper;

namespace Content.Shared._Starlight.Railroading.Components.Handlers.Fax;

[DataDefinition]
public sealed partial class RailroadFaxLetter
{
    [DataField("name", required: true)]
    public string PaperName;

    [DataField("content", required: true)]
    public string PaperContent;

    [DataField]
    public string? PaperLabel;

    [DataField]
    public string? StampState;

    [DataField]
    public List<StampDisplayInfo> StampedBy = [];

    [DataField]
    public string PaperPrototype = "";

    [DataField]
    public bool Locked;
}
