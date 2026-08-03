using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Medical.Virology;

// Cure and vaccine are deliberately the same product - antipathogen serum. It does not need
// to know which job it is doing: someone carrying the strain is cured, someone who is not
// becomes immune. See PathogenCureData for how the serum carries its strain.

/// <summary>
/// A live attenuated dose. Administering it makes the recipient a walking source of
/// immunity rather than of disease - see <see cref="PathogenVaccineCarrierComponent"/>.
///
/// Deliberately restricted to virulent strains. Prevalence caps mean an ambient or
/// emergent strain never reaches more than 10-15% of the crew, so single doses are
/// already adequate for them; only an antagonist strain outruns hand delivery.
/// </summary>
[RegisterComponent]
public sealed partial class PathogenLiveVaccineComponent : Component
{
    [DataField(required: true)]
    public int Strain;

    [DataField]
    public TimeSpan ApplyTime = TimeSpan.FromSeconds(4);
}

/// <summary>
/// Someone carrying a live vaccine. Periodically immunises nearby crew against one strain.
///
/// This is the contamination source system pointed the other way: instead of standing near
/// something that infects you, you stand near someone who protects you. It deliberately
/// does not go through the pathogen registry - a live vaccine is not an infection, it
/// leaves no symptoms, and it must never be displaceable by a real strain.
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

    /// <summary>
    /// How long the carrier keeps shedding immunity. Not permanent, so a single dose does
    /// not immunise the whole station forever.
    /// </summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromMinutes(10);

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextPulse;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan EndTime;
}

/// <summary>
/// Produces treatment doses from a loaded culture.
///
/// The culture is a template, not a consumable: it stays in the machine and doses are
/// unlimited. The work is in getting the culture at all - two patient samples or a suited
/// trip to a source - so metering doses on top of that would be grind rather than depth.
/// </summary>
[RegisterComponent]
public sealed partial class PathogenVaccinatorComponent : Component
{
    public const string CultureContainerId = "pathogen-culture";
    public const string CatalystContainerId = "pathogen-catalyst";
    public const string VesselContainerId = "pathogen-vessel";

    /// <summary>
    /// One run fills a whole vial rather than producing a single dose, so treating a ward
    /// is one press and a wait instead of twenty clicks.
    /// </summary>
    [DataField]
    public TimeSpan ProduceTime = TimeSpan.FromSeconds(10);


    /// <summary>
    /// A live vaccine takes the same run time for a single dose. It costs a crop nobody
    /// else grows and it is the station's answer to a bioterrorist, so it should never be
    /// something you knock out casually.
    /// </summary>
    [DataField]
    public TimeSpan LiveProduceTime = TimeSpan.FromSeconds(10);

    [ViewVariables(VVAccess.ReadOnly)]
    public ContainerSlot? CultureContainer;

    [ViewVariables(VVAccess.ReadOnly)]
    public ContainerSlot? CatalystContainer;

    [ViewVariables(VVAccess.ReadOnly)]
    public ContainerSlot? VesselContainer;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextProduce;
}

[Serializable, NetSerializable]
public enum PathogenVaccinatorUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class PathogenVaccinatorUiState(
    string strain,
    bool hasCulture,
    bool canMakeLive,
    string liveBlockedReason) : BoundUserInterfaceState
{
    public readonly string Strain = strain;
    public readonly bool HasCulture = hasCulture;
    public readonly bool CanMakeLive = canMakeLive;
    public readonly string LiveBlockedReason = liveBlockedReason;
}

[Serializable, NetSerializable]
public sealed class PathogenVaccinatorProduceMessage(bool live) : BoundUserInterfaceMessage
{
    public readonly bool Live = live;
}

[Serializable, NetSerializable]
public sealed partial class PathogenTreatmentDoAfterEvent : SimpleDoAfterEvent;
