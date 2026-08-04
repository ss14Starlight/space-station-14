using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Medical.Virology;

/// <summary>
/// Configures and administers discrete pathogen injector doses. Biological treatment is
/// intentionally separate from chemistry volume and metabolism.
/// </summary>
public sealed partial class PathogenTreatmentSystem : EntitySystem
{
    private static readonly EntProtoId LiveCatalystPrototype = "FoodViroculumCap";

    private const int TreatmentDoses = 5;
    private const int SingleDose = 1;

    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private PathogenRegistrySystem _registry = default!;
    [Dependency] private PathogenSystem _pathogen = default!;
    [Dependency] private PathogenIsolationSystem _isolation = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    private readonly HashSet<EntityUid> _nearby = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PathogenInjectorComponent, ComponentInit>(OnInjectorInit);
        SubscribeLocalEvent<PathogenInjectorComponent, AfterInteractEvent>(OnInjectorInteract);
        SubscribeLocalEvent<PathogenInjectorComponent, PathogenTreatmentDoAfterEvent>(OnInjectorDoAfter);
        SubscribeLocalEvent<PathogenVaccinatorComponent, ComponentInit>(OnVaccinatorInit);
        SubscribeLocalEvent<PathogenVaccinatorComponent, AfterInteractUsingEvent>(OnVaccinatorInteractUsing);
        SubscribeLocalEvent<PathogenVaccinatorComponent, PathogenVaccinatorProduceMessage>(OnProduce);
        SubscribeLocalEvent<PathogenVaccinatorComponent, PathogenVaccinatorEjectMessage>(OnEject);
        SubscribeLocalEvent<PathogenVaccinatorComponent, BoundUIOpenedEvent>(OnVaccinatorUiOpened);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        var carrierQuery = EntityQueryEnumerator<PathogenVaccineCarrierComponent>();
        while (carrierQuery.MoveNext(out var uid, out var carrier))
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

        var vaccinatorQuery = EntityQueryEnumerator<PathogenVaccinatorComponent>();
        while (vaccinatorQuery.MoveNext(out var uid, out var vaccinator))
        {
            if (!vaccinator.Producing || curTime < vaccinator.FinishTime)
                continue;

            FinishProduction((uid, vaccinator));
        }
    }

    private void Pulse(EntityUid uid, PathogenVaccineCarrierComponent carrier)
    {
        if (_isolation.IsIsolated(uid) ||
            !_registry.TryGetStrain(carrier.Strain, out _))
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
                !_interaction.InRangeUnobstructed(uid, target, carrier.Range) ||
                _pathogen.IsInfected(target, carrier.Strain) ||
                _pathogen.IsImmune(target, carrier.Strain) ||
                !_random.Prob(Math.Clamp(carrier.Chance, 0f, 1f)))
            {
                continue;
            }

            _pathogen.GrantImmunity(target, carrier.Strain);
        }
    }

    private void OnInjectorInit(Entity<PathogenInjectorComponent> injector, ref ComponentInit args)
        => UpdateInjectorIdentity(injector);

    public bool CanLoadInjector(EntityUid uid)
        => TryComp<PathogenInjectorComponent>(uid, out var injector) && injector.Empty;

    /// <summary>
    /// Installs a strain-specific payload in an empty injector.
    /// </summary>
    public bool TryConfigureInjector(
        EntityUid uid,
        PathogenInjectorMode mode,
        int strainId)
    {
        if (!TryComp<PathogenInjectorComponent>(uid, out var injector) ||
            !injector.Empty ||
            !_registry.TryGetStrain(strainId, out var strain))
        {
            return false;
        }

        var doses = mode switch
        {
            PathogenInjectorMode.Treatment when !strain.Beneficial => TreatmentDoses,
            PathogenInjectorMode.LiveVaccine when strain.Tier == PathogenTier.Virulent => SingleDose,
            PathogenInjectorMode.BeneficialStrain when strain.Beneficial => SingleDose,
            _ => 0,
        };

        if (doses == 0)
            return false;

        injector.Mode = mode;
        injector.Strain = strainId;
        injector.Doses = doses;
        injector.MaxDoses = doses;
        UpdateInjectorIdentity((uid, injector), strain);
        return true;
    }

    /// <summary>
    /// Applies exactly one complete dose. Failed or redundant administrations do not spend
    /// a charge.
    /// </summary>
    public PathogenAdministrationResult TryAdminister(EntityUid injectorUid, EntityUid target)
    {
        if (!TryComp<PathogenInjectorComponent>(injectorUid, out var injector))
            return PathogenAdministrationResult.Invalid;

        if (injector.Empty || injector.Doses <= 0)
            return PathogenAdministrationResult.Empty;

        if (!_pathogen.CanHost(target) ||
            !_registry.TryGetStrain(injector.Strain, out var strain))
        {
            return PathogenAdministrationResult.Invalid;
        }

        var result = injector.Mode switch
        {
            PathogenInjectorMode.Treatment => ApplyTreatment(target, strain),
            PathogenInjectorMode.LiveVaccine => ApplyLiveVaccine(target, strain),
            PathogenInjectorMode.BeneficialStrain => ApplyBeneficialStrain(target, strain),
            _ => PathogenAdministrationResult.Invalid,
        };

        if (result is PathogenAdministrationResult.Cured or
            PathogenAdministrationResult.Vaccinated or
            PathogenAdministrationResult.LiveVaccineApplied or
            PathogenAdministrationResult.BeneficialStrainApplied)
        {
            ConsumeDose((injectorUid, injector));
        }

        return result;
    }

    private PathogenAdministrationResult ApplyTreatment(EntityUid target, Pathogen strain)
    {
        if (strain.Beneficial)
            return PathogenAdministrationResult.Invalid;

        if (_pathogen.Cure(target, strain.Id, grantImmunity: true))
            return PathogenAdministrationResult.Cured;

        if (_pathogen.IsImmune(target, strain.Id))
            return PathogenAdministrationResult.NoEffect;

        _pathogen.GrantImmunity(target, strain.Id);
        return PathogenAdministrationResult.Vaccinated;
    }

    private PathogenAdministrationResult ApplyLiveVaccine(EntityUid target, Pathogen strain)
    {
        if (strain.Tier != PathogenTier.Virulent ||
            _pathogen.IsInfected(target, strain.Id) ||
            TryComp<PathogenVaccineCarrierComponent>(target, out var existing) &&
            existing.Strain == strain.Id)
        {
            return PathogenAdministrationResult.NoEffect;
        }

        var carrier = EnsureComp<PathogenVaccineCarrierComponent>(target);
        carrier.Strain = strain.Id;
        carrier.NextPulse = _timing.CurTime;
        carrier.EndTime = _timing.CurTime + carrier.Duration;
        _pathogen.GrantImmunity(target, strain.Id);
        return PathogenAdministrationResult.LiveVaccineApplied;
    }

    private PathogenAdministrationResult ApplyBeneficialStrain(EntityUid target, Pathogen strain)
    {
        if (!strain.Beneficial || !_pathogen.TryInfect(target, strain.Id, bypassImmunity: true))
            return PathogenAdministrationResult.NoEffect;

        return PathogenAdministrationResult.BeneficialStrainApplied;
    }

    private void ConsumeDose(Entity<PathogenInjectorComponent> injector)
    {
        injector.Comp.Doses--;

        if (injector.Comp.Doses <= 0)
        {
            injector.Comp.Mode = PathogenInjectorMode.Empty;
            injector.Comp.Strain = 0;
            injector.Comp.Doses = 0;
            injector.Comp.MaxDoses = 0;
            UpdateInjectorIdentity(injector);
            return;
        }

        UpdateInjectorIdentity(injector);
    }

    private void UpdateInjectorIdentity(Entity<PathogenInjectorComponent> injector, Pathogen? strain = null)
    {
        if (injector.Comp.Empty ||
            strain is null && !_registry.TryGetStrain(injector.Comp.Strain, out strain))
        {
            _metaData.SetEntityName(injector, Loc.GetString("pathogen-injector-empty-name"));
            _metaData.SetEntityDescription(injector, Loc.GetString("pathogen-injector-empty-description"));
            return;
        }

        var nameKey = injector.Comp.Mode switch
        {
            PathogenInjectorMode.Treatment => "pathogen-injector-treatment-name",
            PathogenInjectorMode.LiveVaccine => "pathogen-injector-live-name",
            PathogenInjectorMode.BeneficialStrain => "pathogen-injector-beneficial-name",
            _ => "pathogen-injector-empty-name",
        };
        var descriptionKey = injector.Comp.Mode switch
        {
            PathogenInjectorMode.Treatment => "pathogen-injector-treatment-description",
            PathogenInjectorMode.LiveVaccine => "pathogen-injector-live-description",
            PathogenInjectorMode.BeneficialStrain => "pathogen-injector-beneficial-description",
            _ => "pathogen-injector-empty-description",
        };

        _metaData.SetEntityName(
            injector,
            Loc.GetString(nameKey, ("designation", strain.Designation)));
        _metaData.SetEntityDescription(
            injector,
            Loc.GetString(
                descriptionKey,
                ("designation", strain.Designation),
                ("doses", injector.Comp.Doses),
                ("capacity", injector.Comp.MaxDoses)));
    }

    private void OnInjectorInteract(Entity<PathogenInjectorComponent> injector, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (injector.Comp.Empty)
        {
            _popup.PopupEntity(
                Loc.GetString("pathogen-injector-empty"),
                injector,
                args.User,
                PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        if (!_pathogen.CanHost(target))
            return;

        args.Handled = _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            args.User,
            injector.Comp.ApplyTime,
            new PathogenTreatmentDoAfterEvent(),
            injector,
            target,
            injector)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnInjectorDoAfter(
        Entity<PathogenInjectorComponent> injector,
        ref PathogenTreatmentDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        var result = TryAdminister(injector, target);
        var message = result switch
        {
            PathogenAdministrationResult.Cured => "pathogen-treatment-cured",
            PathogenAdministrationResult.Vaccinated => "pathogen-treatment-vaccinated",
            PathogenAdministrationResult.LiveVaccineApplied => "pathogen-live-vaccine-administered",
            PathogenAdministrationResult.BeneficialStrainApplied => "pathogen-beneficial-strain-administered",
            PathogenAdministrationResult.Empty => "pathogen-injector-empty",
            _ => "pathogen-treatment-no-effect",
        };

        _popup.PopupEntity(Loc.GetString(message), target, args.User);
        args.Handled = true;
    }

    private void OnVaccinatorInit(Entity<PathogenVaccinatorComponent> vaccinator, ref ComponentInit args)
    {
        vaccinator.Comp.CultureContainer = _containers.EnsureContainer<ContainerSlot>(
            vaccinator,
            PathogenVaccinatorComponent.CultureContainerId);
        vaccinator.Comp.CatalystContainer = _containers.EnsureContainer<ContainerSlot>(
            vaccinator,
            PathogenVaccinatorComponent.CatalystContainerId);
        vaccinator.Comp.InjectorContainer = _containers.EnsureContainer<ContainerSlot>(
            vaccinator,
            PathogenVaccinatorComponent.InjectorContainerId);
    }

    private void OnVaccinatorInteractUsing(
        Entity<PathogenVaccinatorComponent> vaccinator,
        ref AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (vaccinator.Comp.Producing)
        {
            PopupMachineBusy(vaccinator, args.User);
            args.Handled = true;
            return;
        }

        ContainerSlot? slot;
        if (HasComp<PathogenViableCultureComponent>(args.Used))
        {
            slot = vaccinator.Comp.CultureContainer;
        }
        else if (IsCatalyst(args.Used))
        {
            slot = vaccinator.Comp.CatalystContainer;
        }
        else if (TryComp<PathogenInjectorComponent>(args.Used, out var injector))
        {
            if (!injector.Empty)
            {
                _popup.PopupEntity(
                    Loc.GetString("pathogen-vaccinator-injector-not-empty"),
                    vaccinator,
                    args.User,
                    PopupType.SmallCaution);
                args.Handled = true;
                return;
            }

            slot = vaccinator.Comp.InjectorContainer;
        }
        else
        {
            return;
        }

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

    private void OnVaccinatorUiOpened(
        Entity<PathogenVaccinatorComponent> vaccinator,
        ref BoundUIOpenedEvent args)
        => UpdateVaccinatorUi(vaccinator);

    private void OnProduce(
        Entity<PathogenVaccinatorComponent> vaccinator,
        ref PathogenVaccinatorProduceMessage args)
        => TryStartProduction(vaccinator, args.Actor, args.Live);

    public bool TryStartProduction(
        Entity<PathogenVaccinatorComponent> vaccinator,
        EntityUid user,
        bool live)
    {
        if (vaccinator.Comp.Producing)
        {
            PopupMachineBusy(vaccinator, user);
            return false;
        }

        if (!_power.IsPowered(vaccinator))
        {
            _popup.PopupEntity(
                Loc.GetString("pathogen-vaccinator-no-power"),
                vaccinator,
                user,
                PopupType.SmallCaution);
            return false;
        }

        if (vaccinator.Comp.CultureContainer?.ContainedEntity is not { } culture ||
            !TryComp<PathogenViableCultureComponent>(culture, out var cultureComp) ||
            !_registry.TryGetStrain(cultureComp.Strain, out var strain))
        {
            return false;
        }

        if (vaccinator.Comp.InjectorContainer?.ContainedEntity is not { } injectorUid ||
            !CanLoadInjector(injectorUid))
        {
            _popup.PopupEntity(
                Loc.GetString("pathogen-vaccinator-needs-injector"),
                vaccinator,
                user,
                PopupType.SmallCaution);
            return false;
        }

        var mode = strain.Beneficial
            ? PathogenInjectorMode.BeneficialStrain
            : PathogenInjectorMode.Treatment;
        EntityUid? catalyst = null;
        var duration = vaccinator.Comp.ProduceTime;

        if (live)
        {
            if (strain.Tier != PathogenTier.Virulent)
            {
                _popup.PopupEntity(
                    Loc.GetString("pathogen-vaccinator-live-not-virulent"),
                    vaccinator,
                    user,
                    PopupType.SmallCaution);
                return false;
            }

            if (vaccinator.Comp.CatalystContainer?.ContainedEntity is not { } loadedCatalyst)
            {
                _popup.PopupEntity(
                    Loc.GetString("pathogen-vaccinator-live-needs-catalyst"),
                    vaccinator,
                    user,
                    PopupType.SmallCaution);
                return false;
            }

            mode = PathogenInjectorMode.LiveVaccine;
            catalyst = loadedCatalyst;
            duration = vaccinator.Comp.LiveProduceTime;
        }

        vaccinator.Comp.Producing = true;
        vaccinator.Comp.FinishTime = _timing.CurTime + duration;
        vaccinator.Comp.PendingMode = mode;
        vaccinator.Comp.PendingStrain = strain.Id;
        vaccinator.Comp.PendingInjector = injectorUid;
        vaccinator.Comp.PendingCatalyst = catalyst;
        UpdateVaccinatorUi(vaccinator);
        return true;
    }

    private void FinishProduction(Entity<PathogenVaccinatorComponent> vaccinator)
    {
        if (!_power.IsPowered(vaccinator))
            return;

        if (vaccinator.Comp.PendingInjector is not { } injector ||
            vaccinator.Comp.InjectorContainer?.ContainedEntity != injector ||
            vaccinator.Comp.CultureContainer?.ContainedEntity is not { } culture ||
            !TryComp<PathogenViableCultureComponent>(culture, out var cultureComp) ||
            cultureComp.Strain != vaccinator.Comp.PendingStrain)
        {
            CancelProduction(vaccinator);
            return;
        }

        if (vaccinator.Comp.PendingMode == PathogenInjectorMode.LiveVaccine &&
            (vaccinator.Comp.PendingCatalyst is not { } catalyst ||
             vaccinator.Comp.CatalystContainer?.ContainedEntity != catalyst))
        {
            CancelProduction(vaccinator);
            return;
        }

        if (!TryConfigureInjector(
                injector,
                vaccinator.Comp.PendingMode,
                vaccinator.Comp.PendingStrain))
        {
            CancelProduction(vaccinator);
            return;
        }

        if (vaccinator.Comp.PendingCatalyst is { } consumedCatalyst)
            QueueDel(consumedCatalyst);

        _containers.Remove(injector, vaccinator.Comp.InjectorContainer);

        ClearProduction(vaccinator.Comp);
        _popup.PopupEntity(Loc.GetString("pathogen-vaccinator-complete"), vaccinator);
        UpdateVaccinatorUi(vaccinator);
    }

    private void CancelProduction(Entity<PathogenVaccinatorComponent> vaccinator)
    {
        ClearProduction(vaccinator.Comp);
        _popup.PopupEntity(
            Loc.GetString("pathogen-vaccinator-production-cancelled"),
            vaccinator,
            PopupType.SmallCaution);
        UpdateVaccinatorUi(vaccinator);
    }

    private static void ClearProduction(PathogenVaccinatorComponent vaccinator)
    {
        vaccinator.Producing = false;
        vaccinator.FinishTime = TimeSpan.Zero;
        vaccinator.PendingMode = PathogenInjectorMode.Empty;
        vaccinator.PendingStrain = 0;
        vaccinator.PendingInjector = null;
        vaccinator.PendingCatalyst = null;
    }

    private void OnEject(
        Entity<PathogenVaccinatorComponent> vaccinator,
        ref PathogenVaccinatorEjectMessage args)
    {
        if (vaccinator.Comp.Producing)
        {
            PopupMachineBusy(vaccinator, args.Actor);
            return;
        }

        var slot = args.Slot switch
        {
            PathogenVaccinatorSlot.Culture => vaccinator.Comp.CultureContainer,
            PathogenVaccinatorSlot.Catalyst => vaccinator.Comp.CatalystContainer,
            PathogenVaccinatorSlot.Injector => vaccinator.Comp.InjectorContainer,
            _ => null,
        };

        if (slot?.ContainedEntity is not { } contained)
            return;

        _containers.Remove(contained, slot);
        UpdateVaccinatorUi(vaccinator);
    }

    private void PopupMachineBusy(EntityUid vaccinator, EntityUid user)
    {
        _popup.PopupEntity(
            Loc.GetString("pathogen-vaccinator-busy"),
            vaccinator,
            user,
            PopupType.SmallCaution);
    }

    private void UpdateVaccinatorUi(Entity<PathogenVaccinatorComponent> vaccinator)
    {
        var strainText = Loc.GetString("pathogen-vaccinator-no-culture");
        Pathogen? strain = null;

        if (vaccinator.Comp.CultureContainer?.ContainedEntity is { } culture &&
            TryComp<PathogenViableCultureComponent>(culture, out var cultureComp) &&
            _registry.TryGetStrain(cultureComp.Strain, out strain))
        {
            strainText = Loc.GetString(
                "pathogen-vaccinator-loaded",
                ("designation", strain.Designation));
        }

        var injectorText = vaccinator.Comp.InjectorContainer?.ContainedEntity is { } injector
            ? MetaData(injector).EntityName
            : Loc.GetString("pathogen-vaccinator-no-injector");
        var catalystText = vaccinator.Comp.CatalystContainer?.ContainedEntity is { } catalyst
            ? MetaData(catalyst).EntityName
            : Loc.GetString("pathogen-vaccinator-no-catalyst");
        var hasEmptyInjector = vaccinator.Comp.InjectorContainer?.ContainedEntity is { } injectorUid &&
                               CanLoadInjector(injectorUid);
        var powered = _power.IsPowered(vaccinator);
        var canProduce = !vaccinator.Comp.Producing && powered && strain is not null && hasEmptyInjector;
        var canMakeLive = canProduce &&
                          strain!.Tier == PathogenTier.Virulent &&
                          vaccinator.Comp.CatalystContainer?.ContainedEntity is not null;
        var liveHint = string.Empty;

        if (strain is not null && strain.Tier != PathogenTier.Virulent)
            liveHint = Loc.GetString("pathogen-vaccinator-live-not-virulent");
        else if (strain is not null && vaccinator.Comp.CatalystContainer?.ContainedEntity is null)
            liveHint = Loc.GetString("pathogen-vaccinator-live-needs-catalyst");

        var status = vaccinator.Comp.Producing
            ? Loc.GetString("pathogen-vaccinator-producing")
            : powered
                ? Loc.GetString("pathogen-vaccinator-ready")
                : Loc.GetString("pathogen-vaccinator-no-power");
        var canEject = !vaccinator.Comp.Producing;

        _ui.SetUiState(
            vaccinator.Owner,
            PathogenVaccinatorUiKey.Key,
            new PathogenVaccinatorUiState(
                strainText,
                injectorText,
                catalystText,
                status,
                liveHint,
                canProduce,
                canMakeLive,
                canEject && vaccinator.Comp.CultureContainer?.ContainedEntity is not null,
                canEject && vaccinator.Comp.CatalystContainer?.ContainedEntity is not null,
                canEject && vaccinator.Comp.InjectorContainer?.ContainedEntity is not null));
    }
}
