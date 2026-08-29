using Content.Shared._Starlight.Genetics;
using Content.Shared._Starlight.Genetics.Components;
using Content.Shared._Starlight.Genetics.Genes.Components;
using Content.Shared._Starlight.Genetics.Systems;
using Robust.Server.GameObjects;

namespace Content.Server._Starlight.Genetics.Systems;

public sealed class GeneticsConsoleSystem : SharedGeneticsConsoleSystem
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<GeneticsConsoleComponent>(GeneticsConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
        });
    }

    private void OnOpened(Entity<GeneticsConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUserInterface(ent);
    }

    private void UpdateUserInterface(Entity<GeneticsConsoleComponent> ent)
    {
        var data = new List<GeneData>();
        foreach (var geneEntity in ent.Comp.Genes)
        {
            if (!_entityManager.TryGetComponent<IndividualGeneComponent>(geneEntity, out var individualGene)) continue;
            data.Add(new GeneData(individualGene));
        }
        var state = new GeneticsConsoleState(data);
        _ui.SetUiState(ent.Owner, GeneticsConsoleUiKey.Key, state);
    }
}
