using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Railroading;

[RegisterComponent]
public sealed partial class RailroadCrewRetentionTaskComponent : Component
{
    [DataField]
    public LocId Message = "rail-crew-retention-task";

    [DataField]
    public float Threshold = 0.9f;

    [DataField]
    public float Progress = 0.0f;

    [DataField]
    public SpriteSpecifier Icon = new SpriteSpecifier.Texture(new ResPath("Interface/Actions/harm.png"));
}
