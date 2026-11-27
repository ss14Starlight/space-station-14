using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Light.ColorGrading;

/// <summary>
/// <seealso cref="ColorGradePrototype"/> for alerts.
/// </summary>
[Prototype]
public sealed partial class AlertColorGradesPrototype : IPrototype
{
    public const string DefaultId = "Default";

    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Alert level id to its grade.
    /// </summary>
    [DataField]
    public Dictionary<string, ProtoId<ColorGradePrototype>> Levels = [];
}
