using System.Linq;
using Content.Server.Power.EntitySystems;
using Content.Shared._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared._Sol.Medical.Virology.Events;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Localizations;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Random;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Sol.Medical.Virology;

/// <summary>
/// Analyzer / incubator / synthesizer cycles for the bioterror clandestine lab.
/// </summary>
public sealed class ClandestineLabSystem : EntitySystem
{
    public const string AnalyzerChamberContainerId = "chamber";

    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly PathogenStrainRegistrySystem _registry = default!;
    [Dependency] private readonly PathogenSystem _pathogen = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly RandomHelperSystem _randomHelper = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ClandestineSampleAnalyzerComponent, AfterInteractUsingEvent>(OnAnalyzerInsert);
        SubscribeLocalEvent<ClandestineSampleAnalyzerComponent, ActivateInWorldEvent>(OnAnalyzerActivate);
        SubscribeLocalEvent<ClandestineSampleAnalyzerComponent, PowerChangedEvent>(OnAnalyzerPowerChanged);
        SubscribeLocalEvent<ClandestineSampleAnalyzerComponent, ComponentStartup>(OnAnalyzerStartup);
        SubscribeLocalEvent<ClandestineSampleAnalyzerComponent, ExaminedEvent>(OnAnalyzerExamined);

        SubscribeLocalEvent<ClandestineCultureIncubatorComponent, AfterInteractUsingEvent>(OnIncubatorInsert);
        SubscribeLocalEvent<ClandestineCultureIncubatorComponent, ActivateInWorldEvent>(OnIncubatorActivate);
        SubscribeLocalEvent<ClandestineCultureIncubatorComponent, PowerChangedEvent>(OnIncubatorPowerChanged);
        SubscribeLocalEvent<ClandestineCultureIncubatorComponent, ComponentStartup>(OnIncubatorStartup);
        SubscribeLocalEvent<ClandestineCultureIncubatorComponent, ExaminedEvent>(OnIncubatorExamined);
        SubscribeLocalEvent<ClandestineCultureIncubatorComponent, EntInsertedIntoContainerMessage>(OnIncubatorContainerModified);
        SubscribeLocalEvent<ClandestineCultureIncubatorComponent, EntRemovedFromContainerMessage>(OnIncubatorContainerModified);
        SubscribeLocalEvent<ClandestineCultureIncubatorComponent, SolutionContainerChangedEvent>(OnIncubatorSolutionChanged);
        SubscribeLocalEvent<ClandestineCultureIncubatorComponent, BoundUIOpenedEvent>(OnIncubatorUiOpened);
        SubscribeLocalEvent<ClandestineCultureIncubatorComponent, CultureIncubatorStartMessage>(OnIncubatorStart);
        SubscribeLocalEvent<ClandestineCultureIncubatorComponent, CultureIncubatorEjectSampleMessage>(OnIncubatorEjectSample);
        SubscribeLocalEvent<ClandestineCultureIncubatorComponent, CultureIncubatorEjectAllMessage>(OnIncubatorEjectAll);
        SubscribeLocalEvent<ClandestineCultureIncubatorComponent, CultureIncubatorRetrieveMessage>(OnIncubatorRetrieve);

        SubscribeLocalEvent<MicrobialSampleComponent, ExaminedEvent>(OnSampleExamined);

        SubscribeLocalEvent<ClandestinePathogenSynthesizerComponent, AfterInteractUsingEvent>(OnSynthesizerInsert);
        SubscribeLocalEvent<ClandestinePathogenSynthesizerComponent, PowerChangedEvent>(OnSynthesizerPowerChanged);
        SubscribeLocalEvent<ClandestinePathogenSynthesizerComponent, ComponentStartup>(OnSynthesizerStartup);
        SubscribeLocalEvent<ClandestinePathogenSynthesizerComponent, ExaminedEvent>(OnSynthesizerExamined);
        SubscribeLocalEvent<ClandestinePathogenSynthesizerComponent, EntInsertedIntoContainerMessage>(OnSynthesizerContainerModified);
        SubscribeLocalEvent<ClandestinePathogenSynthesizerComponent, EntRemovedFromContainerMessage>(OnSynthesizerContainerModified);
        SubscribeLocalEvent<ClandestinePathogenSynthesizerComponent, SolutionContainerChangedEvent>(OnSynthesizerSolutionChanged);
        SubscribeLocalEvent<ClandestinePathogenSynthesizerComponent, BoundUIOpenedEvent>(OnSynthesizerUiOpened);
        SubscribeLocalEvent<ClandestinePathogenSynthesizerComponent, ItemSlotInsertAttemptEvent>(OnSynthesizerSubstrateInsertAttempt);
        SubscribeLocalEvent<ClandestinePathogenSynthesizerComponent, PathogenSynthesizerStartMessage>(OnSynthesizerStart);
        SubscribeLocalEvent<ClandestinePathogenSynthesizerComponent, PathogenSynthesizerToggleGeneMessage>(OnSynthesizerToggleGene);
        SubscribeLocalEvent<ClandestinePathogenSynthesizerComponent, PathogenSynthesizerClearSelectionMessage>(OnSynthesizerClearSelection);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var analyzers = EntityQueryEnumerator<ClandestineSampleAnalyzerComponent>();
        while (analyzers.MoveNext(out var uid, out var analyzer))
        {
            TickAnalyzer((uid, analyzer));
        }

        var incubators = EntityQueryEnumerator<ClandestineCultureIncubatorComponent>();
        while (incubators.MoveNext(out var uid, out var incubator))
        {
            TickIncubator((uid, incubator));
        }

        var synthesizers = EntityQueryEnumerator<ClandestinePathogenSynthesizerComponent>();
        while (synthesizers.MoveNext(out var uid, out var synth))
        {
            TickSynthesizer((uid, synth));
        }
    }

    private void OnAnalyzerStartup(Entity<ClandestineSampleAnalyzerComponent> machine, ref ComponentStartup args)
    {
        _containers.EnsureContainer<Container>(machine.Owner, AnalyzerChamberContainerId);
        UpdateAnalyzerVisuals(machine);
    }

    private void OnAnalyzerPowerChanged(Entity<ClandestineSampleAnalyzerComponent> machine, ref PowerChangedEvent args)
    {
        UpdateAnalyzerVisuals(machine);
    }

    private void OnIncubatorStartup(Entity<ClandestineCultureIncubatorComponent> machine, ref ComponentStartup args)
    {
        _containers.EnsureContainer<Container>(machine.Owner, SharedCultureIncubator.ChamberContainerId);
        UpdateIncubatorVisuals(machine);
        UpdateIncubatorUi(machine);
    }

    private void OnIncubatorPowerChanged(Entity<ClandestineCultureIncubatorComponent> machine, ref PowerChangedEvent args)
    {
        UpdateIncubatorVisuals(machine);
        UpdateIncubatorUi(machine);
    }

    private void OnIncubatorContainerModified<T>(Entity<ClandestineCultureIncubatorComponent> machine, ref T args)
        where T : notnull
    {
        UpdateIncubatorUi(machine);
    }

    private void OnIncubatorSolutionChanged(Entity<ClandestineCultureIncubatorComponent> machine, ref SolutionContainerChangedEvent args)
    {
        UpdateIncubatorUi(machine);
    }

    private void OnIncubatorUiOpened(Entity<ClandestineCultureIncubatorComponent> machine, ref BoundUIOpenedEvent args)
    {
        UpdateIncubatorUi(machine);
    }

    private void OnAnalyzerInsert(Entity<ClandestineSampleAnalyzerComponent> machine, ref AfterInteractUsingEvent args)
    {
        if (!args.CanReach || args.Handled)
            return;

        if (!TryComp<MicrobialSampleComponent>(args.Used, out var sample))
            return;

        if (sample.Analyzed)
        {
            _popup.PopupEntity(Loc.GetString("sol-bioterror-analyzer-already"), machine, args.User);
            return;
        }

        if (machine.Comp.Processing)
        {
            _popup.PopupEntity(Loc.GetString("sol-bioterror-analyzer-busy"), machine, args.User);
            return;
        }

        if (machine.Comp.HasFinishedSample)
        {
            _popup.PopupEntity(Loc.GetString("sol-bioterror-analyzer-eject-first"), machine, args.User);
            return;
        }

        if (!_power.IsPowered(machine.Owner))
        {
            _popup.PopupEntity(Loc.GetString("sol-bioterror-lab-unpowered"), machine, args.User);
            return;
        }

        args.Handled = true;

        var pending = EnsureComp<PendingAnalysisDataComponent>(machine);
        pending.ChassisId = null;
        pending.Traits = new List<ProtoId<PathogenTraitPrototype>>(sample.Traits);
        pending.Quality = sample.Quality;
        pending.Contaminated = sample.Contaminated;
        pending.SourceLabel = sample.SourceLabel;
        pending.User = args.User;

        QueueDel(args.Used);

        machine.Comp.Processing = true;
        machine.Comp.CycleEndsAt = _timing.CurTime + machine.Comp.AnalysisDelay;
        Dirty(machine);
        UpdateAnalyzerVisuals(machine);

        _popup.PopupEntity(Loc.GetString("sol-bioterror-analyzer-started"), machine, args.User);
        MarkMachineDeployed(machine.Owner, analyzer: true);
    }

    private void OnAnalyzerActivate(Entity<ClandestineSampleAnalyzerComponent> machine, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        if (machine.Comp.Processing)
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("sol-bioterror-analyzer-busy"), machine, args.User);
            return;
        }

        if (!machine.Comp.HasFinishedSample)
            return;

        args.Handled = true;
        EjectAnalyzedSample(machine, args.User);
    }

    private void TickAnalyzer(Entity<ClandestineSampleAnalyzerComponent> machine)
    {
        if (!machine.Comp.Processing)
            return;

        if (!_power.IsPowered(machine.Owner))
        {
            machine.Comp.Processing = false;
            RemComp<PendingAnalysisDataComponent>(machine.Owner);
            Dirty(machine);
            UpdateAnalyzerVisuals(machine);
            _popup.PopupEntity(Loc.GetString("sol-bioterror-lab-unpowered"), machine, machine);
            return;
        }

        if (_timing.CurTime < machine.Comp.CycleEndsAt)
            return;

        machine.Comp.Processing = false;

        if (!TryComp<PendingAnalysisDataComponent>(machine.Owner, out var pending))
        {
            Dirty(machine);
            UpdateAnalyzerVisuals(machine);
            return;
        }

        var chamber = _containers.EnsureContainer<Container>(machine.Owner, AnalyzerChamberContainerId);
        var sample = Spawn("SolMicrobialSample", Transform(machine).Coordinates);
        var sampleComp = EnsureComp<MicrobialSampleComponent>(sample);
        sampleComp.ChassisId = pending.ChassisId;
        sampleComp.Traits = new List<ProtoId<PathogenTraitPrototype>>(pending.Traits);
        sampleComp.Quality = pending.Quality;
        sampleComp.Contaminated = pending.Contaminated;
        sampleComp.SourceLabel = pending.SourceLabel;
        sampleComp.Analyzed = true;
        Dirty(sample, sampleComp);
        _meta.SetEntityName(sample, Loc.GetString("sol-bioterror-sample-analyzed-name",
            ("source", pending.SourceLabel ?? "unknown")));

        if (!_containers.Insert(sample, chamber))
            _randomHelper.RandomOffset(sample, 0.35f);

        var user = pending.User;
        RemComp<PendingAnalysisDataComponent>(machine.Owner);
        machine.Comp.HasFinishedSample = true;
        Dirty(machine);
        UpdateAnalyzerVisuals(machine);

        _popup.PopupEntity(Loc.GetString("sol-bioterror-analyzer-complete"), machine, user);
    }

    private void EjectAnalyzedSample(Entity<ClandestineSampleAnalyzerComponent> machine, EntityUid user)
    {
        var chamber = _containers.EnsureContainer<Container>(machine.Owner, AnalyzerChamberContainerId);
        foreach (var sample in chamber.ContainedEntities.ToList())
        {
            if (!_containers.Remove(sample, chamber))
                continue;

            if (user.Valid && !Deleted(user))
                _hands.PickupOrDrop(user, sample, checkActionBlocker: false);
            else
                _randomHelper.RandomOffset(sample, 0.35f);
        }

        machine.Comp.HasFinishedSample = false;
        Dirty(machine);
        UpdateAnalyzerVisuals(machine);
        _popup.PopupEntity(Loc.GetString("sol-bioterror-analyzer-retrieved"), machine, user);
    }

    private void OnIncubatorInsert(Entity<ClandestineCultureIncubatorComponent> machine, ref AfterInteractUsingEvent args)
    {
        if (!args.CanReach || args.Handled)
            return;

        if (!TryComp<MicrobialSampleComponent>(args.Used, out var sample) || !sample.Analyzed)
        {
            _popup.PopupEntity(Loc.GetString("sol-bioterror-incubator-need-analyzed"), machine, args.User);
            return;
        }

        if (machine.Comp.CycleInProgress || machine.Comp.HasFinishedCulture)
        {
            _popup.PopupEntity(Loc.GetString("sol-bioterror-incubator-busy"), machine, args.User);
            return;
        }

        var chamber = _containers.EnsureContainer<Container>(machine.Owner, SharedCultureIncubator.ChamberContainerId);
        if (chamber.Count >= machine.Comp.MaxSamples)
        {
            _popup.PopupEntity(Loc.GetString("sol-bioterror-incubator-full"), machine, args.User);
            return;
        }

        if (!_containers.Insert(args.Used, chamber))
            return;

        args.Handled = true;
        UpdateIncubatorUi(machine);
        MarkMachineDeployed(machine.Owner, incubator: true);
    }

    private void OnIncubatorStart(Entity<ClandestineCultureIncubatorComponent> machine, ref CultureIncubatorStartMessage args)
    {
        if (machine.Comp.CycleInProgress || machine.Comp.HasFinishedCulture)
            return;

        var chamber = _containers.EnsureContainer<Container>(machine.Owner, SharedCultureIncubator.ChamberContainerId);
        if (chamber.Count == 0)
            return;

        if (!_power.IsPowered(machine.Owner))
        {
            _popup.PopupEntity(Loc.GetString("sol-bioterror-lab-unpowered"), machine, args.Actor);
            return;
        }

        var entries = new List<PendingCultureEntry>();
        var samplesToConsume = new List<EntityUid>();
        var worstQuality = 1f;
        var anyContaminated = false;

        foreach (var sampleEnt in chamber.ContainedEntities.ToList())
        {
            if (!TryComp<MicrobialSampleComponent>(sampleEnt, out var sample) || !sample.Analyzed)
                continue;

            // Need at least one gene to incubate.
            if (sample.Traits.Count == 0)
                continue;

            worstQuality = Math.Min(worstQuality, sample.Quality);
            anyContaminated |= sample.Contaminated;

            entries.Add(new PendingCultureEntry
            {
                Traits = new List<ProtoId<PathogenTraitPrototype>>(sample.Traits),
                Quality = sample.Quality,
                Contaminated = sample.Contaminated,
            });
            samplesToConsume.Add(sampleEnt);
        }

        if (entries.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("sol-bioterror-incubator-no-viable"), machine, args.Actor);
            return;
        }

        var nutrientCost = ClandestineCultureIncubatorComponent.GetBatchNutrientCost(
            entries.Count,
            machine.Comp.NutrientBaseCost,
            machine.Comp.NutrientExtraCost);

        if (!TryConsumeReagent(machine.Owner, machine.Comp.NutrientReagent, nutrientCost))
        {
            _popup.PopupEntity(Loc.GetString("sol-bioterror-incubator-need-nutrient"), machine, args.Actor);
            return;
        }

        var batch = EnsureComp<PendingCultureBatchComponent>(machine);
        batch.Cultures.Clear();
        batch.Cultures.AddRange(entries);

        foreach (var sampleEnt in samplesToConsume)
            QueueDel(sampleEnt);

        var delay = machine.Comp.CultureDelay * (2f - Math.Clamp(worstQuality, 0.2f, 1f));
        if (anyContaminated)
            delay *= 1.5f;

        machine.Comp.CycleInProgress = true;
        machine.Comp.CycleStartedAt = _timing.CurTime;
        machine.Comp.CycleEndsAt = _timing.CurTime + delay;
        Dirty(machine);
        UpdateIncubatorVisuals(machine);
        UpdateIncubatorUi(machine);
        _popup.PopupEntity(Loc.GetString("sol-bioterror-incubator-started"), machine, args.Actor);
    }

    private void OnIncubatorEjectSample(Entity<ClandestineCultureIncubatorComponent> machine, ref CultureIncubatorEjectSampleMessage args)
    {
        if (machine.Comp.CycleInProgress || machine.Comp.HasFinishedCulture)
            return;

        var chamber = _containers.EnsureContainer<Container>(machine.Owner, SharedCultureIncubator.ChamberContainerId);
        var sample = GetEntity(args.Sample);

        if (!_containers.Remove(sample, chamber))
            return;

        _randomHelper.RandomOffset(sample, 0.35f);
        UpdateIncubatorUi(machine);
    }

    private void OnIncubatorEjectAll(Entity<ClandestineCultureIncubatorComponent> machine, ref CultureIncubatorEjectAllMessage args)
    {
        if (machine.Comp.CycleInProgress || machine.Comp.HasFinishedCulture)
            return;

        var chamber = _containers.EnsureContainer<Container>(machine.Owner, SharedCultureIncubator.ChamberContainerId);
        foreach (var sample in chamber.ContainedEntities.ToList())
        {
            _containers.Remove(sample, chamber);
            _randomHelper.RandomOffset(sample, 0.35f);
        }

        UpdateIncubatorUi(machine);
    }

    private void OnIncubatorRetrieve(Entity<ClandestineCultureIncubatorComponent> machine, ref CultureIncubatorRetrieveMessage args)
    {
        if (!machine.Comp.HasFinishedCulture)
            return;

        RetrieveFinishedCultures(machine, args.Actor);
    }

    private void OnIncubatorActivate(Entity<ClandestineCultureIncubatorComponent> machine, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        if (!machine.Comp.HasFinishedCulture)
            return;

        args.Handled = true;
        RetrieveFinishedCultures(machine, args.User);
    }

    private void RetrieveFinishedCultures(Entity<ClandestineCultureIncubatorComponent> machine, EntityUid user)
    {
        // Finish any pending materialization first (e.g. race with the tick).
        if (TryComp<PendingCultureBatchComponent>(machine.Owner, out _))
            MaterializeFinishedCultures(machine);

        var chamber = _containers.EnsureContainer<Container>(machine.Owner, SharedCultureIncubator.ChamberContainerId);
        var ejected = 0;

        foreach (var culture in chamber.ContainedEntities.ToList())
        {
            if (!HasComp<PathogenCultureComponent>(culture))
                continue;

            if (!_containers.Remove(culture, chamber))
                continue;

            if (user.Valid && !Deleted(user))
                _hands.PickupOrDrop(user, culture, checkActionBlocker: false);
            else
                _randomHelper.RandomOffset(culture, 0.35f);

            ejected++;
        }

        machine.Comp.HasFinishedCulture = false;
        Dirty(machine);
        UpdateIncubatorVisuals(machine);
        UpdateIncubatorUi(machine);

        if (ejected == 0)
            _popup.PopupEntity(Loc.GetString("sol-bioterror-incubator-empty-output"), machine, user);
        else
            _popup.PopupEntity(Loc.GetString("sol-bioterror-incubator-retrieved"), machine, user);
    }

    private void MaterializeFinishedCultures(Entity<ClandestineCultureIncubatorComponent> machine)
    {
        if (!TryComp<PendingCultureBatchComponent>(machine.Owner, out var batch))
            return;

        var chamber = _containers.EnsureContainer<Container>(machine.Owner, SharedCultureIncubator.ChamberContainerId);

        // One cellular-substrate binder for the whole batch; matching genes stack.
        float bestQuality = 0f;
        var anyContaminated = false;
        var genes = new Dictionary<ProtoId<PathogenTraitPrototype>, (int Count, float Quality, bool Contaminated)>();

        foreach (var pending in batch.Cultures)
        {
            bestQuality = Math.Max(bestQuality, pending.Quality);
            anyContaminated |= pending.Contaminated;

            foreach (var traitId in pending.Traits)
            {
                if (genes.TryGetValue(traitId, out var existing))
                {
                    genes[traitId] = (
                        existing.Count + 1,
                        Math.Max(existing.Quality, pending.Quality),
                        existing.Contaminated || pending.Contaminated);
                }
                else
                {
                    genes[traitId] = (1, pending.Quality, pending.Contaminated);
                }
            }
        }

        if (batch.Cultures.Count > 0)
        {
            var junk = CreateSubstrateCulture(machine, bestQuality > 0 ? bestQuality : 0.5f, anyContaminated);
            if (!_containers.Insert(junk, chamber))
                _randomHelper.RandomOffset(junk, 0.35f);
        }

        foreach (var (traitId, data) in genes)
        {
            var culture = CreateGeneCulture(machine, traitId, data.Count, data.Quality, data.Contaminated);
            if (!_containers.Insert(culture, chamber))
                _randomHelper.RandomOffset(culture, 0.35f);
        }

        RemComp<PendingCultureBatchComponent>(machine.Owner);
    }

    private void OnSynthesizerStartup(Entity<ClandestinePathogenSynthesizerComponent> machine, ref ComponentStartup args)
    {
        _containers.EnsureContainer<Container>(machine.Owner, SharedPathogenSynthesizer.GeneStorageContainerId);
        UpdateSynthesizerVisuals(machine);
        UpdateSynthesizerUi(machine);
    }

    private void OnSynthesizerPowerChanged(Entity<ClandestinePathogenSynthesizerComponent> machine, ref PowerChangedEvent args)
    {
        UpdateSynthesizerVisuals(machine);
        UpdateSynthesizerUi(machine);
    }

    private void OnSynthesizerContainerModified<T>(Entity<ClandestinePathogenSynthesizerComponent> machine, ref T args)
        where T : notnull
    {
        PruneSelectedGenes(machine);
        UpdateSynthesizerUi(machine);
    }

    private void OnSynthesizerSolutionChanged(Entity<ClandestinePathogenSynthesizerComponent> machine, ref SolutionContainerChangedEvent args)
    {
        UpdateSynthesizerUi(machine);
    }

    private void OnSynthesizerUiOpened(Entity<ClandestinePathogenSynthesizerComponent> machine, ref BoundUIOpenedEvent args)
    {
        UpdateSynthesizerUi(machine);
    }

    private void OnSynthesizerSubstrateInsertAttempt(Entity<ClandestinePathogenSynthesizerComponent> machine, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled || args.Slot.ID != SharedPathogenSynthesizer.SubstrateSlotId)
            return;

        if (!TryComp<PathogenCultureComponent>(args.Item, out var culture) || !culture.IsChassisCulture)
            args.Cancelled = true;
    }

    private void OnSynthesizerInsert(Entity<ClandestinePathogenSynthesizerComponent> machine, ref AfterInteractUsingEvent args)
    {
        if (!args.CanReach || args.Handled)
            return;

        if (!TryComp<PathogenCultureComponent>(args.Used, out var culture))
        {
            _popup.PopupEntity(Loc.GetString("sol-bioterror-synth-need-culture"), machine, args.User);
            return;
        }

        if (machine.Comp.CycleInProgress)
        {
            _popup.PopupEntity(Loc.GetString("sol-bioterror-synth-busy"), machine, args.User);
            return;
        }

        if (culture.IsChassisCulture)
        {
            if (!_itemSlots.TryInsert(machine.Owner, SharedPathogenSynthesizer.SubstrateSlotId, args.Used, args.User))
            {
                _popup.PopupEntity(Loc.GetString("sol-bioterror-synth-substrate-blocked"), machine, args.User);
                return;
            }

            args.Handled = true;
            UpdateSynthesizerUi(machine);
            MarkMachineDeployed(machine.Owner, synthesizer: true);
            return;
        }

        var storage = _containers.EnsureContainer<Container>(machine.Owner, SharedPathogenSynthesizer.GeneStorageContainerId);

        // Stack matching single-trait gene cultures instead of filling storage with duplicates.
        if (TryGetSingleGene(culture, out var traitId) &&
            TryFindStackableGene(storage, traitId, out var existing))
        {
            var existingComp = Comp<PathogenCultureComponent>(existing);
            existingComp.Count += Math.Max(1, culture.Count);
            existingComp.Viability = Math.Max(existingComp.Viability, culture.Viability);
            Dirty(existing, existingComp);
            RefreshGeneName(existing, existingComp);
            QueueDel(args.Used);
            args.Handled = true;
            UpdateSynthesizerUi(machine);
            MarkMachineDeployed(machine.Owner, synthesizer: true);
            return;
        }

        if (!_containers.Insert(args.Used, storage))
            return;

        RefreshGeneName(args.Used, culture);
        args.Handled = true;
        UpdateSynthesizerUi(machine);
        MarkMachineDeployed(machine.Owner, synthesizer: true);
    }

    private void OnSynthesizerStart(Entity<ClandestinePathogenSynthesizerComponent> machine, ref PathogenSynthesizerStartMessage args)
    {
        BeginSynthesis(machine, args.Actor);
    }

    private void OnSynthesizerToggleGene(Entity<ClandestinePathogenSynthesizerComponent> machine, ref PathogenSynthesizerToggleGeneMessage args)
    {
        if (machine.Comp.CycleInProgress)
            return;

        var gene = GetEntity(args.Gene);
        var storage = _containers.EnsureContainer<Container>(machine.Owner, SharedPathogenSynthesizer.GeneStorageContainerId);
        if (!storage.Contains(gene))
            return;

        if (!machine.Comp.SelectedGenes.Add(gene))
            machine.Comp.SelectedGenes.Remove(gene);

        UpdateSynthesizerUi(machine);
    }

    private void OnSynthesizerClearSelection(Entity<ClandestinePathogenSynthesizerComponent> machine, ref PathogenSynthesizerClearSelectionMessage args)
    {
        if (machine.Comp.CycleInProgress)
            return;

        machine.Comp.SelectedGenes.Clear();
        UpdateSynthesizerUi(machine);
    }

    private void BeginSynthesis(Entity<ClandestinePathogenSynthesizerComponent> machine, EntityUid user)
    {
        if (machine.Comp.CycleInProgress)
            return;

        if (!_power.IsPowered(machine.Owner))
        {
            _popup.PopupEntity(Loc.GetString("sol-bioterror-lab-unpowered"), machine, user);
            return;
        }

        if (!TryBuildRecipe(machine, out var traits, out var viability, out var consumedGenes, out var recipeError))
        {
            _popup.PopupEntity(Loc.GetString("sol-bioterror-synth-invalid", ("error", recipeError ?? "unknown")), machine, user);
            return;
        }

        if (!_registry.TryValidateTraits(traits, machine.Comp.MaxTraitBudget, out var error))
        {
            _popup.PopupEntity(Loc.GetString("sol-bioterror-synth-invalid", ("error", error ?? "unknown")), machine, user);
            TriggerAccident(machine.Owner, PathogenStrainRegistrySystem.CustomBaseId, severity: 0.4f);
            return;
        }

        if (!TryConsumeReagent(machine.Owner, machine.Comp.StabilizerReagent, machine.Comp.StabilizerNeeded))
        {
            _popup.PopupEntity(Loc.GetString("sol-bioterror-synth-need-stabilizer"), machine, user);
            return;
        }

        if (_itemSlots.TryGetSlot(machine.Owner, SharedPathogenSynthesizer.SubstrateSlotId, out var slot)
            && slot.Item is { } substrateEnt)
        {
            QueueDel(substrateEnt);
        }

        foreach (var gene in consumedGenes)
            ConsumeGeneCharge(gene);

        machine.Comp.SelectedGenes.Clear();

        var budget = _registry.GetTraitBudget(traits);
        machine.Comp.PendingTraits = traits;
        machine.Comp.PendingViability = viability;
        machine.Comp.CycleInProgress = true;
        machine.Comp.CycleStartedAt = _timing.CurTime;
        machine.Comp.CycleEndsAt = _timing.CurTime + machine.Comp.GetSynthesisDelay(budget);
        Dirty(machine);
        UpdateSynthesizerVisuals(machine);
        UpdateSynthesizerUi(machine);
        _popup.PopupEntity(Loc.GetString("sol-bioterror-synth-started"), machine, user);
        MarkMachineDeployed(machine.Owner, synthesizer: true);
    }

    private bool TryBuildRecipe(
        Entity<ClandestinePathogenSynthesizerComponent> machine,
        out List<ProtoId<PathogenTraitPrototype>> traits,
        out float viability,
        out List<EntityUid> consumedGenes,
        out string? error)
    {
        traits = new();
        viability = 1f;
        consumedGenes = new();
        error = null;

        if (!_itemSlots.TryGetSlot(machine.Owner, SharedPathogenSynthesizer.SubstrateSlotId, out var slot)
            || !slot.HasItem
            || !TryComp<PathogenCultureComponent>(slot.Item, out var substrate)
            || !substrate.IsChassisCulture)
        {
            error = Loc.GetString("sol-bioterror-synth-need-substrate");
            return false;
        }

        viability = substrate.Viability;

        var storage = _containers.EnsureContainer<Container>(machine.Owner, SharedPathogenSynthesizer.GeneStorageContainerId);
        foreach (var geneEnt in machine.Comp.SelectedGenes.ToList())
        {
            if (!storage.Contains(geneEnt) || !TryComp<PathogenCultureComponent>(geneEnt, out var gene) || gene.IsChassisCulture)
            {
                machine.Comp.SelectedGenes.Remove(geneEnt);
                continue;
            }

            foreach (var trait in gene.Traits)
            {
                if (traits.Contains(trait))
                {
                    error = Loc.GetString("sol-bioterror-synth-error-duplicate-gene", ("gene", FormatTraitName(trait)));
                    return false;
                }

                traits.Add(trait);
            }

            viability = Math.Min(viability, gene.Viability);
            consumedGenes.Add(geneEnt);
        }

        return true;
    }

    private void PruneSelectedGenes(Entity<ClandestinePathogenSynthesizerComponent> machine)
    {
        var storage = _containers.EnsureContainer<Container>(machine.Owner, SharedPathogenSynthesizer.GeneStorageContainerId);
        machine.Comp.SelectedGenes.RemoveWhere(g => !storage.Contains(g));
    }

    private void UpdateSynthesizerUi(Entity<ClandestinePathogenSynthesizerComponent> machine)
    {
        PruneSelectedGenes(machine);

        var stabilizerAmount = (float) _solutions.GetTotalPrototypeQuantity(machine.Owner, machine.Comp.StabilizerReagent);
        var stabilizerMax = 0f;
        if (_solutions.TryGetSolution(machine.Owner, "tank", out _, out var solution))
            stabilizerMax = (float) solution.MaxVolume;

        string? substrateName = null;
        var hasSubstrate = false;
        if (_itemSlots.TryGetSlot(machine.Owner, SharedPathogenSynthesizer.SubstrateSlotId, out var slot) && slot.HasItem)
        {
            hasSubstrate = true;
            substrateName = MetaData(slot.Item!.Value).EntityName;
        }

        var storage = _containers.EnsureContainer<Container>(machine.Owner, SharedPathogenSynthesizer.GeneStorageContainerId);
        var genes = new List<PathogenSynthesizerGeneState>();

        foreach (var ent in storage.ContainedEntities)
        {
            if (!TryComp<PathogenCultureComponent>(ent, out var culture) || culture.IsChassisCulture)
                continue;

            var selected = machine.Comp.SelectedGenes.Contains(ent);
            var cost = _registry.GetTraitBudget(culture.Traits);
            genes.Add(new PathogenSynthesizerGeneState(
                GetNetEntity(ent),
                FormatGeneLabel(culture),
                cost,
                selected));
        }

        string? validationError = null;
        var canStart = false;
        List<ProtoId<PathogenTraitPrototype>> traits = new();
        PathogenSynthesizerForecastState? forecast = null;

        if (TryBuildRecipe(machine, out traits, out _, out _, out validationError))
        {
            if (!_registry.TryValidateTraits(traits, machine.Comp.MaxTraitBudget, out var traitError))
                validationError = traitError;
            else
                canStart = !machine.Comp.CycleInProgress
                    && _power.IsPowered(machine.Owner)
                    && stabilizerAmount >= machine.Comp.StabilizerNeeded
                    && traits.Count > 0;
        }

        if (traits.Count > 0 && validationError == null)
            forecast = BuildForecast(traits);
        else if (machine.Comp.SelectedGenes.Count > 0)
        {
            // Show forecast for selected genes even when substrate is missing.
            var selectedTraits = new List<ProtoId<PathogenTraitPrototype>>();
            foreach (var geneEnt in machine.Comp.SelectedGenes)
            {
                if (!TryComp<PathogenCultureComponent>(geneEnt, out var gene) || gene.IsChassisCulture)
                    continue;
                selectedTraits.AddRange(gene.Traits);
            }

            if (selectedTraits.Count > 0 && _registry.TryValidateTraits(selectedTraits, machine.Comp.MaxTraitBudget, out _))
                forecast = BuildForecast(selectedTraits);
        }

        var budget = _registry.GetTraitBudget(traits.Count > 0 ? traits : GetSelectedTraits(machine));
        var estimated = (float) machine.Comp.GetSynthesisDelay(budget).TotalSeconds;
        var state = new PathogenSynthesizerBoundUserInterfaceState(
            _power.IsPowered(machine.Owner),
            machine.Comp.CycleInProgress,
            stabilizerAmount,
            stabilizerMax,
            machine.Comp.StabilizerReagent,
            machine.Comp.StabilizerNeeded,
            budget,
            machine.Comp.MaxTraitBudget,
            estimated,
            hasSubstrate,
            substrateName,
            genes.ToArray(),
            validationError,
            canStart,
            machine.Comp.CycleStartedAt,
            machine.Comp.CycleEndsAt,
            forecast);

        _ui.SetUiState(machine.Owner, PathogenSynthesizerUiKey.Key, state);
    }

    private List<ProtoId<PathogenTraitPrototype>> GetSelectedTraits(Entity<ClandestinePathogenSynthesizerComponent> machine)
    {
        var traits = new List<ProtoId<PathogenTraitPrototype>>();
        foreach (var geneEnt in machine.Comp.SelectedGenes)
        {
            if (!TryComp<PathogenCultureComponent>(geneEnt, out var gene) || gene.IsChassisCulture)
                continue;
            traits.AddRange(gene.Traits);
        }

        return traits;
    }

    private PathogenSynthesizerForecastState BuildForecast(IReadOnlyList<ProtoId<PathogenTraitPrototype>> traits)
    {
        var def = _registry.PreviewDefinition(traits);

        var routes = new List<string>();
        if ((def.Transmission & PathogenTransmission.Contact) != 0)
            routes.Add(Loc.GetString("sol-pathogen-synth-ui-route-contact"));
        if ((def.Transmission & PathogenTransmission.Airborne) != 0)
            routes.Add(Loc.GetString("sol-pathogen-synth-ui-route-airborne"));
        if ((def.Transmission & PathogenTransmission.Ingestion) != 0)
            routes.Add(Loc.GetString("sol-pathogen-synth-ui-route-ingestion"));
        if ((def.Transmission & PathogenTransmission.Fluid) != 0)
            routes.Add(Loc.GetString("sol-pathogen-synth-ui-route-fluid"));

        var transmission = routes.Count == 0
            ? Loc.GetString("sol-pathogen-synth-ui-route-none")
            : string.Join(", ", routes);

        var symptomParts = new List<string>();
        if (def.CoughChancePerSecond > 0.01f)
            symptomParts.Add(Loc.GetString("sol-pathogen-synth-ui-symptom-cough"));
        if (def.SneezeChancePerSecond > 0.01f)
            symptomParts.Add(Loc.GetString("sol-pathogen-synth-ui-symptom-sneeze"));
        if (def.FeverTemperatureOffset > 0.1f)
            symptomParts.Add(Loc.GetString("sol-pathogen-synth-ui-symptom-fever"));
        foreach (var (damageType, amount) in def.SymptomaticDamage.DamageDict)
        {
            if (amount <= 0)
                continue;
            symptomParts.Add(Loc.GetString("sol-pathogen-synth-ui-symptom-damage",
                ("type", damageType),
                ("amount", amount.Float().ToString("F2"))));
        }

        var symptoms = symptomParts.Count == 0
            ? Loc.GetString("sol-pathogen-synth-ui-symptom-none")
            : string.Join(", ", symptomParts);

        var organs = def.TargetOrgans.Count == 0
            ? Loc.GetString("sol-pathogen-synth-ui-organs-none")
            : string.Join(", ", def.TargetOrgans);

        var treatments = new List<string>();
        foreach (var reagentId in def.Treatments)
        {
            if (_prototypes.TryIndex<Content.Shared.Chemistry.Reagent.ReagentPrototype>(reagentId, out var reagent))
                treatments.Add(reagent.LocalizedName);
            else
                treatments.Add(reagentId);
        }

        var treatmentText = treatments.Count == 0
            ? Loc.GetString("sol-pathogen-synth-ui-treatments-none")
            : string.Join(", ", treatments);

        return new PathogenSynthesizerForecastState(
            transmission,
            FormatDuration(def.IncubationDuration),
            FormatDuration(def.SymptomaticDuration),
            FormatDuration(def.CriticalDuration),
            FormatDuration(def.RecoveryDuration),
            symptoms,
            organs,
            treatmentText,
            Loc.GetString("sol-pathogen-synth-ui-infectivity-value",
                ("chance", (def.BaseInfectionChance * 100f).ToString("F0")),
                ("dose", def.InfectiveDose.ToString("F2"))),
            Loc.GetString("sol-pathogen-synth-ui-lethality-value",
                ("value", (def.Lethality * 100f).ToString("F0"))),
            Loc.GetString("sol-pathogen-synth-ui-sterilant-value",
                ("value", def.SterilantSusceptibility.ToString("F2"))));
    }

    private string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes >= 1)
            return Loc.GetString("sol-pathogen-synth-ui-duration-minutes", ("minutes", duration.TotalMinutes.ToString("F1")));
        return Loc.GetString("sol-pathogen-synth-ui-duration-seconds", ("seconds", duration.TotalSeconds.ToString("F0")));
    }

    private void TickIncubator(Entity<ClandestineCultureIncubatorComponent> machine)
    {
        if (machine.Comp.CycleInProgress)
        {
            if (!_power.IsPowered(machine.Owner))
            {
                machine.Comp.CycleInProgress = false;
                RemComp<PendingCultureBatchComponent>(machine.Owner);
                Dirty(machine);
                UpdateIncubatorVisuals(machine);
                UpdateIncubatorUi(machine);
                TriggerAccident(machine.Owner, PathogenStrainRegistrySystem.CustomBaseId, severity: 0.25f);
                _popup.PopupEntity(Loc.GetString("sol-bioterror-incubator-spoiled"), machine);
                return;
            }

            if (_timing.CurTime < machine.Comp.CycleEndsAt)
                return;

            machine.Comp.CycleInProgress = false;
            MaterializeFinishedCultures(machine);
            machine.Comp.HasFinishedCulture = true;
            machine.Comp.OvergrowAt = _timing.CurTime + TimeSpan.FromSeconds(45);
            Dirty(machine);
            UpdateIncubatorVisuals(machine);
            UpdateIncubatorUi(machine);
            _popup.PopupEntity(Loc.GetString("sol-bioterror-incubator-complete"), machine);
            return;
        }

        if (machine.Comp.HasFinishedCulture && _timing.CurTime >= machine.Comp.OvergrowAt)
        {
            var chamber = _containers.EnsureContainer<Container>(machine.Owner, SharedCultureIncubator.ChamberContainerId);
            foreach (var culture in chamber.ContainedEntities.ToList())
            {
                if (HasComp<PathogenCultureComponent>(culture))
                    QueueDel(culture);
            }

            RemComp<PendingCultureBatchComponent>(machine.Owner);
            machine.Comp.HasFinishedCulture = false;
            Dirty(machine);
            UpdateIncubatorVisuals(machine);
            UpdateIncubatorUi(machine);
            TriggerAccident(machine.Owner, PathogenStrainRegistrySystem.CustomBaseId, severity: 0.6f);
            _popup.PopupEntity(Loc.GetString("sol-bioterror-incubator-overgrown"), machine);
        }
    }

    private EntityUid CreateSubstrateCulture(
        Entity<ClandestineCultureIncubatorComponent> machine,
        float quality,
        bool contaminated)
    {
        var viability = Math.Clamp(quality * (contaminated ? 0.5f : 1f), 0.1f, 1f);
        var spoilsAt = _timing.CurTime + TimeSpan.FromMinutes(8);
        var junk = Spawn("SolPathogenCultureVial", Transform(machine).Coordinates);
        var junkComp = EnsureComp<PathogenCultureComponent>(junk);
        junkComp.ChassisId = null;
        junkComp.Traits.Clear();
        junkComp.IsChassisCulture = true;
        junkComp.Count = 1;
        junkComp.Viability = viability;
        junkComp.SpoilsAt = spoilsAt;
        Dirty(junk, junkComp);
        _meta.SetEntityName(junk, Loc.GetString("sol-bioterror-culture-substrate-name"));
        return junk;
    }

    private EntityUid CreateGeneCulture(
        Entity<ClandestineCultureIncubatorComponent> machine,
        ProtoId<PathogenTraitPrototype> traitId,
        int count,
        float quality,
        bool contaminated)
    {
        var viability = Math.Clamp(quality * (contaminated ? 0.5f : 1f), 0.1f, 1f);
        var spoilsAt = _timing.CurTime + TimeSpan.FromMinutes(8);
        var gene = Spawn("SolPathogenCultureVial", Transform(machine).Coordinates);
        var geneComp = EnsureComp<PathogenCultureComponent>(gene);
        geneComp.ChassisId = null;
        geneComp.Traits = new List<ProtoId<PathogenTraitPrototype>> { traitId };
        geneComp.IsChassisCulture = false;
        geneComp.Count = Math.Max(1, count);
        geneComp.Viability = viability;
        geneComp.SpoilsAt = spoilsAt;
        Dirty(gene, geneComp);
        RefreshGeneName(gene, geneComp);
        return gene;
    }

    private void ConsumeGeneCharge(EntityUid gene)
    {
        if (!TryComp<PathogenCultureComponent>(gene, out var culture))
        {
            QueueDel(gene);
            return;
        }

        if (culture.Count > 1)
        {
            culture.Count--;
            Dirty(gene, culture);
            RefreshGeneName(gene, culture);
            return;
        }

        QueueDel(gene);
    }

    private static bool TryGetSingleGene(PathogenCultureComponent culture, out ProtoId<PathogenTraitPrototype> traitId)
    {
        traitId = default;
        if (culture.IsChassisCulture || culture.Traits.Count != 1)
            return false;

        traitId = culture.Traits[0];
        return true;
    }

    private bool TryFindStackableGene(Container storage, ProtoId<PathogenTraitPrototype> traitId, out EntityUid existing)
    {
        foreach (var ent in storage.ContainedEntities)
        {
            if (!TryComp<PathogenCultureComponent>(ent, out var culture))
                continue;

            if (TryGetSingleGene(culture, out var existingTrait) && existingTrait == traitId)
            {
                existing = ent;
                return true;
            }
        }

        existing = default;
        return false;
    }

    private void RefreshGeneName(EntityUid gene, PathogenCultureComponent culture)
    {
        if (!TryGetSingleGene(culture, out var traitId))
            return;

        _meta.SetEntityName(gene, FormatGeneLabel(culture, traitId));
    }

    private string FormatGeneLabel(PathogenCultureComponent culture, ProtoId<PathogenTraitPrototype>? traitId = null)
    {
        ProtoId<PathogenTraitPrototype> id;
        if (traitId != null)
        {
            id = traitId.Value;
        }
        else if (!TryGetSingleGene(culture, out id))
        {
            return Loc.GetString("sol-bioterror-sample-genetics-none");
        }

        var name = FormatTraitName(id);
        if (culture.Count <= 1)
            return Loc.GetString("sol-bioterror-culture-gene-name", ("gene", name));

        return Loc.GetString("sol-bioterror-culture-gene-stack", ("gene", name), ("count", culture.Count));
    }

    private void UpdateIncubatorUi(Entity<ClandestineCultureIncubatorComponent> machine)
    {
        var chamber = _containers.EnsureContainer<Container>(machine.Owner, SharedCultureIncubator.ChamberContainerId);
        var nutrientAmount = 0f;
        var nutrientMax = 0f;

        if (_solutions.TryGetSolution(machine.Owner, "tank", out _, out var solution))
        {
            nutrientMax = (float) solution.MaxVolume;
        }

        // Prefer entity-wide total so pre-filled tanks are correct even if UI opens later.
        nutrientAmount = (float) _solutions.GetTotalPrototypeQuantity(machine.Owner, machine.Comp.NutrientReagent);

        var samples = new List<CultureIncubatorSampleState>();
        foreach (var sampleEnt in chamber.ContainedEntities)
        {
            if (TryComp<MicrobialSampleComponent>(sampleEnt, out var sample))
            {
                var label = sample.SourceLabel ?? MetaData(sampleEnt).EntityName;
                samples.Add(new CultureIncubatorSampleState(
                    GetNetEntity(sampleEnt),
                    label,
                    sample.Quality,
                    sample.Contaminated,
                    FormatSampleGenetics(sample.Traits)));
                continue;
            }

            if (!TryComp<PathogenCultureComponent>(sampleEnt, out var culture))
                continue;

            samples.Add(new CultureIncubatorSampleState(
                GetNetEntity(sampleEnt),
                MetaData(sampleEnt).EntityName,
                culture.Viability,
                contaminated: false,
                FormatCultureDetail(culture)));
        }

        var inputCount = chamber.ContainedEntities.Count(uid => HasComp<MicrobialSampleComponent>(uid));
        var estimatedCost = ClandestineCultureIncubatorComponent.GetBatchNutrientCost(
            inputCount,
            machine.Comp.NutrientBaseCost,
            machine.Comp.NutrientExtraCost);

        var state = new CultureIncubatorBoundUserInterfaceState(
            _power.IsPowered(machine.Owner),
            machine.Comp.CycleInProgress,
            machine.Comp.HasFinishedCulture,
            nutrientAmount,
            nutrientMax,
            machine.Comp.NutrientReagent,
            machine.Comp.MaxSamples,
            estimatedCost,
            samples.ToArray(),
            machine.Comp.CycleStartedAt,
            machine.Comp.CycleEndsAt);

        _ui.SetUiState(machine.Owner, CultureIncubatorUiKey.Key, state);
    }

    private void TickSynthesizer(Entity<ClandestinePathogenSynthesizerComponent> machine)
    {
        if (!machine.Comp.CycleInProgress)
            return;

        if (!_power.IsPowered(machine.Owner))
        {
            machine.Comp.CycleInProgress = false;
            Dirty(machine);
            UpdateSynthesizerVisuals(machine);
            UpdateSynthesizerUi(machine);
            TriggerAccident(machine.Owner, PathogenStrainRegistrySystem.CustomBaseId, severity: 0.5f);
            ClearPending(machine);
            _popup.PopupEntity(Loc.GetString("sol-bioterror-synth-spoiled"), machine, machine);
            return;
        }

        if (_timing.CurTime < machine.Comp.CycleEndsAt)
            return;

        machine.Comp.CycleInProgress = false;
        UpdateSynthesizerVisuals(machine);
        UpdateSynthesizerUi(machine);

        if (machine.Comp.PendingTraits.Count == 0)
        {
            Dirty(machine);
            return;
        }

        try
        {
            var def = _registry.RegisterStrain(machine.Comp.PendingTraits, creator: null);

            var concentration = 4f * Math.Clamp(machine.Comp.PendingViability, 0.2f, 1f);
            for (var i = 0; i < machine.Comp.AmpoulesProduced; i++)
            {
                var ampoule = Spawn(machine.Comp.AmpoulePrototype, Transform(machine).Coordinates);
                var payload = EnsureComp<PathogenPayloadComponent>(ampoule);
                payload.StrainId = def.Id;
                payload.Concentration = concentration;
                payload.Kind = PathogenPayloadKind.Food;
                Dirty(ampoule, payload);
                _meta.SetEntityName(ampoule, Loc.GetString("sol-bioterror-ampoule-name", ("strain", def.DisplayName)));
            }

            // Also produce one aerosol canister.
            var aerosol = Spawn("SolPathogenAerosolCanister", Transform(machine).Coordinates);
            var aerosolPayload = EnsureComp<PathogenPayloadComponent>(aerosol);
            aerosolPayload.StrainId = def.Id;
            aerosolPayload.Concentration = concentration * 1.25f;
            aerosolPayload.Kind = PathogenPayloadKind.Aerosol;
            Dirty(aerosol, aerosolPayload);

            var synthEv = new BioterrorStrainSynthesizedEvent(def.Id, machine.Owner, null);
            RaiseLocalEvent(ref synthEv);
            UpdateTrackerSynthesized(def.Id);
            _popup.PopupEntity(Loc.GetString("sol-bioterror-synth-complete", ("strain", def.DisplayName)), machine, machine);
        }
        catch (InvalidOperationException)
        {
            TriggerAccident(machine.Owner, PathogenStrainRegistrySystem.CustomBaseId, severity: 0.7f);
            _popup.PopupEntity(Loc.GetString("sol-bioterror-synth-failed"), machine, machine);
        }

        ClearPending(machine);
    }

    private bool TryConsumeReagent(EntityUid machine, string reagentId, float amount)
    {
        var needed = FixedPoint2.New(amount);
        var total = _solutions.GetTotalPrototypeQuantity(machine, reagentId);
        if (total < needed)
            return false;

        foreach (var (_, sol) in _solutions.EnumerateSolutions(machine))
        {
            var qty = sol.Comp.Solution.GetTotalPrototypeQuantity(reagentId);
            if (qty < needed)
                continue;

            _solutions.RemoveReagent(sol, reagentId, needed);
            return true;
        }

        return false;
    }

    private void TriggerAccident(EntityUid machine, string pathogenId, float severity)
    {
        _pathogen.AddOrIncreaseContamination(machine, pathogenId, 3f * severity);
        EntityManager.System<GridPathogenAtmosphereSystem>().AddAirborneLoad(machine, pathogenId, 4f * severity);

        var nearby = new HashSet<EntityUid>();
        // Infect unsealed operators standing on the same tile / nearby via exposure helper.
        var query = EntityQueryEnumerator<Content.Shared.Mobs.Components.MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var mob, out _, out var xform))
        {
            if (xform.Coordinates.GetGridUid(EntityManager) != Transform(machine).GridUid)
                continue;
            if ((xform.Coordinates.Position - Transform(machine).Coordinates.Position).LengthSquared() > 2.25f)
                continue;

            nearby.Add(mob);
        }

        foreach (var mob in nearby)
        {
            _pathogen.TryExpose(mob, pathogenId, 1.5f * severity, PathogenTransmission.Airborne, machine);
        }
    }

    private void ClearPending(Entity<ClandestinePathogenSynthesizerComponent> machine)
    {
        machine.Comp.PendingTraits.Clear();
        machine.Comp.PendingViability = 1f;
        Dirty(machine);
        UpdateSynthesizerUi(machine);
    }

    private void MarkMachineDeployed(EntityUid machine, bool analyzer = false, bool incubator = false, bool synthesizer = false)
    {
        var tracker = EnsureTracker();
        if (analyzer)
            tracker.AnalyzerDeployed = true;
        if (incubator)
            tracker.IncubatorDeployed = true;
        if (synthesizer)
            tracker.SynthesizerDeployed = true;

        if (tracker.AnalyzerDeployed && tracker.IncubatorDeployed && tracker.SynthesizerDeployed)
        {
            var grid = Transform(machine).GridUid;
            tracker.LabEstablishedOffShuttle = tracker.SpawnShuttleGrid == null || grid != tracker.SpawnShuttleGrid;
        }
    }

    private void UpdateTrackerSynthesized(string strainId)
    {
        var tracker = EnsureTracker();
        tracker.SynthesizedStrainId = strainId;
    }

    private BioterrorCellTrackerComponent EnsureTracker()
    {
        var query = EntityQueryEnumerator<BioterrorCellTrackerComponent>();
        while (query.MoveNext(out _, out var tracker))
            return tracker;

        var holder = Spawn();
        return AddComp<BioterrorCellTrackerComponent>(holder);
    }

    private void UpdateAnalyzerVisuals(Entity<ClandestineSampleAnalyzerComponent> machine)
    {
        ClandestineLabVisualState state;
        if (machine.Comp.HasFinishedSample)
            state = ClandestineLabVisualState.Open;
        else if (!_power.IsPowered(machine.Owner))
            state = ClandestineLabVisualState.Off;
        else if (machine.Comp.Processing)
            state = ClandestineLabVisualState.Running;
        else
            state = ClandestineLabVisualState.On;

        _appearance.SetData(machine.Owner, ClandestineLabVisuals.State, state);
    }

    private void UpdateIncubatorVisuals(Entity<ClandestineCultureIncubatorComponent> machine)
    {
        ClandestineLabVisualState state;
        if (machine.Comp.HasFinishedCulture)
            state = ClandestineLabVisualState.Open;
        else if (machine.Comp.CycleInProgress)
            state = ClandestineLabVisualState.Running;
        else if (_power.IsPowered(machine.Owner))
            state = ClandestineLabVisualState.On;
        else
            state = ClandestineLabVisualState.Off;

        _appearance.SetData(machine.Owner, ClandestineLabVisuals.State, state);
    }

    private void UpdateSynthesizerVisuals(Entity<ClandestinePathogenSynthesizerComponent> machine)
    {
        var state = !_power.IsPowered(machine.Owner)
            ? ClandestineLabVisualState.Off
            : machine.Comp.CycleInProgress
                ? ClandestineLabVisualState.Running
                : ClandestineLabVisualState.On;
        _appearance.SetData(machine.Owner, ClandestineLabVisuals.State, state);
    }

    private void OnAnalyzerExamined(Entity<ClandestineSampleAnalyzerComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Processing)
            args.PushMarkup(Loc.GetString("sol-bioterror-analyzer-examine-running"));
        else if (ent.Comp.HasFinishedSample)
            args.PushMarkup(Loc.GetString("sol-bioterror-analyzer-examine-ready"));
        else
            args.PushMarkup(Loc.GetString("sol-bioterror-analyzer-examine"));
    }

    private void OnSampleExamined(Entity<MicrobialSampleComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Analyzed)
        {
            args.PushMarkup(Loc.GetString("sol-bioterror-sample-examine-analyzed",
                ("genetics", FormatSampleGenetics(ent.Comp.Traits)),
                ("quality", ent.Comp.Quality.ToString("F2")),
                ("contaminated", ent.Comp.Contaminated)));
        }
        else
        {
            args.PushMarkup(Loc.GetString("sol-bioterror-sample-examine-raw"));
        }
    }

    private void OnIncubatorExamined(Entity<ClandestineCultureIncubatorComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.CycleInProgress)
            args.PushMarkup(Loc.GetString("sol-bioterror-incubator-examine-running"));
        else if (ent.Comp.HasFinishedCulture)
            args.PushMarkup(Loc.GetString("sol-bioterror-incubator-examine-ready"));
        else
            args.PushMarkup(Loc.GetString("sol-bioterror-incubator-examine"));
    }

    private void OnSynthesizerExamined(Entity<ClandestinePathogenSynthesizerComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.CycleInProgress)
        {
            args.PushMarkup(Loc.GetString("sol-bioterror-synth-examine-running"));
            return;
        }

        args.PushMarkup(Loc.GetString("sol-bioterror-synth-examine"));
    }

    private string FormatSampleGenetics(IReadOnlyList<ProtoId<PathogenTraitPrototype>> traits)
    {
        if (traits.Count == 0)
            return Loc.GetString("sol-bioterror-sample-genetics-none");

        var names = traits.Select(FormatTraitName).ToList();
        return Loc.GetString("sol-bioterror-sample-genetics",
            ("genes", ContentLocalizationManager.FormatList(names)));
    }

    private string FormatCultureDetail(PathogenCultureComponent culture)
    {
        if (culture.IsChassisCulture)
            return Loc.GetString("sol-bioterror-culture-substrate-detail");

        return FormatGeneLabel(culture);
    }

    private string FormatTraitName(ProtoId<PathogenTraitPrototype> traitId)
    {
        if (_prototypes.TryIndex(traitId, out var proto))
            return Loc.GetString(proto.Name);

        return traitId.Id;
    }
}

/// <summary>
/// Temporary analyzer state while a sample is being processed.
/// </summary>
[RegisterComponent]
public sealed partial class PendingAnalysisDataComponent : Component
{
    [DataField]
    public ProtoId<PathogenPrototype>? ChassisId;

    [DataField]
    public List<ProtoId<PathogenTraitPrototype>> Traits = new();

    [DataField]
    public float Quality = 0.5f;

    [DataField]
    public bool Contaminated;

    [DataField]
    public string? SourceLabel;

    [DataField]
    public EntityUid User;
}

/// <summary>
/// Temporary incubator state while a culture batch runs or awaits retrieval.
/// </summary>
[RegisterComponent]
public sealed partial class PendingCultureBatchComponent : Component
{
    [DataField]
    public List<PendingCultureEntry> Cultures = new();
}

[DataDefinition]
public sealed partial class PendingCultureEntry
{
    [DataField]
    public List<ProtoId<PathogenTraitPrototype>> Traits = new();

    [DataField]
    public float Quality = 0.5f;

    [DataField]
    public bool Contaminated;
}
