using Content.Server.DoAfter;
using Content.Shared.Starlight.Antags.Abductor;
using Robust.Server.GameObjects;
using Content.Shared.Tag;
using Robust.Server.Containers;

namespace Content.Server.Starlight.Antags.Abductor;

public sealed partial class AbductorSystem : SharedAbductorSystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly TransformSystem _xformSys = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly ContainerSystem _container = default!;

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
