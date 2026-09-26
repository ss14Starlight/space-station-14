using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Railroading.Components.Tasks;

[RegisterComponent]
public sealed partial class RailroadSupercritPortalTaskComponent : Component
{
    [DataField]
    public LocId Message = "rr-brighteye-portal-crit-desc";

    [DataField]
    public SpriteSpecifier Icon = new SpriteSpecifier.Rsi(new ResPath("_Starlight/Structures/Specific/Anomalies/dark.rsi"), "portal");

    [DataField]
    public bool IsCompleted;
}
