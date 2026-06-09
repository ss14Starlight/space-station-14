using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Railroading;

[RegisterComponent]
public sealed partial class RailroadDesertionTaskComponent : Component
{
    [DataField]
    public LocId Message = "rail-desertion-task";

    [DataField]
    public SpriteSpecifier Icon = new SpriteSpecifier.Rsi(new ResPath("Interface/Actions/jump.rsi"), "icon");

    [DataField]
    public bool IsCompleted;
}
