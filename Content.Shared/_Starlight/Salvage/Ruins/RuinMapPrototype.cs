using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Salvage.Ruins;

/// <summary>
/// Station map used as a source for procedural ruin chunk flood-fill.
/// </summary>
[Prototype]
public sealed partial class RuinMapPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Path to the station map YAML whose floors/walls/windows are sampled.
    /// </summary>
    [DataField(required: true)]
    public ResPath MapPath;
}
