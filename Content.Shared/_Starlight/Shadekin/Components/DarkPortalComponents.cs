using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Shadekin;

/// <summary>
/// Will Autolink to a DarkHubComponent, This comp also has custom anomaly code for the pulse and shadekin link.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DarkPortalComponent : Component
{
    [DataField]
    public EntityUid? Brighteye;

    [DataField]
    public float PulseRange = 5f;

    [DataField]
    public EntProtoId ShadekinShadow = "ShadekinShadow";

    [DataField]
    public int StabilizeCost = 50;
}

/// <summary>
/// The Shadekin/Dark Hub; All DarkPortalComponent init will link to this ent.
/// </summary>
[RegisterComponent]
public sealed partial class DarkHubComponent : Component
{
    /// <summary>
    /// If true, this is the Hub.
    /// </summary>
    [DataField]
    public bool Hub = true;

    [DataField]
    public EntProtoId ShadekinShadow = "ShadekinShadow";
}
