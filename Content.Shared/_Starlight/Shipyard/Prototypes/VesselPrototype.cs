using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Shipyard.Prototypes;

[Prototype]

public sealed partial class VesselPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    ///     Vessel name.
    /// </summary>
    [ViewVariables]
    [DataField("name")] public string Name = string.Empty;

    /// <summary>
    ///     Short description of the vessel.
    /// </summary>
    [ViewVariables]
    [DataField("description")] public string Description = string.Empty;

    /// <summary>
    ///     The price of the vessel
    /// </summary>
    [DataField("price", required: true)]
    public int Price { get; private set; }

    /// <summary>
    ///     The prototype category of the product. (e.g. Small, Medium, Large, Emergency, Special etc.)
    /// </summary>
    [DataField("category")]
    public string Category { get; private set; } = string.Empty;

    /// <summary>
    ///     The prototype group of the product. (e.g. Civilian, Syndicate, Contraband etc.)
    ///     Todo: This is currently unused.
    /// </summary>
    [DataField("group")]
    public string Group { get; private set; } = string.Empty;

    /// <summary>
    ///     Relative directory path to the given shuttle, i.e. `/Maps/Shuttles/yourshittle.yml`
    /// </summary>
    [DataField("shuttlePath", required: true)]
    public ResPath ShuttlePath { get; private set; } = default!;

    /// <summary>
    ///     Delay before shuttles purchased from the shipyard arrive, in seconds.
    /// </summary>
    [DataField]
    public float Delay = 60f;
}
