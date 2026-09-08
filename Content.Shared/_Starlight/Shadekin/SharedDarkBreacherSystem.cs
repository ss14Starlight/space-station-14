using Content.Shared._Starlight.Shadekin.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Shared._Starlight.Shadekin;

public abstract partial class SharedDarkBreacherSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ShadekinSystem _shadekin = default!;

    protected EntityUid? GeneratePortal(DarkBreacherComponent component)
    {
        _shadekin.SpawnTheDark();
        // First lets find "The Dark".
        var query = EntityQueryEnumerator<DarkHubComponent>();
        while (query.MoveNext(out var target, out var portal))
            if (portal.Hub)
            {
                // We find "The Dark" or... at least "The Hub", If we have the hub but no dark you silly.
                var angle = _random.NextAngle();
                var location = angle.ToVec() * component.SpawnDistance;
                var position = _transform.GetWorldPosition(target) + location;
                var coords = new MapCoordinates(position, Transform(target).MapID);
                // Spawn it!
                return EntityManager.PredictedSpawn(component.Portal, coords);
            }

        return null;
    }
}
