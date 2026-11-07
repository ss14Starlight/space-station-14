// ReSharper disable once CheckNamespace
namespace Content.Shared.Disposal.Tube;

public sealed partial class DisposalTubeComponent
{
    /// <summary>
    /// Tubes only connect to neighbours of the same network. Disposal pipes and transit tubes
    /// share the same tube and holder logic but must never join each other.
    /// </summary>
    [DataField]
    public string Network = "Disposal";
}
