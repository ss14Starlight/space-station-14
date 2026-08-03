using System.Linq;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Shared.Body.Components;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Medical.Virology;

/// <summary>
/// Produces and administers treatment. A loaded culture is a template rather than a
/// consumable, so doses are unlimited once identification is done.
/// </summary>
public sealed partial class PathogenTreatmentSystem : EntitySystem
{
    private static readonly EntProtoId LiveVaccinePrototype = "PathogenLiveVaccineDose";

    /// <summary>
    /// Botany input required for a live vaccine. Deliberately a plant grown for no other
    /// purpose, so producing one means botany planted it on virology's behalf.
    /// </summary>
    private static readonly EntProtoId LiveCatalystPrototype = "FoodViroculumCap";

    private static readonly ProtoId<ReagentPrototype> CureReagent = "Antipathogen";

    /// <summary>
    /// Serum produced per run. Five doses at the standard 5u syringe transfer.
    /// </summary>
    private const float BatchVolume = 25f;

    /// <summary>
    /// Serum required for one treatment. Consumed as a block rather than metabolised
    /// gradually, so a partial injection does nothing at all instead of half-curing someone.
    /// </summary>
    private const float DoseSize = 5f;

    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private PathogenRegistrySystem _registry = default!;
    [Dependency] private PathogenSystem _pathogen = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    private readonly HashSet<EntityUid> _nearby = new();
    private readonly HashSet<EntityUid> _outputCheck = new();

    /// <summary>
    /// How many uncollected doses may sit around the machine before it refuses to make
    /// more. Production is one dose per press, but nothing otherwise stops someone holding
    /// the button and burying the room in autoinjectors.
    /// </summary>
    private const int MaxLooseDoses = 10;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PathogenVaccinatorComponent, ComponentInit>(OnVaccinatorInit);
        SubscribeLocalEvent<PathogenVaccinatorComponent, AfterInteractUsingEvent>(OnVaccinatorInteractUsing);
        SubscribeLocalEvent<PathogenVaccinatorComponent, PathogenVaccinatorProduceMessage>(OnProduce);
        SubscribeLocalEvent<PathogenVaccinatorComponent, BoundUIOpenedEvent>(OnVaccinatorUiOpened);
        SubscribeLocalEvent<BloodstreamComponent, SolutionContainerChangedEvent>(OnTreatableSolutionChanged);
        SubscribeLocalEvent<PathogenLiveVaccineComponent, AfterInteractEvent>(OnLiveVaccineInteract);
        SubscribeLocalEvent<PathogenLiveVaccineComponent, PathogenTreatmentDoAfterEvent>(OnLiveVaccineDoAfter);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<PathogenVaccineCarrierComponent>();
        while (query.MoveNext(out var uid, out var carrier))
        {
            if (curTime >= carrier.EndTime)
            {
                RemCompDeferred<PathogenVaccineCarrierComponent>(uid);
                continue;
            }

            if (curTime < carrier.NextPulse)
                continue;

            carrier.NextPulse = curTime + carrier.Interval;
            Pulse(uid, carrier);
        }
    }

    /// <summary>
    /// Immunises eligible crew standing near the carrier. Never infects, never displaces,
    /// and does nothing to anyone already carrying the strain - a live vaccine protects
    /// the well, it does not treat the sick.
    /// </summary>
    private void Pulse(EntityUid uid, PathogenVaccineCarrierComponent carrier)
    {
        if (!_registry.TryGetStrain(carrier.Strain, out _))
            return;

        _nearby.Clear();
        _lookup.GetEntitiesInRange(
            Transform(uid).Coordinates,
            carrier.Range,
            _nearby,
            LookupFlags.Uncontained);

        foreach (var target in _nearby)
        {
            if (target == uid ||
                !_pathogen.CanHost(target) ||
                _pathogen.IsInfected(target, carrier.Strain) ||
                _pathogen.IsImmune(target, carrier.Strain))
            {
                continue;
            }

            _pathogen.GrantImmunity(target, carrier.Strain);
        }
    }

    private void OnVaccinatorInit(Entity<PathogenVaccinatorComponent> vaccinator, ref ComponentInit args)
    {
        vaccinator.Comp.CultureContainer = _containers.EnsureContainer<ContainerSlot>(
            vaccinator,
            PathogenVaccinatorComponent.CultureContainerId);
        vaccinator.Comp.CatalystContainer = _containers.EnsureContainer<ContainerSlot>(
            vaccinator,
            PathogenVaccinatorComponent.CatalystContainerId);
        vaccinator.Comp.VesselContainer = _containers.EnsureContainer<ContainerSlot>(
            vaccinator,
            PathogenVaccinatorComponent.VesselContainerId);
    }

    private void OnVaccinatorInteractUsing(
        Entity<PathogenVaccinatorComponent> vaccinator,
        ref AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        var slot = HasComp<PathogenViableCultureComponent>(args.Used)
            ? vaccinator.Comp.CultureContainer
            : IsCatalyst(args.Used)
                ? vaccinator.Comp.CatalystContainer
                : IsEmptyVessel(args.Used)
                    ? vaccinator.Comp.VesselContainer
                    : null;

        if (slot is null)
            return;

        if (slot.ContainedEntity is not null)
        {
            _popup.PopupEntity(
                Loc.GetString("pathogen-vaccinator-slot-occupied"),
                vaccinator,
                args.User,
                PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        if (!_containers.Insert(args.Used, slot))
            return;

        args.Handled = true;
        UpdateVaccinatorUi(vaccinator);
    }

    private bool IsCatalyst(EntityUid uid)
        => MetaData(uid).EntityPrototype?.ID == LiveCatalystPrototype.Id;

    /// <summary>
    /// Any empty solution container will do - a syringe, a beaker, a vial. Anything already
    /// carrying a specimen or a culture is rejected so those cannot be overwritten.
    /// </summary>
    private bool IsEmptyVessel(EntityUid uid)
        => HasComp<SolutionContainerManagerComponent>(uid) &&
           !HasComp<PathogenViableCultureComponent>(uid) &&
           !HasComp<PathogenSpecimenComponent>(uid);

    /// <summary>
    /// Fills a container with serum keyed to the strain. Refuses anything that already has
    /// something in it, so a batch can never be diluted or contaminated on the way out.
    /// </summary>
    private bool TryFillVessel(EntityUid vessel, int strain)
    {
        if (!_solutions.TryGetFitsInDispenser(vessel, out var soln, out var solution) &&
            !_solutions.TryGetRefillableSolution(vessel, out soln, out solution))
        {
            return false;
        }

        if (solution.Volume > 0)
            return false;

        var serum = new Solution();
        serum.AddReagent(
            new ReagentId(
                CureReagent,
                new List<ReagentData> { new PathogenCureData { Strain = strain } }),
            BatchVolume);

        return _solutions.TryAddSolution(soln.Value, serum);
    }

    /// <summary>
    /// Doses lying around the machine that nobody has picked up yet.
    /// </summary>
    private int LooseDoseCount(EntityUid vaccinator)
    {
        _outputCheck.Clear();
        _lookup.GetEntitiesInRange(
            Transform(vaccinator).Coordinates,
            1.5f,
            _outputCheck,
            LookupFlags.Uncontained);

        var count = 0;
        foreach (var entity in _outputCheck)
        {
            if (HasComp<PathogenLiveVaccineComponent>(entity))
                count++;
        }

        return count;
    }

    private void OnVaccinatorUiOpened(
        Entity<PathogenVaccinatorComponent> vaccinator,
        ref BoundUIOpenedEvent args)
        => UpdateVaccinatorUi(vaccinator);

    private void UpdateVaccinatorUi(Entity<PathogenVaccinatorComponent> vaccinator)
    {
        var strainText = Loc.GetString("pathogen-vaccinator-no-culture");
        var hasCulture = false;
        var canMakeLive = false;
        var liveHint = string.Empty;

        if (vaccinator.Comp.CultureContainer?.ContainedEntity is { } culture &&
            TryComp<PathogenViableCultureComponent>(culture, out var cultureComp) &&
            _registry.TryGetStrain(cultureComp.Strain, out var strain))
        {
            hasCulture = true;
            strainText = Loc.GetString(
                "pathogen-vaccinator-loaded",
                ("designation", strain.Designation));

            var hasCatalyst = vaccinator.Comp.CatalystContainer?.ContainedEntity is not null;

            if (strain.Tier != PathogenTier.Virulent)
                liveHint = Loc.GetString("pathogen-vaccinator-live-not-virulent");
            else if (!hasCatalyst)
                liveHint = Loc.GetString("pathogen-vaccinator-live-needs-catalyst");
            else
                canMakeLive = true;
        }

        _ui.SetUiState(
            vaccinator.Owner,
            PathogenVaccinatorUiKey.Key,
            new PathogenVaccinatorUiState(strainText, hasCulture, canMakeLive, liveHint));
    }

    private void OnProduce(
        Entity<PathogenVaccinatorComponent> vaccinator,
        ref PathogenVaccinatorProduceMessage args)
    {
        if (!_power.IsPowered(vaccinator) ||
            _timing.CurTime < vaccinator.Comp.NextProduce)
        {
            return;
        }

        if (vaccinator.Comp.CultureContainer?.ContainedEntity is not { } culture ||
            !TryComp<PathogenViableCultureComponent>(culture, out var cultureComp) ||
            !_registry.TryGetStrain(cultureComp.Strain, out var strain))
        {
            return;
        }

        var user = args.Actor;

        if (LooseDoseCount(vaccinator) >= MaxLooseDoses)
        {
            _popup.PopupEntity(
                Loc.GetString("pathogen-vaccinator-output-full"),
                vaccinator,
                user,
                PopupType.SmallCaution);
            return;
        }

        if (vaccinator.Comp.VesselContainer?.ContainedEntity is not { } vessel)
        {
            _popup.PopupEntity(
                Loc.GetString("pathogen-vaccinator-needs-vessel"),
                vaccinator,
                user,
                PopupType.SmallCaution);
            return;
        }

        if (args.Live)
        {
            if (strain.Tier != PathogenTier.Virulent)
            {
                _popup.PopupEntity(
                    Loc.GetString("pathogen-vaccinator-live-not-virulent"),
                    vaccinator,
                    user,
                    PopupType.SmallCaution);
                return;
            }

            if (vaccinator.Comp.CatalystContainer?.ContainedEntity is not { } catalyst)
            {
                _popup.PopupEntity(
                    Loc.GetString("pathogen-vaccinator-live-needs-catalyst"),
                    vaccinator,
                    user,
                    PopupType.SmallCaution);
                return;
            }

            QueueDel(catalyst);
            QueueDel(vessel);

            var live = Spawn(LiveVaccinePrototype, Transform(vaccinator).Coordinates);
            Comp<PathogenLiveVaccineComponent>(live).Strain = strain.Id;

            vaccinator.Comp.NextProduce = _timing.CurTime + vaccinator.Comp.LiveProduceTime;
        }
        else
        {
            if (!TryFillVessel(vessel, strain.Id))
            {
                _popup.PopupEntity(
                    Loc.GetString("pathogen-vaccinator-vessel-not-empty"),
                    vaccinator,
                    user,
                    PopupType.SmallCaution);
                return;
            }

            vaccinator.Comp.NextProduce = _timing.CurTime + vaccinator.Comp.ProduceTime;
        }

        UpdateVaccinatorUi(vaccinator);
    }

    /// <summary>
    /// Absorbs antipathogen serum once a full dose has arrived in the patient.
    ///
    /// Deliberately not a metabolism effect: metabolism applies a reagent prototype's
    /// authored effects and never sees the reagent instance, so it cannot read which
    /// strain the serum was cultured against. Watching the solution instead means any
    /// delivery method works - syringe, hypospray, pill, or a beaker poured down someone's
    /// throat - and a partial dose simply sits there until topped up.
    /// </summary>
    private void OnTreatableSolutionChanged(
        Entity<BloodstreamComponent> patient,
        ref SolutionContainerChangedEvent args)
    {
        foreach (var (reagent, quantity) in args.Solution.Contents)
        {
            if (reagent.Prototype != CureReagent.Id ||
                quantity.Float() < DoseSize)
            {
                continue;
            }

            if (reagent.Data?.OfType<PathogenCureData>().FirstOrDefault() is not { } cure)
                continue;

            if (!_solutions.TryGetSolution(
                    patient.Owner,
                    args.SolutionId,
                    out var soln,
                    out _))
            {
                return;
            }

            _solutions.RemoveReagent(soln.Value, reagent, DoseSize);

            // One product, both jobs. The serum reads the patient rather than asking the
            // player which of the two they meant to make.
            var cured = _pathogen.Cure(patient.Owner, cure.Strain, grantImmunity: true);
            if (!cured)
                _pathogen.GrantImmunity(patient.Owner, cure.Strain);

            _popup.PopupEntity(
                Loc.GetString(cured
                    ? "pathogen-treatment-cured"
                    : "pathogen-treatment-vaccinated"),
                patient.Owner,
                patient.Owner);
            return;
        }
    }

    private void OnLiveVaccineInteract(
        Entity<PathogenLiveVaccineComponent> dose,
        ref AfterInteractEvent args)
    {
        if (args.Handled ||
            !args.CanReach ||
            args.Target is not { } target ||
            !_pathogen.CanHost(target))
        {
            return;
        }

        args.Handled = _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            args.User,
            dose.Comp.ApplyTime,
            new PathogenTreatmentDoAfterEvent(),
            dose,
            target,
            dose)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnLiveVaccineDoAfter(
        Entity<PathogenLiveVaccineComponent> dose,
        ref PathogenTreatmentDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        var carrier = EnsureComp<PathogenVaccineCarrierComponent>(target);
        carrier.Strain = dose.Comp.Strain;
        carrier.NextPulse = _timing.CurTime;
        carrier.EndTime = _timing.CurTime + carrier.Duration;

        _pathogen.GrantImmunity(target, dose.Comp.Strain);

        _popup.PopupEntity(
            Loc.GetString("pathogen-live-vaccine-administered"),
            target,
            args.User);

        QueueDel(dose);
        args.Handled = true;
    }
}
