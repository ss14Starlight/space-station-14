using Content.Server.DoAfter;
using Robust.Server.GameObjects;
using Content.Shared.Tag;
using Robust.Server.Containers;
using Content.Shared._Starlight.Antags.Abductor.EntitySystems;

namespace Content.Server._Starlight.Antags.Abductor.EntitySystems;

public sealed partial class AbductorSystem : SharedAbductorSystem
{
    [Dependency] private EntityManager _entityManager = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;
    [Dependency] private DoAfterSystem _doAfter = default!;
    [Dependency] private TransformSystem _xformSys = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private ContainerSystem _container = default!;

    public override void Initialize()
    {
        InitializeActions();
        InitializeGizmo();
        InitializeConsole();
        InitializeOrgans();
        InitializeVest();
        InitializeExtractor();
        InitializeRoundEnd();
        base.Initialize();
    }
}
