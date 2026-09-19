using Content.Shared._Starlight.Genetics.Components;
using Content.Shared.Interaction;

namespace Content.Shared._Starlight.Genetics.Systems;

public abstract partial class SharedGeneticsConsoleSystem : EntitySystem
{
    [Dependency] private EntityManager _entityManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GeneticsConsoleComponent, InteractUsingEvent>(OnInteractUsing, before:
            [typeof(SharedUserInterfaceSystem)]);
    }

    private void OnInteractUsing(Entity<GeneticsConsoleComponent> ent, ref InteractUsingEvent args)
    {
        if (!_entityManager.TryGetComponent<GeneSamplerComponent>(args.Used, out var geneSamplerComponent)) return;
        foreach (var gene in geneSamplerComponent.Genes)
            if (!ent.Comp.Genes.Contains(gene))
                ent.Comp.Genes.Add(gene);

        PredictedQueueDel(args.Used);
        args.Handled = true;
    }
}
