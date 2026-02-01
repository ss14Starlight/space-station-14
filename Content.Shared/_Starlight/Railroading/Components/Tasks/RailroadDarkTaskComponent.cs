using Content.Shared.Destructible.Thresholds;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Railroading;

[RegisterComponent]
public sealed partial class RailroadDarkTaskComponent : Component
{
    [DataField]
    public string Message = "rr-brighteye-dark-task";

    [DataField]
    public MinMax Amount;

    [DataField]
    public float Target;

    [DataField]
    public SpriteSpecifier Icon = new SpriteSpecifier.Rsi(new ResPath("Structures/Specific/Anomalies/shadow_anom.rsi"), "anom");

    [DataField]
    public bool IsCompleted;
}
