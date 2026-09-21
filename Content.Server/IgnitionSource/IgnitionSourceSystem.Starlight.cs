using Content.Shared.IgnitionSource;

namespace Content.Server.IgnitionSource;

public sealed partial class IgnitionSourceSystem : SharedIgnitionSourceSystem
{
    [Dependency] private EntityQuery<IgnitionSourceComponent> _ignitionQuery;
    [Dependency] private EntityQuery<TransformComponent> _transformQuery;

    private float _updateAccumulator;
    private const float UpdateInterval = 0.25f;

    private readonly HashSet<EntityUid> _activeSources = [];
    private readonly List<EntityUid> _sourceSnapshot = [];
    private readonly Dictionary<(EntityUid Grid, Vector2i Tile), IgnitionExposure> _tileExposures = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IgnitionSourceComponent, ComponentStartup>(OnSourceStartup);
        SubscribeLocalEvent<IgnitionSourceComponent, ComponentShutdown>(OnSourceShutdown);
    }

    private void OnSourceStartup(Entity<IgnitionSourceComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.Ignited)
            _activeSources.Add(ent.Owner);
    }

    private void OnSourceShutdown(Entity<IgnitionSourceComponent> ent, ref ComponentShutdown args) => _activeSources.Remove(ent.Owner);

    protected override void OnIgnitionStateChanged(Entity<IgnitionSourceComponent> ent)
    {
        if (ent.Comp.Ignited)
            _activeSources.Add(ent.Owner);
        else
            _activeSources.Remove(ent.Owner);
    }
    private readonly record struct IgnitionExposure(float Temperature, EntityUid Source);
}
