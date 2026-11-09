// ReSharper disable CheckNamespace

namespace Content.Server.Salvage.Expeditions;

public sealed partial class SalvageExpeditionComponent
{
    /// <summary>
    /// Whether expedition generation failed before becoming playable.
    /// </summary>
    [ViewVariables]
    public bool GenerationFailed;
}
