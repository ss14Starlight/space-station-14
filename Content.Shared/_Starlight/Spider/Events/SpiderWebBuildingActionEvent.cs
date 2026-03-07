using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Spider.Events;

public sealed partial class SpiderWebBuildingActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public EntProtoId Building;
}