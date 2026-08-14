using Content.Shared._FarHorizons.Materials;
using Content.Shared._FarHorizons.Materials.Systems;
using Content.Shared.Atmos;
using Content.Shared.Guidebook;
using Content.Shared.Materials;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.Power.Generation.FissionGenerator;

// Ported and modified from goonstation by Jhrushbe.
// CC-BY-NC-SA-3.0
// https://github.com/goonstation/goonstation/blob/ff86b044/code/obj/nuclearreactor/reactorcomponents.dm

/// <summary>
/// A reactor part for the reactor grid.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ReactorPartComponent : Component
{
    [Dependency] private IPrototypeManager _proto = default!;

    /// <summary>
    /// Icon of this component as it shows in the UIs.
    /// </summary>
    [DataField]
    public string IconStateInserted = "base";

    /// <summary>
    /// Icon of this component as it shows in the world.
    /// </summary>
    [DataField]
    public string IconStateCap = "rod_cap";

    /// <summary>
    /// Byte indicating what type of rod this reactor part is
    /// </summary>
    [DataField]
    public int RodType = 0;

    [Flags]
    public enum RodTypes
    {
        None = 0,
        FuelRod = 1 << 0,    // 1 Can be processed by the nuclear centrifuge
        ControlRod = 1 << 1, // 2 Can change its NeutronCrossSection according to control rod setting
        GasChannel = 1 << 2, // 4 Can process gas
    }

    #region Reaction tuning
    /// <summary>
    /// Multiplier for the overall rate of reaction events for this part.
    /// </summary>
    [DataField]
    public float ReactionRate = 10f;

    /// <summary>
    /// Changes the likelyhood of neutron interactions for this part.
    /// </summary>
    [DataField]
    public float NeutronReactionBias = 1f;

    /// <summary>
    /// The amount of a property consumed by a reaction on this part.
    /// </summary>
    [DataField]
    public float ReactionReactant = 0.01f;

    /// <summary>
    /// The amount of a property resultant from a reaction on this part.
    /// </summary>
    [DataField]
    public float ReactionProduct = 0.005f;

    /// <summary>
    /// Multiplier for heating from neutron stimulated reactions on this part.
    /// </summary>
    [DataField]
    public float StimulatedHeatingFactor = 2.5f;

    /// <summary>
    /// Multiplier for heating from spontaneous reactions on this part.
    /// </summary>
    [DataField]
    public float SpontaneousHeatingFactor = 0.35f;

    /// <summary>
    /// Multiplier for how much reactant/product is consumed/produced in spontaneous reactions on this part.
    /// </summary>
    [DataField]
    public float SpontaneousReactionConsumptionMultiplier = 1f;

    /// <summary>
    /// Temperature (in C) when people's bare hands can be burnt by this part.
    /// </summary>
    [DataField]
    [GuidebookData]
    public float HotTemp = 80f;

    /// <summary>
    /// Temperature (in C) when insulated gloves can no longer protect against this part.
    /// </summary>
    [DataField]
    [GuidebookData]
    public float BurnTemp = 400f;

    /// <summary>
    /// Ratio of product to reactant for reactions on this part.
    /// </summary>
    [GuidebookData]
    public float ReactionRatio => ReactionReactant != 0 ? (ReactionProduct / ReactionReactant) : 0;

    /// <summary>
    /// Base heat added by neutron stimulated emission.
    /// </summary>
    [DataField]
    public float NeutronStimulatedHeating = 50f;

    /// <summary>
    /// Base heat added by stimulated emission.
    /// </summary>
    [DataField]
    public float StimulatedHeating = 25f;

    /// <summary>
    /// Base heat added by spontaneous neutron reactions.
    /// </summary>
    [DataField]
    public float SpontaneousNeutronHeating = 20f;

    /// <summary>
    /// Base heat added by spontaneous reactions.
    /// </summary>
    [DataField]
    public float SpontaneousHeating = 10f;
    #endregion

    #region Variables
    /// <summary>
    /// Temperature of this component, starts at room temp Kelvin by default.
    /// </summary>
    [DataField]
    public float Temperature = Atmospherics.T20C;

    /// <summary>
    /// How much does this component share heat with surrounding components? Basically surface area in contact (m2).
    /// </summary>
    [DataField]
    public float ThermalCrossSection = 10;

    /// <summary>
    /// How adept is this component at interacting with neutrons - fuel rods are set up to capture them, heat exchangers are set up not to.
    /// </summary>
    [DataField]
    public float NeutronCrossSection = 0.5f;

    /// <summary>
    /// Control rods don't moderate neutrons, they absorb them.
    /// </summary>
    [DataField]
    public bool IsControlRod = false;

    /// <summary>
    /// Max health to set <see cref="MeltHealth"/> to on init.
    /// </summary>
    [DataField]
    public float MaxHealth = 100;

    /// <summary>
    /// Essentially indicates how long this component can be at a dangerous temperature before it melts.
    /// </summary>
    [DataField]
    public float MeltHealth = 100;

    /// <summary>
    /// If this component is melted, you can't take it out of the reactor and it might do some weird stuff.
    /// </summary>
    [DataField]
    public bool Melted = false;

    /// <summary>
    /// The dangerous temperature above which this component starts to melt. 1700K is the melting point of steel.
    /// </summary>
    [DataField]
    [GuidebookData]
    public float MeltingPoint = 1700;

    /// <summary>
    /// How much gas this component can hold, and will be processed per tick.
    /// </summary>
    [DataField]
    [GuidebookData]
    public float GasVolume = 0;

    /// <summary>
    /// Thermal mass. Basically how much energy it takes to heat this up 1Kelvin.
    /// </summary>
    [DataField]
    public float ThermalMass = 420 * 250; //specific heat capacity of steel (420 J/KgK) * mass of component (Kg)

    [DataField]
    public float SpaceHeatTransferRate = 0.1f;

    [DataField]
    public float MaxBurnDamage = 100f;

    #endregion

    [DataField]
    public ProtoId<MaterialPrototype> Material = "Steel";

    public MaterialProperties Properties
    {
        get
        {
            IoCManager.Resolve(ref _proto);
            _properties ??= new MaterialProperties(_proto.Index(Material).Properties);

            return _properties;
        }
        set => _properties = value;
    }
    [DataField("properties")]
    private MaterialProperties? _properties;

    #region Type specific
    /// <summary>
    /// The target insertion level of the control rod.
    /// </summary>
    [DataField]
    public float ConfiguredInsertionLevel = 1;

    /// <summary>
    /// How adept the gas channel is at transfering heat to/from gasses.
    /// </summary>
    [DataField]
    public float GasThermalCrossSection = 15; //was 15

    /// <summary>
    /// The gas mixture inside the gas channel.
    /// </summary>
    public GasMixture? AirContents;
    #endregion

    /// <summary>
    /// Creates a new <see cref="ReactorPartComponent"> with information from an existing one.
    /// </summary>
    /// <param name="source"></param>
    public ReactorPartComponent(ReactorPartComponent source)
    {
        IconStateInserted = source.IconStateInserted;
        IconStateCap = source.IconStateCap;
        RodType = source.RodType;

        ReactionRate = source.ReactionRate;
        NeutronReactionBias = source.NeutronReactionBias;
        ReactionReactant = source.ReactionReactant;
        ReactionProduct = source.ReactionProduct;
        StimulatedHeatingFactor = source.StimulatedHeatingFactor;
        SpontaneousHeatingFactor = source.SpontaneousHeatingFactor;
        SpontaneousReactionConsumptionMultiplier = source.SpontaneousReactionConsumptionMultiplier;
        HotTemp = source.HotTemp;
        BurnTemp = source.BurnTemp;

        Temperature = source.Temperature;
        ThermalCrossSection = source.ThermalCrossSection;
        NeutronCrossSection = source.NeutronCrossSection;
        IsControlRod = source.IsControlRod;
        MaxHealth = source.MaxHealth;
        MeltHealth = source.MeltHealth;
        Melted = source.Melted;
        MeltingPoint = source.MeltingPoint;
        GasVolume = source.GasVolume;
        ThermalMass = source.ThermalMass;

        Material = source.Material;
        _properties = source._properties;

        ConfiguredInsertionLevel = source.ConfiguredInsertionLevel;
        GasThermalCrossSection = source.GasThermalCrossSection;
        AirContents = source.AirContents;
    }

    public bool HasRodType(RodTypes type) => (RodType & (int)type) == (int)type;

    #region Guidebook
    [GuidebookData]
    public double GuidebookThermalTransferValue => Math.Round(MaterialSystem.CalculateHeatTransferCoefficient(Properties, Properties), 1);

    [GuidebookData]
    public string GuidebookNeutronInteractChance => FormatPercent(Properties.Density * ReactionRate * NeutronReactionBias);

    [GuidebookData]
    public string GuidebookNeutronStimulatedEmmissionChance => FormatPercent(Properties.NeutronRadioactivity * ReactionRate * NeutronReactionBias);

    [GuidebookData]
    public string GuidebookStimulatedEmmissionChance => FormatPercent(Properties.Radioactivity * ReactionRate * NeutronReactionBias);

    [GuidebookData]
    public string GuidebookNeutronDecayChance => FormatPercent(Properties.NeutronRadioactivity * ReactionRate);

    [GuidebookData]
    public string GuidebookDecayChance => FormatPercent(Properties.Radioactivity * ReactionRate);

    [GuidebookData]
    public string GuidebookReflectChance => FormatPercent(Properties.Hardness * ReactionRate);

    private static string FormatPercent(double value) => value <= 0 ? "" : Math.Round(value, 1).ToString() + "%";
    #endregion
}

/// <summary>
/// A virtual neutron that flies around within the reactor.
/// </summary>
[NetworkedComponent]
public sealed class ReactorNeutron
{
    public Direction dir = Direction.North;
    public float velocity = 1;
}
