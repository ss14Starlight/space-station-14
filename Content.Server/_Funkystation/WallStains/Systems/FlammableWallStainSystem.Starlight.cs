using Content.Shared._Funkystation.WallStains.Components;

namespace Content.Server._Funkystation.WallStains.Systems;

public sealed partial class FlammableWallStainSystem : EntitySystem
{
    private readonly List<(
        EntityUid Uid,
        FlammableWallStainComponent FireComp,
        WallStainComponent Stain,
        TransformComponent Xform)> _activeStains = [];
}
