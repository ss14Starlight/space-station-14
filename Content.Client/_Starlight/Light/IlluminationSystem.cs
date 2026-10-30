using Content.Shared._Starlight.Light;
using Robust.Shared.Map;

namespace Content.Client._Starlight.Light;

public sealed partial class IlluminationSystem : SharedIlluminationSystem
{
    public override void GetLightSources(MapCoordinates point, List<LightContribution> results, IlluminationQuery query = default)
    {
    }

    public override void GetLightSources(EntityUid uid, List<LightContribution> results, IlluminationQuery query = default)
    {
    }

    public override void GetLightsInRange(MapCoordinates center, float range, List<Entity<SharedPointLightComponent, TransformComponent>> results)
    {
    }

    public override bool TryGetLightColor(MapCoordinates point, out Color color, IlluminationQuery query = default)
    {
        color = default;
        return false;
    }

    public override bool TryGetLightColor(EntityUid uid, out Color color, IlluminationQuery query = default)
    {
        color = default;
        return false;
    }
}
