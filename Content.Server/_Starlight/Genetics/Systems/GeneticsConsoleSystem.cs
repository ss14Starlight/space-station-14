using System.Linq;
using Content.Shared._Starlight.Genetics;
using Content.Shared._Starlight.Genetics.Components;
using Content.Shared._Starlight.Genetics.Genes.Components;
using Content.Shared._Starlight.Genetics.Genes.Systems;
using Content.Shared._Starlight.Genetics.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

namespace Content.Server._Starlight.Genetics.Systems;

public sealed partial class GeneticsConsoleSystem : SharedGeneticsConsoleSystem
{
    [Dependency] private IEntityManager _entityManager = null!;
    [Dependency] private UserInterfaceSystem _ui = null!;
    [Dependency] private IndividualGeneSystem _iGene = null!;
    [Dependency] private SharedContainerSystem _container = null!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<GeneticsConsoleComponent>(GeneticsConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
        });
    }

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<GeneticsConsoleComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.GenePoolContainer =
            _container.EnsureContainer<Container>(ent, GeneticsConsoleComponent.GenePoolContainerId);
        var pool = ent.Comp.GenePoolContainer;
        foreach (var bundle in ent.Comp.DefaultGenes.Select(proto => _iGene.GeneFromSample(proto))
                     .Select(gene => (gene, Transform(gene))))
            _container.Insert(bundle, pool);
        ent.Comp.InputGenesContainer =
            _container.EnsureContainer<Container>(ent, GeneticsConsoleComponent.InputGenesContainerId);
    }

    [SubscribeLocalEvent]
    private void OnComponentShutdown(Entity<GeneticsConsoleComponent> ent, ref ComponentShutdown args)
    {
        _container.ShutdownContainer(ent.Comp.GenePoolContainer);
        _container.ShutdownContainer(ent.Comp.InputGenesContainer);
    }

    [SubscribeLocalEvent]
    private void OnStageGene(Entity<GeneticsConsoleComponent> ent, ref GeneticsConsoleStageGeneMessage args)
    {
        if (!TryComp<UserInterfaceComponent>(ent, out var ui)) return;
        if (!TryGetEntity(args.Uid, out var uid)) return;
        _container.Insert(uid.Value,
            _container.EnsureContainer<Container>(ent, GeneticsConsoleComponent.InputGenesContainerId));
        _ui.ServerSendUiMessage((ent, ui), GeneticsConsoleUiKey.Key, new GeneticsConsoleRefreshUIMessage());
    }

    [SubscribeLocalEvent]
    private void OnUnstageGene(Entity<GeneticsConsoleComponent> ent, ref GeneticsConsoleUnstageGeneMessage args)
    {
        if (!TryComp<UserInterfaceComponent>(ent, out var ui)) return;
        if (!TryGetEntity(args.Uid, out var uid)) return;
        _container.Insert(uid.Value,
            _container.EnsureContainer<Container>(ent, GeneticsConsoleComponent.GenePoolContainerId));
        _ui.ServerSendUiMessage((ent, ui), GeneticsConsoleUiKey.Key, new GeneticsConsoleRefreshUIMessage());
    }

    [SubscribeLocalEvent]
    private void OnSetSortIndex(Entity<GeneticsConsoleComponent> ent, ref GeneticsConsoleSetSortIndex args)
    {
        if (!TryComp<UserInterfaceComponent>(ent, out var ui)) return;
        if (args.Panel == 0) ent.Comp.GenePoolSortIndex = args.Index;
        else ent.Comp.InputGenesSortIndex = args.Index;
        Dirty(ent);
        _ui.ServerSendUiMessage((ent, ui), GeneticsConsoleUiKey.Key, new GeneticsConsoleRefreshUIMessage());
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
