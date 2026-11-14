using System.Numerics;
using Robust.Shared.Map;

namespace Content.Shared._Starlight.Light;

/// <summary>
/// Tells how lit a point of the world is, so gameplay can react to light without a client.
/// </summary>
public abstract partial class SharedIlluminationSystem : EntitySystem
{
    /// <summary>
    /// Collects every light reaching point, already checked for range, mask and shadows.
    /// </summary>
    public abstract void GetLightSources(MapCoordinates point, List<LightContribution> results, IlluminationQuery query = default);

    /// <summary>
    /// Collects every light reaching entity.
    /// </summary>
    public abstract void GetLightSources(EntityUid uid, List<LightContribution> results, IlluminationQuery query = default);

    /// <summary>
    /// Collects every enabled light that isn't hidden in a container and stands within range of center, no matter how far it shines.
    /// </summary>
    public abstract void GetLightsInRange(MapCoordinates center, float range, List<Entity<SharedPointLightComponent, TransformComponent>> results);

    /// <summary>
    /// Additive light color at <paramref name="point"/>, computed the same way the light shader does.
    /// </summary>
    public abstract bool TryGetLightColor(MapCoordinates point, out Color color, IlluminationQuery query = default);

    /// <inheritdoc cref="TryGetLightColor(MapCoordinates, out Color, IlluminationQuery)"/>
    public abstract bool TryGetLightColor(EntityUid uid, out Color color, IlluminationQuery query = default);

    /// <summary>
    /// Brightness at point, as the luminance of <see cref="TryGetLightColor(MapCoordinates, out Color, IlluminationQuery)"/>.
    /// </summary>
    public float GetLightLevel(MapCoordinates point, IlluminationQuery query = default)
        => TryGetLightColor(point, out var color, query) ? ColorToLevel(color) : 0f;

    /// <inheritdoc cref="GetLightLevel(MapCoordinates, IlluminationQuery)"/>
    public float GetLightLevel(EntityUid uid, IlluminationQuery query = default)
        => TryGetLightColor(uid, out var color, query) ? ColorToLevel(color) : 0f;

    public static float ColorToLevel(Color color)
        => MathF.Max(0f, (0.2126f * color.R) + (0.7152f * color.G) + (0.0722f * color.B));
}

public readonly record struct IlluminationQuery(bool IgnoreAmbient = false, Func<EntityUid, bool>? Ignore = null);

public readonly record struct LightContribution(
    Entity<SharedPointLightComponent, TransformComponent> Light,
    Vector2 Position,
    float Distance,
    float Radius,
    float Mask,
    float Intensity);
