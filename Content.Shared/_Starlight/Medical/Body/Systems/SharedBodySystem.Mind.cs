using Content.Shared._Starlight.Medical.Body.Events;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Body.Systems;

public partial class SharedBodySystem
{
    /// <summary>
    /// Checks both endpoints before a mind transfer caused by inserting or removing an organ.
    /// </summary>
    /// <remarks>
    /// Brain and Nymph systems use this boundary for anatomy-driven transfers. It does not change general mind
    /// transfer rules, so unrelated operations such as mind swap spells remain outside this policy.
    /// </remarks>
    /// <param name="source">Entity whose mind would be moved, including a detached organ that already owns it.</param>
    /// <param name="target">Entity that would receive the mind.</param>
    /// <returns>False if either endpoint is terminating or either endpoint cancels the attempt.</returns>
    public bool CanTransferMindOnOrganChange(EntityUid source, EntityUid target)
    {
        if (TerminatingOrDeleted(source) || TerminatingOrDeleted(target))
            return false;

        var attempt = new BodyMindTransferAttemptEvent(source, target);
        RaiseLocalEvent(source, ref attempt);
        RaiseLocalEvent(target, ref attempt);
        return !attempt.Cancelled;
    }
}
