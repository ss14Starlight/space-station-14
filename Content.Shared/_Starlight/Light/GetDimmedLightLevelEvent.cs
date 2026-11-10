using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Light;

[Serializable, NetSerializable]
public sealed class GetDimmedLightLevelEvent : EntityEventArgs
{
    /// <summary>
    /// Relates to how bright the light produced is.
    /// </summary>
    public float LightEnergy { get; set; }

    /// <summary>
    /// The maximum radius of the point light source this light produces.
    /// </summary>
    public float LightRadius { get; set; }

    /// <summary>
    /// The amount of power used by the light when it's active.
    /// </summary>
    public float PowerUse { get; set; }

    public GetDimmedLightLevelEvent(float lightEnergy, float lightRadius, float powerUse)
    {
        LightEnergy = lightEnergy;
        LightRadius = lightRadius;
        PowerUse = powerUse;
    }
}
