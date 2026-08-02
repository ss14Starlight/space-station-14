namespace Content.Shared._Starlight.Medical.Virology;

/// <summary>
/// Raised after two entities complete a physical interaction that can transmit bacteria.
/// </summary>
public sealed class PathogenContactEvent(EntityUid first, EntityUid second) : EntityEventArgs
{
    public readonly EntityUid First = first;
    public readonly EntityUid Second = second;
}
