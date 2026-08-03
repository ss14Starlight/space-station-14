using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Medical.Virology;

/// <summary>
/// The biological payload installed in a pathogen injector. Doses are discrete; none of
/// these use chemistry volume or metabolism.
/// </summary>
public enum PathogenInjectorMode : byte
{
    Empty,
    Treatment,
    LiveVaccine,
    BeneficialStrain,
}

public enum PathogenAdministrationResult : byte
{
    Invalid,
    Empty,
    NoEffect,
    Cured,
    Vaccinated,
    LiveVaccineApplied,
    BeneficialStrainApplied,
}

/// <summary>
/// A reusable injector configured by a vaccinator for one strain and one purpose.
/// Integer charges make every administration one complete dose.
/// </summary>
[RegisterComponent]
public sealed partial class PathogenInjectorComponent : Component
{
    [DataField]
    public PathogenInjectorMode Mode;

    [DataField]
    public int Strain;

    [DataField]
    public int Doses;

    [DataField]
    public int MaxDoses;

    [DataField]
    public TimeSpan ApplyTime = TimeSpan.FromSeconds(4);

    public bool Empty => Mode == PathogenInjectorMode.Empty && Doses == 0;
}

/// <summary>
/// Someone carrying a live vaccine. Periodically immunises unobstructed nearby crew
/// against one strain, then stops shedding after <see cref="Duration"/>.
/// </summary>
[RegisterComponent]
public sealed partial class PathogenVaccineCarrierComponent : Component
{
    [DataField]
    public int Strain;

    [DataField]
    public float Range = 3f;

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan Duration = TimeSpan.FromMinutes(10);

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextPulse;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan EndTime;
}

/// <summary>
/// Configures empty pathogen injectors from a reusable identified culture.
/// </summary>
[RegisterComponent]
public sealed partial class PathogenVaccinatorComponent : Component
{
    public const string CultureContainerId = "pathogen-culture";
    public const string CatalystContainerId = "pathogen-catalyst";
    public const string InjectorContainerId = "pathogen-injector";

    [DataField]
    public TimeSpan ProduceTime = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan LiveProduceTime = TimeSpan.FromSeconds(10);

    [ViewVariables(VVAccess.ReadOnly)]
    public ContainerSlot? CultureContainer;

    [ViewVariables(VVAccess.ReadOnly)]
    public ContainerSlot? CatalystContainer;

    [ViewVariables(VVAccess.ReadOnly)]
    public ContainerSlot? InjectorContainer;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool Producing;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan FinishTime;

    [ViewVariables(VVAccess.ReadOnly)]
    public PathogenInjectorMode PendingMode;

    [ViewVariables(VVAccess.ReadOnly)]
    public int PendingStrain;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? PendingInjector;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? PendingCatalyst;
}

[Serializable, NetSerializable]
public enum PathogenVaccinatorUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum PathogenVaccinatorSlot : byte
{
    Culture,
    Catalyst,
    Injector,
}

[Serializable, NetSerializable]
public sealed class PathogenVaccinatorUiState(
    string strain,
    string injector,
    string catalyst,
    string status,
    string liveBlockedReason,
    bool canProduce,
    bool canMakeLive,
    bool canEjectCulture,
    bool canEjectCatalyst,
    bool canEjectInjector) : BoundUserInterfaceState
{
    public readonly string Strain = strain;
    public readonly string Injector = injector;
    public readonly string Catalyst = catalyst;
    public readonly string Status = status;
    public readonly string LiveBlockedReason = liveBlockedReason;
    public readonly bool CanProduce = canProduce;
    public readonly bool CanMakeLive = canMakeLive;
    public readonly bool CanEjectCulture = canEjectCulture;
    public readonly bool CanEjectCatalyst = canEjectCatalyst;
    public readonly bool CanEjectInjector = canEjectInjector;
}

[Serializable, NetSerializable]
public sealed class PathogenVaccinatorProduceMessage(bool live) : BoundUserInterfaceMessage
{
    public readonly bool Live = live;
}

[Serializable, NetSerializable]
public sealed class PathogenVaccinatorEjectMessage(PathogenVaccinatorSlot slot) : BoundUserInterfaceMessage
{
    public readonly PathogenVaccinatorSlot Slot = slot;
}

[Serializable, NetSerializable]
public sealed partial class PathogenTreatmentDoAfterEvent : SimpleDoAfterEvent;
