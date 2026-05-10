using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Railroading;

[RegisterComponent]
public sealed partial class RailroadKeepEntityInTheDarkTaskComponent : Component
{
    [DataField]
    public LocId Message = "objective-condition-keep-inthedark-title";

    [DataField]
    public SpriteSpecifier Icon = new SpriteSpecifier.Rsi(new ResPath("_Starlight/Structures/Specific/Anomalies/dark.rsi"), "hub_portal");

    [DataField]
    public bool IsCompleted;
}
