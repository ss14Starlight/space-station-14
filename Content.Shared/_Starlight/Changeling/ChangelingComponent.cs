using Content.Shared.Humanoid;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.StatusIcon;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Changeling;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class ChangelingComponent : Component
{
    #region Prototypes

    [DataField] public List<SoundSpecifier?> SoundPool = new()
    {
        new SoundPathSpecifier("/Audio/Effects/gib1.ogg"),
        new SoundPathSpecifier("/Audio/Effects/gib2.ogg"),
        new SoundPathSpecifier("/Audio/Effects/gib3.ogg"),
    };

    [DataField] public SoundSpecifier ShriekSound = new SoundPathSpecifier("/Audio/Effects/changeling_shriek.ogg");

    [DataField] public float ShriekPower = 2.5f;

    public readonly List<ProtoId<EntityPrototype>> BaseChangelingActions = new()
    {
        "ActionEvolutionMenu",
        "ActionStingExtractDNA",
        "ActionEnterStasis",
        "ActionExitStasis"
    };

    /// <summary>
    ///     The status icon corresponding to the Changlings.
    /// </summary>

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public ProtoId<FactionIconPrototype> StatusIcon { get; set; } = "HivemindFaction";

    #endregion

    public bool IsInStasis = false;

    public bool StrainedMusclesActive = false;

    public bool IsInLesserForm = false;

    public bool StealthEnabled = false;

    public EntityUid? RegenerationAction;
    public EntityUid? PainSuppressionAction;
    public EntityUid? ShockHandAction;

    [DataField, AutoNetworkedField]
    public bool BreathingEnabled = true;

    public ChangelingResources Resources = new();
    public bool HasChemicalSting { get => Resources.HasChemicalSting; set => Resources.HasChemicalSting = value; }
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> StingReagents { get => Resources.StingReagents; set => Resources.StingReagents = value; }
    public TimeSpan NextStingRegeneration { get => Resources.NextStingRegeneration; set => Resources.NextStingRegeneration = value; }
    public bool StingRegenerationEnabled { get => Resources.StingRegenerationEnabled; set => Resources.StingRegenerationEnabled = value; }
    public float StingTargetTemperature { get => Resources.StingTargetTemperature; set => Resources.StingTargetTemperature = value; }
    public int StingDose { get => Resources.StingDose; set => Resources.StingDose = value; }

    [DataField]
    public float StealthDrain = 1.25f; // 6.6 minutes to fully drain 100 chemicals

    [DataField]
    public float StasisDrain = 10f / 60f;


    public Dictionary<string, EntityUid?> Equipment = new();

    /// <summary>
    ///     Amount of biomass changeling currently has.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Biomass { get => Resources.Biomass; set => Resources.Biomass = value; }

    /// <summary>
    ///     Maximum amount of biomass a changeling can have.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxBiomass { get => Resources.MaxBiomass; set => Resources.MaxBiomass = value; }

    [DataField, AutoNetworkedField]
    public float BiomassDeficitWarningPercent = 0.5f;
    [DataField, AutoNetworkedField]
    public float BiomassDeficitJitterPercent = 0.33f;
    [DataField, AutoNetworkedField]
    public float BiomassDeficitVomitPercent = 0.1f;

    /// <summary>
    ///     How much biomass should be removed per cycle.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BiomassDrain = 0.4f;

    /// <summary>
    ///     Current amount of chemicals changeling currently has.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Chemicals { get => Resources.Chemicals; set => Resources.Chemicals = value; }

    /// <summary>
    ///     Maximum amount of chemicals changeling can have.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxChemicals { get => Resources.MaxChemicals; set => Resources.MaxChemicals = value; }

    /// <summary>
    ///     Bonus chemicals regeneration. In case
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BonusChemicalRegen { get => Resources.BonusChemicalRegen; set => Resources.BonusChemicalRegen = value; }

    /// <summary>
    ///     Cooldown between chem regen events.
    /// </summary>
    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan ChemicalNextUpdateTime = TimeSpan.Zero;
    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan ChemicalUpdateCooldown = TimeSpan.FromSeconds(1f);

    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan BiomassNextUpdateTime = TimeSpan.Zero;
    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan BiomassUpdateCooldown = TimeSpan.FromSeconds(60f);

    [ViewVariables(VVAccess.ReadOnly)]
    public List<TransformData> AbsorbedDNA { get => Resources.AbsorbedDNA; set => Resources.AbsorbedDNA = value; }
    /// <summary>
    ///     Index of <see cref="AbsorbedDNA"/>. Used for switching forms.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public int AbsorbedDNAIndex = 0;

    /// <summary>
    ///     Maximum amount of DNA a changeling can absorb.
    /// </summary>
    public int MaxAbsorbedDNA = 5;

    /// <summary>
    ///     Total absorbed DNA. Counts towards objectives.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public int TotalAbsorbedEntities { get => Resources.TotalAbsorbedEntities; set => Resources.TotalAbsorbedEntities = value; }

    /// <summary>
    ///     Total stolen DNA. Counts towards objectives.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public int TotalStolenDNA { get => Resources.TotalStolenDNA; set => Resources.TotalStolenDNA = value; }

    [ViewVariables(VVAccess.ReadOnly)]
    public TransformData? CurrentForm;

    [ViewVariables(VVAccess.ReadOnly)]
    public TransformData? SelectedForm;
}

public sealed class ChangelingResources
{
    public ChangelingGenetics Genetics = new();
    public int TotalAbsorbedEntities { get => Genetics.TotalAbsorbedEntities; set => Genetics.TotalAbsorbedEntities = value; }
    public int TotalStolenDNA { get => Genetics.TotalStolenDNA; set => Genetics.TotalStolenDNA = value; }
    public List<TransformData> AbsorbedDNA { get => Genetics.AbsorbedDNA; set => Genetics.AbsorbedDNA = value; }
    public bool HasChemicalSting { get => Genetics.HasChemicalSting; set => Genetics.HasChemicalSting = value; }

    public float Biomass = 40f;
    public float MaxBiomass = 60f;
    public float Chemicals = 100f;
    public float MaxChemicals = 100f;
    public float BonusChemicalRegen;
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> StingReagents = new();
    public TimeSpan NextStingRegeneration;
    public bool StingRegenerationEnabled = true;
    public float StingTargetTemperature = 293.15f;
    public int StingDose = ChangelingChemistry.Capacity;
}

public sealed class ChangelingGenetics
{
    public int TotalAbsorbedEntities;
    public int TotalStolenDNA;
    public List<TransformData> AbsorbedDNA = new();
    public bool HasChemicalSting;
}

[DataDefinition]
public sealed partial class TransformData
{
    /// <summary>
    ///     Entity's name.
    /// </summary>
    [DataField]
    public string Name;

    /// <summary>
    ///     Entity's fingerprint, if it exists.
    /// </summary>
    [DataField]
    public string? Fingerprint;

    /// <summary>
    ///     Entity's DNA.
    /// </summary>
    [DataField("dna")]
    public string DNA;

    /// <summary>
    ///     Entity's humanoid appearance component.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), NonSerialized]
    public Entity<HumanoidAppearanceComponent> Appearance;
}
