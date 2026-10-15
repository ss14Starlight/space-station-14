using System.Linq;
using Content.Shared._Starlight.Genetics.Components;
using Content.Shared._Starlight.Genetics.Genes.Components;
using Content.Shared._Starlight.Genetics.Genes.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared._Starlight.Genetics.Systems;

public sealed partial class GeneticsConsoleSystem : EntitySystem
{
    [Dependency] private MetaDataSystem _meta = null!;
    [Dependency] private SharedPopupSystem _popup = null!;
    [Dependency] private SharedContainerSystem _container = null!;
    [Dependency] private SharedUserInterfaceSystem _ui = null!;
    [Dependency] private IndividualGeneSystem _iGene = null!;
    [Dependency] private INetManager _net = null!;

    [SubscribeLocalEvent]
    private void OnComponentInit(Entity<GeneticsConsoleComponent> ent, ref ComponentInit args)
    {
        ent.Comp.GenePoolContainer =
            _container.EnsureContainer<Container>(ent, GeneticsConsoleComponent.GenePoolContainerId);
        ent.Comp.InputGenesContainer =
            _container.EnsureContainer<Container>(ent, GeneticsConsoleComponent.InputGenesContainerId);
    }

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<GeneticsConsoleComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient) return; // Only do this part server-side.
        var pool = ent.Comp.GenePoolContainer;
        foreach (var gene in ent.Comp.DefaultGenes.Select(proto => _iGene.GeneFromSample(proto)))
        {
            if (gene is null) continue;
            _container.Insert(gene.Value.Owner, pool);
            ent.Comp.AllGenes.Add(gene.Value);
        }
        Dirty(ent);
    }

    [SubscribeLocalEvent]
    private void OnComponentShutdown(Entity<GeneticsConsoleComponent> ent, ref ComponentShutdown args)
    {
        _container.ShutdownContainer(ent.Comp.GenePoolContainer);
        _container.ShutdownContainer(ent.Comp.InputGenesContainer);
    }

    [SubscribeLocalEvent(before: [typeof(SharedUserInterfaceSystem)])]
    private void OnInteractUsing(Entity<GeneticsConsoleComponent> ent, ref InteractUsingEvent args)
    {
        if (!TryComp<GeneSamplerComponent>(args.Used, out var geneSamplerComponent)) return;
        var count = 0;
        foreach (var gene in from gene in geneSamplerComponent.Genes
                 let comp = Comp<IndividualGeneComponent>(gene)
                 where !IsIdenticalInPool(ent, (gene, comp))
                 select gene)
        {
            count++;
            _container.Insert(gene, ent.Comp.GenePoolContainer);
            ent.Comp.AllGenes.Add(gene);
        }

        _popup.PopupPredicted(
            Loc.GetString(ent.Comp.SamplerInsertPopup ?? string.Empty, ("amount", count > 0 ? count.ToString() : "No")),
            ent, args.User);
        Dirty(ent);

        PredictedQueueDel(args.Used);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnStageGene(Entity<GeneticsConsoleComponent> ent, ref GeneticsConsoleStageGeneMessage args)
    {
        if (!TryComp<UserInterfaceComponent>(ent, out var ui)) return;
        if (!TryGetEntity(args.Uid, out var uid)) return;

        _container.Insert(uid.Value, ent.Comp.InputGenesContainer);

        RefreshUI((ent, ui), args);
    }

    [SubscribeLocalEvent]
    private void OnUnstageGene(Entity<GeneticsConsoleComponent> ent, ref GeneticsConsoleUnstageGeneMessage args)
    {
        if (!TryComp<UserInterfaceComponent>(ent, out var ui)) return;
        if (!TryGetEntity(args.Uid, out var uid)) return;

        _container.Insert(uid.Value, ent.Comp.GenePoolContainer);

        RefreshUI((ent, ui), args);
    }

    [SubscribeLocalEvent]
    private void OnSetSortIndex(Entity<GeneticsConsoleComponent> ent, ref GeneticsConsoleSetSortIndex args)
    {
        if (!TryComp<UserInterfaceComponent>(ent, out var ui)) return;

        if (args.Panel == 0) ent.Comp.GenePoolSortIndex = args.Index;
        else ent.Comp.InputGenesSortIndex = args.Index;
        Dirty(ent);

        RefreshUI((ent, ui), args);
    }

    [SubscribeLocalEvent]
    private void OnRenameGenome(Entity<GeneticsConsoleComponent> ent, ref GeneticsConsoleRenameGeneMessage args)
    {
        if (!TryComp<UserInterfaceComponent>(ent, out var ui)) return;
        if (!TryGetEntity(args.Uid, out var uid)) return;
        if (!TryComp<IndividualGeneComponent>(uid, out var gene)) return;

        gene.Name = args.NewName;
        _meta.SetEntityName(uid.Value, $"{args.NewName} ({gene.TechnicalName})");
        Dirty(ent);

        RefreshUI((ent, ui), args);
    }

    [SubscribeLocalEvent]
    private void OnClearInputs(Entity<GeneticsConsoleComponent> ent, ref GeneticsConsoleClearInputsMessage args)
    {
        if (!TryComp<UserInterfaceComponent>(ent, out var ui)) return;

        foreach (var gene in ent.Comp.InputGenesContainer.ContainedEntities.ToList())
            _container.Insert(gene, ent.Comp.GenePoolContainer);

        RefreshUI((ent, ui), args);
    }

    [SubscribeLocalEvent]
    private void OnDeleteGene(Entity<GeneticsConsoleComponent> ent, ref GeneticsConsoleDeleteGeneMessage args)
    {
        if (!TryComp<UserInterfaceComponent>(ent, out var ui)) return;
        if (!TryGetEntity(args.Uid, out var uid)) return;

        PredictedQueueDel(uid);
        ent.Comp.AllGenes.Remove(uid.Value);
        Dirty(ent);
        RefreshUI((ent, ui), args);
    }

    [SubscribeLocalEvent]
    private void OnDispenseSampler(Entity<GeneticsConsoleComponent> ent, ref GeneticsConsoleDispenseSamplerMessage args)
    {
        if (!TryComp<UserInterfaceComponent>(ent, out var ui)) return;
        PredictedSpawnAtPosition(ent.Comp.SamplerPrototype, Transform(ent).Coordinates);
        _popup.PopupPredicted(Loc.GetString(ent.Comp.SamplerDispensePopup ?? string.Empty), ent, args.Actor);
        RefreshUI((ent, ui), args);
    }

    private void RefreshUI(Entity<UserInterfaceComponent?> ent, BoundUserInterfaceMessage args) =>
        _ui.RaiseUiMessage(ent, GeneticsConsoleUiKey.Key, new GeneticsConsoleRefreshUiMessage
        {
            Actor = args.Actor
        });

    private bool IsIdenticalInPool(Entity<GeneticsConsoleComponent> ent, Entity<IndividualGeneComponent> gene) =>
        ent.Comp.AllGenes.Any(existing =>
        {
            var comp = Comp<IndividualGeneComponent>(existing);
            return _iGene.IsIdentical((existing, comp), gene);
        });
}
