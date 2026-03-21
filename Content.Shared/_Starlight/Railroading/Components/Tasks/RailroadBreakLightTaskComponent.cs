using Content.Shared.Destructible.Thresholds;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Railroading;

[RegisterComponent]
public sealed partial class RailroadBreakLightTaskComponent : Component
{
    [DataField]
    public LocId Message = "rr-brighteye-breaklight";

    [DataField]
    public MinMax Amount;

    [DataField]
    public int LightBroken = 0;

    [DataField]
    public float Target;

    [DataField]
    public SpriteSpecifier Icon = new SpriteSpecifier.Rsi(new ResPath("Objects/Power/light_bulb.rsi"), "normal");

    [DataField]
    public bool IsCompleted;
}

public sealed class OnLightBreakEvent : EntityEventArgs
{
    /// <summary>
    /// The Light ent that was broken.
    /// </summary>
    public EntityUid Light;
    public OnLightBreakEvent(EntityUid light)
    {
        Light = light;
    }
}