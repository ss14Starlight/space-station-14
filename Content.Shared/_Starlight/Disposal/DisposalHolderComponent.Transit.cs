// ReSharper disable once CheckNamespace
namespace Content.Shared.Disposal.Unit;

public sealed partial class DisposalHolderComponent
{
    /// <summary>
    /// A persistent holder is not deleted when it runs empty and never throws its contents out
    /// of the tube. Transit pods are persistent holders.
    /// </summary>
    [DataField]
    public bool Persistent;

    /// <summary>
    /// A halted holder keeps its place in the tube and does not move.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Halted;
}

/// <summary>
/// Raised on a disposal holder before it breaks out of the tube network.
/// Cancel it to keep the holder (and its contents) in the tube.
/// </summary>
[ByRefEvent]
public record struct DisposalHolderExitAttemptEvent
{
    public bool Cancelled;
}
