namespace Content.Shared._FarHorizons.Util;

/// <summary>
///     Raised before an interaction by a user while holding an object in their hand.
///     Cancels interaciton if necessary.
/// </summary>
public sealed class CheckItemCanBeUsedEvent(EntityUid user, EntityUid? target) : CancellableEntityEventArgs
{
    /// <summary>
    ///     Entity that triggered the interaction.
    /// </summary>
    public EntityUid User { get; } = user;

    /// <summary>
    ///     Entity that was interacted on.
    /// </summary>
    public EntityUid? Target { get; } = target;
}