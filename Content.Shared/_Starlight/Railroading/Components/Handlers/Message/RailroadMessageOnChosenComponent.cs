namespace Content.Shared._Starlight.Railroading.Components.Handlers.Message;

[RegisterComponent]
public sealed partial class RailroadMessageOnChosenComponent : Component
{
    [DataField(required: true)]
    public List<MessageLoc> Messages = [];
}

[DataDefinition]
public partial struct MessageLoc
{
    [DataField]
    public LocId Message;

    [DataField]
    public LocId Wrapped;

    [DataField]
    public Color? Color;
}
