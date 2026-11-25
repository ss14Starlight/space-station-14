
// ReSharper disable once CheckNamespace
namespace Content.Shared.Humanoid.Prototypes;

public sealed partial class RandomHumanoidSettingsPrototype
{
    /// <summary>
    ///     A forced custom name for the humanoid,
    /// </summary>
    [DataField]
    public string? Name { get; private set; } = null;

    /// <summary>
    ///     A custom description for the humanoid,
    /// </summary>
    [DataField]
    public string? Description { get; private set; } = null;
}
