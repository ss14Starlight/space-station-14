using Content.Shared.Atmos;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax.Biomes.Markers;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Daiquiri.Planets;

[Prototype]
public sealed partial class PlanetDestinationPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name = string.Empty;

    [DataField]
    public LocId Description = string.Empty;

    [DataField(required: true)]
    public ProtoId<BiomeTemplatePrototype> Biome = string.Empty;

    [DataField]
    public string? Parallax;

    [DataField]
    public SpriteSpecifier? Banner;

    [DataField]
    public Color MapLight = Color.FromHex("#D8B059");

    /// <summary>
    /// Darkest color of the day / night cycle, mirrors <c>LightCycleComponent.MinLevel</c>.
    /// Kept blue-ish so the night reads as deep blue instead of pure black.
    /// </summary>
    [DataField]
    public Color CycleMinLevel = new(0.1f, 0.15f, 0.50f);

    /// <summary>
    /// Brightest color stop of the day / night cycle, mirrors <c>LightCycleComponent.MaxLevel</c>.
    /// Lowering it below the clip thresholds keeps the day from blowing out to white.
    /// </summary>
    [DataField]
    public Color CycleMaxLevel = new(2f, 2f, 5f);

    /// <summary>
    /// Brightness floor of the day / night cycle, mirrors <c>LightCycleComponent.MinLightLevel</c>.
    /// Anything above zero keeps the night from going pitch black.
    /// </summary>
    [DataField]
    public float CycleMinLightLevel;

    [DataField]
    public float Temperature = 20f;

    [DataField]
    public Dictionary<Gas, float> Atmosphere = new()
    {
        [Gas.Oxygen] = 21.824779f,
        [Gas.Nitrogen] = 82.10312f,
    };

    [DataField]
    public Color Accent = Color.FromHex("#4FC3E8");

    [DataField]
    public List<EntProtoId> Weathers = new();

    [DataField]
    public ProtoId<DamageTypePrototype> WeatherDamageType = "Cold";

    [DataField]
    public float WeatherDamagePerSecond = 1f;

    [DataField]
    public LocId? WeatherStartMessage;

    [DataField]
    public LocId? WeatherEndMessage;

    [DataField]
    public Color WeatherColor = Color.White;

    [DataField]
    public EntProtoId? Lightning;

    [DataField]
    public float LightningShockDamage = 15f;

    [DataField]
    public List<ProtoId<BiomeMarkerLayerPrototype>> OreMarkers = new()
    {
        "OreIron",
        "OreQuartz",
        "OreCoal",
        "OreSalt",
        "OreGold",
        "OreSilver",
        "OrePlasma",
        "OreUranium",
        "OreDiamond",
        "OreArtifactFragment",
    };

    [DataField]
    public List<ProtoId<BiomeMarkerLayerPrototype>> MobMarkers = new();

    [DataField]
    public int Order;
}
