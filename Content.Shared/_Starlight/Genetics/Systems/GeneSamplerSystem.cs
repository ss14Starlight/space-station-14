using Content.Shared._Starlight.Genetics.Components;
using Content.Shared._Starlight.Genetics.Genes.Components;
using Content.Shared.Interaction;

namespace Content.Shared._Starlight.Genetics.Systems;

public sealed partial class GeneSamplerSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private EntityManager _entityManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GeneSamplerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<GeneSamplerComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnMapInit(Entity<GeneSamplerComponent> ent, ref MapInitEvent args) => StateChanged(ent);

    private void OnAfterInteract(Entity<GeneSamplerComponent> entity, ref AfterInteractEvent args)
    {
        if (!args.Target.HasValue) return;
        var canReach = _interactionSystem.InRangeUnobstructed(args.User, args.Target.Value);
        if (!canReach) return;
        if (!_entityManager.TryGetComponent<GenesComponent>(args.Target.Value, out var genesComponent)) return;

        if (entity.Comp.IsCurrentlySampling)
            FillGenes(entity, genesComponent.Genes);
        else
        {
            genesComponent.Genes = new HashSet<EntityUid>(entity.Comp.Genes);
            ClearGenes(entity);
        }
        args.Handled = true;
    }

    /// <summary>
    /// Sets the sampler's genes and prepares it for injecting.
    /// Prefer this to directly updating the component.
    /// Safe to be called repeatedly.
    /// </summary>
    /// <param name="entity">The gene sampler.</param>
    /// <param name="genes">The genes it will receive.</param>
    public void FillGenes(Entity<GeneSamplerComponent> entity, HashSet<EntityUid> genes)
    {
        entity.Comp.Genes = genes;
        entity.Comp.IsCurrentlySampling = false;
        StateChanged(entity);
    }

    /// <summary>
    /// Clears the sampler's genes and prepares it for sampling.
    /// Prefer this to directly updating the component.
    /// Safe to be called repeatedly.
    /// </summary>
    /// <param name="entity">The gene sampler.</param>
    public void ClearGenes(Entity<GeneSamplerComponent> entity)
    {
        entity.Comp.Genes = new HashSet<EntityUid>();
        entity.Comp.IsCurrentlySampling = true;
        StateChanged(entity);
    }

    private void StateChanged(Entity<GeneSamplerComponent> entity)
    {
        if (TryComp(entity.Owner, out AppearanceComponent? appearance))
            _appearance.SetData(entity.Owner, GeneSamplerVisuals.Signal, entity.Comp.IsCurrentlySampling, appearance);
        Dirty(entity);
    }
}
